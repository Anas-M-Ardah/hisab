param([ValidateSet('win-x64','win-arm64','win-x86')][string]$Runtime='win-x64')
$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'Hisab.csproj'
$appOutput = Join-Path (Split-Path $PSScriptRoot -Parent) 'app'
if($Runtime -ne 'win-x64'){$appOutput=Join-Path $appOutput $Runtime.Replace('win-','')}
dotnet publish $projectFile -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $appOutput
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host ('Run ' + (Join-Path $appOutput 'Hisab.exe'))
