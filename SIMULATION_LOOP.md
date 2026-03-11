# LivingWorld Simulation Loop

This document describes the simulation loop in terms of event capture and chronicle presentation.

## Core Order

1. monthly systems update world state
2. systems emit canonical `WorldEvent` records when meaningful transitions occur
3. `World.AddEvent(...)` stores those events in `World.Events`
4. `HistoryJsonlWriter` persists structured history
5. `ChronicleEventFormatter` applies chronicle filtering
6. `ChronicleWatchRenderer` displays the surviving lines

## Monthly Systems

- ecology
- food gathering and farming
- trade redistribution
- food consumption
- migration

## Year-End Systems

- population
- advancement discovery
- settlement progression
- fragmentation
- stage progression
- annual agriculture events
- annual trade events
- annual hardship transition events
- focus handoff validation

## Focus And Lineage Continuity

Lineage continuity is handled at the year boundary.

- `LineagePolityFocusSelector` decides whether the chronicle should hand off to a successor polity
- `Simulation.EmitFocusTransitionEvent(...)` records the handoff as a canonical event
- `ChronicleFocus` treats those handoff events as the explicit bridge between the old focal polity and the new one
- after the handoff, ordinary chronicle filtering follows the successor polity

This keeps the watch experience attached to one focal line of history without broadening the chronicle to every same-lineage side event.

## Important Separation

Simulation and history recording happen before chronicle presentation.

That preserves:

- lower-severity events
- cooldown-suppressed events
- population change events that are structured-only by default
- causal metadata
- before/after state
- future alternate history views

## Chronicle Filtering

The chronicle layer currently applies:

- focus and focal-line matching
- event-type eligibility
- default `Major+` threshold
- per-event cooldown suppression
- cooldown bypass for genuine turning points

## Design Outcome

The watch view reads like history rather than telemetry, while the structured event stream remains the authoritative record.
