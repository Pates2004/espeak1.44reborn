# Vario

Vario is the accessible companion application installed with eSpeak. It edits
the eSpeak voice tokens in the registry view matching the installed SAPI
edition, lets the user combine languages with variants, and controls the
optional Sonic speed boost. Registry changes require administrator rights.

## English

The extended-range checkbox is separate from a radio-button choice of three
speed modes. Smooth mode maps the SAPI slider over the full 80–1350 WPM range
and adds Sonic automatically above 300 WPM. NVDA-style mode triples the
ordinary underlying rate; the original mode boosts only upper positive
rates. Smooth mode with the extended range enabled is the default for new
settings. Existing saved modes and the previous checkbox state are retained.
Turning off the extended range restores ordinary eSpeak speed regulation.

Available and installed voices are presented as checkable trees. Checking a
language selects all of its voices, while expanding it permits individual
selection. Delete removes the focused installed voice after confirmation;
Shift+Delete removes it without confirmation. The Remove selected button always
asks for confirmation.

Each installed voice has its own modulation value (`espeakRANGE`) from 0 to
100. Zero is monotone and 50 is eSpeak's normal value. This is independent of
the base pitch setting.

The standard Settings menu is available with Alt or F10. It offers light and
dark themes, interface language (system default, English or Polish), and a
Show hints switch. System default selects Polish only when Windows uses a
Polish UI culture; every other culture selects English. Interface changes
take effect immediately and preserve checked voices, pending additions and
removals, modulation, speed settings and tree navigation. Interface settings
are saved immediately; SAPI voices and speed changes still require Apply.

Disabling hints hides optional help labels, node tooltips and supplementary
screen-reader descriptions. Essential accessible names, states, validation
errors and confirmation dialogs remain. Native keyboard navigation and
tri-state check boxes are retained. High-contrast system colors take priority
over the dark palette.

Own settings are stored in UTF-16 `vario.ini`, section `[Settings]`, beside
`Vario.exe` and `espeak_sapi.dll`. Keys are `SonicBoost`, `SonicMode` (0 original,
1 NVDA-style, 2 smooth), `Theme` (light/dark), `Language` (system/en/pl), and
`ShowHints`. Saving uses an atomic replacement in the same directory and
preserves unknown INI keys and sections. Existing Sonic settings are read
from the matching 32-bit or 64-bit registry view and migrated without deleting
the original registry values. File values take precedence. A write error is
reported; there is no AppData fallback. SAPI voice registration remains in
the Windows registry because that is the operating system's voice interface.
SAPI applications may need to be restarted after applying changes.

Vario is published as a framework-dependent .NET 10 single-file Windows
application. It requires .NET Desktop Runtime 10 matching the installed
edition (x64 or x86); the runtime is not bundled in the installer. If the
required runtime is missing, the standard Windows .NET app host prompts the
user to download it. The native eSpeak engine does not require .NET.

## Polski

Vario zarządza językami i wariantami głosów SAPI właściwej wersji eSpeak.
Zmiana rejestracji głosów wymaga uprawnień administratora.

Pole rozszerzonego zakresu prędkości jest niezależne od wyboru trzech trybów.
Tryb płynny obejmuje cały zakres 80–1350 SNM i automatycznie dołącza Sonic
powyżej 300 SNM. Tryb wzorowany na NVDA mnoży zwykłą prędkość przez trzy,
a dotychczasowy przyspiesza tylko górne dodatnie poziomy SAPI. Nowe ustawienia
domyślnie wybierają tryb płynny i włączony rozszerzony zakres. Dotychczasowy
zapisany tryb i stan pola wyboru pozostają zachowane. Wyłączenie rozszerzonego
zakresu przywraca zwykłą regulację prędkości eSpeak.

Menu Ustawienia można otworzyć klawiszem Alt lub F10. Pozwala wybrać jasny
lub ciemny motyw, język zgodny z systemem, angielski albo polski oraz włączyć
lub wyłączyć podpowiedzi. Domyślna opcja systemowa wybiera polski tylko dla
polskiego języka interfejsu Windows; dla pozostałych wybiera angielski.
Zmiany interfejsu działają od razu, zapisują się automatycznie i nie kasują
zaznaczeń, oczekujących zmian głosów, modulacji, prędkości ani nawigacji
w drzewkach. Zmiany głosów i prędkości nadal wymagają przycisku Zastosuj.

Wyłączenie podpowiedzi ukrywa pomocnicze etykiety, dymki i dodatkowe opisy
dla czytnika ekranu. Nie usuwa niezbędnych nazw kontrolek, stanów, błędów ani
potwierdzeń. Zachowana jest standardowa nawigacja z klawiatury i częściowe
zaznaczenie języków. Systemowy wysoki kontrast ma pierwszeństwo przed ciemną
paletą.

Rozwinięcie języka umożliwia wybór pojedynczych głosów, a zaznaczenie języka
obejmuje wszystkie jego głosy. Delete pyta o usunięcie wskazanego głosu,
Shift+Delete pomija pytanie, a Usuń zaznaczone zawsze prosi o potwierdzenie.
Modulację ustawia się osobno dla każdego głosu w zakresie 0–100: 0 oznacza
monotonię, 50 zwykłą modulację. Nie jest to podstawowa wysokość głosu.

Własna konfiguracja jest zapisywana w pliku UTF-16 `vario.ini` obok
`Vario.exe` i `espeak_sapi.dll`, w sekcji `[Settings]`. Klucze to `SonicBoost`,
`SonicMode` (0 dotychczasowy, 1 jak w NVDA, 2 płynny), `Theme` (light/dark),
`Language` (system/en/pl) i `ShowHints`. Zapis atomowo zastępuje plik w tym
samym katalogu i zachowuje nieznane klucze oraz sekcje. Stare ustawienia
prędkości są odczytywane z odpowiedniego widoku rejestru i przenoszone do
pliku bez kasowania oryginalnych wartości rejestru. Wartości w pliku mają
pierwszeństwo. Brak prawa zapisu powoduje komunikat, bez ukrytego zapisu
w AppData. Systemowa rejestracja głosów SAPI pozostaje w rejestrze Windows.
Po zastosowaniu zmian programy korzystające z SAPI mogą wymagać ponownego
uruchomienia.

Vario jest jednoplikową aplikacją .NET 10 zależną od środowiska uruchomieniowego.
Wymaga .NET Desktop Runtime 10 w architekturze zgodnej z wersją Vario: x64
albo x86. Instalator nie zawiera runtime; przy jego braku standardowy host
.NET dla Windows proponuje pobranie. Sam natywny silnik eSpeak nie wymaga .NET.

## Focused regression checks / Testy regresyjne

`../Vario.Tests` exercises INI persistence and legacy migration, locale
selection, themes, optional accessibility hints and preservation of pending
UI edits. It uses an isolated test voice registry and never changes SAPI. Pass a
new fixture directory inside the repository's ignored Workspace for each run.
These checks do not replace real NVDA or installed-SAPI audio validation.

`../Vario.Tests` sprawdza zapis INI, migrację, wybór języka, motywy, opcjonalne
opisy dostępności i zachowanie niezapisanych zmian. Używa izolowanego rejestru
testowego i nie zmienia SAPI. Każde uruchomienie wymaga nowego katalogu próbek wewnątrz
ignorowanego Workspace projektu. Testy nie zastępują odsłuchu SAPI ani NVDA.

```powershell
dotnet run --project platforms/windows/Vario.Tests -c Release -r win-x64 -- . Workspace/vario-test-run pl-PL
```
