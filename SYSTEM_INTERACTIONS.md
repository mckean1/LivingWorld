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
- ecosystem growth, migration scoring, and carrying capacity now consume those evolved trait offsets
- ecosystem initialization and seasonal growth now let healthy producer biomass translate into stronger herbivore establishment and earlier wildlife expansion
- hunting difficulty, danger, and yield now also consume regional trait divergence instead of using only species baselines
- neighboring wildlife populations can re-establish empty suitable regions through seasonal species migration, which gives local ecology a non-magical recovery path
- regional animal biomass is synchronized from species populations so migration heuristics, region screens, and advancement weighting still have region-level ecological context without creating a second animal-food resource
- polity discoveries, hunting knowledge, and domestication interest are stored on the polity for future systems to consume
- mutation also tracks adaptation milestones on each regional population so adaptation events emit only when a new stage is crossed

Important timing boundary:

- seasonal species exchange happens inside `EcosystemSystem` before mutation runs
- later monthly polity migration happens in `MigrationSystem` after food resolution
- mutation inputs such as `EstablishedThisSeason`, `ReceivedMigrantsThisSeason`, and `SentMigrantsThisSeason` refer only to the first category
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
- starting polities are seeded into viable, spaced regions rather than random stacking
- homeland scoring also prefers nearby support species and connected corridors so early settlement-grounded interaction is more likely

That means early hunting, food stress, migration, and contact pressures begin from a richer world, but they still emerge from regional conditions instead of arbitrary clutter.

## Knowledge Split

The polity model now separates:

- cultural discoveries about the world
- learned advancements that grant capability

The watch-mode panel mirrors that split with separate `Discoveries:` and `Learned:` lines.

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

## Watch Inspection Layer

The new inspection UI is a read-only observer layer on top of those systems:

- `WatchInspectionData` derives what the focal polity currently knows from existing simulation state
- `WatchScreenBuilder` formats that state into chronicle-adjacent inspection screens
- `WatchInputController` changes UI state only; it does not call simulation systems
- pausing stops monthly advancement but does not mutate domain state or generate events
- the simulation loop now schedules month advancement on a timed cadence and uses render invalidation so input polling stays responsive during live play
