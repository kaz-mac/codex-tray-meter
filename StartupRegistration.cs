using Microsoft.Win32;

namespace CodexTrayMeter;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexTrayMeter";

    // F5ではホストがdotnet.exeの場合もあるため、隣接するアプリ本体を登録する。
    internal static string BuildCommand(string baseDirectory)
    {
        var executable = Path.GetFullPath(Path.Combine(baseDirectory, "CodexTrayMeter.exe"));
        if (!File.Exists(executable))
            throw new IOException("自動実行に必要なCodexTrayMeter.exeが見つかりません。アプリをビルドしてください。");
        var command = $"\"{executable}\"";
        if (command.Length > 260)
            throw new IOException("保存先のパスが長すぎます。短いパスのフォルダーに移してから登録してください。");
        return command;
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string command && !string.IsNullOrWhiteSpace(command);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            var command = BuildCommand(AppContext.BaseDirectory);
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

