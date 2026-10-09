$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot 'release'
$package=Join-Path $folder 'Hisab'
New-Item -ItemType Directory -Path $package -Force | Out-Null
foreach($runtime in @('win-x64','win-x86','win-arm64')) {
  $target=Join-Path $package 'app'
  if($runtime -ne 'win-x64'){$target=Join-Path $target $runtime.Replace('win-','')}
  dotnet publish source/Hisab.csproj -c Release -r $runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $target
  if($LASTEXITCODE -ne 0){throw "Publish failed: $runtime"}
  if($runtime -ne 'win-arm64') {
    $tests=Join-Path $env:RUNNER_TEMP ('hisab-tests-'+$runtime+'-'+$env:GITHUB_RUN_ID+'-'+$env:GITHUB_RUN_ATTEMPT)
    $proc=Start-Process -FilePath (Join-Path $target 'Hisab.exe') -ArgumentList '--self-test',('"'+$tests+'"') -WindowStyle Hidden -Wait -PassThru
    if($proc.ExitCode -ne 0){throw "Self-test failed: $runtime"}
    $results=Get-Content (Join-Path $tests 'test-results.txt')
    if(($results | Where-Object {$_ -like 'PASS *'}).Count -ne 121){throw 'Expected 121 passed checks.'}
    $results | Out-File (Join-Path $package ('CHECKS-'+$runtime+'.txt')) -Encoding utf8
  }
}
foreach($file in @('Start-Hisab.cmd','Setup-Guide-Arabic.pdf','QUICKSTART-AR.txt','README.txt','ARCHITECTURE.txt','VALIDATION.txt','UI-DESIGN.txt','CLIENT-FEEDBACK.md','release-notes.md')) {
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $package
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ThirdParty') -Destination $package -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'source') -Destination $package -Recurse
foreach($name in @('bin','obj')) {
  $generated=Join-Path (Join-Path $package 'source') $name
  $full=[IO.Path]::GetFullPath($generated)
  $expected=[IO.Path]::GetFullPath((Join-Path $package 'source'))+[IO.Path]::DirectorySeparatorChar
  if(-not $full.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cleanup path.'}
  if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}
}
Compress-Archive -Path (Join-Path $package 'source') -DestinationPath (Join-Path $package 'Source.zip') -Force
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $folder 'Hisab-Windows.zip') -Force
