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
The same watch renderer applies conservative syntax coloring after formatting: structured status lines color only their value segments, while narrative chronicle lines color only boundary-safe semantic units.
The watch loop now also maintains a small UI state machine for top-level views, detail screens, pause state, and selection memory.

## Monthly Systems

- region biomass refresh
- seasonal regional species population maintenance every third month
- seasonal ecosystem interactions and species exchange every third month
- seasonal settlement hunting every third month
- hunting is resolved per settlement, using that settlement's actual region
- seasonal mutation and divergence pass every third month using the same season's species exchange flags
- seasonal extinction cleanup and biomass sync every third month
- food gathering and farming
- farming is resolved per settlement, with settlements in the same region competing for the same regional arable capacity
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

## Watch Input Timing

In watch mode:

- key input is polled independently of monthly simulation advancement
- the main watch loop checks input every iteration, even while time is running
- monthly simulation advancement occurs only when the next scheduled step time arrives
- changing views does not affect world logic
- `Space` pauses monthly advancement but leaves the watch UI responsive
- while paused, the renderer can still redraw list/detail screens and scroll chronicle history
- unpausing resets the next step deadline instead of trying to catch up missed paused time
- chronicle pacing no longer sleeps inside event recording, so view changes stay immediate during live simulation

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
- settlement-grounded food production instead of polity-region multiplication shortcuts
- population-level evolutionary drift driven by shared ecological pressures
- adaptation milestones grounded in ancestral mismatch, divergence, trait gains, and regional persistence
- player-facing adaptation lines only on new adaptation milestones, with scoped chronicle cooldown on top of source-side suppression
- structured history with causal ancestry
- concise chronicle output that still reads like history rather than telemetry
- safer hot-path lookup behavior through cached lookup snapshots and clearer invariant failures
