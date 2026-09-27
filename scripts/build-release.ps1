param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { (Get-Command dotnet).Source } else { Join-Path $root '.tools\dotnet\dotnet.exe' }
if (!(Test-Path $dotnet)) { throw 'Install the .NET 8 SDK for Windows x64 from https://dotnet.microsoft.com/download/dotnet/8.0' }
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $root '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
function Invoke-Dotnet([string[]]$Arguments) { & $dotnet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
$project = Join-Path $root 'src\FunnyKeys\FunnyKeys.csproj'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
foreach ($name in @('app', 'FunnyKeys-Portable')) {
    $target = [IO.Path]::GetFullPath((Join-Path $dist $name))
    if (![string]::Equals([IO.Path]::GetDirectoryName($target), [IO.Path]::GetFullPath($dist), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe build output path.' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
Invoke-Dotnet @('clean', $project, '-c', 'Release', '--nologo')
Invoke-Dotnet @('restore', $project, '-r', 'win-x64')
if (!$SkipTests) { Invoke-Dotnet @('run', '--project', (Join-Path $root 'tests\FunnyKeys.Tests\FunnyKeys.Tests.csproj'), '-c', 'Release') }
Invoke-Dotnet @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-o', (Join-Path $dist 'app'))
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $dist 'app')
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $dist 'app')
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination (Join-Path $dist 'app\licenses') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $dist 'app\docs') -Recurse
Copy-Item -LiteralPath (Join-Path $dist 'app') -Destination (Join-Path $dist 'FunnyKeys-Portable') -Recurse
Compress-Archive -Path (Join-Path $dist 'FunnyKeys-Portable\*') -DestinationPath (Join-Path $dist 'FunnyKeys-Portable.zip') -Force
Write-Host "Release: $dist\app\FunnyKeys.exe"
