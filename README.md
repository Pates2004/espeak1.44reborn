# eSpeak 1.44.05 Reborn — r34 (32-bit)

This repository is the 32-bit Windows companion to the native 64-bit
eSpeak r34 project:

<https://github.com/Pates2004/espeak-1.44.05-x64>

It keeps the same eSpeak 1.44.05 speech engine, Polish dictionary and rule
updates, while providing a native Win32 build for compatibility with 32-bit
applications and SAPI clients.  The `r34` label follows the 64-bit baseline;
this repository is the matching 32-bit build rather than a separate language
or engine revision.

## Release r34 / Wydanie r34

This maintenance release safely releases shared SAPI/core resources when COM
allows the module to unload, while preserving active voices and supporting later
reinitialization. Voice enumeration and candidate lists grow dynamically, fixing
the old 150-entry overflow without dropping installed languages or variants.
The public C API also supports clean repeated Initialize/Terminate cycles.
Pronunciation data, Vario's interface and speed settings are unchanged.

Wydanie poprawia zwalnianie wspólnych zasobów SAPI i rdzenia po zakończeniu ich
używania oraz ponowną inicjalizację silnika. Dynamiczny katalog głosów usuwa błąd
przepełnienia dawnej tablicy 150 elementów, bez obcinania języków i wariantów.
Słowniki, interfejs Vario i ustawienia szybkości pozostają bez zmian.

The voice-variant collection is synchronized with the 104 variants shipped by
eSpeak NG 1.52.0. The `fast` variant keeps its equivalent classic-eSpeak syntax
so the complete collection loads without parser errors on this engine.

Release r27 restores native Up/Down navigation in Vario and exposes every
language node as one standard checkable tree item, including its selection and
expanded/collapsed state, for NVDA and other UI Automation clients.

Release r28 corrects the Polish `ci` pronunciation in words such as
*druciana*, *bociana*, *starcia* and *tarcia*. The dictionary is identical to
the x64 edition and includes a focused regression check.

Release r29 adds Vario's choice of the original upper-range Sonic boost or
NVDA-style threefold speed across the SAPI rate scale. The NVDA mode is
selected by default, while enabling boost remains a separate choice.

## Local r33 update

User instructions in English and Polish are in
[`platforms/windows/Readme.txt`](platforms/windows/Readme.txt) and
[`Vario/README.md`](platforms/windows/Vario/README.md).

Vario now has an Alt-accessible Settings menu with light/dark themes, an
interface language override (system, English, Polish) and optional usage hints.
System language selects Polish only when the primary Windows UI language is
Polish. Preferences are stored in `vario.ini` beside `Vario.exe`; existing Sonic
settings are migrated from the architecture-specific registry without deleting
that recovery source. SAPI voice registration remains normal Windows integration.

The new smooth speed mode spans 80-1350 WPM. It uses the native engine up to
300 WPM and then holds that articulation while Sonic applies the remaining
compression. Legacy upper-range and NVDA-style modes remain available. Fresh
settings default to smooth; previously saved speed settings are preserved.

The r33 dictionary update corrects `kwadratowy` in Polish square-bracket names
and improves word separation in existing multiword character and symbol names,
including `u zamknięte`. This is a dictionary-only change: ordinary text spacing,
the synthesis engine, Sonic and Vario behavior are not changed.

The earlier r32 dictionary update restored the user-selected pronunciations from
the supplied `BOY` reference for numeric 30, 40 and 200, including those components
in larger numbers. Their written-word equivalents keep the existing Polish
affricate: digits and words intentionally differ. Numeric 300 and `trzysta`
are unchanged. No primary stress markers are moved.

Standard reductions in `pięćdziesiąt`, `sześćdziesiąt`, `dziewięćdziesiąt` and
their derived forms remain unchanged, as in `BOY`. The careful `pierwsz-`
pronunciation retains its `f`, while the fuller `sześćset`/600 articulation with
`ć` remains an explicit user preference, not a claim of standard pronunciation.
The existing `Tarzan` and `kolaż` pronunciation is preserved. This release does
not change engine, Sonic or Vario functionality; the existing features above
remain available.

Vario requires the matching **.NET Desktop Runtime 10** (x64 or x86). It is
framework-dependent and does not include a private runtime. Final local
installers are in `installfiles`; previous builds are kept in `snapshots`.

## Building on Windows

Install Visual Studio Build Tools 2022 with the MSVC x86 toolchain and the
Windows SDK, and .NET 10 SDK for Vario. Inno Setup 6 is required only to create
the installer.

From the repository root run:

```powershell
powershell -ExecutionPolicy Bypass -File platforms\windows\build-x86.ps1
```

The script builds the command-line synthesizer, library, 32-bit SAPI engine
and test application, compiles the Polish dictionary, stages the package and
creates `installfiles\setup_espeak-1.44.05-x86-r33.exe`.

Use `-SkipInstaller` when only the binaries are needed.  `-SkipTests` skips
the optional project smoke checks while retaining the normal compilation and
packaging steps.

Vario is a framework-dependent single-file application and requires .NET
Desktop Runtime 10 x86 on the user's computer, including 64-bit Windows.
The installer does not bundle the runtime. If it is missing, launching Vario
opens the standard Windows .NET app-host download prompt. The native eSpeak
engine works without .NET.

## Installing with the 64-bit build

The x86 and x64 installers use different installer identifiers and SAPI
registry views (`HKLM32` and `HKLM64`).  On 64-bit Windows they are therefore
designed to be installed side by side: the x86 build serves 32-bit clients and
the x64 build serves 64-bit clients.

## License

eSpeak is distributed under the GNU General Public License, version 3.  See
[`License.txt`](License.txt).
