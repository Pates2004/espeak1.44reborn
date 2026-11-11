param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$DataPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $Exe).Path
$dataRoot = (Resolve-Path -LiteralPath $DataPath).Path.TrimEnd('\')
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
foreach ($path in @($exePath, $dataRoot, $outputRoot)) {
    if ($path.IndexOf('"') -ge 0) { throw 'Paths containing a quote are not supported by this fixture.' }
}
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a fresh output directory for each run.' }
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$inputPath = Join-Path $outputRoot 'sentence.txt'
$fileWave = Join-Path $outputRoot 'file.wav'
$stdoutWave = Join-Path $outputRoot 'stdout.wav'
[IO.File]::WriteAllText($inputPath, 'To jest zwykly test zapisu mowy. Polski glos czyta krotkie zdanie.',
    (New-Object Text.UTF8Encoding($false)))

function Invoke-Engine([string]$mode, [string]$name) {
    $process = New-Object Diagnostics.Process
    $process.StartInfo.FileName = $exePath
    $process.StartInfo.Arguments = '--path="' + $dataRoot + '" -v pl -b 1 -f "' + $inputPath + '" ' + $mode
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $bytes = New-Object IO.MemoryStream
    try {
        $process.Start() | Out-Null
        $copy = $process.StandardOutput.BaseStream.CopyToAsync($bytes)
        $errors = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) {
            $process.Kill()
            $process.WaitForExit()
            throw 'The owned ordinary synthesis process exceeded 30 seconds.'
        }
        if (!$copy.Wait(5000) -or !$errors.Wait(5000)) { throw 'Synthesis output did not close.' }
        [IO.File]::WriteAllText((Join-Path $outputRoot ($name + '.stderr.txt')), $errors.Result)
        if ($process.ExitCode -ne 0) { throw ('Synthesis failed: ' + $process.ExitCode) }
        return ,$bytes.ToArray()
    } finally {
        $bytes.Dispose()
        $process.Dispose()
    }
}

$unused = Invoke-Engine ('-w "' + $fileWave + '"') 'file'
[byte[]]$streamed = Invoke-Engine '--stdout' 'stdout'
[IO.File]::WriteAllBytes($stdoutWave, $streamed)
[byte[]]$normal = [IO.File]::ReadAllBytes($fileWave)
if ($normal.Length -le 44 -or $streamed.Length -ne $normal.Length) {
    throw ('Ordinary file/stdout WAV lengths differ: {0}/{1}.' -f $normal.Length, $streamed.Length)
}
if (($normal.Length - 44) % 2 -ne 0) { throw 'PCM must contain complete 16-bit samples.' }
if ([Text.Encoding]::ASCII.GetString($normal, 0, 4) -ne 'RIFF' -or
    [Text.Encoding]::ASCII.GetString($streamed, 8, 4) -ne 'WAVE' -or
    [Text.Encoding]::ASCII.GetString($streamed, 36, 4) -ne 'data') { throw 'Invalid WAV framing.' }
$lfCount = 0
$nonzero = $false
for ($i = 0; $i -lt $normal.Length; $i++) {
    # A nonseekable stdout WAV deliberately has placeholder RIFF/data lengths.
    if (($i -ge 4 -and $i -lt 8) -or ($i -ge 40 -and $i -lt 44)) { continue }
    if ($normal[$i] -ne $streamed[$i]) { throw ('Raw WAV differs at byte ' + $i) }
    if ($i -ge 44) {
        if ($normal[$i] -eq 10) { $lfCount++ }
        if ($normal[$i] -ne 0) { $nonzero = $true }
    }
}
if (!$nonzero -or $lfCount -eq 0) { throw 'The control sentence did not exercise binary PCM LF bytes.' }
if ([BitConverter]::ToUInt32($normal, 4) -ne $normal.Length - 8 -or
    [BitConverter]::ToUInt32($normal, 40) -ne $normal.Length - 44) { throw 'The seekable WAV lengths are invalid.' }
$result = [ordered]@{
    Result = 'PASS'; Executable = $exePath; DataPath = $dataRoot
    PcmBytes = $normal.Length - 44; PcmLfBytes = $lfCount; PcmByteIdentical = $true
    SampleRate = [BitConverter]::ToUInt32($normal, 24)
}
$json = $result | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $outputRoot 'result.json'), $json)
$json
