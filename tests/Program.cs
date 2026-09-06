using CodexTrayMeter;
using System.Text.Json;
var now = DateTimeOffset.Parse("2026-09-06T12:00:00+09:00");
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
UsageSnapshot Parse(string json) { using var doc = JsonDocument.Parse(json); return RateLimitParser.Parse(doc.RootElement); }
foreach (var (minutes, expected) in new (double, string)[] { (-1,"0m"),(0.01,"1m"),(36,"36m"),(59.1,"1h"),(3360,"56h"),(4320,"3d"),(5760,"4d"),(144000,"99+") })
    Check(DisplayContent.Countdown(now.AddMinutes(minutes), now) == expected, expected);
Check(DisplayContent.Countdown(null, now) == "--", "Missing reset");
var week = Parse("""{"rateLimits":{"primary":null,"secondary":{"windowDurationMins":10080,"usedPercent":12,"resetsAt":1800000000}}}""");
Check(DisplayContent.Text(week, DisplayMode.FiveHourPercent, now, true) == "--", "Missing 5H");
Check(DisplayContent.Text(week, DisplayMode.WeekPercent, now, true) == "88%", "Remaining");
var missing = Parse("""{"rateLimits":{"primary":{"windowDurationMins":300,"usedPercent":null},"secondary":{"windowDurationMins":null,"usedPercent":3}}}""");
Check(missing.Windows.Count == 0, "Null is not zero");
var zero = Parse("""{"rateLimits":{"primary":{"windowDurationMins":300,"usedPercent":0}}}""");
Check(DisplayContent.Text(zero, DisplayMode.FiveHourPercent, now) == "0%", "Zero");
Check(DisplayContent.Text(zero, DisplayMode.FiveHourPercent, now, true) == "100%", "Full remaining");
var other = Parse("""{"rateLimitsByLimitId":{"codex_other":{"primary":{"windowDurationMins":300,"usedPercent":80}}},"rateLimits":{"primary":{"windowDurationMins":300,"usedPercent":44}}}""");
Check(other.Windows.Count == 0, "Do not mix buckets");
var multiple = Parse("""{"rateLimitsByLimitId":{"codex":{"primary":{"windowDurationMins":10080,"usedPercent":20},"secondary":{"windowDurationMins":300,"usedPercent":5,"resetsAt":9223372036854775807}}},"rateLimits":{"primary":{"windowDurationMins":300,"usedPercent":99}}}""");
Check(DisplayContent.Text(multiple, DisplayMode.FiveHourPercent, now) == "5%", "Bucket precedence");
Check(DisplayContent.Text(multiple, DisplayMode.FiveHourReset, now) == "--", "Invalid timestamp");
var legacy = DisplaySettings.Parse("[1,4]");
Check(legacy == new DisplaySettings(DisplayMode.FiveHourPercent, DisplayMode.Reset), "Legacy settings");
var setting = new DisplaySettings(DisplayMode.WeekPercent, DisplayMode.WeekBar, true);
Check(DisplaySettings.Parse(JsonSerializer.Serialize(setting)) == setting, "Settings roundtrip");
var sample = new UsageSnapshot("Test", [new(300, 46, now.AddMinutes(36)), new(10080, 86, now.AddDays(4))]);
foreach (var size in new[] {16,20,24,32,48})
{
    foreach (var mode in Enum.GetValues<DisplayMode>())
    {
        using var image = UsageIconRenderer.Render(sample, mode, mode, now, size, true);
        Check(image.GetPixel(0, size / 2).A == 0, "Transparent gap");
    }
}
using var used = UsageIconRenderer.Render(sample, DisplayMode.FiveHourBar, DisplayMode.WeekBar, now, 16);
using var remaining = UsageIconRenderer.Render(sample, DisplayMode.FiveHourBar, DisplayMode.WeekBar, now, 16, true);
Check(used.GetPixel(1,10) == remaining.GetPixel(1,10), "Stable warning color");
Check(used.GetPixel(5,10).ToArgb() != Color.White.ToArgb() && remaining.GetPixel(5,10).ToArgb() == Color.White.ToArgb(), "Remaining bar width");
Console.WriteLine($"{count} checks passed. No network, credentials, or startup settings accessed.");
