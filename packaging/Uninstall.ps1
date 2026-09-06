$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$target = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\CodexTrayMeter')).TrimEnd('\')
if ([IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') -ne $target) { throw 'このスクリプトはインストール先から実行してください。直接実行版は自動実行をオフにして終了後、フォルダーを削除してください。' }
if (Get-Process -Name CodexTrayMeter -ErrorAction SilentlyContinue) { throw 'Codex Tray Meterを右クリックの「終了」で終了してから、再実行してください。' }
if ([Windows.Forms.MessageBox]::Show('Codex Tray Meterをアンインストールしますか？表示設定は保持します。', 'Codex Tray Meter', 'YesNo', 'Question') -ne 'Yes') { exit 0 }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$entry = (Get-ItemProperty -LiteralPath $runKey -Name CodexTrayMeter -ErrorAction SilentlyContinue).CodexTrayMeter
if ($entry -eq ('"' + (Join-Path $target 'CodexTrayMeter.exe') + '"')) { Remove-ItemProperty -LiteralPath $runKey -Name CodexTrayMeter }
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex Tray Meter'
foreach ($file in @('Codex Tray Meter.lnk','Codex Tray Meter をアンインストール.lnk')) {
    $path = Join-Path $menu $file
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}
# インストーラーが配置したファイルだけを削除する。フォルダーの再帰削除はしない。
foreach ($file in @('CodexTrayMeter.exe','README.md','LICENSE','Uninstall.cmd','Uninstall.ps1')) {
    $path = Join-Path $target $file
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}
[Windows.Forms.MessageBox]::Show('アンインストールが完了しました。', 'Codex Tray Meter', 'OK', 'Information') | Out-Null
