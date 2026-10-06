[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EspeakExe,
    [Parameter(Mandatory = $true)][string]$DataPath
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $EspeakExe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path
$fixturePath = Join-Path $PSScriptRoot 'test-data\polish-symbol-names.tsv'
$sourcePath = Join-Path $PSScriptRoot '..\..\dictsource\pl_list'
$cases = @(Import-Csv -LiteralPath $fixturePath -Delimiter "`t" -Encoding UTF8)
if ($cases.Count -ne 55) {
    throw 'The Polish symbol fixture must contain exactly 55 existing entries.'
}

$requiredColumns = @('Key', 'Text', 'Kind', 'OriginalPhonemes', 'ExpectedPhonemes', 'ExpectedTrace')
foreach ($column in $requiredColumns) {
    if ($column -cnotin @($cases[0].PSObject.Properties.Name)) {
        throw "The Polish symbol fixture is not frozen: missing column '$column'."
    }
}

$sourceLines = @(Get-Content -LiteralPath $sourcePath -Encoding UTF8)
$nbspKey = '_' + [char]0x00A0
$acuteKey = [string][char]0x00B4
$oAcute = [string][char]0x00F3
$oAcuteAlias = '_' + $oAcute
$squaredKey = [string][char]0x00B2
$ecuKey = '_' + [char]0x20A0
$accentCharacters = @{
    _ced = 0x00E7
    _cir = 0x00E2
    _dot = 0x010B
    _ogo = 0x012F
    _rng = 0x00E5
    _tld = 0x00E3
}
$keys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$kindCounts = @{ punctuation = 0; direct = 0; accent = 0 }

function Assert-SquareVoicing([string]$Label, [string]$Phonemes, [int]$Count = 1) {
    if ([regex]::Matches($Phonemes, 'kf').Count -ne $Count -or $Phonemes -cmatch '[kg]v') {
        throw "The square name '$Label' must use kf, not kv/gv: '$Phonemes'."
    }
}

# Validate the complete fixture before starting any synthesis. OriginalPhonemes
# is historical evidence; compare current dictionary source with ExpectedPhonemes.
foreach ($case in $cases) {
    if ([string]::IsNullOrEmpty($case.Key) -or -not $keys.Add($case.Key)) {
        throw 'The Polish symbol fixture has an empty or duplicate key.'
    }
    foreach ($column in @('Text', 'Kind', 'OriginalPhonemes', 'ExpectedPhonemes')) {
        if ([string]::IsNullOrWhiteSpace($case.$column)) {
            throw "The Polish symbol fixture is not frozen: '$($case.Key)' lacks '$column'."
        }
    }
    if ($case.Kind -cnotin @('punctuation', 'direct', 'accent')) {
        throw "Unknown symbol kind for '$($case.Key)': '$($case.Kind)'."
    }
    $kindCounts[$case.Kind]++
    switch -CaseSensitive ($case.Kind) {
        'punctuation' {
            if ($case.Key.Length -ne 2 -or -not $case.Key.StartsWith('_') -or $case.Key -ceq $oAcuteAlias) {
                throw "Invalid punctuation key: '$($case.Key)'."
            }
        }
        'direct' {
            if ($case.Key.Length -ne 1 -and $case.Key -cne $oAcuteAlias) {
                throw "Invalid direct-character key: '$($case.Key)'."
            }
        }
        'accent' {
            if ($case.Key -cnotin @($accentCharacters.Keys)) {
                throw "No supported ordinary accented letter for '$($case.Key)'."
            }
        }
    }

    if ($case.Key -ceq $nbspKey) {
        # Existing _<NBSP> is unreachable through the public spelling path:
        # whitespace lookup uses _#160, while ordinary punctuation trims it.
        # An empty expected trace explicitly means source-only, not runtime success.
        if (-not [string]::IsNullOrEmpty($case.ExpectedTrace)) {
            throw 'The source-only NBSP row must leave ExpectedTrace empty.'
        }
    } elseif ([string]::IsNullOrWhiteSpace($case.ExpectedTrace)) {
        throw "The Polish symbol fixture is not frozen: '$($case.Key)' lacks an exact ExpectedTrace."
    }

    $keyPattern = '^' + [regex]::Escape($case.Key) + '[\t ]+'
    $sourceEntries = @($sourceLines | Where-Object { $_ -cmatch $keyPattern })
    if ($sourceEntries.Count -ne 1) {
        throw "Expected exactly one source entry for '$($case.Key)', found $($sourceEntries.Count)."
    }
    $sourcePhonemes = (($sourceEntries[0] -creplace $keyPattern, '') -split '[\t ]+')[0]
    if ($sourcePhonemes -cne $case.ExpectedPhonemes) {
        throw "Dictionary source differs from the frozen fixture for '$($case.Key)': '$sourcePhonemes'."
    }

    # Boundaries are semantic guards independent of exact recorded traces.
    # Pause count/duration and number pronunciation have their own scoped tests.
    $wordCount = @($case.Text -split ' ' | Where-Object { $_.Length -ne 0 }).Count
    $boundaryCount = [regex]::Matches($case.ExpectedPhonemes, '\|\|').Count
    if ($boundaryCount -ne ($wordCount - 1)) {
        throw "Wrong word-boundary count for '$($case.Key)': $boundaryCount for $wordCount words."
    }
    if ($case.Key -cin @('_[', '_]', $squaredKey)) {
        Assert-SquareVoicing $case.Key $case.ExpectedPhonemes
    }
}
if ($kindCounts.punctuation -ne 33 -or $kindCounts.direct -ne 16 -or $kindCounts.accent -ne 6) {
    throw 'The symbol fixture must retain 33 punctuation, 16 direct and six accent entries.'
}
if (-not $keys.Contains($nbspKey)) {
    throw 'The source-only NBSP record is missing.'
}

function Get-CharacterMarkup([string]$Text) {
    # Numeric entities preserve exact BMP characters and safely escape XML symbols.
    $entities = foreach ($character in $Text.ToCharArray()) {
        '&#' + [int]$character + ';'
    }
    return '<say-as interpret-as="characters">' + ($entities -join '') + '</say-as>'
}

function Get-PolishSymbolTrace([string]$Text, [switch]$Punctuation, [switch]$Markup) {
    if ([string]::IsNullOrEmpty($Text) -or $Text.Length -gt 256) {
        throw 'Symbol regression probes must contain between one and 256 characters.'
    }
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $exePath
    $quotedDataRoot = $dataRoot -replace '(\\+)$', '$1$1'
    $startInfo.Arguments = '"--path=' + $quotedDataRoot + '" -q -x -b 1 -v pl --stdin'
    if ($Punctuation) { $startInfo.Arguments += ' --punct' }
    if ($Markup) { $startInfo.Arguments += ' -m' }
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
        if (-not $started) { throw 'The Polish symbol process could not start.' }
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        $inputBytes = [System.Text.Encoding]::UTF8.GetBytes($Text + "`n")
        $process.StandardInput.BaseStream.Write($inputBytes, 0, $inputBytes.Length)
        $process.StandardInput.BaseStream.Close()
        if (-not $process.WaitForExit(15000)) {
            throw "Polish symbol synthesis timed out for: $Text"
        }
        $trace = $outputTask.Result.Trim()
        $errorText = $errorTask.Result.Trim()
        if ($process.ExitCode -ne 0 -or $errorText.Length -ne 0) {
            throw "Polish symbol synthesis failed for '$Text': $errorText"
        }
        if ([string]::IsNullOrWhiteSpace($trace)) {
            throw "The runtime symbol probe produced no pronunciation: $Text"
        }
        return $trace
    } finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
        }
        $process.Dispose()
    }
}

$runtimeCount = 0
foreach ($case in $cases) {
    if ($case.Key -ceq $nbspKey) { continue }
    if ($case.Kind -ceq 'accent') {
        $inputText = Get-CharacterMarkup ([string][char]$accentCharacters[$case.Key])
        $trace = Get-PolishSymbolTrace $inputText -Markup
    } elseif ($case.Key -ceq $ecuKey) {
        # This currency symbol is skipped by ordinary punctuation handling.
        $trace = Get-PolishSymbolTrace (Get-CharacterMarkup $case.Key.Substring(1)) -Markup -Punctuation
    } elseif ($case.Kind -ceq 'punctuation') {
        $trace = Get-PolishSymbolTrace $case.Key.Substring(1) -Punctuation
    } elseif ($case.Key -ceq $oAcuteAlias -or $case.Key -ceq $acuteKey) {
        # The unprefixed acute accent has no ordinary-text output; spell it.
        $character = if ($case.Key -ceq $oAcuteAlias) { $oAcute } else { $acuteKey }
        $trace = Get-PolishSymbolTrace (Get-CharacterMarkup $character) -Markup
    } else {
        $trace = Get-PolishSymbolTrace $case.Key
    }
    # Keep case, stress, internal whitespace and all pause markers exact.
    if ($trace -cne $case.ExpectedTrace) {
        throw "Unexpected symbol pronunciation for '$($case.Key)': expected '$($case.ExpectedTrace)', received '$trace'."
    }
    if ($case.Key -cin @('_[', '_]', $squaredKey)) {
        Assert-SquareVoicing $case.Key $trace
    }
    $runtimeCount++
}

# A few short ordinary controls exercise adjacency and capital-letter spelling.
# These have independent semantic assertions, not expectations learned at runtime.
$controls = @(
    @{ Text = '/'; Pattern = "sl[',]ES"; Count = 1 },
    @{ Text = '//'; Pattern = "sl[',]ES"; Count = 2 },
    @{ Text = '\'; Pattern = "b[',]EkslES"; Count = 1 },
    # Existing regressive voicing changes final S to Z before the next b.
    @{ Text = '\\'; Pattern = "b[',]EkslE[SZ]"; Count = 2 }
)
foreach ($control in $controls) {
    $trace = Get-PolishSymbolTrace $control.Text
    if ([regex]::Matches($trace, $control.Pattern).Count -ne $control.Count) {
        throw "Slash/backslash names changed for '$($control.Text)': '$trace'."
    }
}
$bracketTrace = Get-PolishSymbolTrace '[]' -Punctuation
Assert-SquareVoicing '[]' $bracketTrace 2
$capitalText = $oAcute + [char]0x00D3
$capitalTrace = Get-PolishSymbolTrace (Get-CharacterMarkup $capitalText) -Markup
if ([regex]::Matches($capitalTrace, "z[',]*amkn").Count -ne 2) {
    throw "Repeated lower/uppercase o-acute must preserve both existing letter names: '$capitalTrace'."
}

# Composed labels use another fixture because their descriptor is combined with
# the base letter at runtime. Preserve the first 55 cases as independent evidence.
$compositionPath = Join-Path $PSScriptRoot 'test-data\polish-composed-symbols.tsv'
$compositions = @(Import-Csv -LiteralPath $compositionPath -Delimiter "`t" -Encoding UTF8)
if ($compositions.Count -ne 17) {
    throw 'The composed-symbol fixture must contain exactly 17 bounded cases.'
}
foreach ($column in @('Key', 'Character', 'Mode', 'OriginalPhonemes', 'ExpectedPhonemes', 'ExpectedTrace')) {
    if ($column -cnotin @($compositions[0].PSObject.Properties.Name)) {
        throw "The composed-symbol fixture lacks '$column'."
    }
}
$descriptorKeys = @('_cap', '_lig', '_acu', '_ac2', '_brv', '_dia', '_grv', '_hac', '_mcn', '_stk')
$compositionKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$compositionIds = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
$compositionRuntimeCount = 0
$compositionSourceOnlyCount = 0
foreach ($case in $compositions) {
    if ($case.Key -cnotin $descriptorKeys -or $case.Mode -cnotin @('characters', 'tts:char', 'source_only')) {
        throw "Unknown composed-symbol key or mode: '$($case.Key)' / '$($case.Mode)'."
    }
    $caseId = $case.Key + '|' + $case.Character + '|' + $case.Mode
    if (-not $compositionIds.Add($caseId)) {
        throw "Duplicate composed-symbol case: '$caseId'."
    }
    $null = $compositionKeys.Add($case.Key)
    if ([string]::IsNullOrWhiteSpace($case.OriginalPhonemes) -or [string]::IsNullOrWhiteSpace($case.ExpectedPhonemes)) {
        throw "The composed-symbol fixture is incomplete for '$($case.Key)'."
    }
    $keyPattern = '^' + [regex]::Escape($case.Key) + '[\t ]+'
    $sourceEntries = @($sourceLines | Where-Object { $_ -cmatch $keyPattern })
    if ($sourceEntries.Count -ne 1) {
        throw "Expected exactly one source descriptor for '$($case.Key)'."
    }
    $sourcePhonemes = (($sourceEntries[0] -creplace $keyPattern, '') -split '[\t ]+')[0]
    if ($sourcePhonemes -cne $case.ExpectedPhonemes) {
        throw "Composed descriptor source differs from the fixture: '$($case.Key)'."
    }
    $originalSounds = $case.OriginalPhonemes.Replace('||', '').Replace('_:', '').Replace('_', '')
    $expectedSounds = $case.ExpectedPhonemes.Replace('||', '').Replace('_:', '').Replace('_', '')
    if ($originalSounds -cne $expectedSounds) {
        throw "The descriptor '$($case.Key)' changed beyond its pause/word boundary."
    }
    if ($case.Key -cin @('_cap', '_lig')) {
        if (-not $case.ExpectedPhonemes.EndsWith('||_:')) {
            throw "Prefix descriptor '$($case.Key)' must separate the following letter name."
        }
    } elseif (-not $case.ExpectedPhonemes.StartsWith('||_:')) {
        throw "Suffix descriptor '$($case.Key)' must separate the preceding letter name."
    }
    if ($case.Mode -ceq 'source_only') {
        if ($case.Key -cne '_ac2' -or $case.Character.Length -ne 0 -or $case.ExpectedTrace.Length -ne 0) {
            throw 'Only the currently unreachable double-acute descriptor is source-only here.'
        }
        $compositionSourceOnlyCount++
        continue
    }
    if ($case.Character.Length -ne 1 -or [string]::IsNullOrWhiteSpace($case.ExpectedTrace)) {
        throw "Incomplete runtime composition case: '$caseId'."
    }
    $inputText = (Get-CharacterMarkup $case.Character).Replace('"characters"', '"' + $case.Mode + '"')
    $trace = Get-PolishSymbolTrace $inputText -Markup
    if ($trace -cne $case.ExpectedTrace -or -not $trace.Contains('_:')) {
        throw "Unexpected composed-symbol pronunciation for '$caseId': expected '$($case.ExpectedTrace)', received '$trace'."
    }
    $compositionRuntimeCount++
}
if ($compositionKeys.Count -ne 10 -or $compositionSourceOnlyCount -ne 1 -or $compositionRuntimeCount -ne 16) {
    throw 'Composed-symbol coverage must retain ten descriptors, 16 runtime cases and one source-only case.'
}

Write-Host "Polish symbol test OK: 65 unique source records, $runtimeCount exact symbol traces, $compositionRuntimeCount exact composition traces and six short controls."
Write-Host 'Limits: NBSP and the double-acute descriptor are source-only; audio pause duration is not measured here. The existing two-letter joining inside generic ligatures is unchanged.'
