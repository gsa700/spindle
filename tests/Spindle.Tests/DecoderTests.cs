using System.Runtime.InteropServices;
using Spindle.Codecs;

namespace Spindle.Tests;

/// Every decoder against ffmpeg's decode of the same file, by tools/engine-check.cs
/// compare's rule: lossless formats sample for sample, lossy ones within ISO
/// full accuracy (RMS under 2^-15/sqrt(12), no sample off by more than 2^-14),
/// and every one the same length, gapless trimming included.
public sealed class DecoderTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    public static TheoryData<string> Files => new()
    {
        "tone16.wav", "tone24.wav", "tone16.flac", "tone24.flac", "tone16-alac.m4a", "tone-aac.m4a", "mono.mp3",
    };

    private static float[] All(IAudioTrack t)
    {
        var all = new List<float>();
        var buf = new float[4096 * t.Channels];
        int n;
        while ((n = t.Read(buf)) > 0) all.AddRange(buf.AsSpan(0, n * t.Channels));
        return all.ToArray();
    }

    private static float[] Ffmpeg(string file) =>
        MemoryMarshal.Cast<byte, float>(File.ReadAllBytes(Fixture(file + ".f32"))).ToArray();

    [Theory]
    [MemberData(nameof(Files))]
    public void Decodes_as_ffmpeg_does(string file)
    {
        var t = AudioTracks.Open(file, Fixture(file));
        var ours = All(t);
        var reference = Ffmpeg(file);
        Assert.Equal(reference.Length / t.Channels, ours.Length / t.Channels);
        // The length the track states, where its format states it: an MP3 with
        // no LAME/Info tag only has an estimate from its size (Mp3Track).
        if (t is not Mp3Track { StartPad: 0, EndPad: 0 }) Assert.Equal(t.TotalFrames, ours.Length / t.Channels);

        double sum = 0, max = 0;
        for (var i = 0; i < ours.Length; i++)
        {
            var d = Math.Abs((double)ours[i] - reference[i]);
            sum += d * d;
            max = Math.Max(max, d);
        }
        if (t.BitsPerSample > 0)
            Assert.Equal(0, max);
        else
        {
            Assert.True(Math.Sqrt(sum / ours.Length) < Math.Pow(2, -15) / Math.Sqrt(12), $"rms {Math.Sqrt(sum / ours.Length):0.0e0}");
            Assert.True(max <= Math.Pow(2, -14), $"max {max:0.0e0}");
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void A_seek_lands_on_the_exact_sample(string file)
    {
        var whole = All(AudioTracks.Open(file, Fixture(file)));
        var t = AudioTracks.Open(file, Fixture(file));
        var ch = t.Channels;
        var frames = whole.Length / ch;
        var buf = new float[256 * ch];
        foreach (var at in new long[] { frames / 2, 1, frames - 300, 0, 4097, frames / 3 })
        {
            t.Seek(at);
            var n = t.Read(buf);
            Assert.Equal(Math.Min(256, frames - at), n);
            Assert.Equal(whole.AsSpan((int)at * ch, n * ch).ToArray(), buf.AsSpan(0, n * ch).ToArray());
        }
    }

    /// Security review 2026-10-26, finding 4: a WAV header claiming
    /// 3,221,225,472 Hz reached an output as a negative rate and took the app
    /// down. It is refused when the track is opened.
    [Fact]
    public void A_wav_claiming_an_impossible_rate_is_refused()
    {
        var bytes = File.ReadAllBytes(Fixture("tone16.wav"));
        var fmt = IndexOf(bytes, "fmt "u8);
        BitConverter.GetBytes(3_221_225_472u).CopyTo(bytes, fmt + 12);
        var path = Path.Combine(Path.GetTempPath(), $"spindle-rate-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, bytes);
        try { Assert.ThrowsAny<IOException>(() => AudioTracks.Open("rate.wav", path)); }
        finally { DeleteSoon(path); }
    }

    /// A file cut short (a copy stopped part way, a disk error) ends early or
    /// is refused as damaged; it never hangs or takes the engine down.
    [Theory]
    [MemberData(nameof(Files))]
    public async Task A_file_cut_short_ends_or_is_refused(string file)
    {
        var bytes = File.ReadAllBytes(Fixture(file));
        var path = Path.Combine(Path.GetTempPath(), $"spindle-cut-{Guid.NewGuid():N}{Path.GetExtension(file)}");
        File.WriteAllBytes(path, bytes[..(bytes.Length * 2 / 3)]);
        try
        {
            var run = Task.Run(() =>
            {
                try
                {
                    var t = AudioTracks.Open(file, path);
                    return All(t).Length / t.Channels;
                }
                catch (IOException) { return -1; }
            });
            var frames = await run.WaitAsync(TimeSpan.FromSeconds(10));     // throws TimeoutException if it hangs
            var whole = Ffmpeg(file).Length / AudioTracks.Open(file, Fixture(file)).Channels;
            Assert.True(frames < whole, $"{frames} of {whole} frames from two thirds of the file");
        }
        finally { DeleteSoon(path); }
    }

    private static int IndexOf(byte[] bytes, ReadOnlySpan<byte> what) => bytes.AsSpan().IndexOf(what);

    /// Deletes a file Hymie may still be reading: a track opens at once and its
    /// bytes arrive in the background (TrackBytes.Fill), which closes the file
    /// when it has read it, a moment after a refusal. Windows will not delete a
    /// file that is open (CI, 2026-11-02), so this tries for a second.
    private static void DeleteSoon(string path)
    {
        for (var i = 0; ; i++)
        {
            try { File.Delete(path); return; }
            catch (IOException) when (i < 20) { Thread.Sleep(50); }
        }
    }
}
