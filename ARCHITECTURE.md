# ARCHITECTURE.md

# LivingWorld Architecture

LivingWorld is a systems-driven simulation. Core simulation logic remains full-world and unchanged in scope; logging and presentation are now split into dedicated event capture and output paths.

---

## Core Structure

World contains:

- Regions
- Species
- Polities
- Time
- Structured world events

Major systems:

- Food / ecology
- Agriculture
- Migration
- Population
- Advancement
- Settlement
- Fragmentation
- Polity stage progression

---

## Event and Logging Architecture

Canonical flow:

`simulation systems -> canonical WorldEvent -> render/persist outputs`

Outputs:

1. Focused chronicle renderer (player-facing)
2. JSONL history writer (developer-facing)

### Canonical Event Model

`WorldEvent` now stores:

- `EventId`, `Year`, `Month`, `Season`
- `Type`, `Severity`, `Narrative`, `Details`, `Reason`
- polity/species/region/settlement ids and names
- optional `Before`, `After`, `Metadata` dictionaries

### Severity

- `Debug`
- `Normal`
- `Notable`
- `Critical`

### Event Capture

`World.AddEvent(...)` is the source of truth:

- enriches event time/id
- appends to `World.Events`
- publishes `EventRecorded` for sinks

---

## Focused Chronicle Path

`NarrativeRenderer` produces a yearly focused chronicle for one focal polity:

- header snapshot
- `This Year` focal events (up to 3, collapsed summaries)
- optional notable before/after changes
- optional rare world notes (0-2)

Chronicle focus is tracked by `ChronicleFocus` and chosen by `IPolityFocusSelector` (default: first starting polity).

Food-state notable changes use a persisted prior-year snapshot on each polity (`LastResolvedFoodState` + capture year), not recomputation after annual counters reset.
Yearly chronicle rendering now collapses:

- multiple migration events into one summary line
- food stress into one worst-condition yearly line
- population micro-events into a single population before/after notable change

Knowledge breadth debug diffs are omitted from player-facing output.

---

## Structured History Path

`HistoryJsonlWriter` subscribes to `World.EventRecorded` and writes append-only JSONL records.

Default output:

- `logs/history-{timestamp}.jsonl`

This path is intentionally independent of console formatting.

---

## Simulation Loop Notes

Monthly:

- ecology, gathering, farming, consumption, migration

Year-end:

- population, advancement, settlement, fragmentation, stage, annual agriculture events, annual food stress events
- persist each active polity's resolved year-end food state snapshot
- focused chronicle rendering
- annual stat reset

Debug mode still prints broad world summaries and raw yearly events.
