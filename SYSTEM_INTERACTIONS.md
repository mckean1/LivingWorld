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
- hunting reads the same regional populations and writes pressure back into them
- mutation reads those same pressure markers, stores accumulated evolutionary pressure on each regional population, and writes trait offsets back into the same records
- ecosystem growth, migration scoring, and carrying capacity now consume those evolved trait offsets
- hunting difficulty, danger, and yield now also consume regional trait divergence instead of using only species baselines
- regional biomass is synchronized from species populations so existing food gathering and migration heuristics still have region-level ecological context
- polity discoveries, hunting knowledge, and domestication interest are stored on the polity for future systems to consume

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
