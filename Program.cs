namespace CodexTrayMeter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // サインイン時の自動実行と手動起動が重なっても、アイコンを重複させない。
        using var instance = new Mutex(true, @"Local\CodexTrayMeter.App", out var createdNew);
        if (!createdNew) return;
        try
        {
            ApplicationConfiguration.Initialize();
            using var context = new TrayApplicationContext();
            Application.Run(context);
        }
        finally { instance.ReleaseMutex(); }
    }
}

