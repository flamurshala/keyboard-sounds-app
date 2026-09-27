$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { (Get-Command dotnet).Source } else { Join-Path $root '.tools\dotnet\dotnet.exe' }
if (!(Test-Path $dotnet)) { throw 'Install the .NET 8 SDK for Windows x64 from https://dotnet.microsoft.com/download/dotnet/8.0' }
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:NUGET_PACKAGES = Join-Path $root '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet run --project (Join-Path $root 'src\FunnyKeys\FunnyKeys.csproj')
if ($LASTEXITCODE -ne 0) { throw 'FunnyKeys development run failed.' }
