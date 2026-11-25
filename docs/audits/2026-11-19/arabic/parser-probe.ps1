$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../../../../source"
$source=Get-Content "$root/Store.cs" -Raw
$match=[regex]::Match($source,'public static decimal Parse\(string text\)\s*\{[\s\S]*?return n;\s*\}')
if(-not $match.Success){throw 'Parser source not found'}
Add-Type -TypeDefinition ('using System; using System.Linq; using System.Globalization; public static class ExactStoreParser {'+$match.Value+'}')
$cases=@('123.456','١٢٣٫٤٥٦','-123.456','-١٢٣٫٤٥٦','1250.500','١٢٥٠٫٥٠٠','1,250.500','١٬٢٥٠٫٥٠٠')
$results=foreach($inputText in $cases){try{[pscustomobject]@{input=$inputText;actual=[ExactStoreParser]::Parse($inputText).ToString([cultureinfo]::InvariantCulture);accepted=$true}}catch{[pscustomobject]@{input=$inputText;actual='rejected';accepted=$false}}}
$results|ConvertTo-Json|Set-Content -Encoding utf8 "$PSScriptRoot/parser-results.json"
$results|Format-Table
