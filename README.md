# LivingWorld

LivingWorld is a command-line autonomous world simulation where ecosystems, species, and polities evolve over time.

The player-facing experience is now chronicle-first: the console is a live watch view of one focal polity's history, while the full simulation continues running for the whole world in the background.

## Core Principles

- Full-world simulation, focused player presentation
- Chronicle lines over yearly diagnostics
- Structured append-only history under every visible event
- Low-noise output that favors meaningful historical beats

## Default Player Experience

Default runs use watch mode.

Watch mode shows:

- a fixed polity status panel docked at the top of the console
- a live chronicle beneath it
- newest chronicle entries at the top, older entries below
- a chronicle viewport sized from the current console height
- concise notable events only

Default player-facing output no longer prints large yearly report blocks.

Examples of chronicle lines:

- `Year 18 - River Clan migrated to Red Valley.`
- `Year 41 - River Clan discovered Agriculture.`
- `Year 84 - River Clan became a Settled Society.`
- `Year 136 - Stone Clan split from River Clan.`

## Chronicle Pipeline

LivingWorld keeps simulation, storage, formatting, and playback separate:

`simulation systems -> World.AddEvent -> structured WorldEvent store -> ChronicleEventFormatter -> ChronicleWatchRenderer + HistoryJsonlWriter`

This preserves a clean path for future features such as:

- Civilization History views
- alternate chronicle perspectives over the same event stream
- post-run analysis tools

## Structured History

Important events are still stored as structured append-only JSONL records.

Default path:

- `logs/history-{timestamp}.jsonl`

Stored fields include, when available:

- `eventId`, `year`, `month`, `season`
- `type`, `severity`, `reason`
- polity, related polity, region, and settlement identifiers/names
- `before`, `after`, `metadata`
- concise narrative text

The console chronicle is only a presentation layer over this data.

## Runtime Options

Default mode is watch mode. Useful flags:

- `--fast` for no playback delay
- `--delay-ms <n>` for slower chronicle playback
- `--buffer-size <n>` to raise the retained chronicle history floor
- `--focus-polity <id>` to watch a specific polity
- `--debug` to restore developer-oriented yearly summaries and raw yearly event listings

Example:

```powershell
dotnet run --project LivingWorld -- --years 120 --delay-ms 250
```

## Simulation Overview

The simulation runs in monthly ticks with yearly system passes:

1. ecology and food updates
2. trade and redistribution checks
3. migration checks
4. year-end population, advancement, settlement, fragmentation, and stage passes
5. structured event emission and persistence
6. live chronicle playback in watch mode

## Long-Term Direction

LivingWorld is moving toward a playable history experience where the chronicle is the game, while the underlying event model remains strong enough to support richer history views later.
