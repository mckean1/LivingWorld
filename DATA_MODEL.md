# DATA_MODEL.md

# LivingWorld Data Model

LivingWorld uses aggregated entities (not individual agents) so centuries of simulation can run efficiently.

---

## Core Entities

- `World`
- `Region`
- `Species`
- `Polity`
- `PolityStage`
- `WorldEvent`
- `ChronicleFocus` (presentation-level focus state)

---

## World

`World` is the root simulation container.

Key properties:

- `Regions`
- `Species`
- `Polities`
- `Events`
- `Time`

Event responsibilities:

- `AddEvent(...)` enriches and stores canonical event records
- `EventRecorded` publishes events to sinks (for example, JSONL writer)

---

## Polity

`Polity` represents a social group.

Selected fields:

- identity: `Id`, `Name`, `SpeciesId`, `RegionId`, `ParentPolityId`
- demographics: `Population`, `YearsSinceFounded`, `Stage`
- food stress and stores: monthly + annual aggregates
- migration state: `MigrationPressure`, `MovedThisYear`, `MovesThisYear`
- fragmentation state: `FragmentationPressure`, `FoodStressYears`, `SplitCooldownYears`
- settlement state: `SettlementStatus`, `SettlementCount`, `YearsSinceFirstSettlement`
- knowledge: `Advancements`, derived `Capabilities`

---

## WorldEvent (Canonical)

`WorldEvent` includes:

- time: `EventId`, `Year`, `Month`, `Season`
- classification: `Type`, `Severity`
- readable text: `Narrative`, `Details`, `Reason`
- entity refs: polity/species/region/settlement ids + names
- optional context maps: `Before`, `After`, `Metadata`

This model supports both narrative rendering and structured persistence.

---

## Presentation and Persistence Types

- `ChronicleFocus`: stores current focal polity id
- `IPolityFocusSelector`: selects initial focus
- `HistoryJsonlWriter`: append-only writer for structured event history

---

## Design Notes

- simulation behavior and scope remain full-world
- event capture is now source-of-truth and output-agnostic
- console output and history output are intentionally separate
