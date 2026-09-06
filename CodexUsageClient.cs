using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexTrayMeter;

internal static class CodexUsageClient
{
    public static string FindExecutable()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory.Trim('"'), "codex.exe");
            if (File.Exists(candidate)) return candidate;
        }
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bundled))
        {
            var candidate = Directory.EnumerateDirectories(bundled).Select(d => Path.Combine(d, "codex.exe"))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (candidate is not null) return candidate;
        }
        throw new FileNotFoundException("Codexが見つかりません。CodexアプリまたはWindows用Codex CLIを導入してください。");
    }

    public static async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        var start = new ProcessStartInfo(FindExecutable())
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("app-server");
        using var process = new Process { StartInfo = start };
        process.Start();
        // stderrには認証や環境の情報が含まれ得るため、表示・保存せず排出する。
        string? startupFailure = null;
        var drain = DrainAsync(process.StandardError, token, message => startupFailure = message);
        try
        {
            await SendAsync(new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "codex_tray_meter", title = "Codex Tray Meter", version = "1.0.0" } } });
            await ReadResultAsync(1);
            await SendAsync(new { method = "initialized", @params = new { } });
            await SendAsync(new { id = 2, method = "account/rateLimits/read" });
            return RateLimitParser.Parse(await ReadResultAsync(2));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IOException("使用量の取得が30秒でタイムアウトしました。自動的に再試行します。");
        }
        finally
        {
            // 自分で起動した取得用プロセスだけを終了する。
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            timeout.Cancel();
            await drain;
        }

        async Task SendAsync(object message)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), token);
            await process.StandardInput.FlushAsync(token);
        }
        async Task<JsonElement> ReadResultAsync(int expected)
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(token);
                if (line is null)
                {
                    await drain;
                    throw new IOException(startupFailure ?? "Codexとの接続が終了しました。Codexアプリが正常に起動できるか確認してください。");
                }
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var number) || number != expected) continue;
                if (root.TryGetProperty("error", out _))
                    throw new IOException("Codexが使用量を返しませんでした。CodexでChatGPTアカウントにログインしているか確認してください。");
                if (!root.TryGetProperty("result", out var result)) throw new InvalidDataException("Codexの応答形式を認識できません。");
                return result.Clone();
            }
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token, Action<string> report)
    {
        var buffer = new char[4096];
        try
        {
            int count;
            string tail = "";
            while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
            {
                var chunk = tail + new string(buffer, 0, count);
                if (chunk.Contains("failed to initialize sqlite", StringComparison.OrdinalIgnoreCase))
                    report("Codexの内部データベースを開けませんでした。通常のWindowsユーザー環境から起動して確認してください。");
                else if (chunk.Contains("os error 5", StringComparison.OrdinalIgnoreCase))
                    report("Codexの起動時にアクセスが拒否されました。Codexの保存フォルダーへのアクセスを確認してください。");
                tail = chunk.Length > 80 ? chunk[^80..] : chunk;
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }
}


