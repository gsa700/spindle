// Spindle, Hymie — sound out on FreeBSD, through OSS: /dev/dsp, the kernel's
// own sound system (2026-11-07).
//
// Nothing to install and no daemon to run: every FreeBSD has sound(4), and
// /dev/dsp is the default device (hw.snd.default_unit). The kernel gives each
// program that opens it a virtual channel and mixes them, so this plays beside
// Firefox (through sndiod) or anything else, the same good neighbour PulseOutput
// is on Linux. With bit-perfect mode on (dev.pcm.N.bitperfect=1) the kernel
// mixes nothing and passes these samples to the hardware untouched; then only
// one program can play at a time, which is that setting's bargain, not ours.
//
// Samples go out as 32-bit integers (AFMT_S32_LE), converted from Hymie's
// floats as they are written: exact for 24-bit sources, understood by every
// FreeBSD the apps run on (AFMT_F32_LE is new in 15), and taken as they are by
// the hardware in bit-perfect mode. The file's own sample rate is asked for;
// with virtual channels the kernel converts it to the mixer's rate.
//
// Like PulseOutput: one thread of our own does every call, writing 1024 frames
// at a time into a kernel buffer of about 0.2 s.
//
//   * Pause: what the kernel has not played yet is thrown away at once
//     (SNDCTL_DSP_RESET) and kept here, to be written first when play resumes,
//     so a pause is heard within a write and nothing is lost across it.
//   * Volume: a multiply as the samples are written, heard after what is
//     already buffered (0.2 s), as on Linux.
//   * Played: written minus what the kernel still holds (SNDCTL_DSP_GETODELAY),
//     asked after every write and run on by the clock in between.
//   * The end: SNDCTL_DSP_SYNC waits until everything written has played.
//
// The ioctl numbers and formats are read from FreeBSD 15's <sys/soundcard.h>.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spindle.Playback;

internal sealed unsafe class OssOutput : IOutput
{
    private const int Chunk = 1024;             // frames per write
    private const double Buffered = 0.2;        // seconds the kernel holds
    private const string Device = "/dev/dsp";

    public int SampleRate { get; }
    public int Channels { get; }

    private readonly IOutput.Source _source;
    private readonly object _gate = new();      // Monitor: the writer waits on it while paused
    private Thread? _thread;
    private volatile bool _wantPlaying;
    private volatile bool _stopping;
    private volatile bool _ended;               // the source has said it has no more
    private volatile bool _drained;
    private volatile float _volume = 1f;

    // On the writer thread only.
    private int _fd = -1;
    private bool _live;                         // samples are with the kernel
    private long _written;                      // frames with the kernel, net of resets
    private readonly float[] _history;          // the last second written, before the volume
    private float[] _pending = [];              // thrown away at a pause, written first after it
    private int _pendingAt;

    // The timeline, under _gate: Played was _base at _baseAt, and runs on
    // from there while _running, never past _limit.
    private long _base, _limit;
    private long _baseAt;
    private bool _running;

    public OssOutput(int sampleRate, int channels, IOutput.Source source)
    {
        SampleRate = sampleRate;
        Channels = channels;
        _source = source;
        _history = new float[sampleRate * channels];
    }

    /// Whether the machine has a sound device. /dev/dsp is made by devfs when
    /// it is looked up, so it is found even though a listing does not show it.
    public static bool IsAvailable => File.Exists(Device);

    /// The device is opened on the first Start, not before, so a record
    /// restored paused holds no channel open.
    public void Start()
    {
        lock (_gate)
        {
            if (_stopping) return;
            _wantPlaying = true;
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "engine output" };
                _thread.Start();
            }
            Monitor.PulseAll(_gate);
        }
    }

    /// Heard within a write (23 ms at 44.1 kHz), when the writer next looks.
    /// The clock stops now, so the position stands still from the moment asked.
    public void Pause()
    {
        lock (_gate)
        {
            _base = Played;
            _running = false;
            _wantPlaying = false;
        }
    }

    public void SetVolume(float gain) => _volume = gain;

    public long Played
    {
        get
        {
            lock (_gate)
            {
                if (!_running) return _base;
                var ran = (long)(Stopwatch.GetElapsedTime(_baseAt).TotalSeconds * SampleRate);
                return Math.Min(_base + ran, _limit);
            }
        }
    }

    public bool Drained => _drained;

    private int BytesPerFrame => 4 * Channels;

    private void Run()
    {
        _fd = open(Device, O_WRONLY | O_CLOEXEC);
        if (_fd < 0)
        {
            Console.WriteLine($"[engine] output: cannot open {Device}: {LastError()}");
            _ended = _drained = true;
            return;
        }

        var buf = new float[Chunk * Channels];
        var loud = new int[Chunk * Channels];
        try
        {
            if (!Configure()) { _ended = _drained = true; return; }

            while (!_stopping)
            {
                if (!_wantPlaying || _drained)
                {
                    if (_live) Hold();
                    lock (_gate)
                        while ((!_wantPlaying || _drained) && !_stopping) Monitor.Wait(_gate);
                    continue;
                }

                int frames;
                if (_pendingAt < _pending.Length)
                {
                    var n = Math.Min(buf.Length, _pending.Length - _pendingAt);
                    _pending.AsSpan(_pendingAt, n).CopyTo(buf);
                    _pendingAt += n;
                    frames = n / Channels;
                }
                else if (!_ended)
                {
                    frames = _source(buf);
                    if (frames < Chunk) _ended = true;
                }
                else
                {
                    // Everything is written: let it play out.
                    if (ioctl(_fd, SNDCTL_DSP_SYNC, null) < 0) Console.WriteLine($"[engine] output: sync: {LastError()}");
                    lock (_gate) { _base = _limit = _written; _running = false; }
                    _live = false;
                    _drained = true;
                    continue;
                }
                if (frames == 0) continue;

                Remember(buf.AsSpan(0, frames * Channels));
                var gain = _volume;
                for (var i = 0; i < frames * Channels; i++)
                {
                    // Full scale is 2^31; 2147483520 is the largest float below it,
                    // so a sample at +1.0 cannot wrap to the most negative value.
                    var v = Math.Clamp(buf[i] * gain, -1f, 1f) * 2147483520f;
                    loud[i] = (int)v;
                }
                if (!WriteAll(loud, frames * Channels))
                {
                    _ended = _drained = true;
                    break;
                }
                _written += frames;
                _live = true;
                Clock();
            }
        }
        catch (Exception ex)
        {
            // The engine's thread, not the app's: say so and stop, no more.
            Console.WriteLine($"[engine] output: {ex.Message}");
            _ended = _drained = true;
        }
        finally
        {
            ioctl(_fd, SNDCTL_DSP_RESET, null);
            close(_fd);
            _fd = -1;
        }
    }

    /// Format, channels, rate and buffer, in the order OSS asks for them. Done
    /// again after every reset, since the device need not keep them.
    private bool Configure()
    {
        // About 0.2 s in 4 KiB fragments: the high half is how many, the low the size as a power of two.
        var fragments = Math.Max(2, (int)Math.Ceiling(Buffered * SampleRate * BytesPerFrame / 4096));
        var fragment = (fragments << 16) | 12;
        ioctl(_fd, SNDCTL_DSP_SETFRAGMENT, &fragment);     // a request; the kernel may round it

        var format = AFMT_S32_LE;
        if (ioctl(_fd, SNDCTL_DSP_SETFMT, &format) < 0 || format != AFMT_S32_LE)
        {
            Console.WriteLine($"[engine] output: {Device} will not take 32-bit samples (got format 0x{format:x})");
            return false;
        }
        var channels = Channels;
        if (ioctl(_fd, SNDCTL_DSP_CHANNELS, &channels) < 0 || channels != Channels)
        {
            Console.WriteLine($"[engine] output: {Device} will not take {Channels} channels (got {channels})");
            return false;
        }
        var rate = SampleRate;
        if (ioctl(_fd, SNDCTL_DSP_SPEED, &rate) < 0 || rate != SampleRate)
        {
            // With virtual channels the kernel converts any rate; only a device in
            // bit-perfect mode can refuse one, and playing at its rate would play
            // at the wrong speed.
            Console.WriteLine($"[engine] output: {Device} will not play at {SampleRate} Hz (offers {rate} Hz; bit-perfect mode?)");
            return false;
        }
        return true;
    }

    /// write(2) until all of it is with the kernel, through signals.
    private bool WriteAll(int[] samples, int count)
    {
        var bytes = (nint)count * 4;
        nint done = 0;
        fixed (int* p = samples)
        {
            while (done < bytes)
            {
                var n = write(_fd, (byte*)p + done, (nuint)(bytes - done));
                if (n < 0)
                {
                    if (Marshal.GetLastPInvokeError() == EINTR) continue;
                    Console.WriteLine($"[engine] output: write: {LastError()}");
                    return false;
                }
                done += n;
            }
        }
        return true;
    }

    /// Frames written but not yet played, as the kernel counts them.
    private long Unplayed()
    {
        int bytes;
        return ioctl(_fd, SNDCTL_DSP_GETODELAY, &bytes) < 0 ? 0 : bytes / BytesPerFrame;
    }

    /// Where the speakers are: what has been written, less what the kernel
    /// says it still has to play.
    private void Clock()
    {
        var behind = Unplayed();
        lock (_gate)
        {
            if (!_wantPlaying) return;      // a pause has stopped the clock
            _base = Math.Clamp(_written - behind, Math.Min(_base, _written), _written);
            _baseAt = Stopwatch.GetTimestamp();
            _limit = _written;
            _running = true;
        }
    }

    /// A pause: what the kernel has not played yet is thrown away and kept,
    /// ahead of anything still kept from an earlier pause.
    private void Hold()
    {
        var unplayed = Math.Clamp(Unplayed(), 0, Math.Min(_written, _history.Length / Channels));
        ioctl(_fd, SNDCTL_DSP_RESET, null);
        Configure();

        var rest = _pending.Length - _pendingAt;
        var kept = new float[unplayed * Channels + rest];
        var cap = _history.Length;
        var from = (int)((_written - unplayed) * Channels % cap);
        for (var i = 0; i < unplayed * Channels; i++) kept[i] = _history[(from + i) % cap];
        _pending.AsSpan(_pendingAt, rest).CopyTo(kept.AsSpan((int)(unplayed * Channels)));
        _pending = kept;
        _pendingAt = 0;

        _written -= unplayed;
        _live = false;
        lock (_gate) { _base = _limit = _written; _running = false; }
    }

    /// The last second written, for Hold.
    private void Remember(ReadOnlySpan<float> samples)
    {
        var cap = _history.Length;
        var at = (int)(_written * Channels % cap);
        for (var i = 0; i < samples.Length; i++) _history[(at + i) % cap] = samples[i];
    }

    /// Immediately: what was waiting is thrown away, and the source is not
    /// asked for more after this returns.
    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            _stopping = true;
            thread = _thread;
            Monitor.PulseAll(_gate);
        }
        if (thread is not null && !thread.Join(2000))
            Console.WriteLine("[engine] output: the writer did not stop within 2 s");
    }

    private static string LastError() => Marshal.GetPInvokeErrorMessage(Marshal.GetLastPInvokeError());

    // <fcntl.h>, <errno.h> and <sys/soundcard.h>, FreeBSD 15.
    private const int O_WRONLY = 0x0001;
    private const int O_CLOEXEC = 0x00100000;
    private const int EINTR = 4;
    private const int AFMT_S32_LE = 0x00001000;
    private const ulong SNDCTL_DSP_RESET = 0x20005000;
    private const ulong SNDCTL_DSP_SYNC = 0x20005001;
    private const ulong SNDCTL_DSP_SPEED = 0xc0045002;
    private const ulong SNDCTL_DSP_SETFMT = 0xc0045005;
    private const ulong SNDCTL_DSP_CHANNELS = 0xc0045006;
    private const ulong SNDCTL_DSP_SETFRAGMENT = 0xc004500a;
    private const ulong SNDCTL_DSP_GETODELAY = 0x40045017;

    // libc by its versioned name: FreeBSD's /usr/lib/libc.so is a linker
    // script, which dlopen cannot load.
    private const string Libc = "libc.so.7";

    [DllImport(Libc, SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport(Libc, SetLastError = true)] private static extern int close(int fd);
    [DllImport(Libc, SetLastError = true)] private static extern nint write(int fd, void* buf, nuint count);
    // ioctl(2) is variadic in C; on amd64 and arm64 FreeBSD a pointer passed as the
    // third argument goes where a fixed third argument would.
    [DllImport(Libc, SetLastError = true)] private static extern int ioctl(int fd, ulong request, void* arg);
}
