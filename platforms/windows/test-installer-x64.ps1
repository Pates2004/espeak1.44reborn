[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Installer
)

$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$temporaryRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
$temporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
$installDir = [IO.Path]::GetFullPath((Join-Path $temporaryRoot 'espeak-x64-installer-test'))
$temporaryPrefix = $temporaryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $installDir.StartsWith($temporaryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe installer test directory: $installDir"
}

$uninstaller = Join-Path $installDir 'unins000.exe'
$installed = $false
try {
    $installArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=`"$installDir`""
    $process = Start-Process -FilePath $installerPath -ArgumentList $installArguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "The installer failed with exit code $($process.ExitCode)."
    }
    $installed = $true

    $installedExe = Join-Path $installDir 'command_line\espeak.exe'
    $installedSapi = Join-Path $installDir 'espeak_sapi.dll'
    $installedDictionary = Join-Path $installDir 'espeak-data\pl_dict'
    foreach ($path in $installedExe,$installedSapi,$installedDictionary,$uninstaller) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "The installer did not create the required file: $path"
        }
    }

    & $installedExe "--path=$installDir" -q -v pl 'Test instalatora i polskiego glosu.'
    if ($LASTEXITCODE -ne 0) {
        throw 'The installed command-line synthesizer failed.'
    }

    $classKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\CLSID\{BE985C8D-BE32-4A22-AA93-55C16A6D1D91}\InprocServer32'
    if (-not (Test-Path -LiteralPath $classKey)) {
        throw 'The installed SAPI COM class is not registered.'
    }
    $registeredServer = (Get-Item -LiteralPath $classKey).GetValue('')
    if ([IO.Path]::GetFullPath($registeredServer) -ne [IO.Path]::GetFullPath($installedSapi)) {
        throw "The SAPI COM class points to an unexpected DLL: $registeredServer"
    }
}
finally {
    if ($installed -and (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru -WindowStyle Hidden
        if ($process.ExitCode -ne 0) {
            throw "The uninstaller failed with exit code $($process.ExitCode)."
        }
    }
}

if (Test-Path -LiteralPath $installDir) {
    throw "The uninstaller left the application directory behind: $installDir"
}

Write-Host 'Installer test OK: files, Polish synthesis, SAPI registration and uninstall.'
