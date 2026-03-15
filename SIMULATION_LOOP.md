# LivingWorld Simulation Loop

This document describes the simulation loop in terms of event capture, propagation, and chronicle presentation.

The runtime flow now lives on the canonical `PrehistoryRuntimePhase` ladder (`BootstrapWorldFrame`, `PrehistoryRunning`, `ReadinessCheckpoint`, `FocalSelection`, `ActivePlay`, `GenerationFailure`). Monthly systems always run from the same pipeline, with the orchestration layer pausing/evaluating in `ReadinessCheckpoint` and freezing while `FocalSelection` holds time before handing off to `ActivePlay`. Legacy `WorldStartupStage` labels now only provide generator diagnostics and UI-friendly subphase labels; the runtime truth for gating ticks has moved entirely into `PrehistoryRuntimePhase`, while startup presentation detail comes from `PrehistoryRuntimeDetailView` rather than the old pass ladder.

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
Before active play, startup now runs an explicit primitive-life-first bootstrap.
The agreed 4-pass startup plan is:

1. biological world foundation
2. evolution and divergence
3. sentience and social formation
4. polity start and player entry

Pass 1 through Pass 4 now live inside a clearer runtime scaffold: the generator starts in a `BootstrapWorldFrame`, enters a shared `PrehistoryRunning` pipeline that still describes the internal biological/evolutionary/social subphases in user-facing text, pauses in `ReadinessCheckpoint` whenever evaluator logic examines post-tick facts, and then resolves into `FocalSelection`, `ActivePlay`, or the new `GenerationFailure` state through explicit `PrehistoryCheckpointOutcome` results (ContinuePrehistory / EnterFocalSelection / ForceEnterFocalSelection / GenerationFailure). Canonical events retain origin metadata, the world keeps a live-chronicle boundary marker, and the renderer cleans out the startup buffer before the watch loop begins so prehistory stays structured instead of leaking into the live chronicle.
The startup path now records organic-vs-fallback diagnostics, candidate rejection reasons, and observer/candidate pool snapshots inside `World.Prehistory.Evaluation` instead of mutating the base geography or polity truth. `PrehistoryEvaluationSnapshot.LegacyCompatibility` holds transitional readiness and diagnostic artifacts, while `PrehistoryEvaluationSnapshot.CandidateSelection` holds the surfaced pool. The PR-2 observer layer stores recent monthly `PeopleMonthlySnapshot` truth in `World.PrehistoryObserver` and projects `PeopleHistoryWindowSnapshot`, `RegionEvaluationSnapshot`, and `NeighborContextSnapshot` artifacts as factual evidence only. The new `ActivePlayHandoffState` records the player's chosen focal polity plus world-age and candidate summary metadata, while `PrehistoryFocalSelectionPresentationState` keeps presentation hints for future UI passes.
PR-3 now layers the real stop-condition system on top of that observer substrate. `WorldReadinessReport` owns the world-level readiness verdict: it evaluates Biological, Social Emergence, World Structure, Candidate, Variety, and Agency readiness with `Pass` / `Warning` / `Blocker` semantics, enforces hard current-month vetoes and hard candidate truth floors, and resolves the checkpoint honestly to `ContinuePrehistory`, `EnterFocalSelection`, `ForceEnterFocalSelection`, or `GenerationFailure`. The runtime still consumes only those checkpoint outcomes; the report exists to summarize why the checkpoint resolved the way it did without polluting observer snapshots or starting the live chronicle early.
Startup remains robust: became storytelling about real focal candidates, not invented entires, thanks to explicit checkpoint outcomes. The same monthly pipeline now runs for prehistory and active play, the evaluation checkpoints only read from simulation facts, and `StartupProgressRenderer` owns the console until `PrehistoryRuntimePhase.FocalSelection` hands things to `ChronicleWatchRenderer`.

`FocalSelection` now explicitly freezes advancement while candidates are reviewed; `ChronicleWatchRenderer` keeps time paused, displays the candidate pool and an explicit `Handoff summary`, and resumes the shared pipeline only after the chosen polity is recorded in `ActivePlayHandoffState` and `LiveChronicleStartYear` marks the chronicle boundary. A `GenerationFailure` checkpoint leaves the world paused with a clear failure summary, does not start the live chronicle boundary, and remains representable as a world state rather than only as a thrown exception.

## Monthly Systems

- region plant biomass refresh
- seasonal regional species population maintenance every third month
- seasonal ecosystem interactions, founder migration, and species exchange every third month
- ecosystem work is now sparse: it iterates existing regional populations and explicit frontier candidates rather than forcing every species into every region each season
- seasonal settlement hunting every third month once a later startup pass has actually created settlements
- hunting is resolved per settlement, using that settlement's actual region
- seasonal mutation, divergence, and speciation pass every third month using the same season's species exchange flags once startup has advanced past Pass 1
- speciation eligibility now also depends on species age, global population, sustained readiness, and descendant stabilization
- founder isolation, ecology-distance divergence, and local-extinction replacement openings now also feed that biological bootstrap so weak seed families can build deeper history without lowering readiness
- seasonal extinction cleanup and biomass sync every third month
- plant gathering and farming
- farming is resolved per settlement, with settlements in the same region competing for the same regional arable capacity
- trade redistribution
- food consumption
- migration

The seasonal ecosystem pass now also carries more of the "alive world" burden in years `0-20`:

- initial consumer populations are seeded from ecological capacity and habitat fit
- region ecology profiles cache derived productivity, habitability, migration ease, and volatility so seeding and simulation use the same tuned environmental frame
- producer-rich regions give herbivores stronger early recovery and expansion headroom
- predator starts stay narrower than herbivore coverage so prey foundations usually establish first
- neighboring fauna can now create small founder populations in suitable adjacent regions over time instead of depending entirely on generation-era ranges
- predator founder populations now use a short establishment window after migration, so they either grow into viable regional predators or fail under prey shortage and habitat mismatch
- each regional population now exposes food/support pressure, reproduction pressure, migration pressure, stress, and trend state for debugging and for later startup passes

`Region.AnimalBiomass` is not consumed during the monthly food-gathering step.
Instead:

- plant gathering subtracts from `Region.PlantBiomass`
- hunting subtracts from actual `RegionSpeciesPopulation` prey counts
- seasonal ecosystem cleanup derives `Region.AnimalBiomass` back from surviving non-producer populations
- seasonal cleanup also resolves one-shot local/global extinction bookkeeping after the mutation/speciation pass

At the start of each month, temporary propagation bonuses tick down.
Ecosystem migration pacing itself is centralized in `EcosystemSettings`, including source thresholds, suitability gates, founder sizing, predator establishment/failure thresholds, prey-support requirements, and cooldowns.

If the world is still in `WorldStartupStage.PrimitiveEcologyFoundation`, the loop intentionally stops after ecology work.
If the world has advanced to `WorldStartupStage.EvolutionaryExpansion`, the loop runs ecology plus mutation/divergence/speciation/sentience-capability work, then still stops before polity-facing systems.
Once bootstrap reaches `WorldStartupStage.FocalSelection`, time is intentionally frozen while the player reviews real simulated candidates.
Only after selection does `WorldStartupStage.ActivePlay` begin and the existing polity-facing loop take over from a world that already has social continuity plus a chosen focal line.
During that bootstrap, social actors are no longer static placeholders: groups, societies, settlements, and early polities now change population under food support, storage, cohesion, carrying pressure, migration pressure, and fragmentation pressure before the player-entry gate evaluates them.
Polity settlement expansion during bootstrap now depends on current subsistence mode plus network age, which keeps healthy starts from flattening into one oversized settlement profile before candidate selection.

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
- `FocalSelection` is a dedicated frozen state with its own candidate-list navigation
- while paused, the renderer can still redraw list/detail screens and scroll chronicle history
- `Left` / `Right` now page chronicle or detail scroll state and jump faster through list selection
- `Enter` on `My Polity` is intentionally non-drilling so the focal polity never loses visibility by entering a more filtered detail screen
- unpausing resets the next step deadline instead of trying to catch up missed paused time
- chronicle pacing no longer sleeps inside event recording, so view changes stay immediate during live simulation

## Anti-Spam Rules

- emit on state transitions, not every tick
- do not narrate bootstrap-created baseline state as live history
- do not narrate prehistory candidate-building or readiness-stop context as live chronicle history
- do not treat fallback-only or emergency-only startup candidates as equivalent to organic starts; startup diagnostics and gating must keep those paths explicit
- do not mix startup progress lines with the live chronicle buffer; startup status is a separate render path and must be cleared on handoff
- do not allow status-panel or focal-selection summary fragments into the chronicle buffer; chronicle entries must remain complete historical lines
- settlement starvation and failed-aid logging therefore key off starvation-stage transitions rather than repeating each monthly starving result
- do not let responsive internal economy identity promote directly into chronicle reputation; visible `known for` and trade-good turns now require minimum settlement age, sustained monthly confirmation, and stronger thresholds than the hidden economy layer
- dedupe identical follow-ups inside a propagation step
- cap propagation depth
- cap total follow-up count
- keep chronicle cooldowns separate from storage
- chronicle cooldowns now use event-family profiles with actor scope plus semantic state keys, so changed-state turning points can bypass earlier than repeated same-state lines
- visible families without a custom semantic state key still fall back to normalized narrative-by-actor suppression, which prevents exact repeated chronicle lines from slipping through default presentation paths
- source systems now also suppress repeated biology status events through milestone guards and year-level cooldowns before those events ever hit storage
- explicit bootstrap state seeding initializes previous-state trackers so the first active month does not misread baseline conditions as a fresh historical transition
- related economy identity beats now also share a tighter visible family key and source-side anti-stacking gate, so one settlement-material pair does not generate back-to-back specialization and trade-good chronicle lines in the same early window

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
- identity-style economy milestones now only surface when the settlement has stayed old enough and strong enough for long enough to read as earned history rather than as one responsive month of internal pressure

The next planned loop-facing expansions follow the canonical roadmap order:

- Phase 19 extends route logic into inter-polity exchange, imports, exports, and dependency pressure
- Phase 20 adds infrastructure and construction sinks that turn repeated surplus into durable settlement capability
- Phase 21 lets supply routes, scarcity, and cross-polity pressure generate diplomacy, raiding, and early conflict chains

Further chronicle dedupe tuning and later `Discoveries` / `Learned` presentation cleanup remain secondary follow-up work around the same loop, not replacements for those next core systems.
