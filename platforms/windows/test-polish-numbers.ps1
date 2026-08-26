[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$EspeakExe,

    [Parameter(Mandatory = $true)]
    [string]$DataPath
)

$ErrorActionPreference = 'Stop'

function Get-PolishPhonemes([string]$Text) {
    $output = & $EspeakExe "--path=$DataPath" -q -x -v pl $Text
    if ($LASTEXITCODE -ne 0) {
        throw "eSpeak failed for Polish number: $Text"
    }
    return ($output | Out-String).Trim()
}

function Assert-Contains([string]$Actual, [string]$Expected, [string]$Description) {
    if (-not $Actual.Contains($Expected)) {
        throw "$Description`nExpected fragment: $Expected`nActual: $Actual"
    }
}

$magnitudeCases = @(
    @{ Text = '1000000000000000';                Expected = "b;'iljaRd";       Name = 'one billiard' },
    @{ Text = '2000000000000000';                Expected = "b;ilj'aRdy";      Name = 'two billiards' },
    @{ Text = '5000000000000000';                Expected = "b;ilj'aRduf";     Name = 'five billiards' },
    @{ Text = '1000000000000000000';             Expected = "tR'yljOn";        Name = 'one trillion' },
    @{ Text = '1000000000000000000000';          Expected = "tR'yljaRd";       Name = 'one trilliard' },
    @{ Text = '1000000000000000000000000';       Expected = "kfadR'yljOn";     Name = 'one quadrillion' },
    @{ Text = '1000000000000000000000000000';    Expected = "kfadR'yljaRd";    Name = 'one quadrilliard' },
    @{ Text = '1000000000000000000000000000000'; Expected = "kfint'yljOn";     Name = 'one quintillion' }
)

foreach ($case in $magnitudeCases) {
    $phonemes = Get-PolishPhonemes $case.Text
    Assert-Contains $phonemes $case.Expected $case.Name
}

$denseNumber = Get-PolishPhonemes '1234567890123456789012345678901'
if ($denseNumber.Length -le 400) {
    throw "The dense 31-digit number was truncated: $denseNumber"
}
Assert-Contains $denseNumber "m;ilj'aRduf" 'Dense number lost its milliard group'
Assert-Contains $denseNumber "dz;'Ev;En^ts;sEtj'EdEn" 'Dense number lost its final 901 group'

$beyondNaturalLimit = Get-PolishPhonemes '10000000000000000000000000000000'
if ($beyondNaturalLimit.Contains("kR'Opka")) {
    throw "A synthetic thousands separator was spoken: $beyondNaturalLimit"
}

$longDigitString = Get-PolishPhonemes ('1' * 120)
if ($longDigitString.Contains("kR'Opka")) {
    throw "A synthetic separator was spoken in a 120-digit string: $longDigitString"
}
if (([regex]::Matches($longDigitString, "j'EdEn")).Count -ne 120) {
    throw "The 120-digit string was truncated: $longDigitString"
}

$fourDecimalDigits = Get-PolishPhonemes '3,5555'
Assert-Contains $fourDecimalDigits "p;'En^ts;tyS;'Entsy" 'Four decimal digits were not spoken as one number'

$fiveDecimalDigits = Get-PolishPhonemes '3,55555'
Assert-Contains $fiveDecimalDigits "p;En^dz;'ES;Ontp;'En^ts;tyS;'Entsy" 'Five decimal digits were not spoken as one number'

Write-Host 'Polish number tests OK: 31 integer digits, 5 decimal digits, no synthetic spoken separators.'
