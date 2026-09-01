# eSpeak 1.44.05 Reborn — r25 (32-bit)

This repository is the 32-bit Windows companion to the native 64-bit
eSpeak r25 project:

<https://github.com/Pates2004/espeak-1.44.05-x64>

It keeps the same eSpeak 1.44.05 speech engine, Polish dictionary and rule
updates, while providing a native Win32 build for compatibility with 32-bit
applications and SAPI clients.  The `r25` label follows the 64-bit baseline;
this repository is the matching 32-bit build rather than a separate language
or engine revision.

## Building on Windows

Install Visual Studio Build Tools 2022 with the MSVC x86 toolchain and the
Windows SDK.  Inno Setup 6 is required only to create the installer.

From the repository root run:

```powershell
powershell -ExecutionPolicy Bypass -File platforms\windows\build-x86.ps1
```

The script builds the command-line synthesizer, library, 32-bit SAPI engine
and test application, compiles the Polish dictionary, stages the package and
creates `build\x86\installer\setup_espeak-1.44.05-x86-r25.exe`.

Use `-SkipInstaller` when only the binaries are needed.  `-SkipTests` skips
the optional project smoke checks while retaining the normal compilation and
packaging steps.

## Installing with the 64-bit build

The x86 and x64 installers use different installer identifiers and SAPI
registry views (`HKLM32` and `HKLM64`).  On 64-bit Windows they are therefore
designed to be installed side by side: the x86 build serves 32-bit clients and
the x64 build serves 64-bit clients.

## License

eSpeak is distributed under the GNU General Public License, version 3.  See
[`License.txt`](License.txt).
