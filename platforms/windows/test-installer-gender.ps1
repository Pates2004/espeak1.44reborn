param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$voicesRoot = Join-Path $repository 'espeak-data\voices'
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a fresh fixture output directory.' }
if (!$Iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $Iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$Iscc) { throw 'Inno Setup 6 was not found.' }
}
$compiler = (Resolve-Path -LiteralPath $Iscc).Path
foreach ($path in @($repository, $outputRoot, $compiler)) {
    if ($path.IndexOf('"') -ge 0) { throw 'A fixture path contains a quote.' }
}
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$casePath = Join-Path $outputRoot 'cases.tsv'
$resultPath = Join-Path $outputRoot 'parser-result.txt'
$cases = New-Object 'Collections.Generic.List[string]'

function Read-Gender([string]$path) {
    $gender = ''
    foreach ($line in [IO.File]::ReadAllLines($path)) {
        $content = ($line -split '//', 2)[0]
        if ($content -match '^\s*gender\s+(male|female)(?:\s|$)') {
            $gender = if ($Matches[1] -ieq 'female') { 'Female' } else { 'Male' }
        }
    }
    return $gender
}

$baseGenders = @{}
foreach ($file in Get-ChildItem -LiteralPath $voicesRoot -Recurse -File) {
    $relative = $file.FullName.Substring($voicesRoot.Length + 1)
    if (($relative -split '\\')[0] -in @('!v', 'test')) { continue }
    $gender = Read-Gender $file.FullName
    if (!$gender) { $gender = 'Male' }
    $baseGenders[$file.Name] = $gender
    $cases.Add($voicesRoot + "`t" + $file.Name + "`t" + $gender)
}
if (!$baseGenders.ContainsKey('pl')) { throw 'The Polish base voice is missing.' }
$variantCount = 0
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $voicesRoot '!v') -File) {
    $gender = Read-Gender $file.FullName
    if (!$gender) { $gender = $baseGenders['pl'] }
    $cases.Add($voicesRoot + "`tpl+" + $file.Name + "`t" + $gender)
    $variantCount++
}
if ($variantCount -ne 104) { throw 'Review a changed bundled variant collection.' }
foreach ($female in @('Alicia','Andrea','anika','anikaRobot','Annie','aunty','belinda','grandma',
    'linda','steph','steph2','steph3','whisperf')) {
    if ((Read-Gender (Join-Path $voicesRoot ('!v\' + $female))) -ne 'Female') {
        throw ('The reference female definition is missing: ' + $female)
    }
}

$synthetic = Join-Path $outputRoot 'synthetic-voices'
[IO.Directory]::CreateDirectory((Join-Path $synthetic '!v')) | Out-Null
[IO.File]::WriteAllText((Join-Path $synthetic 'base'), "// gender male`r`nGender`tFeMaLe// actual declaration`r`n")
[IO.File]::WriteAllText((Join-Path $synthetic '!v\neutral'), "name gender male`r`npitch 80 120`r`n")
[IO.File]::WriteAllText((Join-Path $synthetic '!v\override'), "gender female`r`n gender`tMALE // last override`r`n")
foreach ($voice in @('base', 'base+neutral', 'BASE+NEUTRAL')) { $cases.Add($synthetic + "`t" + $voice + "`tFemale") }
foreach ($voice in @('base+override', 'unknown', 'unknown+neutral')) { $cases.Add($synthetic + "`t" + $voice + "`tMale") }
[IO.File]::WriteAllLines($casePath, $cases, (New-Object Text.UTF8Encoding($true)))

function Invoke-Owned([string]$exe, [string]$arguments, [string]$name) {
    $process = New-Object Diagnostics.Process
    $process.StartInfo.FileName = $exe
    $process.StartInfo.Arguments = $arguments
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    try {
        $process.Start() | Out-Null
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'Owned metadata fixture exceeded 30 seconds.'
        }
        [IO.File]::WriteAllText((Join-Path $outputRoot ($name + '.stdout.txt')), $stdout.Result)
        [IO.File]::WriteAllText((Join-Path $outputRoot ($name + '.stderr.txt')), $stderr.Result)
        return $process.ExitCode
    } finally { $process.Dispose() }
}

$compileArgs = '"/DFixtureOutput=' + $outputRoot + '" "/DCaseFile=' + $casePath +
    '" "/DResultFile=' + $resultPath + '" "' + (Join-Path $PSScriptRoot 'test-data\voice-gender-fixture.iss') + '"'
$compilerExit = Invoke-Owned $compiler $compileArgs 'compiler'
if ($compilerExit -ne 0) { throw ('Metadata fixture compilation failed: ' + $compilerExit) }
$originalExit = Invoke-Owned (Join-Path $outputRoot 'voice-gender-fixture.exe') `
    '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' 'fixture'
if ($originalExit -eq 0 -or (Test-Path -LiteralPath ($resultPath + '.installation-requested'))) {
    throw 'The metadata fixture did not abort before installation.'
}
if (!(Test-Path -LiteralPath $resultPath) -or
    [IO.File]::ReadAllText($resultPath) -ne ('PASS cases=' + $cases.Count)) {
    throw 'The metadata parser assertions did not pass.'
}
$result = [ordered]@{
    ParserAssertions = 'PASS'; Cases = $cases.Count; BundledVariants = $variantCount
    OriginalFixtureExitCode = $originalExit; InstallationCompleted = $false
    VoiceRoot = $voicesRoot; IncludeSHA256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'voice-gender.iss')).Hash
}
$json = $result | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $outputRoot 'result.json'), $json)
$json
