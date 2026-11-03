// Spindle — the three JSON reads the update check needs, from Marginal.Core's Json.
using System.Text.Json;

namespace Spindle.Install;

internal static class JsonText
{
    public static JsonElement Get(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

    public static string? Str(JsonElement e, string name) =>
        Get(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static bool True(JsonElement e, string name) => Get(e, name).ValueKind == JsonValueKind.True;
}
