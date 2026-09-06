namespace CodexTrayMeter;

internal enum DisplayMode { FiveHourBar, FiveHourPercent, WeekBar, WeekPercent, Reset, FiveHourReset, WeekReset, None }

internal static class DisplayContent
{
    public static string Label(DisplayMode mode) => mode switch
    {
        DisplayMode.FiveHourBar => "5H Bar", DisplayMode.FiveHourPercent => "5H %",
        DisplayMode.WeekBar => "Week Bar", DisplayMode.WeekPercent => "Week %",
        DisplayMode.Reset => "Reset（最も近いリセット）",
        DisplayMode.FiveHourReset => "5H Reset", DisplayMode.WeekReset => "Week Reset", _ => "非表示"
    };

    public static UsageWindow? Window(UsageSnapshot snapshot, DisplayMode mode) => mode switch
    {
        DisplayMode.FiveHourBar or DisplayMode.FiveHourPercent or DisplayMode.FiveHourReset => snapshot.Windows.FirstOrDefault(w => w.DurationMinutes == 300),
        DisplayMode.WeekBar or DisplayMode.WeekPercent or DisplayMode.WeekReset => snapshot.Windows.FirstOrDefault(w => w.DurationMinutes == 10080),
        DisplayMode.Reset => snapshot.Windows.Where(w => w.ResetsAt.HasValue).OrderBy(w => w.ResetsAt).FirstOrDefault(),
        _ => null
    };

    public static bool IsBar(DisplayMode mode) => mode is DisplayMode.FiveHourBar or DisplayMode.WeekBar;
    public static bool IsReset(DisplayMode mode) => mode is DisplayMode.Reset or DisplayMode.FiveHourReset or DisplayMode.WeekReset;

    public static string Countdown(DateTimeOffset? reset, DateTimeOffset now)
    {
        if (reset is null) return "--";
        var remaining = reset.Value - now;
        if (remaining <= TimeSpan.Zero) return "0m";
        // 切り上げ表示。60mは1hに、72hは3dに揃え、最大3文字に収める。
        var minutes = Math.Ceiling(remaining.TotalMinutes);
        if (minutes < 60) return $"{minutes:0}m";
        var hours = Math.Ceiling(remaining.TotalHours);
        if (hours < 72) return $"{hours:0}h";
        var days = Math.Ceiling(remaining.TotalDays);
        return days > 99 ? "99+" : $"{days:0}d";
    }

    public static double Percent(UsageWindow window, bool showRemaining = false)
    {
        var used = Math.Clamp(window.UsedPercent, 0, 100);
        return showRemaining ? 100 - used : used;
    }

    public static string Text(UsageSnapshot snapshot, DisplayMode mode, DateTimeOffset now, bool showRemaining = false)
    {
        if (mode == DisplayMode.None) return "";
        var window = Window(snapshot, mode);
        if (IsReset(mode)) return Countdown(window?.ResetsAt, now);
        return window is null ? "--" : $"{Math.Round(Percent(window, showRemaining)):0}%";
    }
}


