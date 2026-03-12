# LivingWorld Architecture

LivingWorld keeps simulation logic, event storage, propagation, formatting, and console playback separate.

## Core Structure

`World` contains:

- regions
- species
- polities
- polity-owned settlements
- time
- canonical world events

Major systems:

- world generation
- food / ecology
- regional species populations
- ecosystem interactions
- settlement hunting
- mutation and divergence foundations
- agriculture
- trade
- migration
- population
- advancement
- settlement
- fragmentation
- polity stage progression

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

## Generation Architecture

World seeding is now split into a few explicit pieces so density tuning stays debuggable:

- `WorldGenerationSettings` holds the main numeric knobs for region, species, and polity counts
- `WorldGenerationCatalog` provides curated biome layout, name pools, and species templates
- `WorldGenerator` turns those settings and templates into connected regions, seeded species ranges, homeland-support-aware polity placement, and anchored starting polities

This keeps the fuller starting world explicit rather than burying scale changes in scattered magic numbers.

## Canonical Event Architecture

The canonical flow is now:

`simulation systems -> World.AddEvent -> EventPropagationCoordinator -> World.Events + EventRecorded -> output sinks`

### Event Source Of Truth

`World.AddEvent(...)` is still the only supported entry point for canonical events. It is responsible for:

- assigning event ids
- stamping time
- copying structured payloads
- recording chronological history
- invoking the propagation coordinator
- publishing `EventRecorded`

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
- `WatchUiState`
- `WatchInputController`
- `WatchInspectionData`
- `WatchScreenBuilder`

Important traits:

- structured history remains chronological and append-only
- the visible chronicle buffer is rendered newest-first
- only `Major+` turning points are shown in normal player mode
- internal propagation events remain structured-first unless they are promoted into genuine historical beats
- the fixed top panel shows focal polity context such as species
- the fixed top panel now also shows `RUNNING` / `PAUSED` and the active watch view
- the fixed top panel separates discoveries from learned advancements
- chronicle lines do not append species to every polity name
- syntax coloring is applied after formatting, with structured status-line parsing first and boundary-aware semantic matching for narrative lines

## Watch UI Architecture

The watch UI is now a thin observation layer over the simulation rather than a chronicle-only screen.

- `WatchUiState` stores active view, pause state, per-view selection, per-view scroll offsets, and the detail back stack
- `WatchInputController` translates key presses into UI navigation and pause behavior
- `WatchInspectionData` computes grounded first-pass visibility sets for known regions, species, and polities
- `WatchScreenBuilder` renders top-level and detail inspection screens from current world state
- `ChronicleWatchRenderer` remains responsible for low-flicker console drawing, the fixed top panel, chronicle retention, and viewport slicing
- `Simulation` now acts as the watch-loop coordinator: it polls input every iteration, advances months only when the next step time has arrived, and renders only when invalidated
- initial focus selection now runs after regional populations are initialized, so the selector can prefer a live starting polity rather than blindly taking the first id

Simulation advancement remains independent from the active screen. The UI reads world state, while `Space` explicitly gates whether monthly ticks continue.

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
- seasonal mutation and divergence processing after same-season species exchange
- seasonal extinction cleanup and biomass sync after mutation processing
- plant gathering, farming, trade redistribution, consumption, migration
- propagation state bonuses tick down
- systems emit canonical events on meaningful transitions
- follow-up events are processed immediately through the same event pipeline

The food architecture is intentionally asymmetric now:

- `FoodSystem` gathers only plant biomass from regions
- `HuntingSystem` is the only system that converts wildlife into animal food
- `EcosystemSystem` owns consumer population recovery, decline, migration, and derived `AnimalBiomass`
- `Region.AnimalBiomass` exists for ecology context, watch screens, migration heuristics, and advancement weighting, not as an independent meat store

The later monthly `MigrationSystem` still handles polity relocation after food resolution. Mutation does not read polity movement directly; it reads seasonal species-exchange state on `RegionSpeciesPopulation`.
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

## Design Direction

The architecture continues to prioritize:

- one structured event stream
- explainable cause-and-effect
- concise chronicle presentation
- regional ecology and hunting as shared simulation state rather than isolated subsystems
- population-level biological divergence layered on regional populations rather than rewritten species definitions
- future hooks for speciation, lineage naming, domestication variants, and cultural discovery of remarkable fauna
- future alternate history views without rewriting simulation systems
- source-side milestone guards plus chronicle cooldown keys so visible history beats stay about transitions rather than repeated reaffirmation
