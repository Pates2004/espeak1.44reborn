eSpeak 1.44.05 r34 - Windows 32-bit (x86)
====================================

r34: Safe shared SAPI/core teardown and reinitialization; dynamic voice catalogs
remove the old 150-entry overflow without dropping languages or variants.
Dictionary data, Vario UI and speed settings are unchanged.

r34: Bezpieczne zwalnianie wspólnych zasobów SAPI/rdzenia i ponowna inicjalizacja.
Dynamiczny katalog usuwa przepełnienie dawnej tablicy 150 głosów, bez obcinania
języków ani wariantów. Słowniki, interfejs Vario i szybkość pozostają bez zmian.

English
-------
This edition provides the native 32-bit (x86) SAPI 5 engine and the command-line
synthesizer in command_line. The 32-bit (x86) and 64-bit (x64) editions can coexist;
a SAPI application uses voices matching its own architecture.

The optional Vario application is selected by default in the installer and
appears in the eSpeak Start menu group. It manages installed SAPI languages,
voice variants and per-voice inflection without reinstalling eSpeak. Vario
requires .NET Desktop Runtime 10 x86, which is not bundled. If it is
missing, the standard Windows .NET host offers its download. The native speech
engine itself does not require .NET. Voice registration requires administrator
rights. If Vario was omitted, re-run the installer to add it.

Expand a language to choose individual voices, or check the language to select
all of them. A partial selection has an intermediate checkbox state. Delete
asks before removing the current installed voice; Shift+Delete omits that
confirmation. Remove selected always asks. Apply saves pending voice and speed
changes. Inflection adjusts pitch variation, not the base pitch.

Speed modes: smooth (new default), NVDA-style x3, and legacy upper-range boost.
Smooth covers 80-1350 words per minute, retaining native articulation up to 300;
above 300 Sonic progressively shortens the audio. Existing saved speed settings
are preserved. Disabling the extended speed range restores ordinary eSpeak
speed control. Restart SAPI clients after applying changes if necessary.

Use Alt or F 10 for Settings: light/dark theme, system/English/Polish interface,
and optional usage hints. System selects Polish only for Polish Windows UI;
other languages use English. Essential labels, states and errors remain when
hints are hidden. Appearance changes preserve pending edits and tree selection.

Own preferences are stored atomically in vario.ini beside Vario.exe and
espeak_sapi.dll, with no AppData fallback. Legacy registry speed preferences
are migrated without deleting their originals. SAPI voice registration stays
in the Windows registry because it is part of OS integration. A write error
is reported instead of silently choosing another configuration location.

All 104 bundled variants are listed under espeak-data\voices\!v. Voice names
can include a variant, for example pl+f3. The catalog comes from eSpeak NG 1.52.0;
the synthesis core remains the maintained classic eSpeak 1.44.05.
The r33 dictionary update corrects kwadratowy in Polish square-bracket names
and improves separation in existing multiword character and symbol names,
including u zamknięte. Ordinary text spacing and engine behavior are unchanged.
The earlier r32 dictionary update restored the user-selected BOY pronunciations
for numeric 30, 40 and 200, including these components in larger numbers.
Written trzydzieści, czterdzieści and dwieście retain their existing Polish ć;
digits and written words intentionally differ. Numeric 300 and trzysta are
unchanged. Primary stress is not moved.
The standard reductions in pięćdziesiąt, sześćdziesiąt, dziewięćdziesiąt and
their families remain as in BOY. The careful pierwsz- family retains f.
The fuller sześćset/600 pronunciation retains ć as an explicit user preference,
not a claim of conformity with the standard. Tarzan and kolaż are unchanged.
Existing engine, Sonic and Vario functionality is preserved in this release.

Command-line help: docs\commands.html. Source and license: License.txt (GPLv3).

Polski
------
Ta edycja zawiera natywny silnik SAPI 5 32-bit (x86) oraz syntezator konsolowy
w katalogu command_line. Wersje 32-bit (x86) i 64-bit (x64) mogą być zainstalowane
jednocześnie; aplikacja SAPI korzysta z głosów zgodnych ze swoją architekturą.

Opcjonalne Vario jest domyślnie zaznaczone w instalatorze i dostępne w grupie
eSpeak w menu Start. Zarządza językami SAPI, wariantami i modulacją pojedynczych
głosów bez reinstalacji syntezatora. Wymaga .NET Desktop Runtime 10 x86,
którego nie dołączono do instalatora. Przy jego braku standardowy host .NET
proponuje pobranie. Sam silnik mowy nie potrzebuje .NET. Zmiana rejestracji
głosów wymaga uprawnień administratora. Jeśli pominięto Vario, można dodać je
przez ponowne uruchomienie instalatora.

Rozwiń język, aby wybrać pojedyncze głosy, lub zaznacz język, aby wybrać wszystkie.
Częściowy wybór jest oznaczony stanem pośrednim. Delete pyta przed usunięciem
bieżącego zainstalowanego głosu, Shift+Delete pomija pytanie, a Usuń zaznaczone
zawsze wymaga potwierdzenia. Zastosuj zapisuje zmiany głosów i prędkości.
Modulacja oznacza zmienność wysokości mowy, a nie podstawową wysokość głosu.

Tryby prędkości: płynny (nowy domyślny), jak w NVDA x3 oraz dotychczasowe
przyspieszanie górnej części skali. Tryb płynny obejmuje 80-1350 słów na minutę:
do 300 pracuje natywny silnik, wyżej Sonic stopniowo skraca dźwięk przy stałej
artykulacji silnika. Dotychczasowe zapisane ustawienia pozostają zachowane.
Wyłączenie rozszerzonego zakresu przywraca zwykłe sterowanie prędkością eSpeak.
Po zmianie może być potrzebne ponowne uruchomienie aplikacji używającej SAPI.

Alt lub F 10 otwiera Ustawienia: motyw jasny/ciemny, język systemowy/angielski/
polski oraz podpowiedzi. Opcja systemowa wybiera polski przy polskim interfejsie
Windows, a angielski przy innych. Wyłączenie podpowiedzi nie usuwa etykiet,
stanów ani błędów. Zmiana wyglądu zachowuje oczekujące zmiany i stan drzewek.

Własne ustawienia zapisują się atomowo w vario.ini obok Vario.exe i
espeak_sapi.dll, bez zapisu awaryjnego w AppData. Dawne ustawienia prędkości
z rejestru są przenoszone do pliku bez kasowania oryginałów. Rejestracja głosów
SAPI pozostaje w rejestrze Windows jako integracja systemowa. Brak możliwości
zapisu powoduje komunikat, a nie cichą zmianę lokalizacji konfiguracji.

Katalog espeak-data\voices\!v zawiera 104 warianty. Wariant można dopisać do
nazwy głosu, np. pl+f3. Zestaw pochodzi z eSpeak NG 1.52.0, ale sam silnik
pozostaje rozwijaną wersją klasycznego eSpeak 1.44.05.
Słownik r33 poprawia kwadratowy w polskich nazwach nawiasów kwadratowych oraz
rozdzielenie słów w istniejących wielowyrazowych nazwach liter i symboli,
w tym u zamknięte. Odstępy w zwykłym tekście i działanie silnika są bez zmian.
Wcześniejsza aktualizacja słownika r32 przywróciła
wybrane przez użytkownika starsze brzmienie z BOY dla cyfr 30, 40 i 200,
także jako składników większych liczb. Słowa trzydzieści, czterdzieści i dwieście
zachowują dotychczasowe polskie ć: cyfry i słowa celowo brzmią inaczej.
Liczba 300 i słowo trzysta pozostają bez zmian. Nie przesunięto głównego akcentu.
Uproszczenia w pięćdziesiąt, sześćdziesiąt, dziewięćdziesiąt i ich rodzinach
pozostają takie jak w BOY. Staranna wymowa rodziny pierwsz- zachowuje f.
Pełniejsza wymowa sześćset/600 zachowuje ć zgodnie z wyraźną preferencją
użytkownika, a nie jako deklarację zgodności z normą. Tarzan i kolaż są bez zmian.
Wydanie zachowuje dotychczasowe funkcje silnika, Sonica i Vario.

Pomoc konsolowa: docs\commands.html. Licencja: License.txt (GPLv3).
