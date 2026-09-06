using System.Text.Json;

namespace CodexTrayMeter;

internal sealed record DisplaySettings(DisplayMode Top = DisplayMode.FiveHourBar,
    DisplayMode Bottom = DisplayMode.WeekBar, bool ShowRemaining = false)
{
    public static DisplaySettings Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        // 以前の上下2要素の設定もそのまま読み込む。
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var modes = JsonSerializer.Deserialize<DisplayMode[]>(json);
            return modes is { Length: 2 } && modes.All(m => Enum.IsDefined(m))
                ? new(modes[0], modes[1]) : new();
        }
        var settings = JsonSerializer.Deserialize<DisplaySettings>(json) ?? new();
        return settings with
        {
            Top = Enum.IsDefined(settings.Top) ? settings.Top : DisplayMode.FiveHourBar,
            Bottom = Enum.IsDefined(settings.Bottom) ? settings.Bottom : DisplayMode.WeekBar
        };
    }
}

