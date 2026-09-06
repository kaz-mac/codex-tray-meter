using System.Text.Json;

namespace CodexTrayMeter;

internal static class RateLimitParser
{
    public static UsageSnapshot Parse(JsonElement result)
    {
        JsonElement bucket;
        if (result.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object)
        {
            // 別モデル用の枠を通常のCodex枠として表示しない。
            if (!map.TryGetProperty("codex", out bucket)) return new("Codex枠のデータなし", []);
        }
        else if (!result.TryGetProperty("rateLimits", out bucket))
            throw new InvalidDataException("使用量の応答形式を認識できません。Codexを更新してください。");
        if (bucket.ValueKind != JsonValueKind.Object) return new("使用量のデータなし", []);
        if (bucket.TryGetProperty("limitId", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { } name && name != "codex")
            return new("Codex枠のデータなし", []);
        List<UsageWindow> windows = [];
        foreach (var key in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(key, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (!window.TryGetProperty("windowDurationMins", out var duration) || duration.ValueKind != JsonValueKind.Number || !duration.TryGetInt32(out var minutes) || minutes <= 0) continue;
            if (!window.TryGetProperty("usedPercent", out var usage) || usage.ValueKind != JsonValueKind.Number || !usage.TryGetDouble(out var percent) || !double.IsFinite(percent) || percent < 0) continue;
            DateTimeOffset? reset = null;
            if (window.TryGetProperty("resetsAt", out var timestamp) && timestamp.ValueKind == JsonValueKind.Number && timestamp.TryGetInt64(out var seconds))
            {
                try { reset = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                catch (ArgumentOutOfRangeException) { }
            }
            windows.Add(new(minutes, percent, reset));
        }
        return new(windows.Count == 0 ? "使用量のデータなし" : "実データ", windows.OrderBy(w => w.DurationMinutes).ToArray());
    }
}


