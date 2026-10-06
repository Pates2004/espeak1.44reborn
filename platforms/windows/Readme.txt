eSpeak 1.44.05 r30 - Windows 32-bit (x86)
====================================

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
The r30 Polish dictionary retains the consonant in pierwsz- and fuller number
clusters including sześćset, pięćdziesiąt, sześćdziesiąt and dziewięćdziesiąt.
Normal Polish voicing assimilation remains. Fuller clusters are a requested
pronunciation preference, not a statement that accepted reductions are wrong.

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
pozostaje rozwijaną wersją klasycznego eSpeak 1.44.05. Słownik r30 zachowuje
spółgłoskę w rodzinie pierwsz- i pełniejsze zbitki w liczebnikach sześćset,
pięćdziesiąt, sześćdziesiąt oraz dziewięćdziesiąt. Zwykłe upodobnienia polskie
pozostają. Pełniejsza wymowa jest wybraną preferencją, a nie negowaniem
poprawności dopuszczalnych uproszczeń językowych.

Pomoc konsolowa: docs\commands.html. Licencja: License.txt (GPLv3).
