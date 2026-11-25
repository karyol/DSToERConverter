**`PROGRESS.md`**

```markdown
# Stan

## Kontekst

Port Pontiffa Sulyvahna `c5140` (DS3) → `c9010` (ER), pipeline C#/WinForms `PortGui`.
Donor: `c2130` (Morgott). Docelowy `NpcParam` `90100000`, `chr\c9010.chrbnd.dcx`.
Środowisko: WitchyBND, UXM, Smithbox, SoulsFormatsNEXT.

## Zrobione

- `ParamSource`, `FreeIdFinder`, `MatClone` naprawione.
- `FlverConv` przepisany: flagi wg struktury, matmap z wildcardami, naprawa nazw UTF-16, synteza `NameHash`, `--attach-orphans` / `--attach-master`, `--rebuild-links`, audyt UTF-8 bez BOM.
- Wszystkie indeksy przez `S(int)`.
- Matmapa dopasowuje literalne nawiasy `C[ARSN]` i wariant klasowy.
- c9010: 126 węzłów, 18 `Disabled` (108–125), 6 siatek, 6 materiałów, 92 dummy, gx 1/2/1/2/0/1, zero osieroconych.

## Otwarte

- `--attach-orphans` vs `--attach-master` — ocena wizualna w Smithboxie, wybór domyślnego.
- Nie odpalono ponownie po poprawce kolejności attach (siatki → `Master`).
- `initialShadingGroup` dzieli matbin 0 z materiałem `05 - Default` — do potwierdzenia, że tak ma zostać.

## Następne

1. Rerun FlverConv z `--attach-orphans`, odesłanie `logs/*.audit.txt`.
2. Kontrola drzewa, materiałów i dummy w Smithboxie + widok 3D.
3. Wpięcie `FlverConv` w `Pipeline.cs` i `Settings` wg `docs/PIPELINE.md`.
4. Test end-to-end na `c1100`, potem `c5140` na wolnym ID.
5. Weryfikacja w grze.

## Fakty

- `c3010` nie istnieje w DS3; odpowiedniki `c1100`, `c5010`.
- Audit: `--audit`, UTF-8 bez BOM.
- Dumpy wejściowe bywają puste lub UTF-16 — używać audytu.