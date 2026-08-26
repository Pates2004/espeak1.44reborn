[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $projectRoot 'platforms\windows\x64\espeak.vcxproj'
$espeakExe = Join-Path $projectRoot 'build\x64\Release\espeak.exe'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw 'The 64-bit MSVC tools were not found.'
}

$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild $project /t:Rebuild /m:1 /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 /p:RunCodeAnalysis=false /p:EnableASAN=true
if ($LASTEXITCODE -ne 0) {
    throw 'The AddressSanitizer build failed.'
}

$asanRuntime = Get-ChildItem -LiteralPath (Join-Path $vsPath 'VC\Tools\MSVC') -Recurse -Filter 'clang_rt.asan_dynamic-x86_64.dll' |
    Where-Object { $_.FullName -like '*\Hostx64\x64\*' } |
    Select-Object -First 1
if (-not $asanRuntime) {
    throw 'The x64 AddressSanitizer runtime was not found.'
}

$env:Path = "$($asanRuntime.DirectoryName);$env:Path"
$env:ASAN_OPTIONS = 'halt_on_error=1:abort_on_error=1:detect_leaks=0'

& (Join-Path $PSScriptRoot 'test-polish-numbers.ps1') -EspeakExe $espeakExe -DataPath $projectRoot
& (Join-Path $PSScriptRoot 'test-long-input.ps1') -EspeakExe $espeakExe -DataPath $projectRoot

Write-Host 'AddressSanitizer tests OK.'
