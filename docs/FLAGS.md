# Flagi nodów FLVER

Ustalone empirycznie na dumpie c2130 (Morgott) i c5140 (Pontiff). Wartości:

| Wartość | Znaczenie |
|---|---|
| 1 | `Disabled` |
| 2 | `DummyOwner` |
| 4 | `Mesh` |
| 8 | `Bone` |
| 10 | `Bone` + `DummyOwner` |

## Reguły nadawania

1. Blok końcowy węzłów, który w źródle ma `Disabled`, dostaje `Disabled` — granica liczona od końca listy (`FindDisabledBlockStart`).
2. Węzeł wskazany przez `Mesh.NodeIndex` dostaje `Mesh`, **nigdy** `Bone`.
3. `DummyOwner` idzie na węzeł, który jest rodzicem dummy'ego (`Dummy.ParentBoneIndex`) albo celem przypięcia (`Dummy.AttachBoneIndex`). Wariant permisywny — brak flagi psuje dummy, nadmiarowa flaga nic nie robi.
4. `DummyOwner` nie trafia na liście `BoneOnlyHints` (palce, oczy, usta, rzęsy, brew, nozdrza, uszy, obojczyk, łokieć, skrzydło).
5. Zero węzłów z flagą `0` po konwersji.

## Siatki

Siatka z `parent = -1` ma transformację w przestrzeni modelu. Przypięcie jej do kości z własną transformacją daje podwójne przekształcenie. Siatki idą **wyłącznie na `Master` (0)**.

## Typy pól

`FLVER.Node` i `FLVER2.SkeletonSet.Bone` trzymają indeksy jako **`short`**. Każdy zapis z `int` przez `S(int)` z kontrolą zakresu.

`FLVER.Dummy`: `ReferenceID` (`short`), `NameHash` (`int`, CRC32 nazwy), `ParentBoneIndex` (`short`, domyślnie -1), `AttachBoneIndex` (`short`, domyślnie -1), `UsesAttachBoneIndex`, `UseUpwardVector`, `Unk30` (tylko Sekiro, zachować).

`Dummy` **nie ma** pola `Name`. Nazwę zastępuje `NameHash`; porównanie przez `Crc32(nazwa)`. Kontrola implementacji: `Crc32("Dmypoly_SFX_死亡全身") == 0x66917631`.

## SkeletonSet

`BaseSkeleton` i `AllSkeletons` mają po jednym `Bone` na węzeł, `Bone.NodeIndex == pozycja`, indeksy skopiowane z noda. `Bone` nie ma transformacji — te zostają na `Node`.

## Nazwy

DS3 potrafi trzymać nazwy jako UTF-16 w pliku czytanym bajtowo. Naprawa: składanie bajtów, wybór wariantu o mniejszej liczbie śmieci (`Score`), usuwanie NUL. `Master`, `Head`, `Neck`, `Jaw` nie są przemianowywane — `--rename-dummies` dotyczy tylko nazw zaczynających się od `Dummy`.

## Materiały

Pole `MTD` w DS3 zawiera **literalne** nawiasy, np. `N:\FDP\data\Material\mtd\character\C[ARSN].mtd`. Dopasowanie normalizuje nawiasy po obu stronach i dopuszcza wariant klasowy (`CA.mtd`).

Cel w ER: `N:\GR\data\Material\mtd\Chr\cXXXX\cXXXX_NNN.matxml`. `gxIdx`: 0 metal/broń, 1 skóra/twarz/szata, 2 efekty/kryształ.