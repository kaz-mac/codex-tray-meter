# Codex Tray Meter

Windowsの通知領域で、Codexの使用率・残量・リセットまでの時間を確認する小さな常駐アプリです。
OpenAIの非公式アプリです。

## 動作環境

- Windows 11 x64
- [.NET Desktop Runtime 10（Windows x64）](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Windows版Codexアプリ、またはPATHから実行できる `codex.exe`
- CodexでChatGPTアカウントにログイン済みであること、インターネット接続

.NETランタイムとCodex本体は同梱していません。通常の.NET Runtimeだけでは動作しません。
APIキーだけの認証には対応しません。プランにより取得できる枠が異なります。

## インストール

1. GitHub Releasesから `CodexTrayMeter-v1.0.0-win-x64.zip` をダウンロードし、すべて展開します。
2. `Install.cmd` を実行します。.NET Desktop Runtimeがなければ、公式ページを開く案内が表示されます。「.NET Desktop Runtime」のWindows x64を導入し、再試行してください。
3. インストール完了後、スタートメニューの「Codex Tray Meter」から起動します。

インストール先は `%LOCALAPPDATA%\Programs\CodexTrayMeter` です。アプリの導入には通常、管理者権限は不要です（ランタイムの導入では必要になる場合があります）。
Install.cmdは同梱のPowerShellスクリプトを実行します。このプロセスに限って実行ポリシーを指定し、PC全体の設定は変更しません。組織のポリシーで禁止される場合は管理者に確認してください。

インストールせず、ZIP内の `CodexTrayMeter.exe` を直接実行することもできます。設定はユーザーフォルダーに保存します。
本リリースのexe・スクリプトはコード署名されていません。

## 使い方

アイコンを右クリックして設定します。見つからないときは通知領域の「隠れているインジケーター」を確認してください。

- **上段／下段**: `5H Bar`、`5H %`、`Week Bar`、`Week %`、`Reset`、`5H Reset`、`Week Reset`、非表示。
- **割合の表示（上下共通）**: 使用率または残量。初期設定は使用率。
- **Windows起動時に自動実行**: サインイン時の自動起動を登録・解除。
- **今すぐ更新／取得状況**: 再取得、取得結果やエラーを確認。
- **終了**: 常駐を終了。

緑は余裕あり、黄は残り25%以下、赤は残り10%以下です。表示設定は再起動後も保持します。
Resetは最も近いリセットまでの時間です。切り上げで、1時間未満は `36m`、72時間未満は `56h`、それ以上は `4d` のように表示します。正確な日時はマウスを重ねて確認できます。

起動後に取得し、通常60秒ごとに更新します。更新中は直前の表示を維持します。初回取得前・取得失敗・未提供の枠は `--` です。取得できない5時間枠を0%とは扱いません。失敗時は最大5分間隔で再試行します。

## 更新・アンインストール

更新時は右クリックで終了してから、新版のInstall.cmdを実行します。
アンインストールはスタートメニューの「Codex Tray Meter をアンインストール」、またはインストール先のUninstall.cmdから行います。表示設定は保持します。不要なら `%LOCALAPPDATA%\CodexTrayMeter` を手動で削除してください。
直接実行していた場合は、自動実行をオフにして終了後、展開フォルダーを削除します。
フォルダーを移動したときは自動実行をオフ→オンにして再登録してください。
旧試作版「Codex Usage Monitor」は自動実行をオフにして終了してください。旧版の表示設定は初回読み込み時に引き継ぎます。

## 接続・プライバシー

既存Codexの `app-server` を非表示で起動し、公式の `account/rateLimits/read` を使用します。会話や生成タスクは作成しません。監視アプリは認証トークンをコピー・保存しません。Codex自身が認証更新や内部ログを管理します。
通常のCodex枠のみを表示し、別モデルの利用枠は合算しません。Codex側の仕様変更で取得できなくなる場合があります。
取得に失敗した場合はCodexのログイン状態と更新状況を確認してください。

## ソースからビルド

必要: Windows x64、.NET 10 SDK。VS CodeのC#拡張は任意です。

```powershell
dotnet build CodexTrayMeter.csproj -c Release
dotnet run --project tests/Checks.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Release.ps1
```

配布ファイルは `artifacts/release` に生成します。VS Codeではフォルダーを開きF5で実行できます。

## ライセンス

[MIT License](LICENSE)。Codexおよび.NETの権利はそれぞれの権利者に帰属します。

