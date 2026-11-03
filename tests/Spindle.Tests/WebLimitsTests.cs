namespace Spindle.Tests;

/// From Deadwax's SecurityTests (security review 2026-10-10), with WebLimits.
public class WebLimitsTests
{
    [Fact]
    public void Only_real_mbids_and_archive_urls_are_used()
    {
        Assert.True(WebLimits.IsMbid("8f6bd1e4-fbe1-4f50-aa9b-94c450ec0f11"));
        Assert.False(WebLimits.IsMbid("../../../x"));
        Assert.False(WebLimits.IsMbid("8f6bd1e4-fbe1-4f50-aa9b-94c450ec0f1/"));
        Assert.True(WebLimits.IsArchiveUrl("https://coverartarchive.org/release/x/1-500.jpg"));
        Assert.True(WebLimits.IsArchiveUrl("https://ia800.us.archive.org/x.jpg"));
        Assert.False(WebLimits.IsArchiveUrl("http://coverartarchive.org/x.jpg"));
        Assert.False(WebLimits.IsArchiveUrl("https://192.0.2.10/x.jpg"));
        Assert.False(WebLimits.IsArchiveUrl("https://notarchive.org/x.jpg"));
        Assert.False(WebLimits.IsArchiveUrl("https://archive.org.evil.example/x.jpg"));
    }

    [Fact]
    public async Task Oversized_downloads_are_refused()
    {
        var small = new ByteArrayContent(new byte[100]);
        Assert.Equal(100, (await WebLimits.ReadCappedAsync(small, 1000, default))!.Length);
        var big = new ByteArrayContent(new byte[2000]);
        big.Headers.ContentLength = null;   // no header: caught as it arrives
        Assert.Null(await WebLimits.ReadCappedAsync(new StreamContent(new MemoryStream(new byte[2000])), 1000, default));
        Assert.Null(await WebLimits.ReadCappedAsync(big, 1000, default));
    }
}
