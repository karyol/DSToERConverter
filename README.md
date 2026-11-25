# DSToERConverter

Very early stage experiment. **Not usable yet** — wait until there is something that actually works
before trying to run anything.

Goal: tooling that helps port assets — models, materials, params, animations, behavior — from
games in the Dark Souls series to Elden Ring.

Current scope is deliberately narrow: a single DS3 boss (Pontiff Sulyvahn, `c5140`) targeting
`c9010` in Elden Ring, until an end-to-end flow actually works.

## Layout

| Path | Role |
|---|---|
| `src/PortGui` | WinForms front-end that drives the stages |
| `src/FlverConv` | FLVER `0x20014` (DS3) to `0x2001A` (ER) conversion |
| `src/PortKit` | CLI: material cloning, params, anibnd, behbnd |
| `src/FlverDump` | FLVER structure dumps |
| `src/ApiProbe` | Elden Ring API surface probing |
| `docs/` | Technical findings |
| `data/` | Material map, logs, dumps |
| `PROGRESS.md` | Current state and open items |

## Status

Expect breakage and silent wrong output. There is no usage documentation yet.
