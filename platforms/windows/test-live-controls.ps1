param(
    [Parameter(Mandatory=$true)][string]$TestExe,
    [Parameter(Mandatory=$true)][string]$CandidateDll,
    [Parameter(Mandatory=$true)][string]$DataPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ReferenceDll
)
$ErrorActionPreference = 'Stop'
$test = (Resolve-Path -LiteralPath $TestExe).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDll).Path
$data = (Resolve-Path -LiteralPath $DataPath).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $root) { throw 'A fresh output directory is required.' }
$candidateRuntime = Join-Path $root 'candidate-runtime'
[IO.Directory]::CreateDirectory($candidateRuntime) | Out-Null
$candidateCopy = Join-Path $candidateRuntime 'espeak_sapi.dll'
Copy-Item -LiteralPath $candidate -Destination $candidateCopy
$referenceCopy = $null
if ($ReferenceDll) {
    $reference = (Resolve-Path -LiteralPath $ReferenceDll).Path
    $referenceRuntime = Join-Path $root 'reference-runtime'
    [IO.Directory]::CreateDirectory($referenceRuntime) | Out-Null
    $referenceCopy = Join-Path $referenceRuntime 'espeak_sapi.dll'
    Copy-Item -LiteralPath $reference -Destination $referenceCopy
}
$rows = New-Object 'Collections.Generic.List[object]'

function Invoke-Case([string]$dll, [string]$name, [int]$rate, [string]$mode, [int]$expected=0) {
    $wave = Join-Path $root ($name + '.wav')
    $p = New-Object Diagnostics.Process
    $p.StartInfo.FileName = $test
    $p.StartInfo.Arguments = '"' + $dll + '" "' + $data + '" "' + $wave + '" ' + $rate + ' "' + $mode + '"'
    $p.StartInfo.UseShellExecute = $false
    $p.StartInfo.CreateNoWindow = $true
    $p.StartInfo.RedirectStandardOutput = $true
    $p.StartInfo.RedirectStandardError = $true
    try {
        $p.Start() | Out-Null
        $stdout = $p.StandardOutput.ReadToEndAsync()
        $stderr = $p.StandardError.ReadToEndAsync()
        if (!$p.WaitForExit(30000)) { $p.Kill(); $p.WaitForExit(); throw ('Owned short synthesis timed out: '+$name) }
        [IO.File]::WriteAllText((Join-Path $root ($name+'.stdout.txt')), $stdout.Result)
        [IO.File]::WriteAllText((Join-Path $root ($name+'.stderr.txt')), $stderr.Result)
        $rows.Add([pscustomobject]@{Name=$name;Rate=$rate;Mode=$mode;ExitCode=$p.ExitCode;ExpectedExit=$expected;
            DLL=$dll;DLLSHA256=(Get-FileHash -LiteralPath $dll).Hash;Wave=$wave})
        if ($p.ExitCode -ne $expected) { throw ('Short synthesis case failed: '+$name+'; see its retained log.') }
        if ($expected -ne 0 -and ($stdout.Result -notmatch 'passed=0' -or $stdout.Result -notmatch 'HRESULT=00000000')) {
            throw ('The old-binary negative did not fail the intended live-control assertion: '+$name)
        }
    } finally { $p.Dispose() }
}

function Compare-PCM([string]$first, [string]$second) {
    [byte[]]$left = [IO.File]::ReadAllBytes((Join-Path $root ($first+'.wav')))
    [byte[]]$right = [IO.File]::ReadAllBytes((Join-Path $root ($second+'.wav')))
    if ($left.Length -le 44 -or $left.Length -ne $right.Length) { throw ('Static PCM length changed: '+$first) }
    for ($index=44; $index -lt $left.Length; $index++) {
        if ($left[$index] -ne $right[$index]) { throw ('Static PCM changed: '+$first+' at byte '+$index) }
    }
}

function Check-InitialMute {
    [byte[]]$muted = [IO.File]::ReadAllBytes((Join-Path $root 'off-volume0.wav'))
    [byte[]]$audible = [IO.File]::ReadAllBytes((Join-Path $root 'off-static.wav'))
    if ($muted.Length -ne $audible.Length) { throw 'Initial mute must retain the render timing needed for unmute.' }
    for ($index=44; $index -lt $muted.Length; $index++) {
        if ($muted[$index] -ne 0) { throw 'Initial mute emitted nonzero PCM.' }
    }
    if ($referenceCopy) {
        [byte[]]$oldMuted = [IO.File]::ReadAllBytes((Join-Path $root 'reference-volume0.wav'))
        # Old native volume zero changes waveform generation itself. Retaining
        # its silent sample count would prevent later unmuting that same render.
        [pscustomobject]@{CandidateFrames=($muted.Length-44)/2;ReferenceFrames=($oldMuted.Length-44)/2;
            CandidateAllZero=$true;Comparison='Silent output; candidate uses audible render timing for live unmute'} |
            ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $root 'initial-mute-timing.json')
    }
}

try {
    foreach ($configuration in @(@{Name='off';Boost=0;Mode=2;Static=0;Up=0},
        @{Name='nvda';Boost=1;Mode=1;Static=0;Up=0},
        @{Name='smooth';Boost=1;Mode=2;Static=5;Up=0},
        @{Name='legacy';Boost=1;Mode=0;Static=5;Up=1})) {
        $ini = "[Settings]`r`nSonicBoost="+$configuration.Boost+"`r`nSonicMode="+$configuration.Mode+"`r`n"
        [IO.File]::WriteAllText((Join-Path $candidateRuntime 'vario.ini'),$ini,[Text.Encoding]::Unicode)
        if ($referenceCopy) { [IO.File]::WriteAllText((Join-Path $referenceRuntime 'vario.ini'),$ini,[Text.Encoding]::Unicode) }
        $prefix = $configuration.Name
        Invoke-Case $candidateCopy ($prefix+'-static') $configuration.Static '0'
        if ($referenceCopy) {
            Invoke-Case $referenceCopy ($prefix+'-reference-static') $configuration.Static '0'
            Compare-PCM ($prefix+'-static') ($prefix+'-reference-static')
        }
        Invoke-Case $candidateCopy ($prefix+'-static-mixed') $configuration.Static 'mixedstatic'
        if ($referenceCopy) {
            Invoke-Case $referenceCopy ($prefix+'-reference-static-mixed') $configuration.Static 'mixedstatic'
            Compare-PCM ($prefix+'-static-mixed') ($prefix+'-reference-static-mixed')
        }
        Invoke-Case $candidateCopy ($prefix+'-rate-up') $configuration.Up 'liverateup'
        Invoke-Case $candidateCopy ($prefix+'-rate-down') 5 'liveratedown'
        Invoke-Case $candidateCopy ($prefix+'-mute') $configuration.Static 'mute'
        Invoke-Case $candidateCopy ($prefix+'-unmute') $configuration.Static 'unmute'
        Invoke-Case $candidateCopy ($prefix+'-mixed-rate') $configuration.Up 'livemixed'
        Invoke-Case $candidateCopy ($prefix+'-rate-stop-resume') $configuration.Up 'liverateup-abort'
        if ($prefix -eq 'off') {
            foreach ($volume in @('volume50','volume0')) {
                Invoke-Case $candidateCopy ('off-'+$volume) 0 $volume
                if ($referenceCopy) {
                    Invoke-Case $referenceCopy ('reference-'+$volume) 0 $volume
                    if ($volume -eq 'volume50') { Compare-PCM ('off-'+$volume) ('reference-'+$volume) }
                }
            }
            Check-InitialMute
            Invoke-Case $candidateCopy 'ordinary-stop-partial-resume' 0 'abortpartial'
            Invoke-Case $candidateCopy 'ordinary-partial-write' 0 'partialwrite'
            if ($referenceCopy) {
                Invoke-Case $referenceCopy 'old-rate-update-negative' 0 'liverateup' 8
                Invoke-Case $referenceCopy 'old-volume-update-negative' 0 'mute' 8
            }
        }
    }
    $rows.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $root 'cases.json')
    Write-Output ('PASS: '+$rows.Count+' bounded SAPI cases, including static PCM comparisons and original-failure controls.')
} catch {
    $rows.ToArray() | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $root 'failed-cases.json')
    throw
}
