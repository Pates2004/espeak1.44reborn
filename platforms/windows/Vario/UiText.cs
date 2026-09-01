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
    internal static string AddGroup => Pick("Add a SAPI voice", "Dodaj głos SAPI");
    internal static string Language => Pick("&Language or voice:", "&Język lub głos:");
    internal static string Variant => Pick("&Variant:", "&Wariant:");
    internal static string NoVariant => Pick("(no variant)", "(bez wariantu)");
    internal static string Add => Pick("&Add", "&Dodaj");
    internal static string InstalledGroup => Pick("Voices currently exposed to SAPI", "Głosy obecnie udostępniane przez SAPI");
    internal static string Remove => Pick("&Remove selected", "&Usuń zaznaczone");
    internal static string Sonic => Pick(
        "Use Sonic for additional speed boost at positive SAPI rates (up to 3x)",
        "Używaj Sonic do dodatkowego przyspieszania przy dodatniej prędkości SAPI (do 3×)");
    internal static string Apply => Pick("A&pply", "&Zastosuj");
    internal static string Reload => Pick("&Reload", "&Odśwież");
    internal static string Close => Pick("&Close", "Za&mknij");
    internal static string Ready => Pick("Ready.", "Gotowe.");
    internal static string Added(string voice) => Pick($"Added {voice} to the pending list.", $"Dodano {voice} do listy oczekujących.");
    internal static string Duplicate => Pick("That voice is already on the list.", "Ten głos jest już na liście.");
    internal static string NothingSelected => Pick("Select at least one voice first.", "Najpierw zaznacz co najmniej jeden głos.");
    internal static string Removed(int count) => Pick($"Removed {count} voice(s) from the pending list.", $"Usunięto głosy z listy oczekujących: {count}.");
    internal static string Saved(int count) => Pick(
        $"Saved {count} SAPI voice(s). Restart applications using SAPI to refresh their voice lists.",
        $"Zapisano głosy SAPI: {count}. Uruchom ponownie programy korzystające z SAPI, aby odświeżyć ich listy głosów.");
    internal static string LoadFailed => Pick("Vario could not read this eSpeak installation.", "Vario nie może odczytać tej instalacji eSpeak.");
    internal static string SaveFailed => Pick("The changes could not be saved.", "Nie udało się zapisać zmian.");
    internal static string ConfirmReload => Pick(
        "Discard changes that have not been applied?",
        "Odrzucić zmiany, które nie zostały zastosowane?");
    internal static string ConfirmClose => Pick(
        "Close without applying the pending changes?",
        "Zamknąć bez zastosowania oczekujących zmian?");
    internal static string Warning => Pick("Vario", "Vario");
}
