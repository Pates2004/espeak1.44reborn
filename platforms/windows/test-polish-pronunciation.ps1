[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EspeakExe,
    [Parameter(Mandatory = $true)][string]$DataPath
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $EspeakExe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path
$fixturePath = Join-Path $PSScriptRoot 'test-data\polish-pronunciation.tsv'
$cases = @(Import-Csv -LiteralPath $fixturePath -Delimiter "`t" -Encoding UTF8)
if ($cases.Count -eq 0) {
    throw 'The Polish pronunciation fixture is empty.'
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
        if (-not $started) { throw 'The Polish pronunciation process could not start.' }
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $inputBytes = [System.Text.Encoding]::UTF8.GetBytes($Text + "`n")
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.BaseStream.Close()
        if (-not $process.WaitForExit(15000)) {
            throw "Polish pronunciation timed out for: $Text"
        }
        $phonemes = $outputTask.Result.Trim()
        $errorText = $errorTask.Result.Trim()
        if ($process.ExitCode -ne 0) {
            throw "Polish pronunciation synthesis failed for '$Text': $errorText"
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

foreach ($case in $cases) {
    if ([string]::IsNullOrWhiteSpace($case.Text) -or [string]::IsNullOrWhiteSpace($case.Expected)) {
        throw 'The Polish pronunciation fixture contains an incomplete case.'
    }
    $phonemes = Get-PolishPhonemes $case.Text
    if (-not $phonemes.Contains($case.Expected)) {
        throw "Unexpected Polish pronunciation for '$($case.Text)': expected '$($case.Expected)', received '$phonemes'."
    }
}

Write-Host "Polish pronunciation test OK: $($cases.Count) UTF-8 word and number cases."
