# Stan

## Kontekst

Port Pontiffa Sulyvahna `c5140` (DS3) → `c9010` (ER), pipeline C#/WinForms `PortGui`.
Donor: `c2130` (Morgott). Docelowy `NpcParam` `90100000`, `chr\c9010.chrbnd.dcx`.
Środowisko: WitchyBND, UXM, Smithbox, SoulsFormatsNEXT.

Repo: https://github.com/karyol/DSToERConverter (branch `master`).
Układ: `src/` projekty, `data/` zasoby i logi, `lib/` zależności, `bin/` wynik budowy (ignorowany).

## Zrobione

- `ParamSource`, `FreeIdFinder`, `ParamPorter`, `MatClone` naprawione.
- `FlverConv` przepisany: flagi wg struktury, matmap z wildcardami, naprawa nazw UTF-16,
  synteza `NameHash`, `--attach-orphans` / `--attach-master`, `--rebuild-links`, audyt UTF-8 bez BOM.
- Wszystkie indeksy przez `S(int)`; siatki przypinane wyłącznie do `Master`.
- Matmapa dopasowuje literalne nawiasy `C[ARSN]` i wariant klasowy.
- `SoulsFormats.dll` przeniesione do `lib/`, wszystkie csproj wskazują `..\..\lib\`.
- Wspólny katalog wyjściowy `bin\` + `build.ps1`; `Settings` sam znajduje narzędzia.
- Ścieżki zasobów absolutne, zakotwiczone w `data/`; `Settings.Validate()` blokuje start przy brakach.
- `matclone` dostaje `--src` (źródłowy FLVER) i `--donor`; `{CID}` podstawiane jako `cXXXX`.
- c9010: 126 węzłów, 18 `Disabled` (108–125), 6 siatek, 6 materiałów, 92 dummy, gx 1/2/1/2/0/1,
  zero osieroconych, hashe niezerowe.

## Otwarte

- `--attach-orphans` vs `--attach-master` — ocena wizualna w Smithboxie, wybór domyślnego.
- Nie odpalono end-to-end po poprawkach spięcia.
- `initialShadingGroup` dzieli matbin 0 z `05 - Default` — do potwierdzenia.

## Następne

1. `build.ps1`, potem `PortGui` na `c5140` → `c9010`.
2. Przegląd `data/logs/*.audit.txt` i `*run.log`.
3. Kontrola drzewa, materiałów, dummy i widoku 3D w Smithboxie.
4. Test end-to-end na `c1100`, potem `c5140` na wolnym ID.
5. Weryfikacja w grze.

## Fakty

- `c3010` nie istnieje w DS3; odpowiedniki `c1100`, `c5010`.
- Audit UTF-8 bez BOM, `data/logs/`.
- Dumpy wejściowe bywają puste lub UTF-16 — używać audytu, nie dumpów.