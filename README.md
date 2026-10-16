> Current installers are in this checkout's `installfiles`. Build and useful diagnostic files are in `D:\work\Workspace\espeak1.44reborn`; audits in `D:\work\Forme\espeak1.44reborn`, previous builds in `D:\work\Snapshots\espeak1.44reborn`, and disposable files in `D:\work\Trash\espeak1.44reborn`. Scripts resolve these category paths automatically. Use `C:\work` only if drive D: does not exist.

# eSpeak 1.44.05 Reborn — r37 (32-bit)

This repository is the 32-bit Windows companion to the native 64-bit
eSpeak r37 project:

<https://github.com/Pates2004/espeak-1.44.05-x64>

It keeps the same eSpeak 1.44.05 speech engine, Polish dictionary and rule
updates, while providing a native Win32 build for compatibility with 32-bit
applications and SAPI clients.  The `r37` label follows the 64-bit baseline;
this repository is the matching 32-bit build rather than a separate language
or engine revision.

## Release r37 / Wydanie r37

Vario announces real checkbox state changes for both the speed checkbox and
three-state voice trees. Native tree navigation, localized root labels and optional
hints are retained. Sonic is now off for fresh settings; existing saved choices
and legacy settings are preserved. The selected speed mode remains available.
In Polish punctuation, ASCII `-` is named "minus". Bracket names are unchanged.

Vario udostępnia czytnikom ekranu rzeczywiste zmiany zaznaczenia pola szybkości
oraz trójstanowych drzew głosów. Zachowano zwykłą nawigację drzewa, tłumaczone
etykiety i opcjonalne podpowiedzi. Sonic jest domyślnie wyłączony przy nowych
ustawieniach; zapisane wybory i starsze ustawienia pozostają zachowane. Wybrany
tryb szybkości pozostaje dostępny. Znak ASCII `-` ma nazwę „minus”; nawiasów
nie zmieniono.

## Release r36 / Wydanie r36

This dictionary correction uses separate hard `z` and `i` in `ziścić`, `ziszcz-`
and their inflections, including forms beginning with `nie-`. WSJP records
`ziścić` as [z-iścić] and `ziszczać się` as [z-iszczać się]. The soft-`ź`
exception introduced for `nieziszcz-` in r35 was incorrect and has been removed.
The sing-family correction, earlier approved pronunciations, Vario and synthesis
settings remain in place.

Wydanie słownikowe przywraca osobne, twarde `z` i samogłoskę `i` w rodzinie
`ziścić`/`ziszcz-`, również w odmianach i formach z `nie-`. WSJP podaje
[z-iścić] oraz [z-iszczać się]. Miękki wyjątek dla `nieziszcz-` dodany w r35
był błędny i został usunięty. Poprawka rodziny `sing-`, wcześniejsze uzgodnione
wymowy, Vario i ustawienia syntezy pozostają zachowane.

Sources / Źródła: [WSJP: ziścić](https://wsjp.pl/haslo/podglad/65243/ziscic),
[WSJP: ziszczać się](https://wsjp.pl/haslo/podglad/43703/ziszczac-sie).

## Release r35 / Wydanie r35 — historical / historyczne

The soft-`ź` claim for `nieziszcz-` below was an incorrect linguistic conclusion,
superseded by r36. Poniższy opis miękkiego `ź` w `nieziszcz-` był błędną oceną
wymowy i został zastąpiony poprawką r36. It is retained here as release history.

The Polish dictionary no longer forces hard `s` in every word beginning with
`sing-`. English-spelled `sing`, `singing` and `singer` now use the requested
soft Polish onset. Polish `singiel` and the `singl-` family retain hard `s`.
Other established Polish hard-onset exceptions remain explicit and bounded.
The `nieziszcz-` family now retains the voiced soft `ź` of `ziszcz-` after `nie-`.
Build output and installer inputs now consistently use the central material
directory; native synthesis, Vario controls and speed modes are unchanged.

Polski słownik nie wymusza już twardego `s` we wszystkich słowach na `sing-`.
`Sing`, `singing` i `singer` mają miękki początek zgodnie z życzeniem użytkownika.
`Singiel` i rodzina `singl-` zachowują twarde `s`. Pozostałe polskie wyjątki mają
osobne, ograniczone reguły. Wyniki kompilacji i dane instalatora korzystają
z centralnego katalogu materiałów; działanie Vario i tryby szybkości są zachowane.
Rodzina `nieziszcz-` zachowuje dźwięczne, miękkie `ź` także po przedrostku `nie-`.

`build/...` in build instructions resolves to `Workspace/build/...` under the
central material directory. Sources and project configuration stay in this checkout.

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
