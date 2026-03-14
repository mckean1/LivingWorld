# LivingWorld Architecture

LivingWorld keeps simulation logic, event storage, propagation, formatting, and console playback separate.

The startup direction is now explicitly primitive-life-first:

1. biological world foundation
2. evolution and divergence
3. sentience and social formation
4. polity start and player entry

Pass 1 is implemented now. The default generated world stops at ecological foundation instead of assuming sapient species and polities already exist.

## Core Structure

`World` contains:

- regions
- species
- polities
- polity-owned settlements
- time
- current simulation phase (`Bootstrap` vs `Active`)
- canonical world events

Major systems:

- world generation
- food / ecology
- regional species populations
- ecosystem interactions
- settlement hunting
- mutation, divergence, and early speciation foundations
- agriculture
- trade
- migration
- population
- advancement
- settlement
- fragmentation
- polity stage progression

Only some of those systems are active in the default startup stage.
`World.StartupStage` now gates the default path so Pass 1 worlds run ecological foundation first and defer mutation, sentience, polity formation, and player-focus assumptions to later startup passes.

## Settlement-Grounded Locality

`Polity` now owns lightweight `Settlement` records.
Those records are the local execution points for hunting, farming, and settlement-aware trade endpoints.
The polity-level `RegionId` still exists as the polity's current center of movement and presentation, but food-production systems no longer assume one abstract settlement multiplied by `SettlementCount`.

`WorldLookup` is the current shared lookup layer for hot-path systems.
It builds cached dictionaries for regions, species, polities, active populations by region, and settlements by region so systems can:

- avoid repeated linear scans in inner loops
- recover gracefully where a missing reference can be ignored
- throw clearer invariant errors where corruption would otherwise surface as an opaque LINQ exception

`Region` also now keeps direct indexing for species-population entries, which removes another repeated lookup hotspot from seasonal ecology and hunting code.
Those entries are sparse by default now: regions keep active or historically meaningful populations instead of materializing every species in every region.

## Generation Architecture

World seeding is now split into a few explicit pieces so density tuning stays debuggable:

- `WorldGenerationSettings` holds the main numeric knobs for region, species, and polity counts
- `WorldGenerationCatalog` provides curated biome layout, name pools, and species templates
- `WorldGenerator` now turns those settings and templates into connected regions, ecology profiles, primitive lineage templates, weighted seeded ranges, and a Phase A ecological bootstrap
- producer coverage is intentionally broad while consumer and predator spread is narrower and more uneven, so the world opens biologically alive without becoming uniform
- `EcosystemSettings` now centralizes the long-run fauna spread knobs that take over after generation: migration thresholds, founder-population sizing, prey-support gates, predator establishment windows, and cooldown pacing
- `PhaseAReadinessEvaluator` summarizes whether the generated world has broad, uneven, functioning ecological foundations rather than relying on a fixed time cutoff

This keeps the startup world explicit rather than burying scale changes in scattered magic numbers.

## Canonical Event Architecture

The canonical flow is now:

`simulation systems -> World.AddEvent -> EventPropagationCoordinator -> World.Events + EventRecorded -> output sinks`

### Event Source Of Truth

`World.AddEvent(...)` is still the only supported entry point for canonical events. It is responsible for:

- assigning event ids
- stamping time
- stamping the current simulation phase so setup and live history remain distinguishable
- copying structured payloads
- recording chronological history
- invoking the propagation coordinator
- publishing `EventRecorded`
- leaving any year-local control caches to higher-level simulation logic rather than mixing them into canonical storage

### EventPropagationCoordinator

The coordinator is a lightweight in-process dispatcher. It:

- records the initial event
- routes it to subscribed handlers
- enqueues deterministic follow-up events
- preserves parent-child causal links
- enforces per-step dedupe
- enforces max propagation depth
- enforces max events per source event

Current default limits:

- max depth: `4`
- max events per step: `64`

### Current Handler Subscriptions

- `FoodStressPropagationHandler`
  - listens to `food_stress`, `trade_relief`
  - can emit `migration_pressure`, `starvation_risk`, `food_stabilized`
- `AgriculturePropagationHandler`
  - listens to `learned_advancement`, `cultivation_expanded`
  - can emit `cultivation_expanded`, `settlement_stabilized`
- `MigrationPropagationHandler`
  - listens to `migration_pressure`, `migration`
  - can emit `schism_risk`, `local_tension`
- `FragmentationPropagationHandler`
  - listens to `fragmentation`
  - emits `polity_founded`

## Canonical Event Model

`WorldEvent` stores:

- `eventId`
- `rootEventId`
- `parentEventIds`
- `propagationDepth`
- `simulationPhase`
- `origin`
- `year`, `month`, `season`
- `type`, `severity`, `scope`
- `narrative`, `details`, `reason`
- polity / related polity / species / region / settlement references
- `before`, `after`, `metadata`

`scope` is used to express how far an event matters:

- `Local`
- `Regional`
- `Polity`
- `World`

## Player-Facing Chronicle Path

Default player output still uses watch mode rather than yearly reports.

Watch mode is built from:

- `ChronicleEventFormatter`
- `ChronicleWatchRenderer`
- `ChronicleColorWriter`
- `WatchViewCatalog`
- `WatchUiState`
- `WatchInputController`
- `WatchInspectionData`
- `WatchKnowledgeSnapshot`
- `WatchScreenBuilder`

Important traits:

- structured history remains chronological and append-only
- the visible chronicle buffer is rendered newest-first
- only `Major+` turning points are shown in normal player mode
- internal propagation events remain structured-first unless they are promoted into genuine historical beats
- bootstrap-created baseline events remain canonical/internal but are not eligible for the player chronicle or recent-major-event summaries
- event origin now distinguishes bootstrap baseline/setup context from true live transitions, which gives the chronicle pipeline a final safety guard against bootstrap-derived leaks
- economy identity families now use a clearer two-layer model: internal specialization / trade-good state can respond quickly, while visible `known for` milestones require settlement maturity, sustained confirmation, and stricter promotion thresholds
- related specialization and trade-good turns for the same settlement-material pair now also share one presentation family key, which gives the chronicle a final anti-stacking guard even if an upstream producer emits both
- chronicle presentation now uses per-family visibility profiles with semantic scope keys, state signatures, and separate same-state versus changed-state cooldown gaps
- when a visible event family has no custom state signature yet, chronicle presentation falls back to actor scope plus normalized narrative so exact repeated lines are still suppressed
- the fixed top panel shows focal polity context such as species
- the fixed top panel now also shows `RUNNING` / `PAUSED` and the active watch view
- the fixed top panel separates discoveries from learned advancements
- chronicle lines do not append species to every polity name
- syntax coloring is applied after formatting, with structured status-line parsing first and boundary-aware semantic matching for narrative lines

## Watch UI Architecture

The watch UI is now a thin observation layer over the simulation rather than a chronicle-only screen.

- `WatchUiState` stores active view, pause state, per-view selection, per-view scroll offsets, and the detail back stack
- `WatchViewCatalog` centralizes top-level screen labels, numbering, and control mapping
- `WatchInputController` translates key presses into shared list/detail navigation, paging, and pause behavior
- `WatchKnowledgeSnapshot` is the shared player-knowledge projection for one render/input pass
- `WatchInspectionData` builds that knowledge snapshot from the focal polity's visible horizon and discovery records
- `WatchScreenBuilder` renders top-level and detail inspection screens from that filtered world view instead of from omniscient raw state
- `ChronicleWatchRenderer` remains responsible for low-flicker console drawing, the fixed top panel, chronicle retention, and viewport slicing
- `Simulation` now acts as the watch-loop coordinator: it polls input every iteration, advances months only when the next step time has arrived, and renders only when invalidated
- initial focus selection now runs after regional populations are initialized, so the selector can prefer a live starting polity rather than blindly taking the first id

Simulation advancement remains independent from the active screen. The UI reads world state, while `Space` explicitly gates whether monthly ticks continue.
Left/Right paging is view-agnostic now: list screens page selection, while chronicle and detail screens page scroll offsets.
`My Polity` is also a special focal-polity view rather than just a shortcut into generic polity detail. `Enter` intentionally leaves the player there so focal-only information cannot be downgraded by a foreign-polity-safe renderer path.
Simulation now also performs an explicit bootstrap seeding pass before active play begins. In Pass 1 that bootstrap is ecological: world generation runs primitive seeding and Phase A stabilization internally, records readiness, then resets visible time back to the active simulation boundary.
Later bootstrap layers such as economy baselines still exist in the codebase, but they are skipped for the primitive-ecology startup stage because societies and settlements are intentionally deferred.

## Focus And Continuity

`ChronicleFocus` stores the currently watched polity and lineage.

`LineagePolityFocusSelector` handles:

1. initial focus selection
2. deterministic year-end handoff when the watched polity fragments, collapses, or disappears

Focus handoff events are still canonical `WorldEvent` records.

## Simulation Loop Notes

Monthly:

- region plant biomass refresh
- seasonal regional species population update on season boundaries
- seasonal ecosystem food-web processing and species exchange on season boundaries
- seasonal settlement hunting on season boundaries
- those seasonal hunts iterate actual settlements in actual regions
- regional animal biomass is derived during seasonal ecosystem sync from surviving consumer populations and is not harvested directly
- seasonal mutation, divergence, and speciation processing after same-season species exchange
- descendant-species creation now feeds into a stabilization window and readiness rebuild rather than inheriting near-ready speciation state
- seasonal extinction cleanup and biomass sync after mutation processing
- plant gathering, farming, trade redistribution, consumption, migration
- propagation state bonuses tick down
- systems emit canonical events on meaningful transitions
- follow-up events are processed immediately through the same event pipeline

In startup Pass 1, the active monthly path intentionally stops after ecological systems.
Hunting, mutation/speciation, economy, migration, settlement, and polity-year-end systems remain available for later startup stages, but the default generated world does not activate them yet.

The food architecture is intentionally asymmetric now:

- `FoodSystem` gathers only plant biomass from regions
- `HuntingSystem` is the only system that converts wildlife into animal food
- `EcosystemSystem` owns consumer population seeding, recovery, decline, migration, predator founder establishment/collapse, recolonization bookkeeping, and derived `AnimalBiomass`
- `Region.AnimalBiomass` exists for ecology context, watch screens, migration heuristics, and advancement weighting, not as an independent meat store
- early herbivore establishment now comes from range-limited worldgen plus carrying-capacity-driven initialization and producer-supported growth rather than from any abstract animal stock
- ongoing fauna spread now also lives in `EcosystemSystem`: adjacent-region founder migration happens after seasonal food-web pressure is known and before mutation consumes same-season exchange markers
- ecology processing is now sparse and active-population driven: suitability and carrying-capacity refresh happens for existing regional populations, while empty region-species pairs are evaluated only when an explicit founder or recolonization attempt targets them

The later monthly `MigrationSystem` still handles polity relocation after food resolution. Mutation does not read polity movement directly; it reads seasonal species-exchange state on `RegionSpeciesPopulation`.
`MutationSystem` now owns the regional evolution pass on those same records: pressure accumulation, local trait drift, divergence pressure, adaptation milestones, and descendant-species creation for long-isolated viable populations.
Speciation is now additionally gated by descendant-species age, global species population, sustained readiness, and stabilization rules so evolutionary storytelling remains visible without turning into recursive geometric growth.
When polity migration does occur, settlement records are relocated with the polity so settlement-grounded systems keep a coherent local model until a later phase introduces true cross-region polity settlement networks.

Year-end:

- population
- learned advancements
- settlement progression
- fragmentation
- stage progression
- annual agriculture and trade review
- annual hardship transition events
- focus validation and handoff
- optional perf instrumentation snapshots

## Design Direction

The architecture continues to prioritize:

- one structured event stream
- explainable cause-and-effect
- concise chronicle presentation
- regional ecology and hunting as shared simulation state rather than isolated subsystems
- population-level biological divergence layered on regional populations rather than rewritten species definitions
- the first lineage-aware descendant-species path now exists; later phases can deepen naming, deep-history, and domestication variants without replacing the population-level model
- future alternate history views without rewriting simulation systems
- source-side milestone guards plus chronicle cooldown keys so visible history beats stay about transitions rather than repeated reaffirmation
- sparse regional-population tracking and descendant-species stabilization as first-class performance safeguards
- a stricter presentation split where structured history may still keep repeated pressure/follow-up events even when the live chronicle hides them
- startup-stage honesty, so the generated world only contains layers that have actually been simulated into existence
## Phase 12 - Settlement Aid Layer

Regional food exchange now sits between settlement production/consumption bookkeeping and later social-pressure systems. The new monthly pass is settlement-centric: it derives each settlement's local food requirement, production share, and stored food share from the current polity-level totals, classifies the settlement food state, and then performs intra-polity redistribution.

This layer does not introduce markets, prices, currency, or diplomacy. It is a logistical redistribution system whose main responsibilities are:
- classify settlement food pressure
- move food along region-aware routes inside a polity
- apply transport loss by regional distance
- emit high-signal aid and famine-relief events through the normal world event pipeline

## Phase 13/14 - Managed Food Architecture

Domestication and cultivation now extend the same settlement-grounded architecture rather than creating a disconnected economy layer.

- `DomesticationSystem` evaluates local animal and plant familiarity after gathering
- `AgricultureSystem` still owns cultivated land and farm output, while cultivated crops now modify that output
- settlement-managed food remains local first, gradual, and event-driven
- `DomesticationPropagationHandler` turns meaningful domestication success into later stabilization follow-ups through the normal event pipeline
- annual managed-food stabilization is now a polity state transition, not a yearly steady-state reminder

The new managed-food entities stay population-level:

- `ManagedHerd` tracks a domesticated animal variant, reliability, growth, and managed yield for one settlement
- `CultivatedCrop` tracks a cultivated plant variant, yield multiplier, resilience, and stability effect for one settlement

This keeps the model cheap, explicit, and extensible for later pack animals, labor animals, milk/fiber outputs, and selective breeding without replacing the current architecture.

## Phase 17 - Material Economy Architecture

The new material layer extends the same settlement-grounded model instead of creating a parallel economy simulator.

- `Region` remains a capacity provider through per-material abundance values rather than a finite inventory
- `Settlement` is the core material actor and now owns stockpiles, reserve targets, pressure states, annual production history, tool tier, and specialization tags
- `MaterialEconomySystem` runs monthly after gathering and domestication familiarity, then before farm output and later hardship resolution
- extraction, production, pressure classification, redistribution, and specialization all operate on the same settlement records
- `MaterialEconomyPropagationHandler` turns meaningful preservation, toolmaking, and convoy success into downstream stability effects through the normal event pipeline
- grouped material-crisis events are emitted in addition to lower-level per-material shortage events so player-facing history stays settlement-scale and readable while structured history keeps the exact material breakdown

This preserves LivingWorld's existing architecture rules:

- cause-and-effect remains explicit
- structured events stay canonical
- routine operational state remains inspectable without spamming the chronicle
- later construction, infrastructure, and inter-polity trade can build on the same stockpile model
- player-facing major-event views now share a common dedupe identity so the live chronicle and visible-major-event summaries do not diverge on duplicate suppression

The canonical next planned sequence after this material-economy foundation is:

Phase 18 is now implemented on top of that stockpile model:

- the economy remains hybrid and pressure-based internally rather than becoming a player-facing price screen
- settlements now keep smoothed need, value, opportunity, production-focus, and external-pull readiness signals
- those signals feed extraction, recipe choice, redistribution priority, bottleneck handling, and specialization drift
- player-facing summaries stay label-based and readable instead of exposing raw market math

The canonical next planned sequence is now:

- Phase 19 - external trade, trade routes, and inter-polity exchange beyond same-polity redistribution
- Phase 20 - settlement infrastructure and construction as durable material sinks and capability multipliers
- Phase 21 - diplomacy, raiding, and conflict foundations grounded in logistics, dependency, and supply disruption

Chronicle dedupe polish and later `Discoveries` / `Learned` list cleanup remain supporting presentation follow-through, not the primary next architecture phases.
