## Materiały projektu — układ kategorii od 2026-10-16

- Projekt źródłowy: `D:\espeak-1.44.05-source\espeak1.44reborn`.
- Nazwa rzeczywistego repozytorium wyznacza osobny folder projektu: `espeak1.44reborn`.
- Audyty i dokumentacja wewnętrzna: `D:\work\Forme\espeak1.44reborn`.
- Przydatne materiały robocze: `D:\work\Workspace\espeak1.44reborn`.
- Poprzednie wersje i buildy: `D:\work\Snapshots\espeak1.44reborn`.
- Potwierdzone odpady: `D:\work\Trash\espeak1.44reborn`; niczego automatycznie nie usuwaj.
- Aktualne finalne instalatory pozostają lokalnie w `D:\espeak-1.44.05-source\espeak1.44reborn\installfiles`; folder jest ignorowany przez Git.
- Nie używaj starego układu `D:\work\espeak-1.44.05-source\...\<kategoria>`. Skrypty wyznaczają nowe ścieżki przez `project_materials.ps1`.
- Historyczne dowody zachowują dawne ścieżki jako pochodzenie. Bieżąca mapa i manifesty migracji: `D:\work\Forme\espeak\CATEGORY-LAYOUT-2026-10-16.md` oraz `D:\work\Workspace\espeak\layout-migration-2026-10-16`.
- Dysk D: istnieje. C:\work wolno użyć tylko przy nieistniejącym D:. Źródła, właściwe testy, konfiguracja, dokumentacja użytkownika i dane programu nie są materiałami asystenta.
- Zachowaj wyjątki AVD opisane w nadrzędnym `D:\espeak-1.44.05-source\AGENTS.md` oraz klucze w `signing`.

## Nawigacja i kontrole

- Stosuj [globalny indeks i właściwe moduły](<C:/Users/Pates/.codex/AGENTS.md>).
- Przed zmianą odczytaj istniejący audyt w [centralnym Forme](<D:/work/Forme/espeak1.44reborn>). Bieżący zakres i pauzy mają pierwszeństwo przed dawnymi planami.
- Przed pracą: [README.md](<D:/espeak-1.44.05-source/espeak1.44reborn/README.md>), [ReadMe](<D:/espeak-1.44.05-source/espeak1.44reborn/ReadMe>).
- Przed budowaniem lub zmianą ścieżek odczytaj właściwy skrypt: [project_materials.ps1](<D:/espeak-1.44.05-source/espeak1.44reborn/project_materials.ps1>). Nie uruchamiaj buildu tylko z powodu porządkowania instrukcji.
- Odnośniki nie wczytują treści automatycznie. Nie rozbijaj krótkiej, spójnej mapy projektu na dodatkowe pliki.

## Dwie wersje polskiego słownika

- Przy każdej zmianie polskiego słownika i wydaniu przygotuj obie wersje: domyślną z poprawioną wymową liczb i akcentowaniem starannym oraz alternatywną ze starszą wymową liczb i uzgodnionym akcentowaniem potocznym w śledzonym przez Git katalogu `mydict` tego repozytorium.
- `mydict` zawiera pełne źródła alternatywnego polskiego słownika w `dictsource`, skompilowany `pl.dict` i instrukcję PL/EN podmiany pod rzeczywistą nazwą `pl_dict`. Obie wersje muszą zawierać wspólne poprawki rzeczywistych błędów (np. Monika) i korzystać ze zgodnych tabel fonemów. Różnice obejmują wyłącznie udokumentowane wpisy liczbowe i uzgodnione wyjątki akcentowe; nie utożsamiaj każdego wariantu potocznego z błędem. Dla „kolażu” zachowaj uznane koLAŻ w wersji domyślnej, a wybrane przez użytkownika KOlaż w alternatywnej, jeśli potwierdzają to źródła.
- Do instalatora trafia wersja domyślna. Nie twórz osobnego instalatora ani silnika dla starszej wymowy. Przed wydaniem sprawdź odtwarzalność obu plików ze źródeł i wymowę liczb oraz zwykłych słów. Nie zmieniaj pozostałych liczb bez potwierdzonej podstawy.
