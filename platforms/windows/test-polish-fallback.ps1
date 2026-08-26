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
$inputPath = Join-Path $PSScriptRoot 'test-data\polish-fallback-symbols.txt'

$output = & $exePath "--path=$dataRoot" -q -x --punct -b 1 -v pl -f $inputPath
if ($LASTEXITCODE -ne 0) {
    throw 'Polish fallback synthesis failed.'
}

$phonemes = $output -join ' '
$required = @(
    "pS,ERyv'ana p;On'Ova kR'Eska",
    "p'awza",
    "l'Evy tsudz'yswuf pOdv'ujny",
    "v;,ElOkR'OpEk",
    "pR'Omil",
    "pOdv'ujny vykS'ykn^ik",
    "s'ymbOl_ 'u pl'uz"
)

foreach ($fragment in $required) {
    if (-not $phonemes.Contains($fragment)) {
        throw "Missing Polish fallback pronunciation: $fragment"
    }
}

if ($phonemes -match '_\^_en|_::en') {
    throw 'Polish fallback unexpectedly switched to English phonemes.'
}

Write-Host 'Polish fallback test OK.'
