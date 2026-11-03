using System.Net;
using System.Text;
using System.Text.Json;

namespace Spindle.Tests;

/// LRCLIB, never asked for real: its answers' shape, the 2 s rule, the retries
/// (Dave, 2026-10-31: a 503 twice in a row ended Find lyrics for the album),
/// and answers of the wrong shape (Marginal's security review, A2, A8). From
/// Marginal's LyricsTests, LrcLibRetryTests and ParsingSecurityTests.
public sealed class LrcLibTests
{
    private const string Synced = "[00:01.00] line one\n[00:02.50] line two\n[00:04.25] line three";

    // ---- LRCLIB's answers and the 2 s rule ---------------------------------------------

    private static LrcLibResult Result(string track, double duration, bool synced = true, string artist = "Fixture Band", string album = "Test Album") =>
        LrcLibClient.Result(JsonDocument.Parse(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = 1, ["trackName"] = track, ["artistName"] = artist, ["albumName"] = album, ["duration"] = duration,
            ["instrumental"] = false, ["plainLyrics"] = "line one\nline two", ["syncedLyrics"] = synced ? Synced : null,
        })).RootElement);

    [Fact]
    public void Reads_an_answer_in_lrclibs_shape()
    {
        var r = Result("Second", 61.2);
        Assert.Equal(("Second", "Fixture Band", "Test Album", 61.2), (r.Track, r.Artist, r.Album, r.Duration));
        Assert.True(r.HasSynced && r.HasPlain);
        Assert.False(Result("Second", 61, synced: false).HasSynced);
    }

    [Theory]
    [InlineData(61.0, true)]       // the same length
    [InlineData(62.4, true)]       // within 2 s
    [InlineData(64.0, false)]      // 3 s: asked first
    [InlineData(92.0, false)]      // a live version
    public void Automatic_only_within_two_seconds(double duration, bool automatic) =>
        Assert.Equal(automatic, LrcLibClient.Fit(Result("Second", duration), "Fixture Band", "Second", TimeSpan.FromSeconds(61)).Automatic);

    [Fact]
    public void Another_title_or_artist_is_never_automatic()
    {
        Assert.False(LrcLibClient.Fit(Result("Second (Live)", 61), "Fixture Band", "Second", TimeSpan.FromSeconds(61)).Automatic);
        Assert.False(LrcLibClient.Fit(Result("Second", 61, artist: "Someone Else"), "Fixture Band", "Second", TimeSpan.FromSeconds(61)).Automatic);
        // Case and punctuation do not count as different.
        Assert.True(LrcLibClient.Fit(Result("second!", 61, artist: "fixture band"), "Fixture Band", "Second", TimeSpan.FromSeconds(61)).Automatic);
    }

    [Fact]
    public void Ranks_automatic_then_synced_then_nearest()
    {
        var ranked = LrcLibClient.Rank([Result("Second", 92), Result("Second", 61, synced: false), Result("Second", 61.5)],
            "Fixture Band", "Second", TimeSpan.FromSeconds(61));
        Assert.Equal([61.5, 61.0, 92.0], ranked.Select(f => f.Result.Duration));
    }

    private static readonly TimeSpan[] NoWait = [TimeSpan.Zero, TimeSpan.Zero];

    /// Answers with each status in turn, then the last one forever; counts the questions.
    private sealed class Script(params HttpStatusCode[] codes) : HttpMessageHandler
    {
        public int Asked;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var code = codes[Math.Min(Asked, codes.Length - 1)];
            Asked++;
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task A_503_is_asked_three_times_before_giving_up()
    {
        var script = new Script(HttpStatusCode.ServiceUnavailable);
        using var lrc = new LrcLibClient(script, NoWait);
        var e = await Assert.ThrowsAsync<LrcLibException>(() => lrc.SearchAsync("A", "T"));
        Assert.Equal(3, script.Asked);
        Assert.Contains("503", e.Message);
    }

    [Fact]
    public async Task An_answer_on_the_third_try_is_taken()
    {
        var script = new Script(HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK);
        using var lrc = new LrcLibClient(script, NoWait);
        Assert.Empty(await lrc.SearchAsync("A", "T"));
        Assert.Equal(3, script.Asked);
    }

    [Fact]
    public async Task Not_found_and_other_4xx_are_not_asked_again()
    {
        var notFound = new Script(HttpStatusCode.NotFound);
        using (var lrc = new LrcLibClient(notFound, NoWait)) Assert.Null(await lrc.GetAsync("A", "T", "", TimeSpan.Zero));
        Assert.Equal(1, notFound.Asked);

        var tooMany = new Script(HttpStatusCode.TooManyRequests);
        using (var lrc = new LrcLibClient(tooMany, NoWait)) await Assert.ThrowsAsync<LrcLibException>(() => lrc.SearchAsync("A", "T"));
        Assert.Equal(1, tooMany.Asked);
    }

    [Fact]
    public void The_default_waits_are_2_then_6_seconds() =>
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)], LrcLibClient.DefaultRetryWaits);

    // ---- A2, A8: web answers of the wrong shape ----------------------------------

    private sealed class Answer(HttpStatusCode code, string body, string type = "application/json") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, type) });
    }

    private sealed class Endless : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new Zeros()) });

        private sealed class Zeros : Stream
        {
            public override int Read(byte[] buffer, int offset, int count) { Array.Clear(buffer, offset, count); return count; }
            public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();
            public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        }
    }

    private const string Html = "<html><body>Down for maintenance</body></html>";

    [Fact]
    public async Task LrcLib_maintenance_page_is_LRCLIB_failing()
    {
        using var lrc = new LrcLibClient(new Answer(HttpStatusCode.OK, Html, "text/html"));
        await Assert.ThrowsAsync<LrcLibException>(() => lrc.SearchAsync("a", "b"));
        await Assert.ThrowsAsync<LrcLibException>(() => lrc.GetAsync("a", "b", "c", TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public async Task LrcLib_odd_values_are_read_as_none()
    {
        const string body = """[{"id":1.5,"trackName":"T","artistName":"A","duration":-1e300,"syncedLyrics":"[00:01.00] x"}, 5, "x", {"id":3e30}]""";
        using var lrc = new LrcLibClient(new Answer(HttpStatusCode.OK, body));
        var results = await lrc.SearchAsync("A", "T");
        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[0].Id);
        Assert.Equal(0, results[0].Duration);
        var fit = LrcLibClient.Fit(results[0], "A", "T", TimeSpan.FromMinutes(3));
        Assert.False(fit.Automatic);
        Assert.Equal("length unknown", fit.Text);
        Assert.Empty(await new LrcLibClient(new Answer(HttpStatusCode.OK, "[1,2]")).SearchAsync("A", "T"));
        Assert.Null(await new LrcLibClient(new Answer(HttpStatusCode.OK, "[1,2]")).GetAsync("A", "T", "", TimeSpan.Zero));
    }

    [Fact]
    public async Task LrcLib_endless_answer_is_cut_off()
    {
        using var lrc = new LrcLibClient(new Endless());
        await Assert.ThrowsAsync<LrcLibException>(() => lrc.SearchAsync("a", "b"));
    }

    [Fact]
    public void It_says_who_asks()
    {
        var seen = new List<string>();
        var handler = new Seen(seen);
        using (var named = new LrcLibClient(handler, userAgent: "Deadwax/0.9.0 ( https://github.com/gsa700/deadwax )"))
            named.SearchAsync("A", "T").GetAwaiter().GetResult();
        Assert.StartsWith("Deadwax/0.9.0", seen.Single());
    }

    private sealed class Seen(List<string> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            seen.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
        }
    }
}
