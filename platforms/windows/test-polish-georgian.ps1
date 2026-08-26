[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EspeakExe,

    [Parameter(Mandatory = $true)]
    [string]$DataPath
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $EspeakExe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path
$inputPath = Join-Path $PSScriptRoot 'test-data\polish-georgian.txt'

$output = & $exePath "--path=$dataRoot" -q -x -b 1 -v pl -f $inputPath
if ($LASTEXITCODE -ne 0) {
    throw 'Polish Georgian synthesis failed.'
}

$phonemes = $output -join ' '
if ($phonemes -match "litERa|s'ymbOl|_\^_en|_::en") {
    throw 'A Georgian letter used a spelling or English fallback.'
}

$required = @('dZ', 'tS', 'ts', 'R')
foreach ($fragment in $required) {
    if (-not $phonemes.Contains($fragment)) {
        throw "Missing Georgian phoneme approximation: $fragment"
    }
}

Write-Host 'Polish Georgian test OK.'
