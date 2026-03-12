# LivingWorld Simulation Loop

This document describes the simulation loop in terms of event capture, propagation, and chronicle presentation.

## Core Order

1. monthly systems update world state
2. systems emit canonical `WorldEvent` records on meaningful transitions
3. `World.AddEvent(...)` records the event and routes it through `EventPropagationCoordinator`
4. subscribed handlers may enqueue deterministic follow-up events
5. all canonical events are stored in `World.Events`
6. `HistoryJsonlWriter` persists structured history
7. `ChronicleEventFormatter` applies chronicle filtering
8. `ChronicleWatchRenderer` displays the surviving lines

Watch mode keeps species in the fixed status panel, separates discoveries from learned advancements there, and leaves chronicle lines leaner by omitting repeated polity species suffixes.

## Monthly Systems

- region biomass refresh
- seasonal regional species population maintenance every third month
- seasonal ecosystem interactions every third month
- seasonal settlement hunting every third month
- seasonal mutation and divergence pass every third month
- seasonal extinction cleanup and biomass sync every third month
- food gathering and farming
- trade redistribution
- food consumption
- migration

At the start of each month, temporary propagation bonuses tick down.

## Year-End Systems

- population
- learned advancement discovery
- settlement progression
- fragmentation
- polity stage progression
- annual agriculture review
- annual trade review
- annual hardship transition events
- focus handoff validation

## Propagation Timing

Propagation is immediate. When a system calls `World.AddEvent(...)`:

- the event is recorded
- handlers can react in the same simulation step
- any follow-up events go through the same canonical path

This keeps causal chains deterministic and local to the state transition that created them.

## Anti-Spam Rules

- emit on state transitions, not every tick
- dedupe identical follow-ups inside a propagation step
- cap propagation depth
- cap total follow-up count
- keep chronicle cooldowns separate from storage

## Focus And Lineage Continuity

Lineage continuity is still handled at the year boundary.

- `LineagePolityFocusSelector` decides whether the chronicle should hand off to a successor polity
- `Simulation.EmitFocusTransitionEvent(...)` records the handoff as a canonical event
- after the handoff, ordinary chronicle filtering follows the successor polity

## Design Outcome

The simulation loop now supports:

- stronger cause-and-effect chains
- ecology, hunting, and polity history sharing one data model
- population-level evolutionary drift driven by shared ecological pressures
- structured history with causal ancestry
- concise chronicle output that still reads like history rather than telemetry
