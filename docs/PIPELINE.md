# Pipeline portu

## Kolejność etapów

1. Rozpakowanie źródła (WitchyBND) — DS3.
2. `FlverConv` — konwersja FLVER, audyt, gate weryfikacji.
3. `MatClone` — klonowanie materiałów.
4. `anibnd` — szkielet i animacje.
5. `behbnd` — behavior (opcjonalnie).
6. Pakowanie chrbnd.
7. `ParamPorter` — klon `NpcParam` na `<ID>0000`.
8. Dostawa do projektu.

`FlverConv` **musi** być przed pakowaniem.

## Zasoby — jedno miejsce

| Co | Gdzie | W repo |
|---|---|---|
| `matmap.csv` | `data/matmap.csv` | tak |
| audit + run log | `data/logs/` | tak |
| dumpy FLVER, `api.txt` | `data/dumps/` | tak |
| wejścia/wyjścia FLVER | `data/flver/` | nie |
| paczki, `regulation.ported.bin` | `data/out/` | nie |

Ścieżki do `matmap.csv` i `data/` są absolutne, liczone od katalogu, w którym leży `matmap.csv`
(`Settings.Resolve`). Katalog roboczy procesu nie ma znaczenia.

## Narzędzia

Wszystkie projekty budują się do `bin\` w korzeniu repo (`build.ps1`). `Settings.Resolve`
szuka `FlverConv.exe` i `PortKit.exe` najpierw w `AppContext.BaseDirectory`, potem w górę drzewa.
Nie ma ręcznego wpisywania ścieżek.

## Argumenty FlverConv

FlverConv <in.flver> <out.flver> <matmap.csv> [--model NNNN] [--audit plik]
[--no-hash] [--rename-dummies] [--attach-orphans|--attach-master]
[--rebuild-links] [--dump-mats]

Pipeline zawsze przekazuje: `--model`, `--audit`, `--dump-mats`, `--rename-dummies`, tryb attach z `Settings`.

## Argumenty matclone

matclone --matmap <csv> --er \<dir> --out \<dir> --model <id> --src <ds3.flver> --donor <id>

`--src` to **źródłowy** FLVER DS3, nie skonwertowany — klucze matmapy są nazwami MTD z DS3.

## Kody wyjścia

| Kod | Znaczenie | Reakcja pipeline |
|---|---|---|
| 0 | OK | kontynuuj |
| 1 | weryfikacja nie przeszła | przerwij, pokaż linie `FAIL` |
| 2 | złe argumenty | przerwij |
| 3 | liczba węzłów > `short.MaxValue` | przerwij |

Dodatkowo linia wyjścia zawierająca `FAIL` blokuje etap niezależnie od kodu.

## Settings

- `DataDir`, `MatMap` — wyliczane automatycznie
- `FlverConv`, `PortKit` — wyliczane automatycznie z `bin\`
- `AttachMode` (`Smart` → `--attach-orphans`, `Master` → `--attach-master`)
- `DonorId`, `IdFrom`, `IdTo`

`Settings.Validate()` sprawdza komplet przed startem; braki zatrzymują pipeline z listą powodów.