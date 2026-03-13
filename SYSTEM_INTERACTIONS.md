# LivingWorld System Interactions

LivingWorld systems interact through shared state and the structured event pipeline rather than by writing directly to the chronicle.

## Standard Pattern

Each major system:

- reads world state
- detects pressure or opportunity
- updates its own domain state
- emits canonical events on meaningful transitions
- lets propagation handlers and sinks react afterward

## Current Major Systems

- food and ecology
- regional species populations
- ecosystem interactions
- settlement hunting
- mutation and divergence
- agriculture
- trade
- migration
- population
- settlement
- fragmentation
- polity stage progression
- advancement

## Current Propagation Subscriptions

### FoodStressPropagationHandler

Subscribes to:

- `food_stress`
- `trade_relief`

Reacts by:

- raising migration pressure bonuses
- raising starvation risk events
- easing pressure when hardship recovers or trade relief stabilizes food

### AgriculturePropagationHandler

Subscribes to:

- `learned_advancement`
- `cultivation_expanded`

Reacts by:

- remembering the causal agriculture event
- creating field-preparation follow-ups
- improving settlement momentum
- creating settlement stabilization events when cultivation becomes meaningful

### MigrationPropagationHandler

Subscribes to:

- `migration_pressure`
- `migration`

Reacts by:

- surfacing schism risk when migration pressure overlaps with internal strain
- adding settlement momentum after relocation
- emitting local tension when crowded destinations are stressed

### FragmentationPropagationHandler

Subscribes to:

- `fragmentation`

Reacts by:

- emitting `polity_founded` for the child polity

## Shared-State Reactions

Some handlers do more than emit follow-up events. They can also update temporary polity pressure bonuses that later systems consume:

- migration pressure bonus
- fragmentation pressure bonus
- settlement chance bonus

## Shared Ecology Layer

The new ecology phase is shared state for multiple systems:

- `Region.SpeciesPopulations` feeds ecosystem predation and prey support
- `FoodSystem` now gathers only plant biomass, so it no longer depletes wildlife outside the hunting layer
- world generation seeds those populations only inside each species' initial viable range instead of treating the world as globally occupied
- world generation now seeds herbivores and omnivores more broadly than predators, so fertile regions usually start with `2-4` meaningful consumer populations where biome fit supports them
- if a fertile region would otherwise be fauna-empty, world generation now attaches it to the nearest plausible herbivore cluster instead of leaving hunting and prey chains with no local foothold
- hunting reads the same regional populations and writes pressure back into them
- mutation reads those same pressure markers plus same-season species-exchange flags, stores accumulated evolutionary pressure on each regional population, and writes trait offsets back into the same records
- mutation now also builds speciation readiness gradually and requires descendant-species stabilization before a new lineage can branch again
- mutation also enforces regional root-lineage cooldowns and crowding penalties so chronically isolated basins cannot recursively spawn unlimited same-ancestor descendants
- ecosystem growth, migration scoring, and carrying capacity now consume those evolved trait offsets
- ecosystem initialization and seasonal growth now let healthy producer biomass translate into stronger herbivore establishment and earlier wildlife expansion
- seasonal fauna migration now turns that pressure into real founder populations in neighboring regions, so empty but suitable regions can join the food web without any separate spawn system
- predator follow migration is now stricter than herbivore spread: prey support, suitability, source stability, and local predator competition all shape whether a new predator colony is attempted
- hunting difficulty, danger, and yield now also consume regional trait divergence instead of using only species baselines
- neighboring wildlife populations can re-establish empty suitable regions through seasonal species migration, which gives local ecology a non-magical recovery path
- regional animal biomass is synchronized from species populations so migration heuristics, region screens, and advancement weighting still have region-level ecological context without creating a second animal-food resource
- polity discoveries, hunting knowledge, and domestication interest are stored on the polity for future systems to consume
- mutation also tracks adaptation milestones on each regional population so adaptation events emit only when a new stage is crossed
- mutation now also tracks divergence pressure, founder/source lineage metadata, and descendant-species creation from isolated high-divergence populations
- sparse regional-population storage means those mutation and migration rules now operate primarily on active populations plus explicit founder targets, not on dormant region-species placeholders everywhere

Important timing boundary:

- seasonal species exchange happens inside `EcosystemSystem` before mutation runs
- role-specific founder migration is part of that same seasonal ecology step, after local pressure is known and before mutation reads exchange flags
- predator founders then continue through that same seasonal ecology step, where prey-rich colonies can establish and prey-poor colonies can fail without adding special-case spawn logic
- later monthly polity migration happens in `MigrationSystem` after food resolution
- mutation inputs such as `EstablishedThisSeason`, `ReceivedMigrantsThisSeason`, and `SentMigrantsThisSeason` refer only to the first category
- extinction cleanup now marks local extinction once, emits global extinction once per species, and leaves later recovery to the same neighboring founder-migration path
- repeated biology status events such as isolation and minor mutation are now source-throttled more aggressively so the event pipeline preserves causality without late-game spam
- chronicle presentation then applies its own scoped cooldown rules, including a dedicated adaptation key for visible adaptation beats

## Settlement-Grounded Production Layer

Hunting, farming, and settlement-aware trade now share the same locality layer:

- `Polity.Settlements` are the concrete execution points
- hunting reads wildlife from each settlement's region
- agriculture allocates each region's arable capacity across all settlements in that region
- trade prefers real settlement endpoints when settlements exist and falls back to camps/hearth labels only when necessary
- starting polities now usually enter the world with one home settlement anchor so those systems can act immediately

This keeps cause-and-effect local:

- local prey decline comes from the settlements that hunted there
- animal food gains come from the species those settlements actually hunted
- farm output comes from settlements actually occupying fertile land
- migration relocates settlement records so later systems do not read stale locality state

## Generation To Simulation Handoff

The fuller starting world is still intentionally constrained at handoff time:

- region biome profiles shape baseline fertility, water, and biomass
- species start in clustered biome-suitable ranges rather than universal placement
- fertile biomes now more reliably hand off a real prey base into the first decade instead of abundant producers paired with token herbivores
- predator seeding now remains subordinate to herbivore support, so predator-only range islands are trimmed out during worldgen
- default world generation now also includes the full built-in predator and apex roster so regional predator variety is not lost before simulation even starts
- starting polities are seeded into viable, spaced regions rather than random stacking
- homeland scoring also prefers nearby support species and connected corridors so early settlement-grounded interaction is more likely

That means early hunting, food stress, migration, and contact pressures begin from a richer world, but they still emerge from regional conditions instead of arbitrary clutter.

## Knowledge Split

The polity model now separates:

- cultural discoveries about the world
- learned advancements that grant capability

The watch-mode panel mirrors that split with separate `Discoveries:` and `Learned:` lines.
Species inspection now reads the same regional-population records directly for fit, capacity, mutation, divergence, founder, and lineage signals instead of inventing UI-only biology summaries.

## Chronicle Naming Rule

Presentation now splits polity naming context by UI surface:

- fixed watch-mode status panel shows the focal polity species
- chronicle lines show only the polity name

This keeps species visible without weighing down every visible history line. Debug details, structured history, and internal ids remain unchanged.

These bonuses decay over time, so the propagation effects stay lightweight and deterministic.

## Chronicle Separation

Simulation and propagation may create many structured events in one year, but only `Major` and `Legendary` turning points are shown in the default chronicle.

That separation keeps the architecture clean:

- simulation systems stay honest about causality
- structured history preserves the full chain
- chronicle presentation stays readable
- watch-mode coloring remains token-aware so semantic highlights do not bleed into unrelated prose
- chronicle presentation can now suppress same-state repeats for one actor while still allowing distinct actors, regions, or changed-state milestones through

## Watch Inspection Layer

The new inspection UI is a read-only observer layer on top of those systems:

- `WatchInspectionData` derives what the focal polity currently knows from existing simulation state
- `WatchKnowledgeSnapshot` centralizes that knowledge horizon for one render/input pass
- `WatchScreenBuilder` formats that filtered state into chronicle-adjacent inspection screens
- `WatchInputController` changes UI state only; it does not call simulation systems
- pausing stops monthly advancement but does not mutate domain state or generate events
- the simulation loop now schedules month advancement on a timed cadence and uses render invalidation so input polling stays responsive during live play
- foreign-polity detail intentionally hides that polity's private discoveries and learned capabilities unless it is the current focal polity
- focal-polity inspection is intentionally separate: `My Polity` is treated as the already-expanded self-view, so `Enter` there does not fall through to the generic polity-detail renderer
## Phase 12 - Food Redistribution Interactions

`FoodSystem` and `AgricultureSystem` still generate polity-level food totals, but those totals are now projected back onto settlements each month so settlement inspection and aid routing can operate on concrete local states.

`SettlementFoodRedistributionSystem` consumes:
- monthly food gathered
- monthly food farmed
- current polity food stores
- regional connectivity
- settlement cultivated land and location

It produces:
- settlement food pressure classifications
- settlement aid totals for UI visibility
- structured aid events for the event pipeline

The chronicle continues to stay high-signal by showing only `Major` and `Legendary` rescue/failure outcomes.

## Phase 13/14 - Domestication Interactions

`DomesticationSystem` now connects:

- `HuntingSystem` familiarity and domestication interest
- regional species populations and trait resolution
- settlement continuity and food pressure
- `AgricultureSystem` yield and winter resilience
- structured event propagation and chronicle formatting

The result is intentionally asymmetric:

- managed herds supplement hunting rather than replacing it immediately
- cultivated crops strengthen farming where agriculture already exists
- both systems remain local to settlements and regions, so remote collapse or regional breadbaskets still emerge from the same underlying food logic

## Phase 17 - Material Economy Interactions

`MaterialEconomySystem` now connects:

- regional abundance on `Region`
- settlement labor, hardship state, and reserve targets
- learned capability gates such as `StoneTools`, `FoodStorage`, `BasicConstruction`, and `CraftSpecialization`
- `AgricultureSystem` through tool effectiveness
- `HuntingSystem` through better hunting reliability
- `FoodSystem` through pottery-backed storage and preserved-food buffering
- same-polity logistics through route-prioritized material convoys
- watch-mode inspection through settlement, region, polity, and world-overview summaries

The result is still concrete and local:

- geography creates raw-material advantages
- settlements convert those advantages into visible craft roles
- convoys move physical surplus rather than abstract trade value
- lower-level shortage and convoy detail remains available in structured history
- major visible events only appear when the material state changes meaningfully, usually as one grouped settlement crisis beat rather than several same-tick per-material lines

More broadly, player-facing major-event summaries now reuse the same visible dedupe identity as the live chronicle. That means a settlement recovery, famine turn, or grouped material crisis that already represents one visible historical outcome will not be repeated in summary views just because equivalent events exist underneath in canonical history.

Phase 18 now deepens those interactions internally:

- settlement need, scarcity, and surplus feed hidden value and opportunity signals
- those signals alter extraction, production focus, convoy priority, and specialization drift
- persistent surplus can produce trade-good identity, while bottlenecks can suppress favored output and create explainable downstream weakness
- the watch UI reads those interactions through small readable labels rather than exposing raw market equations

The canonical next interaction-focused phases now proceed in this order:

- Phase 19 - external trade and inter-polity exchange add foreign links, imports, exports, route consequences, and dependency
- Phase 20 - infrastructure and construction turn materials into long-term settlement capability, resilience, and logistics improvements
- Phase 21 - diplomacy, raiding, and conflict foundations let those economic and route interactions create coercion, disruption, and border pressure

Chronicle dedupe follow-through and later `Discoveries` / `Learned` list-view cleanup still matter, but they remain secondary player-facing follow-up work beside the next core system interactions.
