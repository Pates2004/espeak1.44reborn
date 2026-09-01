using System.Globalization;

namespace Vario;

internal static class UiText
{
    private static readonly bool Polish =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pl", StringComparison.OrdinalIgnoreCase);

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
        "Use Sonic for additional speed boost at positive SAPI rates (up to 3x)",
        "Używaj Sonic do dodatkowego przyspieszania przy dodatniej prędkości SAPI (do 3×)");
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
    internal static string Warning => "Vario";
}
