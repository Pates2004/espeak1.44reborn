[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EspeakExe,
    [Parameter(Mandatory = $true)][string]$DataPath
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $EspeakExe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path
$sample = 'druciana bociana bocianem bocianowi starcia tarcia natarcia druciany tarciem policja Alicia'
$output = & $exePath "--path=$dataRoot" -q -x -v pl $sample
if ($LASTEXITCODE -ne 0) { throw 'Polish ci synthesis failed.' }
$phonemes = $output -join ' '

$required = @(
    "dRuts;'ana", "bOts;'ana", "bOts;'anEm", "b,Ots;an'Ovi",
    "st'aRts;a", "t'aRts;a", "nat'aRts;a",
    "dRuts;'any", "t'aRts;Em", "pOl'itsja", "al'isja"
)
foreach ($fragment in $required) {
    if (-not $phonemes.Contains($fragment)) {
        throw "Missing expected Polish ci pronunciation: $fragment"
    }
}

if ($phonemes -match 'dRusj|bOsj|aRs.ija') {
    throw 'Polish ci was incorrectly pronounced as si.'
}
Write-Host 'Polish ci pronunciation test OK.'
