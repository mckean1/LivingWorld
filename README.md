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
2. Regional food trade and redistribution checks
3. Migration checks
4. Year-end population, advancement, settlement, fragmentation, stage, and trade passes
5. Structured event emission
6. Focused yearly chronicle rendering
7. Append-only JSONL history persistence

---

## Focused Society Chronicle (Default Console Output)

Default yearly output follows one focal polity:

- Header: year, polity, region, population (+/- delta), food state, stage, knowledge summary
- `This Year`: up to 3 short focal events
- `Notable Changes`: optional before -> after lines
- `World Notes`: optional 0-2 rare outside-world notes

Food transitions in `Notable Changes` are year-boundary comparisons: prior-year resolved food state (persisted before annual reset) versus current year-end resolved state.
Migration lines are collapsed to one yearly summary (start vs end region), and food stress is collapsed to one worst-condition yearly summary line.
Knowledge breadth diffs are no longer shown in the player-facing chronicle.

The wider world still simulates fully; only presentation is filtered.

Chronicle console output also applies lightweight semantic color styling for readability:

- Year headers: cyan
- Section headers (`This Year`, `Notable Changes`): dark gray
- Polity/actor names only: yellow
- Regions/places: blue
- Knowledge/discoveries: magenta
- Positive outcomes: green
- Warnings/shortages: dark yellow
- Catastrophes: red
- Food status values: `Surplus` green, `Hunger` dark yellow, `Famine` red, `Stable` dark gray (label remains neutral)

Coloring is presentation-only and does not affect structured history output.

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

Trade is food-first and hybrid settlement/polity in the current refinement:

- conservative surplus/deficit heuristics
- internal-priority matching first (lineage/internal bloc), then external trade
- settlement-aware endpoints in trade links/events, with polity-level accounting
- local-network reachability using limited region-hop distance
- continuity preference for existing reliable links
- partial and full shortage relief accounting (not just all-or-nothing rescue)
- notable trade milestones in narrative output
- detailed structured trade records in JSONL history

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
