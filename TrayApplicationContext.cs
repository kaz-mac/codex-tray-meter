using System.Text.Json;

namespace CodexTrayMeter;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ToolStripMenuItem refreshItem = new("今すぐ更新");
    private bool reading;
    private bool closing;
    private bool showRemaining;
    private readonly ToolStripMenuItem usedItem = new("使用率（何%使ったか）");
    private readonly ToolStripMenuItem remainingItem = new("残量（何%残っているか）");
    private string PercentLabel => showRemaining ? "残り" : "使用";
    private int failures;
    private DateTimeOffset nextRead = DateTimeOffset.MinValue;
    private string connectionStatus = "接続待ち";
    private string? failureDetail;
    private readonly ToolStripMenuItem startupItem = new("Windows起動時に自動実行");
    private readonly ToolStripMenuItem statusItem = new() { Enabled = false };
    private readonly List<(ToolStripMenuItem Item, bool Top, DisplayMode Mode)> displayItems = [];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTrayMeter", "display.json");
    private DisplayMode top = DisplayMode.FiveHourBar;
    private DisplayMode bottom = DisplayMode.WeekBar;
    private UsageSnapshot snapshot;
    private Icon? currentIcon;
    private string? lastRenderKey;

    public TrayApplicationContext()
    {
        LoadSettings();
        menu.Items.Add(new ToolStripMenuItem("Codex Tray Meter") { Enabled = false });
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        AddDisplayMenu("上段", true);
        AddDisplayMenu("下段", false);
        var percentageMenu = new ToolStripMenuItem("割合の表示（上下共通）");
        usedItem.Click += (_, _) => SetPercentageMode(false);
        remainingItem.Click += (_, _) => SetPercentageMode(true);
        percentageMenu.DropDownItems.AddRange([usedItem, remainingItem]);
        menu.Items.Add(percentageMenu);
        menu.Items.Add(new ToolStripSeparator());
        refreshItem.Click += async (_, _) => { await RefreshUsageAsync(); };
        menu.Items.Add(refreshItem);
        menu.Items.Add("取得状況", null, (_, _) => ShowStatus());
        snapshot = new("接続待ち", []);
        menu.Items.Add(new ToolStripSeparator());
        startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add(startupItem);
        menu.Opening += (_, _) => RefreshStartup();
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitThread());
        trayIcon = new NotifyIcon { ContextMenuStrip = menu };
        UpdateDisplay();
        trayIcon.Visible = true;
        timer.Tick += async (_, _) =>
        {
            UpdateDisplay();
            if (!reading && DateTimeOffset.UtcNow >= nextRead) await RefreshUsageAsync();
        };
        timer.Start();
    }

    private async Task RefreshUsageAsync()
    {
        if (reading || closing) return;
        reading = true;
        refreshItem.Enabled = false;
        connectionStatus = "取得中";
        // 更新中は直前の取得結果を維持し、成功時に新しい結果へ差し替える。
        UpdateDisplay();
        try
        {
            var data = await Task.Run(() => CodexUsageClient.ReadAsync(lifetime.Token));
            if (closing) return;
            failures = 0;
            failureDetail = null;
            connectionStatus = $"{DateTimeOffset.Now:HH:mm:ss} 更新 / {data.Name}";
            snapshot = data;
        }
        catch (OperationCanceledException) when (closing) { }
        catch (Exception ex)
        {
            if (closing) return;
            failures++;
            connectionStatus = "取得失敗（自動再試行）";
            failureDetail = ex is IOException ? ex.Message : "Codexとの通信に失敗しました。Codexの動作とログイン状態を確認してください。";
            snapshot = new("取得失敗", []);
        }
        finally
        {
            reading = false;
            nextRead = DateTimeOffset.UtcNow.AddSeconds(failures == 0 ? 60 : Math.Min(300, 60 * failures));
            if (!closing)
            {
                refreshItem.Enabled = true;
                UpdateDisplay();
            }
        }
    }

    private void ShowStatus()
    {
        var lines = new List<string> { connectionStatus };
        if (failureDetail is not null) lines.Add(failureDetail);
        foreach (var window in snapshot.Windows)
            lines.Add($"{window.Label}: {DisplayContent.Percent(window, showRemaining):0.##}%{PercentLabel} / Reset: {window.ResetsAt?.ToLocalTime().ToString("yyyy/M/d HH:mm") ?? "不明"}");
        {
            lines.Add("5H: " + (snapshot.Windows.Any(w => w.DurationMinutes == 300) ? "取得あり" : "データなし"));
            lines.Add("Week: " + (snapshot.Windows.Any(w => w.DurationMinutes == 10080) ? "取得あり" : "データなし"));
        }
        MessageBox.Show(string.Join("\n", lines), "Codex 使用量の取得状況", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    private void RefreshStartup()
    {
        try
        {
            startupItem.Checked = StartupRegistration.IsRegistered();
            startupItem.Enabled = true;
            startupItem.Text = "Windows起動時に自動実行";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            startupItem.Checked = false;
            startupItem.Enabled = false;
            startupItem.Text = "Windows起動時に自動実行（設定を読み取れません）";
        }
    }

    private void ToggleStartup()
    {
        try
        {
            StartupRegistration.SetEnabled(!StartupRegistration.IsRegistered());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show("自動実行の設定を変更できませんでした。\n" + ex.Message,
                "Codex Tray Meter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        RefreshStartup();
    }
    private void AddDisplayMenu(string title, bool isTop)
    {
        var parent = new ToolStripMenuItem(title);
        foreach (var mode in Enum.GetValues<DisplayMode>())
        {
            var item = new ToolStripMenuItem(DisplayContent.Label(mode));
            item.Click += (_, _) =>
            {
                if (isTop) top = mode; else bottom = mode;
                UpdateDisplay();
                SaveSettings();
            };
            displayItems.Add((item, isTop, mode));
            parent.DropDownItems.Add(item);
        }
        menu.Items.Add(parent);
    }

    private void UpdateDisplay()
    {
        var now = DateTimeOffset.Now;
        usedItem.Checked = !showRemaining;
        remainingItem.Checked = showRemaining;
        string Describe(DisplayMode mode)
        {
            if (mode == DisplayMode.None) return "非表示";
            var window = DisplayContent.Window(snapshot, mode);
            if (window is null) return $"{DisplayContent.Label(mode)}: 未取得";
            if (DisplayContent.IsReset(mode))
                return window.ResetsAt is { } reset
                    ? $"{window.Label} Reset: {DisplayContent.Countdown(reset, now)} ({reset.ToLocalTime():M/d HH:mm})"
                    : $"{window.Label} Reset: 時刻未取得";
            return $"{window.Label}: {DisplayContent.Percent(window, showRemaining):0}%{PercentLabel}";
        }
        var tooltip = $"Codex［{connectionStatus}］\n上: {Describe(top)}\n下: {Describe(bottom)}";
        var renderKey = $"{snapshot.Name}|{top}|{bottom}|{showRemaining}|{DisplayContent.Text(snapshot, top, now, showRemaining)}|{DisplayContent.Text(snapshot, bottom, now, showRemaining)}";
        if (renderKey != lastRenderKey)
        {
            var icon = UsageIconRenderer.Create(snapshot, top, bottom, now, showRemaining);
            trayIcon.Icon = icon;
            var previous = currentIcon;
            currentIcon = icon;
            previous?.Dispose();
            lastRenderKey = renderKey;
        }
        trayIcon.Text = tooltip.Length <= 127 ? tooltip : tooltip[..127];
        statusItem.Text = connectionStatus;
        foreach (var (item, isTop, mode) in displayItems) item.Checked = (isTop ? top : bottom) == mode;
    }

    private void SetPercentageMode(bool remaining)
    {
        showRemaining = remaining;
        UpdateDisplay();
        SaveSettings();
    }

    private void LoadSettings()
    {
        try
        {
            var source = File.Exists(settingsPath) ? settingsPath : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageMonitor", "display.json");
            if (!File.Exists(source)) return;
            var settings = DisplaySettings.Parse(File.ReadAllText(source));
            (top, bottom, showRemaining) = (settings.Top, settings.Bottom, settings.ShowRemaining);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new DisplaySettings(top, bottom, showRemaining)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("表示は変更しましたが、設定を保存できませんでした。\n" + ex.Message,
                "Codex Tray Meter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void ExitThreadCore()
    {
        closing = true;
        lifetime.Cancel();
        timer.Stop();
        trayIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closing = true;
            lifetime.Cancel();
            timer.Dispose();
            trayIcon.Dispose();
            currentIcon?.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}







