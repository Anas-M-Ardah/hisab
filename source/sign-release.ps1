param([Parameter(Mandatory=$true)][ValidatePattern('^[0-9A-Fa-f]{40}$')][string]$CertificateThumbprint,[Parameter(Mandatory=$true)][string]$SignToolPath,[string]$Executable=(Join-Path (Split-Path $PSScriptRoot -Parent) 'app\Hisab.exe'),[string]$TimestampUrl='http://timestamp.digicert.com')
$ErrorActionPreference='Stop'
if (!(Test-Path -LiteralPath $SignToolPath)) { throw 'Specify the Windows SDK signtool.exe path.' }
& $SignToolPath sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Executable
if ($LASTEXITCODE -ne 0) { throw 'Signing failed.' }
& $SignToolPath verify /pa /all /v $Executable
if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
Write-Host 'Signature verified. Rebuild the release ZIP after signing.'
