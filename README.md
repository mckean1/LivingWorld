# LivingWorld

LivingWorld is a command-line autonomous world simulation where ecosystems, species, and polities evolve over time.

The player-facing experience is chronicle-first: the console watches one focal line of history, while the full simulation continues running for the whole world underneath.

## Core Principles

- Full-world simulation, focused player presentation
- Chronicle lines over yearly diagnostics
- Structured append-only history under every visible event
- Low-noise output that favors meaningful historical turning points

## Default Player Experience

Default runs use watch mode.

Watch mode shows:

- a fixed polity status panel docked at the top of the console
- a live chronicle beneath it
- newest chronicle entries at the top, older entries below
- a chronicle viewport sized from the current console height
- only `Major` and `Legendary` events by default

The watch experience follows one focal polity line across fragmentation, collapse, and lineage handoff events. Routine population change messages are not shown by default; they remain available in structured history and debug output.

Examples of chronicle lines:

- `Year 18 - River Clan migrated to Red Valley.`
- `Year 41 - River Clan began farming.`
- `Year 84 - River Clan became a Settled Society.`
- `Year 136 - Stone Clan split from River Clan in High Ridge.`
- `Year 179 - River Clan recovered from famine.`

## Chronicle Pipeline

LivingWorld keeps simulation, storage, formatting, and playback separate:

`simulation systems -> World.AddEvent -> structured WorldEvent store -> ChronicleEventFormatter -> ChronicleWatchRenderer + HistoryJsonlWriter`

The chronicle is a filtered presentation layer over the richer event stream. Lower-severity events and chronicle-suppressed events remain available in structured history and debug views even when they do not appear in the live chronicle.

Per-event chronicle cooldowns suppress repeated narrative lines for the same actor scope, while bypass rules still allow real turning points such as severity escalation, famine entry and recovery, stage changes, settlement founding, fragmentation, collapse, and lineage handoffs to appear immediately.

## Structured History

Important events are stored as structured append-only JSONL records.

Default path:

- `logs/history-{timestamp}.jsonl`

Stored fields include, when available:

- `eventId`, `year`, `month`, `season`
- `type`, `severity`, `reason`
- polity, related polity, region, and settlement identifiers and names
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
6. chronicle filtering by severity, focal-line matching, and cooldown in watch mode

## Long-Term Direction

LivingWorld is moving toward a playable history experience where the chronicle is the game, while the underlying event model remains strong enough to support richer history views later.
