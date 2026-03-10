# LivingWorld Architecture

LivingWorld keeps simulation logic, event storage, formatting, and console playback separate.

## Core Structure

World contains:

- regions
- species
- polities
- time
- canonical world events

Major systems:

- food / ecology
- agriculture
- trade
- migration
- population
- advancement
- settlement
- fragmentation
- polity stage progression

## Event Architecture

Canonical flow:

`simulation systems -> World.AddEvent -> World.Events + EventRecorded -> output sinks`

Primary sinks:

1. `HistoryJsonlWriter` for append-only structured history
2. `ChronicleEventFormatter` for player-facing chronicle text
3. `ChronicleWatchRenderer` for live console playback

### Canonical Event Model

`WorldEvent` stores:

- `EventId`, `Year`, `Month`, `Season`
- `Type`, `Severity`, `Narrative`, `Details`, `Reason`
- polity / related polity / species / region / settlement identifiers and names
- optional `Before`, `After`, `Metadata`

`World.AddEvent(...)` is the source of truth. It timestamps, ids, stores, and publishes each event.

## Player-Facing Chronicle Path

Default player output uses watch mode rather than yearly report rendering.

Watch mode is built from two small presentation pieces:

- `ChronicleEventFormatter`: filters structured events into concise chronicle lines
- `ChronicleWatchRenderer`: maintains the reverse-chronological chronicle buffer and redraws only the docked status panel lines and chronicle viewport lines that changed

Important traits:

- the structured event list remains chronological and append-only
- the visible chronicle buffer is rendered newest-first
- the chronicle viewport height is derived from the current console height after reserving the docked panel
- only notable focal-polity events are shown in normal player mode
- yearly debug summaries remain available only through `OutputMode.Debug`

### Docked Status Panel

The watch panel is a stable HUD for the currently focused polity. It shows:

- polity name
- polity stage
- current region
- population
- settlement count
- food stores with resolved food state
- concise knowledge summary
- current year

## Focus and Continuity

`ChronicleFocus` stores the currently watched polity and lineage.

`LineagePolityFocusSelector` is responsible for:

1. initial focus selection
2. deterministic year-end handoff when the watched polity fragments, collapses, or disappears

Focus handoffs are themselves canonical `WorldEvent` records, so they appear in both:

- structured JSONL history
- the live chronicle

## Simulation Loop Notes

Monthly:

- ecology, gathering, farming, trade redistribution, consumption, migration
- systems emit structured events as notable outcomes happen
- watch mode immediately formats and plays qualifying chronicle entries

Year-end:

- population, advancement, settlement, fragmentation, stage, agriculture, and annual trade passes
- annual food stress events
- focus validation and handoff events
- persisted year-end food-state snapshot updates
- status panel refresh
- annual stat reset

## Extensibility Path

The current architecture intentionally preserves a path for:

- Civilization History views built from the same stored event stream
- multiple chronicle perspectives over the same world history
- richer post-run history tooling without rewriting simulation systems
