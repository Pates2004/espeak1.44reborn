[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet(32,64)][int]$RegistryBits = 64,
    [string]$Iscc
)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $repository 'project_materials.ps1')
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-ProjectMaterialPath $outputRoot $repository)) { throw 'Fixture output is outside project materials.' }
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a fresh fixture output directory.' }
if (!$Iscc) { $Iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe' }
$compiler = (Resolve-Path -LiteralPath $Iscc).Path
foreach ($path in @($outputRoot,$compiler)) {
    if ($path.Contains('"')) { throw 'Quoted fixture paths are not supported.' }
}
$null = New-Item -ItemType Directory -Path $outputRoot
$key = 'Software\Pates2004\eSpeakInstallerFixture\' + [Guid]::NewGuid().ToString('N')
$registryPath = 'Registry::HKEY_CURRENT_USER\' + $key
if (Test-Path -LiteralPath $registryPath) { throw 'Isolated registry fixture unexpectedly exists.' }
$view = if ($RegistryBits -eq 64) { 256 } else { 512 }

function Invoke-OwnedFixtureProcess([string]$File, [string]$Arguments, [string]$Label) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $File
    $info.Arguments = $Arguments
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
            throw 'Owned fixture process exceeded its 30-second bound.'
        }
        [IO.File]::WriteAllText((Join-Path $outputRoot ($Label + '.stdout.txt')), $stdout.Result)
        [IO.File]::WriteAllText((Join-Path $outputRoot ($Label + '.stderr.txt')), $stderr.Result)
        return $process.ExitCode
    } finally { $process.Dispose() }
}

$arguments = '"/DFixtureOutput=' + $outputRoot + '" "/DRegistryFixtureRoot=' + $key +
    '" /DRegistryFixtureView=' + $view + ' "' + (Join-Path $PSScriptRoot 'test-data\voice-registration-fixture.iss') + '"'
$compilerExit = Invoke-OwnedFixtureProcess $compiler $arguments 'compiler'
if ($compilerExit -ne 0) { throw ('Registration fixture compile failed: ' + $compilerExit) }
$fixtureExit = Invoke-OwnedFixtureProcess (Join-Path $outputRoot 'voice-registration-fixture.exe') `
    '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' 'fixture'
$resultFile = Join-Path $outputRoot 'registration-result.txt'
if (Test-Path -LiteralPath (Join-Path $outputRoot 'registration-error.txt')) {
    throw ([IO.File]::ReadAllText((Join-Path $outputRoot 'registration-error.txt')))
}
if ($fixtureExit -ne 1 -or !(Test-Path -LiteralPath $resultFile) -or
    (Test-Path -LiteralPath (Join-Path $outputRoot 'installation-requested.txt')) -or
    (Test-Path -LiteralPath $registryPath)) { throw 'Fixture exit, preinstall abort or owned-key cleanup failed.' }
$result = [IO.File]::ReadAllText($resultFile)
if ($result -notmatch '^PASS assertions=(\d+)$') { throw 'Fixture assertions did not complete.' }
[pscustomobject]@{Result='PASS';Assertions=[int]$Matches[1];RegistryBits=$RegistryBits;
    OriginalCompilerExit=$compilerExit;OriginalFixtureExit=$fixtureExit;InstallationCompleted=$false;
    RegistryPrefix=$key;OwnedRegistryRemoved=$true;
    IncludeSHA256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'voice-registration.iss') -Algorithm SHA256).Hash} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'result.json') -Encoding UTF8
Write-Output ('Installer registration fixture PASS (' + $RegistryBits + '-bit view).')
