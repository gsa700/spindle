using System.Diagnostics;

namespace Spindle.Tests;

/// Synced lyrics in the LRC form (Lrc), from Marginal's LyricsTests and its
/// security review's lyrics cases (2026-10-31, A3, A4, A10).
public sealed class LrcTests
{
    private const string Synced = "[00:01.00] line one\n[00:02.50] line two\n[00:04.25] line three";

    // ---- LRC -----------------------------------------------------------------------

    [Fact]
    public void Parses_times_in_order()
    {
        var lines = Lrc.Parse("[ar:Somebody]\n[00:02.50] second\n[00:01.00] first\n\n[01:05] third");
        Assert.Equal(["first", "second", "third"], lines.Select(l => l.Text));
        Assert.Equal(TimeSpan.FromMilliseconds(2500), lines[1].Time);
        Assert.Equal(TimeSpan.FromSeconds(65), lines[2].Time);
    }

    [Fact]
    public void A_line_with_two_times_is_sung_twice_and_offset_moves_them()
    {
        var lines = Lrc.Parse("[offset:+500]\n[00:10.00][00:30.00] chorus");
        Assert.Equal([TimeSpan.FromSeconds(9.5), TimeSpan.FromSeconds(29.5)], lines.Select(l => l.Time));
        Assert.All(lines, l => Assert.Equal("chorus", l.Text));
    }

    [Fact]
    public void Formats_and_reads_back()
    {
        var text = Lrc.Format(Lrc.Parse(Synced));
        Assert.Equal(Synced, text);
    }

    [Fact]
    public void Plain_takes_the_times_out()
    {
        Assert.Equal("line one\nline two\nline three", Lrc.Plain(Synced));
        Assert.Equal("just words", Lrc.Plain("just words  \r\n"));
    }

    // ---- timing by hand ------------------------------------------------------------

    [Fact]
    public void Tapping_stamps_plain_lines_one_after_another()
    {
        var text = "[ar:Somebody]\nline one\n\nline two";
        var first = Lrc.NextTimeable(text, -1);
        Assert.Equal(1, first);                                    // the header and the blank are passed over
        text = Lrc.SetTime(text, first, TimeSpan.FromSeconds(12.34));
        var second = Lrc.NextTimeable(text, first);
        Assert.Equal(3, second);
        text = Lrc.SetTime(text, second, TimeSpan.FromSeconds(15.5));
        Assert.Equal("[ar:Somebody]\n[00:12.34] line one\n\n[00:15.50] line two", text);
        Assert.Equal(-1, Lrc.NextTimeable(text, second));
    }

    [Fact]
    public void A_new_time_replaces_the_old_one()
    {
        var text = Lrc.SetTime(Synced, 1, TimeSpan.FromSeconds(3));
        Assert.Equal("[00:03.00] line two", Lrc.Lines(text)[1]);
        Assert.Equal(3, Lrc.Parse(text).Count);
    }

    [Fact]
    public void A_stamp_can_be_taken_off_again()
    {
        var text = Lrc.ClearTime(Synced, 1);
        Assert.Equal("line two", Lrc.Lines(text)[1]);
        Assert.Equal(2, Lrc.Parse(text).Count);
        Assert.Equal(text, Lrc.ClearTime(text, 1));             // nothing to take off: unchanged
    }

    [Fact]
    public void Nudge_and_shift()
    {
        var nudged = Lrc.Nudge(Synced, 0, TimeSpan.FromMilliseconds(100));
        Assert.Equal("[00:01.10] line one", Lrc.Lines(nudged)[0]);
        Assert.Equal("[00:00.90] line one", Lrc.Lines(Lrc.Nudge(Synced, 0, TimeSpan.FromMilliseconds(-100)))[0]);
        var shifted = Lrc.ShiftAll(Synced, TimeSpan.FromSeconds(-2));
        Assert.Equal([TimeSpan.Zero, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(2.25)], Lrc.Parse(shifted).Select(l => l.Time));
    }

    [Fact]
    public void The_line_being_sung()
    {
        Assert.Equal(-1, Lrc.LineAt(Synced, TimeSpan.FromSeconds(0.5)));
        Assert.Equal(0, Lrc.LineAt(Synced, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, Lrc.LineAt(Synced, TimeSpan.FromSeconds(3)));
        Assert.Equal(2, Lrc.LineAt(Synced, TimeSpan.FromMinutes(5)));
    }

    // ---- A3, A4, A10: lyrics -------------------------------------------------

    private const string Huge = "[offset:99999999999]\n[00:01.00] first line\n[00:05.00] second line";

    [Fact]
    public void Offset_too_large_is_no_offset()
    {
        Assert.Equal(TimeSpan.Zero, Lrc.Offset(Huge));
        Assert.Equal(2, Lrc.Parse(Huge).Count);
        Assert.Equal(2, Lrc.LineAt(Huge, TimeSpan.FromSeconds(6)));   // line 0 is the header
        Assert.Contains("[00:03.00] first line", Lrc.ShiftAll(Huge, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Offset_is_applied_when_playing()
    {
        const string text = "[offset:+2000]\n[00:03.00] first\n[00:10.00] second";
        Assert.Equal(TimeSpan.FromSeconds(2), Lrc.Offset(text));
        Assert.Equal(1, Lrc.LineAt(text, TimeSpan.FromSeconds(1.5)));   // 3 s - 2 s; line 0 is the header
        Assert.Equal(-1, Lrc.LineAt(text, TimeSpan.FromSeconds(0.5)));
        Assert.Equal(TimeSpan.FromSeconds(3), Lrc.TimeOf("[00:03.00] first"));   // as written
    }

    [Fact]
    public void Blank_lines_are_read_in_linear_time()
    {
        var text = "a" + new string('\n', 300_000) + "b";
        var clock = Stopwatch.StartNew();
        Assert.Equal(LyricsKind.Plain, Lrc.KindOf(text));
        Assert.Equal(TimeSpan.Zero, Lrc.Offset(text));
        Assert.True(clock.ElapsedMilliseconds < 1000, $"{clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Long_lyrics_shift_and_play_quickly()
    {
        var text = string.Join('\n', Enumerable.Range(0, 20_000).Select(i => $"[{i / 60 % 100:00}:{i % 60:00}.00] line {i}"));
        var clock = Stopwatch.StartNew();
        var shifted = Lrc.ShiftAll(text, TimeSpan.FromSeconds(1));
        Assert.True(clock.ElapsedMilliseconds < 2000, $"ShiftAll {clock.ElapsedMilliseconds} ms");
        Assert.StartsWith("[00:01.00] line 0", shifted);
        var timeline = Lrc.TimelineOf(text);
        clock.Restart();
        for (var i = 0; i < 20; i++) timeline.LineAt(TimeSpan.FromMinutes(10));
        Assert.True(clock.ElapsedMilliseconds < 500, $"LineAt {clock.ElapsedMilliseconds} ms for 20");
    }

    [Theory]
    [InlineData(null, LyricsKind.None)]
    [InlineData("  \n ", LyricsKind.None)]
    [InlineData("just words\nmore words", LyricsKind.Plain)]
    [InlineData("[ar:Someone]\n[00:12.40] a line", LyricsKind.Synced)]
    [InlineData("  [01:02] indented", LyricsKind.Synced)]
    public void What_kind_of_lyrics_a_text_is(string? text, LyricsKind kind) =>
        Assert.Equal(kind, Lrc.KindOf(text));
}
