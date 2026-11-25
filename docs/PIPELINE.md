# Pipeline portu

## Kolejność etapów

1. Rozpakowanie źródła (WitchyBND) — DS3.
2. `FlverConv` — konwersja FLVER, audyt, gate weryfikacji.
3. `MatClone` — klonowanie materiałów.
4. Pakowanie chrbnd.
5. Klon `NpcParam` na `<ID>0000`.
6. Patch regulacji.

`FlverConv` **musi** być przed pakowaniem.

## Argumenty FlverConv

FlverConv <in.flver> <out.flver> <matmap.csv> [--model NNNN] [--audit plik]
[--no-hash] [--rename-dummies] [--attach-orphans|--attach-master]
[--rebuild-links] [--dump-mats]

W pipeline na stałe: `--dump-mats`, `--rename-dummies`, `--audit`. Tryb attach z `Settings`.

## Kody wyjścia

| Kod | Znaczenie | Reakcja pipeline |
|---|---|---|
| 0 | OK | kontynuuj |
| 1 | weryfikacja nie przeszła | przerwij, pokaż linie `FAIL` |
| 2 | złe argumenty | przerwij |
| 3 | liczba węzłów > `short.MaxValue` | przerwij |

Dodatkowo: każda linia wyjścia zaczynająca się od `  FAIL` blokuje etap niezależnie od kodu.

## Settings

- `FlverConvPath`
- `MatMapPath`
- `AttachMode` (`Smart` → `--attach-orphans`, `Master` → `--attach-master`)

## Wywołanie

```csharp
var psi = new ProcessStartInfo(settings.FlverConvPath)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    CreateNoWindow = true,
};
foreach (string a in new[]
{
    inFlver, outFlver, settings.MatMapPath,
    "--model", modelId.ToString(),
    "--audit", auditPath, "--dump-mats", "--rename-dummies",
    settings.AttachMode == AttachMode.Smart ? "--attach-orphans" : "--attach-master",
})
    psi.ArgumentList.Add(a);

using var p = Process.Start(psi)!;
string stdout = await p.StandardOutput.ReadToEndAsync();
string stderr = await p.StandardError.ReadToEndAsync();
await p.WaitForExitAsync();

Log(stdout);
if (stderr.Length > 0) LogWarn(stderr);
if (p.ExitCode != 0 || stdout.Contains("\n  FAIL"))
    throw new PipelineException($"FlverConv kod {p.ExitCode}; audit: {auditPath}");

## Matmap

Jedna linia na część. Klucz = nazwa pliku MTD (C[ARSN]_e.mtd) albo nazwa materiału. Kolumny: klucz ; ścieżka ER ; gxIdx. Tokeny: {CID} = cXXXX, {CIDU} = CXXXX, {ID} = XXXX.