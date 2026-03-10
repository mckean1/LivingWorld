# LivingWorld

LivingWorld is a command-line autonomous world simulation where ecosystems, species, polities, and early civilizations emerge over time.

The simulation runs the full world in the background. Default output is now a focused, readable yearly chronicle centered on one focal polity, while a structured append-only history file captures major events across all polities.

---

## Core Design Principles

- Emergent simulation over scripted storylines
- Full-world simulation scope at all times
- Readable player-facing chronicle output
- Structured developer-facing event history

---

## Simulation Overview

The simulation runs in monthly ticks with yearly aggregation:

1. Ecology and food updates
2. Migration checks
3. Year-end population, advancement, settlement, fragmentation, and stage passes
4. Structured event emission
5. Focused yearly chronicle rendering
6. Append-only JSONL history persistence

---

## Focused Society Chronicle (Default Console Output)

Default yearly output follows one focal polity:

- Header: year, polity, region, population (+/- delta), food state, stage, knowledge summary
- `This Year`: 1-5 short focal events
- `Notable Changes`: optional before -> after lines
- `World Notes`: optional 0-2 rare outside-world notes

The wider world still simulates fully; only presentation is filtered.

---

## Structured Event History (Developer Output)

Important events are written as JSONL records, append-only during the run.

Default path:

- `logs/history-{timestamp}.jsonl`

Stored fields include, as available:

- `eventId`, `year`, `month`, `season`
- `type`, `severity`, `reason`
- polity/species/region/settlement ids and names
- `before`, `after`, `metadata`
- concise narrative text

This history is separate from console formatting and is intended for debugging, analysis, and balancing.

---

## Event Architecture

Simulation systems emit canonical structured events:

`simulation systems -> world event model -> chronicle renderer + JSONL writer`

Severity levels:

- `Debug`
- `Normal`
- `Notable`
- `Critical`

Default chronicle prioritizes focal `Notable`/`Critical` events.

---

## Focal Polity Selection

A lightweight focus abstraction tracks the polity shown in the chronicle.

Current default selector:

- first starting polity (lowest initial polity id)

This is designed to evolve later into explicit player lineage tracking.

---

## Runtime Options

`SimulationOptions` supports:

- `OutputMode` (`Narrative` or `Debug`)
- `FocusedChronicleEnabled`
- `FocusedPolityId` override
- `WriteStructuredHistory`
- `HistoryFilePath`

---

## Long-Term Goal

LivingWorld aims to generate believable, emergent history where civilizations rise and fall from interacting ecological and societal systems, while keeping both player output and developer diagnostics readable.
