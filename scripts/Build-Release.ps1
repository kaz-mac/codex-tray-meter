param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\release' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$version = '1.0.0'
$publish = Join-Path $root 'artifacts\publish'
$stage = Join-Path $root 'artifacts\package'
New-Item -ItemType Directory -Path $OutputDirectory,$publish,$stage -Force | Out-Null
& dotnet run --project (Join-Path $root 'tests\Checks.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
& dotnet publish (Join-Path $root 'CodexTrayMeter.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $publish 'CodexTrayMeter.exe') -Destination $stage -Force
foreach ($file in @('README.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage -Force }
foreach ($file in @('Install.cmd','Install.ps1','Uninstall.cmd','Uninstall.ps1')) { Copy-Item -LiteralPath (Join-Path $root ('packaging\' + $file)) -Destination $stage -Force }
$binaryZip = Join-Path $OutputDirectory ('CodexTrayMeter-v' + $version + '-win-x64.zip')
$packageFiles = @('CodexTrayMeter.exe','README.md','LICENSE','Install.cmd','Install.ps1','Uninstall.cmd','Uninstall.ps1') | ForEach-Object { Join-Path $stage $_ }
Compress-Archive -LiteralPath $packageFiles -DestinationPath $binaryZip -Force
# Explicit source allowlist: never package credentials, build caches, or user settings.
$source = Join-Path $root 'artifacts\source'
New-Item -ItemType Directory -Path $source -Force | Out-Null
Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Extension -in '.cs','.csproj' -or $_.Name -in 'README.md','LICENSE','.gitignore' } | Copy-Item -Destination $source -Force
foreach ($directory in @('.vscode','packaging','scripts','tests')) {
    $destination = Join-Path $source $directory
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root $directory) -File | Copy-Item -Destination $destination -Force
}
$sourceZip = Join-Path $OutputDirectory ('CodexTrayMeter-v' + $version + '-source.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip }
[IO.Compression.ZipFile]::CreateFromDirectory($source, $sourceZip)
foreach ($file in @('README.md','LICENSE')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $OutputDirectory -Force }
$hashes = foreach ($file in @($binaryZip,$sourceZip)) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($file) }
$hashes | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('Release files: ' + $OutputDirectory)
