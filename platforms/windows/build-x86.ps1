[CmdletBinding()]
param(
    [switch]$SkipInstaller,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$x86ProjectDir = Join-Path $projectRoot 'platforms\windows\x86'
$releaseDir = Join-Path $projectRoot 'build\x86\Release'
$stageDir = Join-Path $projectRoot 'build\x86\package'
$installerDir = Join-Path $projectRoot 'build\x86\installer'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'vswhere.exe was not found. Install Visual Studio Build Tools with the C++ component.'
}

$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw 'The MSVC x86/x64 tools were not found.'
}
$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild was not found: $msbuild"
}

# Some automation hosts expose both Path and PATH. MSBuild's managed launcher
# treats them as duplicate keys, so rebuild a clean process-local Path.
$machineSearchPath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
$userSearchPath = [Environment]::GetEnvironmentVariable('Path', 'User')
[Environment]::SetEnvironmentVariable('PATH', $null, 'Process')
[Environment]::SetEnvironmentVariable('Path', "$machineSearchPath;$userSearchPath", 'Process')

$projects = @(
    'espeak.vcxproj',
    'espeak_lib.vcxproj',
    'espeak_sapi.vcxproj',
    'TTSApp.vcxproj',
    'sapi_smoke.vcxproj'
)

foreach ($project in $projects) {
    & $msbuild (Join-Path $x86ProjectDir $project) /m /nologo /v:minimal /p:Configuration=Release /p:Platform=Win32
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $project"
    }
}

$espeakExe = Join-Path $releaseDir 'espeak.exe'
& $espeakExe --path=$projectRoot --compile=pl
if ($LASTEXITCODE -ne 0) {
    throw 'The Polish dictionary compilation failed.'
}

if (-not $SkipTests) {
    & (Join-Path $PSScriptRoot 'test-polish-numbers.ps1') -EspeakExe $espeakExe -DataPath $projectRoot
    & (Join-Path $PSScriptRoot 'test-long-input.ps1') -EspeakExe $espeakExe -DataPath $projectRoot
    & (Join-Path $PSScriptRoot 'test-polish-fallback.ps1') -EspeakExe $espeakExe -DataPath $projectRoot
    & (Join-Path $PSScriptRoot 'test-polish-georgian.ps1') -EspeakExe $espeakExe -DataPath $projectRoot

    $wavPath = Join-Path $releaseDir 'smoke-pl.wav'
    & $espeakExe --path=$projectRoot -v pl -w $wavPath '32-bit synthesis test.'
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $wavPath)) {
        throw 'The WAV synthesis test failed.'
    }

    & (Join-Path $releaseDir 'sapi_smoke.exe') (Join-Path $releaseDir 'espeak_sapi.dll')
    if ($LASTEXITCODE -ne 0) {
        throw 'The COM/SAPI class factory test failed.'
    }
}

$resolvedBuildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'build'))
$resolvedStage = [IO.Path]::GetFullPath($stageDir)
if (-not $resolvedStage.StartsWith($resolvedBuildRoot + [IO.Path]::DirectorySeparatorChar,
                                   [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe package staging path: $resolvedStage"
}
if (Test-Path -LiteralPath $resolvedStage) {
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedStage,(Join-Path $resolvedStage 'command_line') | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'espeak-data') -Destination $resolvedStage -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'dictsource') -Destination $resolvedStage -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $resolvedStage -Recurse
Copy-Item -LiteralPath (Join-Path $releaseDir 'espeak_sapi.dll'),(Join-Path $releaseDir 'TTSApp.exe') -Destination $resolvedStage
Copy-Item -LiteralPath (Join-Path $releaseDir 'espeak.exe'),(Join-Path $releaseDir 'espeak_lib.dll'),(Join-Path $projectRoot 'src\speak_lib.h') -Destination (Join-Path $resolvedStage 'command_line')
Copy-Item -LiteralPath (Join-Path $projectRoot 'platforms\windows\Readme.txt') -Destination (Join-Path $resolvedStage 'Readme.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'License.txt') -Destination $resolvedStage

if (-not $SkipInstaller) {
    $isccCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw 'ISCC.exe was not found. Install Inno Setup 6 (winget install JRSoftware.InnoSetup).'
    }

    & $iscc (Join-Path $projectRoot 'platforms\windows\make_espeak-x86.iss')
    if ($LASTEXITCODE -ne 0) {
        throw 'Installer compilation failed.'
    }
}

Write-Host "Binaries: $releaseDir"
Write-Host "Package:  $resolvedStage"
if (-not $SkipInstaller) {
    Write-Host "Installer: $(Join-Path $installerDir 'setup_espeak-1.44.05-x86-r21.exe')"
}
