param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
function Has-DesktopRuntime {
    $dotnet = Join-Path $env:ProgramW6432 'dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnet)) { return $false }
    return [bool]((& $dotnet --list-runtimes) -match '^Microsoft.WindowsDesktop.App 10\.0\.\d+ \[')
}
if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { throw 'Windows x64版を使用してください。' }
if ($CheckOnly) { Write-Output ('DesktopRuntime10=' + (Has-DesktopRuntime)); exit 0 }
while (-not (Has-DesktopRuntime)) {
    $choice = [Windows.Forms.MessageBox]::Show('.NET Desktop Runtime 10 (Windows x64) が必要です。公式ページを開きますか？', 'Codex Tray Meter', 'YesNo', 'Information')
    if ($choice -ne 'Yes') { exit 1 }
    Start-Process 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0'
    $choice = [Windows.Forms.MessageBox]::Show('公式ページの「.NET Desktop Runtime」→ Windows x64 をインストール後、「再試行」を押してください。', 'Codex Tray Meter', 'RetryCancel', 'Information')
    if ($choice -ne 'Retry') { exit 1 }
}
$target = Join-Path $env:LOCALAPPDATA 'Programs\CodexTrayMeter'
if (Get-Process -Name CodexTrayMeter -ErrorAction SilentlyContinue) { throw 'Codex Tray Meterを右クリックの「終了」で終了してから、再実行してください。' }
if ([IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') -eq [IO.Path]::GetFullPath($target).TrimEnd('\')) { throw 'ダウンロードしたZIPを別フォルダーに展開してから実行してください。' }
$files = @('CodexTrayMeter.exe','README.md','LICENSE','Uninstall.cmd','Uninstall.ps1')
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $file))) { throw ('ファイル不足: ' + $file + '。ZIPをすべて展開してください。') } }
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $target $file) -Force }
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex Tray Meter'
New-Item -ItemType Directory -Path $menu -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $menu 'Codex Tray Meter.lnk'))
$link.TargetPath = Join-Path $target 'CodexTrayMeter.exe'
$link.WorkingDirectory = $target
$link.Save()
$link = $shell.CreateShortcut((Join-Path $menu 'Codex Tray Meter をアンインストール.lnk'))
$link.TargetPath = Join-Path $target 'Uninstall.cmd'
$link.WorkingDirectory = $target
$link.Save()
# 有効化済みの登録だけを新しいインストール先に更新する。
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -LiteralPath $runKey -Name CodexTrayMeter -ErrorAction SilentlyContinue) {
    Set-ItemProperty -LiteralPath $runKey -Name CodexTrayMeter -Value ('"' + (Join-Path $target 'CodexTrayMeter.exe') + '"')
}
[Windows.Forms.MessageBox]::Show('インストールが完了しました。スタートメニューからCodex Tray Meterを起動してください。', 'Codex Tray Meter', 'OK', 'Information') | Out-Null
