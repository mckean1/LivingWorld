# LivingWorld Data Model

LivingWorld uses aggregated entities rather than individual agents so long historical runs remain practical.

## Core Entities

- `World`
- `Region`
- `Species`
- `Polity`
- `PolityStage`
- `WorldEvent`
- `ChronicleFocus`

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
- `EventRecorded` publishes each stored event to sinks

## Polity

`Polity` represents a social group.

Selected fields:

- identity: `Id`, `Name`, `SpeciesId`, `RegionId`, `LineageId`, `ParentPolityId`
- demographics: `Population`, `YearsSinceFounded`, `Stage`
- food: `FoodStores` and annual food aggregates
- migration: `MigrationPressure`, `MovedThisYear`, `MovesThisYear`
- fragmentation: `FragmentationPressure`, `FoodStressYears`, `SplitCooldownYears`
- settlements: `SettlementStatus`, `SettlementCount`, `YearsSinceFirstSettlement`
- knowledge: `Advancements`, derived `Capabilities`
- year-boundary food snapshot: `LastResolvedFoodState`, `LastResolvedFoodStateYear`

These fields support both simulation logic and the watch-mode status panel.

## WorldEvent

`WorldEvent` is the canonical historical record.

It includes:

- time: `EventId`, `Year`, `Month`, `Season`
- classification: `Type`, `Severity`
- narrative fields: `Narrative`, `Details`, `Reason`
- entity references: polity, related polity, species, region, settlement ids and names
- context maps: `Before`, `After`, `Metadata`

Severity now uses:

- `Debug`
- `Minor`
- `Notable`
- `Major`
- `Legendary`

The console chronicle is derived from this model. It is not a separate source of truth.

## Presentation and Persistence Types

- `ChronicleFocus`: currently watched polity and lineage
- `ChronicleFocusSelection`: initial focus result
- `ChronicleFocusTransition`: year-end focus handoff result
- `IPolityFocusSelector`: focus selection / handoff abstraction
- `LineagePolityFocusSelector`: default lineage-aware selector
- `ChroniclePresentationPolicy`: centralized chronicle severity threshold, event eligibility, cooldowns, and bypass rules
- `ChronicleEventFormatter`: player-facing line formatter backed by the presentation policy
- `ChronicleWatchRenderer`: fixed-panel live chronicle playback
- `HistoryJsonlWriter`: append-only structured history sink

The visible chronicle buffer in watch mode is presentation state only. Chronological history remains in `World.Events` and the JSONL log.

Annual hardship transition tracking is held in simulation/runtime state rather than replacing canonical event history. It exists to decide when to emit new hardship events for a polity as conditions begin, worsen, persist, or recover.

## Design Notes

- simulation behavior remains full-world
- event capture is source-of-truth and output-agnostic
- storage order and display order are intentionally different
- lower-severity and cooldown-suppressed events remain structured even when hidden from the live chronicle
- this separation preserves a future path for Civilization History and multiple chronicle perspectives
