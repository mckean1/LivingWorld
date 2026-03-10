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
- Trade
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

- optional major-event milestone banners (rare, headline style)
- header snapshot
- `This Year` focal events (up to 3, collapsed summaries)
- optional notable before/after changes
- optional rare world notes (0-2)

Chronicle focus is tracked by `ChronicleFocus` and chosen/validated by `IPolityFocusSelector` (default: `LineagePolityFocusSelector`).

### Player Lineage Focus

`ChronicleFocus` stores:

- current focal polity id
- current focal lineage id

`LineagePolityFocusSelector` handles two responsibilities:

1. initial focus selection at simulation start
2. year-end validation and deterministic handoff when continuity breaks

Year-end handoff rules:

- if the focused polity survives and did not fragment, keep following it
- if it fragmented this year, choose the strongest direct child in the same lineage
- if it collapsed or vanished, choose the best surviving polity in the same lineage
- if the lineage is extinct, fall back to the closest strong surviving polity outside the lineage

Successor choice is deterministic and weighted from existing simulation state:

- population
- settlement count
- stage
- direct descent distance
- regional continuity
- species continuity for fallback cases

When focus changes, `Simulation` emits a canonical focus-transition `WorldEvent` before chronicle rendering. That same event feeds both:

- the concise narrative chronicle
- the append-only JSONL history

Food-state notable changes use a persisted prior-year snapshot on each polity (`LastResolvedFoodState` + capture year), not recomputation after annual counters reset.
Yearly chronicle rendering now collapses:

- multiple migration events into one summary line
- food stress into one worst-condition yearly line
- population micro-events into a single population before/after notable change

Knowledge breadth debug diffs are omitted from player-facing output.

Trade milestones use the same event pipeline, while ordinary monthly transfers stay mostly in structured history.

Console coloring is handled by a centralized presentation helper (`ChronicleColorWriter`) that applies semantic colors to chronicle text segments without changing simulation/event data.
It supports segmented line styling (for example, `Year N` and polity name rendered separately, dim section headers, and value-only food status coloring).
Chronicle color semantics reserve yellow for actor names only; warning/shortage phrases use dark yellow so actor identity and risk state remain visually distinct.
Major-event banner formatting (separators, headline line, placement) is also presentation-only and does not alter canonical event records.

---

## Structured History Path

`HistoryJsonlWriter` subscribes to `World.EventRecorded` and writes append-only JSONL records.

Default output:

- `logs/history-{timestamp}.jsonl`

This path is intentionally independent of console formatting.

---

## Simulation Loop Notes

Monthly:

- ecology, gathering, farming, trade redistribution, consumption, migration

Year-end:

- population, advancement, settlement, fragmentation, stage, annual agriculture events, annual food stress events, annual trade dependency/link maintenance
- persist each active polity's resolved year-end food state snapshot
- validate lineage focus and emit a handoff event if the chronicle switches to a successor
- focused chronicle rendering
- annual stat reset

Debug mode still prints broad world summaries and raw yearly events.

Trade system refinement notes:

- matching order is internal-priority first, then external
- link continuity (age + recency) influences partner reuse
- reachability uses constrained region-hop networks (local clusters, not global shipping)
- transfer accounting remains polity-level for stability, but links/events include settlement-aware endpoint fields for future settlement-first expansion
- relief tracks partial and full shortage mitigation
