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
The seed world entering this loop is now larger by default, but scale tuning remains centralized in `WorldGenerationSettings` rather than spread across systems.

## Monthly Systems

- region plant biomass refresh
- seasonal regional species population maintenance every third month
- seasonal ecosystem interactions, founder migration, and species exchange every third month
- ecosystem work is now sparse: it iterates existing regional populations and explicit frontier candidates rather than forcing every species into every region each season
- seasonal settlement hunting every third month
- hunting is resolved per settlement, using that settlement's actual region
- seasonal mutation, divergence, and speciation pass every third month using the same season's species exchange flags
- speciation eligibility now also depends on species age, global population, sustained readiness, and descendant stabilization
- seasonal extinction cleanup and biomass sync every third month
- plant gathering and farming
- farming is resolved per settlement, with settlements in the same region competing for the same regional arable capacity
- trade redistribution
- food consumption
- migration

The seasonal ecosystem pass now also carries more of the "alive world" burden in years `0-20`:

- initial consumer populations are seeded from ecological capacity and habitat fit
- producer-rich regions give herbivores stronger early recovery and expansion headroom
- predator starts stay narrower than herbivore coverage so prey foundations usually establish first
- neighboring fauna can now create small founder populations in suitable adjacent regions over time instead of depending entirely on generation-era ranges
- predator founder populations now use a short establishment window after migration, so they either grow into viable regional predators or fail under prey shortage and habitat mismatch

`Region.AnimalBiomass` is not consumed during the monthly food-gathering step.
Instead:

- plant gathering subtracts from `Region.PlantBiomass`
- hunting subtracts from actual `RegionSpeciesPopulation` prey counts
- seasonal ecosystem cleanup derives `Region.AnimalBiomass` back from surviving non-producer populations
- seasonal cleanup also resolves one-shot local/global extinction bookkeeping after the mutation/speciation pass

At the start of each month, temporary propagation bonuses tick down.
Ecosystem migration pacing itself is centralized in `EcosystemSettings`, including source thresholds, suitability gates, founder sizing, predator establishment/failure thresholds, prey-support requirements, and cooldowns.

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
- `Left` / `Right` now page chronicle or detail scroll state and jump faster through list selection
- `Enter` on `My Polity` is intentionally non-drilling so the focal polity never loses visibility by entering a more filtered detail screen
- unpausing resets the next step deadline instead of trying to catch up missed paused time
- chronicle pacing no longer sleeps inside event recording, so view changes stay immediate during live simulation

## Anti-Spam Rules

- emit on state transitions, not every tick
- settlement starvation and failed-aid logging therefore key off starvation-stage transitions rather than repeating each monthly starving result
- dedupe identical follow-ups inside a propagation step
- cap propagation depth
- cap total follow-up count
- keep chronicle cooldowns separate from storage
- chronicle cooldowns now use event-family profiles with actor scope plus semantic state keys, so changed-state turning points can bypass earlier than repeated same-state lines
- visible families without a custom semantic state key still fall back to normalized narrative-by-actor suppression, which prevents exact repeated chronicle lines from slipping through default presentation paths
- source systems now also suppress repeated biology status events through milestone guards and year-level cooldowns before those events ever hit storage

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
- a fuller opening world without globally-uniform species placement or overcrowded starting polity stacks
- a fuller early wildlife layer without reintroducing abstract animal harvesting
- anchored starting polities and more viable focal starts without scripting fake drama
- structured history with causal ancestry
- concise chronicle output that still reads like history rather than telemetry
- safer hot-path lookup behavior through cached lookup snapshots and clearer invariant failures
- lightweight perf instrumentation for species count, active regional populations, mutation/speciation checks, event categories, and year-end hot-path timings when `--perf` is enabled
## Phase 12 Integration Point

The monthly loop now runs settlement food redistribution after:
- food gathering
- farm production
- trade updates
- food consumption

and before:
- migration pressure resolution
- later annual starvation-driven population change

That ordering matters because settlement aid is intended to alter the final local hardship state that downstream systems react to, without introducing a separate market simulation.

## Phase 13/14 Integration Point

The monthly loop now inserts domestication and cultivation support between raw gathering and later hardship resolution.

- after gathering, polities accumulate familiarity with useful nearby animals and plants
- before downstream hardship consequences, managed herds and cultivated crops can add reliable local food
- annual review can emit `agriculture_stabilized_food_supply` when managed food covers a meaningful yearly share

Domestication is therefore not a random flavor event. It is a local state transition produced by repeated contact, species suitability, settlement continuity, and already learned capability.

## Phase 17 Integration Point

The monthly loop now runs material economy processing after:

- plant gathering
- domestication familiarity updates

and before:

- farm output
- managed-animal food
- trade
- food consumption
- settlement food aid redistribution

That ordering matters because tools, pottery, preserved food, and convoy relief are meant to change the same month's later food and hardship outcomes.
At the presentation layer, the material pass now also emits grouped settlement-level material-crisis beats after per-material pressure transitions are known, so the chronicle sees one historical turn instead of a burst of same-tick shortage lines.

Phase 18 now adds stronger economic response behavior inside this loop:

- after reserve targets and pressure are known, settlements calculate internal need, value, opportunity, and production-focus signals
- those signals steer extraction, production, convoy priority, and specialization drift without introducing currency or a buy/sell market screen
- major economy turns such as highly valued goods or trade-good identity still pass through the same structured event pipeline and chronicle filters

The next planned loop-facing expansions follow the canonical roadmap order:

- Phase 19 extends route logic into inter-polity exchange, imports, exports, and dependency pressure
- Phase 20 adds infrastructure and construction sinks that turn repeated surplus into durable settlement capability
- Phase 21 lets supply routes, scarcity, and cross-polity pressure generate diplomacy, raiding, and early conflict chains

Further chronicle dedupe tuning and later `Discoveries` / `Learned` presentation cleanup remain secondary follow-up work around the same loop, not replacements for those next core systems.
