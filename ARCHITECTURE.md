# LivingWorld Architecture

LivingWorld keeps simulation logic, event storage, propagation, formatting, and console playback separate.

## Core Structure

`World` contains:

- regions
- species
- polities
- time
- canonical world events

Major systems:

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

Important traits:

- structured history remains chronological and append-only
- the visible chronicle buffer is rendered newest-first
- only `Major+` turning points are shown in normal player mode
- internal propagation events remain structured-first unless they are promoted into genuine historical beats
- the fixed top panel shows focal polity context such as species
- the fixed top panel separates discoveries from learned advancements
- chronicle lines do not append species to every polity name

## Focus And Continuity

`ChronicleFocus` stores the currently watched polity and lineage.

`LineagePolityFocusSelector` handles:

1. initial focus selection
2. deterministic year-end handoff when the watched polity fragments, collapses, or disappears

Focus handoff events are still canonical `WorldEvent` records.

## Simulation Loop Notes

Monthly:

- region biomass refresh
- seasonal regional species population update on season boundaries
- seasonal ecosystem food-web processing on season boundaries
- seasonal settlement hunting on season boundaries
- seasonal mutation and divergence processing on season boundaries
- seasonal extinction cleanup and biomass sync after mutation processing
- gathering, farming, trade redistribution, consumption, migration
- propagation state bonuses tick down
- systems emit canonical events on meaningful transitions
- follow-up events are processed immediately through the same event pipeline

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
