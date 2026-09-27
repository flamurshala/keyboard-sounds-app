param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build-release.ps1') }
$candidates = @($env:ISCC_PATH, (Join-Path $root '.tools\inno\ISCC.exe'), "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
if (Get-Command ISCC.exe -ErrorAction SilentlyContinue) { $candidates += (Get-Command ISCC.exe).Source }
$compiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (!$compiler) { throw 'Install Inno Setup 6 or 7 from https://jrsoftware.org/isdl.php (or set ISCC_PATH to ISCC.exe), then run this script again.' }
& $compiler (Join-Path $root 'installer\FunnyKeys.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
Write-Host "Installer: $root\dist\FunnyKeys-Setup.exe"
