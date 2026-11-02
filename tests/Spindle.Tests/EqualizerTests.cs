using Spindle.Playback;

namespace Spindle.Tests;

public sealed class EqualizerTests
{
    private static float[] Noise(int n)
    {
        var r = new Random(7);
        return Enumerable.Range(0, n).Select(_ => (float)(r.NextDouble() * 1.2 - 0.6)).ToArray();
    }

    /// Off, flat or absent, the sound goes through untouched: not one sample changed.
    [Fact]
    public void Off_or_flat_passes_the_sound_through_untouched()
    {
        foreach (var eq in new EqSettings?[] { null, new() { On = false }, new() { On = true } })
        {
            var samples = Noise(48000 * 2);
            var before = samples.ToArray();
            new EqualizerStage(48000, 2, () => eq).Process(samples);
            Assert.Equal(before, samples);
        }
    }

    [Fact]
    public void A_boost_is_heard_where_it_is_set()
    {
        var eq = new EqSettings { On = true };
        eq.Bands[1].Frequency = 1000;
        eq.Bands[1].Gain = 6;
        Assert.InRange(Equalizer.ResponseDb(eq, 1000), 5.9, 6.1);
        Assert.InRange(Math.Abs(Equalizer.ResponseDb(eq, 15000)), 0, 0.5);
    }
}
