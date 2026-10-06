using System.Globalization;

namespace Vario;

internal static class UiText
{
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    private static AppLanguage language;
    private static bool Polish => language == AppLanguage.Polish ||
        (language == AppLanguage.System && SystemUiCulture.TwoLetterISOLanguageName.Equals("pl", StringComparison.OrdinalIgnoreCase));

    internal static void SetLanguage(AppLanguage value) => language = value;

    internal static string Pick(string english, string polish) => Polish ? polish : english;

    internal static string Title => Pick("Vario - eSpeak voice manager", "Vario - menedżer głosów eSpeak");
    internal static string Architecture(string architecture) => Pick(
        $"SAPI edition: {architecture}. Installation: {AppContext.BaseDirectory}",
        $"Wersja SAPI: {architecture}. Instalacja: {AppContext.BaseDirectory}");
    internal static string AvailableGroup => Pick("Voices available to add", "Głosy dostępne do dodania");
    internal static string InstalledGroup => Pick("Voices currently exposed to SAPI", "Głosy obecnie udostępniane przez SAPI");
    internal static string AvailableTreeHelp => Pick(
        "Expand a language, select voice check boxes, then choose Add selected. Selecting a language checks all of its voices.",
        "Rozwiń język, zaznacz pola wyboru głosów i wybierz Dodaj zaznaczone. Zaznaczenie języka zaznacza wszystkie jego głosy.");
    internal static string InstalledTreeHelp => Pick(
        "Expand a language and select voice check boxes for removal. Delete removes the focused voice after confirmation; Shift+Delete skips confirmation.",
        "Rozwiń język i zaznacz pola wyboru głosów do usunięcia. Delete usuwa wskazany głos po potwierdzeniu, a Shift+Delete pomija potwierdzenie.");
    internal static string AddSelected => Pick("&Add selected", "&Dodaj zaznaczone");
    internal static string RemoveSelected => Pick("&Remove selected", "&Usuń zaznaczone");
    internal static string InflectionGroup => Pick("Voice modulation (inflection)", "Modulacja głosu (inflection)");
    internal static string Inflection => Pick("&Modulation, 0 to 100:", "&Modulacja, od 0 do 100:");
    internal static string InflectionHelp => Pick(
        "eSpeak pitch range: 0 is monotone, 50 is normal and higher values increase intonation changes.",
        "Zakres zmian wysokości eSpeak: 0 oznacza głos monotonny, 50 wartość normalną, a wyższe wartości zwiększają intonację.");
    internal static string SelectVoiceForInflection => Pick(
        "Select an installed voice, not a language, to edit its modulation.",
        "Wybierz zainstalowany głos, a nie język, aby zmienić jego modulację.");
    internal static string VoiceWithInflection(string voice, int value) => Pick(
        $"{voice} — modulation {value}",
        $"{voice} — modulacja {value}");
    internal static string InflectionFor(string voice, int value) => Pick(
        $"Modulation for {voice}: {value}",
        $"Modulacja głosu {voice}: {value}");
    internal static string InflectionChanged(string voice, int value) => Pick(
        $"Set modulation for {voice} to {value}.",
        $"Ustawiono modulację głosu {voice} na {value}.");
    internal static string Sonic => Pick(
        "Enable the extended speed range (Sonic)",
        "Włącz rozszerzony zakres prędkości (Sonic)");
    internal static string SonicModeGroup => Pick("Speed boost mode", "Tryb dodatkowego przyspieszania");
    internal static string SonicModeNvda => Pick(
        "NVDA-style: triple the speed across the entire range",
        "Jak w NVDA: trzykrotna prędkość na całej skali");
    internal static string SonicModeSmooth => Pick(
        "Smooth: full range, automatic Sonic above 300 WPM (default for new settings)",
        "Płynny: pełny zakres, automatyczny Sonic powyżej 300 SNM (domyślnie dla nowych ustawień)");
    internal static string SonicModeLegacy => Pick(
        "Original: boost only at high positive SAPI rates",
        "Dotychczasowy: przyspieszanie tylko przy wysokich dodatnich prędkościach SAPI");
    internal static string Apply => Pick("A&pply", "&Zastosuj");
    internal static string Reload => Pick("&Reload", "&Odśwież");
    internal static string Close => Pick("&Close", "Za&mknij");
    internal static string Ready => Pick("Ready.", "Gotowe.");
    internal static string Added(int count) => Pick(
        $"Added {count} voice(s) to the pending installation.",
        $"Dodano głosy do oczekującej instalacji: {count}.");
    internal static string NothingSelected => Pick(
        "Select at least one voice check box first.",
        "Najpierw zaznacz co najmniej jedno pole wyboru głosu.");
    internal static string Removed(int count) => Pick(
        $"Removed {count} voice(s) from the pending list.",
        $"Usunięto głosy z listy oczekujących: {count}.");
    internal static string ConfirmRemove(int count) => Pick(
        $"Remove the selected voice(s): {count}?",
        $"Czy na pewno usunąć zaznaczone głosy? Liczba: {count}.");
    internal static string TooManyVoices(int maximum) => Pick(
        $"No more than {maximum} SAPI voices can be registered.",
        $"Można zarejestrować najwyżej {maximum} głosów SAPI.");
    internal static string Saved(int count) => Pick(
        $"Saved {count} SAPI voice(s). Restart applications using SAPI to refresh their voice lists.",
        $"Zapisano głosy SAPI: {count}. Uruchom ponownie programy korzystające z SAPI, aby odświeżyć ich listy głosów.");
    internal static string LoadFailed => Pick(
        "Vario could not read this eSpeak installation.",
        "Vario nie może odczytać tej instalacji eSpeak.");
    internal static string SaveFailed => Pick("The changes could not be saved.", "Nie udało się zapisać zmian.");
    internal static string ConfirmReload => Pick(
        "Discard changes that have not been applied?",
        "Odrzucić zmiany, które nie zostały zastosowane?");
    internal static string ConfirmClose => Pick(
        "Close without applying the pending changes?",
        "Zamknąć bez zastosowania oczekujących zmian?");
    internal static string VoicesDirectoryMissing(string path) => Pick(
        $"The installed voice directory was not found: {path}",
        $"Nie znaleziono katalogu zainstalowanych głosów: {path}");
    internal static string InvalidSonicMode => Pick(
        "The selected speed boost mode is not supported.",
        "Wybrany tryb dodatkowego przyspieszania nie jest obsługiwany.");
    internal static string InvalidInflection => Pick(
        "Voice modulation must be between 0 and 100.",
        "Modulacja głosu musi mieścić się w zakresie od 0 do 100.");
    internal static string VoiceRegistryUnavailable => Pick(
        "The SAPI voice registry could not be opened.",
        "Nie udało się otworzyć rejestru głosów SAPI.");
    internal static string CannotCreateVoiceToken(string name) => Pick(
        $"Cannot create SAPI token {name}.",
        $"Nie można utworzyć wpisu głosu SAPI: {name}.");
    internal static string CannotCreateVoiceAttributes(string name) => Pick(
        $"Cannot create attributes for {name}.",
        $"Nie można utworzyć atrybutów wpisu głosu: {name}.");
    internal static string PhoneConverterUnavailable => Pick(
        "The eSpeak phone converter could not be opened.",
        "Nie udało się otworzyć wpisu konwertera fonemów eSpeak.");
    internal static string PhoneConverterAttributesUnavailable => Pick(
        "The eSpeak phone converter attributes could not be opened.",
        "Nie udało się otworzyć atrybutów konwertera fonemów eSpeak.");
    internal static string SettingsMenuName => Pick("Settings", "Ustawienia");
    internal static string SettingsMenu => Pick("&Settings", "U&stawienia");
    internal static string ThemeMenu => Pick("&Theme", "&Motyw");
    internal static string LightTheme => Pick("&Light", "&Jasny");
    internal static string DarkTheme => Pick("&Dark", "&Ciemny");
    internal static string LanguageMenu => Pick("&Language", "&Język");
    internal static string SystemLanguage => Pick("&System default", "Zgodny z &systemem");
    internal static string EnglishLanguage => Pick("&English", "&Angielski");
    internal static string PolishLanguage => Pick("&Polish", "&Polski");
    internal static string ShowHints => Pick("Show &hints", "Pokazuj po&dpowiedzi");
    internal static string PreferencesSaved => Pick("Interface settings saved.", "Zapisano ustawienia interfejsu.");
    internal static string PreferencesSaveFailed(string path) => Pick(
        $"Interface settings could not be saved to {path}. The installation directory must be writable.",
        $"Nie udało się zapisać ustawień interfejsu do {path}. Katalog instalacji musi zezwalać na zapis.");
    internal static string InvalidPreferences => Pick(
        "The selected theme or interface language is not supported.",
        "Wybrany motyw lub język interfejsu nie jest obsługiwany.");
    internal static string StartFailed => Pick(
        "Vario could not start. Settings must be readable and writable beside Vario.exe.",
        "Nie udało się uruchomić Vario. Ustawienia obok Vario.exe muszą zezwalać na odczyt i zapis.");
    internal static string SettingsMigrated => Pick(
        "Existing speed settings were copied to vario.ini beside Vario.exe. The original registry values were preserved.",
        "Dotychczasowe ustawienia prędkości przeniesiono do vario.ini obok Vario.exe. Zachowano oryginalne wartości w rejestrze.");
    internal static string Warning => "Vario";
}
