namespace CodexTrayMeter;

// プラン名から枠を推測せず、取得できた期間と使用率を表示するためのモデル。
internal sealed record UsageWindow(int DurationMinutes, double UsedPercent, DateTimeOffset? ResetsAt = null)
{
    public string Label => DurationMinutes switch
    {
        300 => "5時間",
        10080 => "週間",
        _ when DurationMinutes % 1440 == 0 => $"{DurationMinutes / 1440}日",
        _ when DurationMinutes % 60 == 0 => $"{DurationMinutes / 60}時間",
        _ => $"{DurationMinutes}分"
    };
}

internal sealed record UsageSnapshot(string Name, IReadOnlyList<UsageWindow> Windows);


