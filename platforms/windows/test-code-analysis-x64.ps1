[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $projectRoot 'platforms\windows\x64\espeak.vcxproj'
$analysisFile = Join-Path $projectRoot 'build\x64\obj\espeak\Release\vc.nativecodeanalysis.all.xml'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw 'The 64-bit MSVC tools were not found.'
}

$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
& $msbuild $project /t:Rebuild /m:1 /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 /p:RunCodeAnalysis=true
if ($LASTEXITCODE -ne 0) {
    throw 'The MSVC Code Analysis build failed.'
}
if (-not (Test-Path -LiteralPath $analysisFile)) {
    throw "The MSVC Code Analysis report was not created: $analysisFile"
}

[xml]$analysis = Get-Content -LiteralPath $analysisFile
$defects = @($analysis.DEFECTS.DEFECT | Where-Object { $null -ne $_ })
if ($defects.Count -ne 0) {
    $summary = $defects | ForEach-Object {
        $source = $_.SFA | Select-Object -First 1
        "$($source.FILENAME):$($source.LINE) C$($_.DEFECTCODE) $($_.DESCRIPTION)"
    }
    throw "MSVC Code Analysis reported $($defects.Count) defect(s):`n$($summary -join "`n")"
}

Write-Host 'MSVC Code Analysis OK: 0 defects.'
