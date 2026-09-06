using System.Runtime.InteropServices;

namespace CodexTrayMeter;

internal static class UsageIconRenderer
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(UsageSnapshot snapshot, DisplayMode top, DisplayMode bottom, DateTimeOffset now, bool showRemaining = false)
    {
        using var bitmap = Render(snapshot, top, bottom, now, Math.Max(16, SystemInformation.SmallIconSize.Width), showRemaining);
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    internal static Bitmap Render(UsageSnapshot snapshot, DisplayMode top, DisplayMode bottom, DateTimeOffset now, int size, bool showRemaining = false)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        var gap = Math.Max(2, size / 8);
        var height = (size - gap) / 2;
        void Fill(Color color, int x, int y, int width, int h)
        {
            using var brush = new SolidBrush(color);
            graphics.FillRectangle(brush, x, y, width, h);
        }
        void Row(DisplayMode mode, int y)
        {
            if (mode == DisplayMode.None) return;
            var window = DisplayContent.Window(snapshot, mode);
            if (DisplayContent.IsBar(mode) && window is not null)
            {
                var percent = Math.Clamp(window.UsedPercent, 0, 100);
                var color = percent >= 90 ? Color.FromArgb(230, 65, 65)
                    : percent >= 75 ? Color.FromArgb(255, 205, 45) : Color.FromArgb(40, 185, 95);
                var border = Math.Max(1, size / 16);
                Fill(Color.Black, 0, y, size, height);
                Fill(Color.White, border, y + border, size - 2 * border, height - 2 * border);
                var width = (int)Math.Round((size - 2 * border) * DisplayContent.Percent(window, showRemaining) / 100);
                if (width > 0) Fill(color, border, y + border, width, height - 2 * border);
                return;
            }
            Fill(Color.White, 0, y, size, height);
            var text = DisplayContent.Text(snapshot, mode, now, showRemaining);
            var scale = Math.Max(1, height / 7);
            var font = TinyAsciiFont.Width(text, TinyAsciiFont.Regular) * scale <= size
                ? TinyAsciiFont.Regular : TinyAsciiFont.Narrow;
            var left = (size - TinyAsciiFont.Width(text, font) * scale) / 2;
            var textY = y + (height - 7 * scale) / 2;
            // 字形を整数倍で直接描画する。フォントの補間・アンチエイリアスは使わない。
            foreach (var character in text)
            {
                var glyph = font[character];
                for (var gy = 0; gy < 7; gy++)
                    for (var gx = 0; gx < glyph[0].Length; gx++)
                        if (glyph[gy][gx] == '1') Fill(Color.Black, left + gx * scale, textY + gy * scale, scale, scale);
                left += (glyph[0].Length + 1) * scale;
            }
        }
        Row(top, 0);
        Row(bottom, size - height);
        return bitmap;
    }
}


