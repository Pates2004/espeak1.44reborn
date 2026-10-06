# eSpeak 1.44.05 — Windows x86

This directory contains native MSVC x86 projects for:

- `espeak.exe` — command-line synthesizer with a WinMM audio backend;
- `espeak_lib.dll` — public eSpeak API library;
- `espeak_sapi.dll` — 32-bit SAPI 5 engine;
- `TTSApp.exe` — lightweight SAPI voice test application.

Build all binaries, run smoke tests, stage the package, and create the Inno
Setup installer from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File platforms\windows\build-x86.ps1
```

Requirements:

- Visual Studio Build Tools 2022 with the MSVC x86 toolchain and Windows SDK;
- .NET 10 SDK for Vario;
- Inno Setup 6 (`winget install JRSoftware.InnoSetup`) for the installer only.

Use `-SkipInstaller` when only the binaries are needed. The build uses the
same script to publish the framework-dependent x86 Vario voice manager as a
single executable. Vario requires .NET Desktop Runtime 10 x86 on the target
computer, including 64-bit Windows; the standard .NET app host offers its
download if it is missing. The runtime is not bundled with Vario or the
installer. The regular
installer places Vario beside eSpeak and adds it to the eSpeak Start menu
group. Vario manages this edition's 32-bit SAPI registry view and its optional
Sonic speed boost.
The native projects use the static MSVC runtime, so the resulting programs do not require a separate
Visual C++ Redistributable installation.
