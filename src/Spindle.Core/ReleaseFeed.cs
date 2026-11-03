// Spindle — the two release channels, Edge and Stable, read from GitHub.
//
// From the apps (AlbumWall.Domain, Deadwax.Core and Marginal.Core each had the
// same code), moved here 2026-11-02.
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spindle;

/// The two release channels (spec: release model, 2026-10-10). Every build is
/// published as a GitHub pre-release: that is Edge. A build that has run on Edge
/// without trouble is promoted, the same binary with its pre-release flag
/// cleared: that is Stable. GitHub's /releases/latest never returns a
/// pre-release, so Stable reads that; Edge reads the release list and takes the
/// newest version in it, pre-release or not.
public static partial class ReleaseFeed
{
    /// Only tags shaped exactly vX.Y.Z (what every app's release.sh makes)
    /// are app releases: AlbumWall's repository also holds libmpv-* engine
    /// pre-releases that must never be offered.
    [GeneratedRegex(@"^v\d+\.\d+\.\d+$")]
    private static partial Regex AppTag();

    public static bool IsAppTag(string? tag) => tag is not null && AppTag().IsMatch(tag);

    /// The newest release in a /releases list by version (VersionOrder), never a
    /// draft; null when the list holds none.
    public static JsonElement? Newest(JsonElement releases)
    {
        if (releases.ValueKind != JsonValueKind.Array) return null;
        JsonElement? best = null;
        string? bestTag = null;
        foreach (var r in releases.EnumerateArray())
        {
            if (r.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True) continue;
            if (!r.TryGetProperty("tag_name", out var t) || t.GetString() is not { } tag || !IsAppTag(tag)) continue;
            if (bestTag is null || VersionOrder.IsNewer(tag, bestTag)) { best = r; bestTag = tag; }
        }
        return best;
    }
}
