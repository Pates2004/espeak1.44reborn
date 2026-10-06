[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EspeakExe,
    [Parameter(Mandatory = $true)][string]$DataPath
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $EspeakExe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path
$fixturePath = Join-Path $PSScriptRoot 'test-data\polish-tens.tsv'
$cases = @(Import-Csv -LiteralPath $fixturePath -Delimiter "`t" -Encoding UTF8)
if ($cases.Count -ne 20) {
    throw 'The Polish tens fixture must contain exactly the numbers 30 through 49.'
}

function Get-PolishPhonemes([string]$Text) {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $exePath
    $quotedDataRoot = $dataRoot -replace '(\\+)$', '$1$1'
    $startInfo.Arguments = '"--path=' + $quotedDataRoot + '" -q -x -b 1 -v pl --stdin'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $started = $false
    try {
        $started = $process.Start()
        if (-not $started) { throw 'The Polish tens process could not start.' }
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $inputBytes = [System.Text.Encoding]::UTF8.GetBytes($Text + "`n")
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.BaseStream.Close()
        if (-not $process.WaitForExit(15000)) {
            throw "Polish tens synthesis timed out for: $Text"
        }
        $phonemes = $outputTask.Result.Trim()
        $errorText = $errorTask.Result.Trim()
        if ($process.ExitCode -ne 0 -or $errorText.Length -ne 0) {
            throw "Polish tens synthesis failed for '$Text': $errorText"
        }
        return $phonemes
    } finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
        }
        $process.Dispose()
    }
}

function Get-TensExpectation([int]$Number) {
    if ($Number -lt 30 -or $Number -gt 49) {
        throw "Number outside the bounded Polish tens test: $Number"
    }

    # The user selected the supplied legacy BOY numeric realization (c;), while
    # written words retain their existing ts; realization from the same source.
    # Both paths keep primary stress on DZIE and an unstressed final vowel.
    $numericHead = if ($Number -lt 40) { "tSydz;'ES;c;i" } else { "tStERdz;'ES;c;i" }
    $writtenHead = if ($Number -lt 40) { "tSydz;'ES;ts;i" } else { "tStERdz;'ES;ts;i" }
    $units = @(
        '', "j'EdEn", "dv'a", "tS'y", "tSt'ERy", "p;'En^ts;",
        "S'ES;ts;", "S;'EdEm", "'OS;Em", "dz;'Ev;En^ts;"
    )
    $unit = $Number % 10

    # The trace includes ';' after final i before osiem: keep it explicitly.
    # Numeric components concatenate; written components retain a word boundary.
    $link = if ($unit -eq 8) { ';' } else { '' }
    $wordBoundary = if ($unit -eq 0) { '' } else { ' ' }
    $writtenTail = if (@(1, 2, 7, 8) -contains $unit) { '_' } else { '' }
    return [pscustomobject]@{
        Numeric = $numericHead + $link + $units[$unit] + '_!'
        Written = $writtenHead + $link + $wordBoundary + $units[$unit] + $writtenTail
    }
}

function Assert-Phonemes([string]$Text, [string]$Actual, [string]$Expected) {
    # Case and all phonemes, stress marks, internal whitespace and pause markers
    # must match. Do not strip punctuation or merely look for a valid substring.
    if ($Actual -cne $Expected) {
        throw "Unexpected Polish tens pronunciation for '$Text': expected '$Expected', received '$Actual'."
    }
}

$writtenNumbers = @{}
$synthesisCount = 0
for ($index = 0; $index -lt $cases.Count; $index++) {
    $case = $cases[$index]
    $number = 30 + $index
    if ($case.Number -cne [string]$number -or [string]::IsNullOrWhiteSpace($case.Written)) {
        throw "Incomplete or out-of-order Polish tens fixture at number $number."
    }
    $writtenNumbers[$number] = $case.Written
    $expected = Get-TensExpectation $number
    Assert-Phonemes $case.Number (Get-PolishPhonemes $case.Number) $expected.Numeric
    Assert-Phonemes $case.Written (Get-PolishPhonemes $case.Written) $expected.Written
    $synthesisCount += 2
}

# Ordinary grammatical sentence pairs include the user's specific examples.
$contexts = @(
    @{ Number = 31; Noun = 'lat'; Ending = "l'at" },
    @{ Number = 32; Noun = 'lata'; Ending = "l'ata#_" },
    @{ Number = 35; Noun = 'lat'; Ending = "l'at" },
    @{ Number = 45; Noun = 'lat'; Ending = "l'at" },
    @{ Number = 46; Noun = 'lat'; Ending = "l'at" },
    @{ Number = 48; Noun = 'lat'; Ending = "l'at" }
)
foreach ($context in $contexts) {
    $expected = Get-TensExpectation $context.Number
    $numericText = 'Mam {0} {1}.' -f $context.Number, $context.Noun
    $writtenText = 'Mam {0} {1}.' -f $writtenNumbers[$context.Number], $context.Noun
    $numericPhonemes = "m'am_ " + $expected.Numeric + ' ' + $context.Ending
    $writtenPhonemes = "m'am_ " + $expected.Written + ' ' + $context.Ending
    Assert-Phonemes $numericText (Get-PolishPhonemes $numericText) $numericPhonemes
    Assert-Phonemes $writtenText (Get-PolishPhonemes $writtenText) $writtenPhonemes
    $synthesisCount += 2
}

Write-Host "Polish tens test OK: $synthesisCount exact UTF-8 checks, 30-49 and six sentence pairs; DZIE stress and unstressed final ci preserved."
