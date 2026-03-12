# LivingWorld Event Types

LivingWorld events represent meaningful state transitions, not arbitrary notifications.

Every event should answer:

- what changed
- where it changed
- who was involved
- what caused it
- what other systems may react

## Terminology Rule

- Use `Discovery` for learning what exists in the world: regions, resources, edible or toxic species, geography.
- Use `Learned` for advancements that grant capability.

Current implementation uses `learned_advancement` for capability-granting breakthroughs such as Agriculture.

## Core Event Categories

### Food And Survival

- `food_stress`
  - emitted on meaningful hardship transitions
  - can propagate into `migration_pressure`, `starvation_risk`, `food_stabilized`
- `food_stabilized`
  - emitted when hardship meaningfully improves or recovery becomes durable
- `harvest`
  - used for notable good or poor harvest outcomes

### Migration

- `migration_pressure`
  - structured follow-up showing that hardship translated into movement pressure
- `migration`
  - major regional movement event
- `local_tension`
  - optional local follow-up when an arrival lands in a crowded destination

### Settlement And Agriculture

- `settlement_founded`
  - first durable settlement
- `settlement_consolidated`
  - transition into a more settled society
- `cultivation_expanded`
  - fields become active or expand meaningfully
  - the event can now be grounded in a polity's real settlement network
- `settlement_stabilized`
  - follow-up event when cultivation improves settlement reliability

### Knowledge And Capability

- `learned_advancement`
  - capability gain such as Fire, Storage, Agriculture, or Leadership Traditions
- `knowledge_discovered`
  - reserved for world-facing discoveries rather than capability unlocks
- `edible_species_discovered`
  - polity discovered that a species is edible
- `toxic_food_discovered`
  - polity discovered that a species is toxic to eat

### Polity Pressure And Change

- `schism_risk`
  - structured signal that internal pressure has risen toward fragmentation
- `fragmentation`
  - split event from a parent polity
- `polity_founded`
  - downstream event for the newly emerged polity
- `stage_changed`
  - polity progression such as Tribe or Settled Society
- `polity_collapsed`
  - total collapse

### Trade

- `trade_transfer`
- `trade_link_started`
- `trade_relief`
- `trade_dependency`
- `trade_link_collapsed`

Trade events are often structured-first, while their consequences may surface later through hardship recovery or stabilization events.
When a trade event includes settlement references, they should describe the actual settlement endpoint if one exists, not an inferred polity-region placeholder.

### Ecology And Hunting

- `species_population_established`
  - migration or expansion created a new regional population
- `predator_pressure`
  - predator food shortages became historically meaningful
- `prey_collapse`
  - predation drove a regional prey population into collapse
- `local_species_extinction`
  - one region lost a species population entirely
- `global_species_extinction`
  - no populations of the species remain anywhere
- `hunting_success`
  - a real settlement hunt materially changed food supply or local wildlife pressure
- `hunting_disaster`
  - a hunt failed badly enough to become a historical turning point
- `dangerous_prey_killed_hunters`
  - prey danger inflicted notable casualties
- `overhunting_pressure`
  - repeated hunting drove a local species population toward collapse
- `ecosystem_collapse`
  - regional food-web instability became severe
- `legendary_hunt`
  - rare high-risk major-prey hunt worthy of the main chronicle
- `species_population_mutated`
  - minor regional trait drift; usually structured/debug-first
- `species_population_major_mutation`
  - rare major lineage-level change with stronger ecological consequences
- `species_population_isolated`
  - prolonged separation without meaningful exchange
- `species_population_adapted_to_region`
  - regional lineage overcame ancestral habitat mismatch through sustained pressure, divergence, and improved effective fit
  - emitted on adaptation-stage transitions rather than repeated reaffirmation of the same regional condition
- `species_population_evolutionary_turning_point`
  - divergence milestone that may become chronicle-worthy

## Current Propagation Chains

### Food Stress Chain

`food_stress -> migration_pressure -> migration`

`food_stress -> starvation_risk`

`food_stress recovery or trade relief -> food_stabilized`

### Agriculture Chain

`learned_advancement (Agriculture) -> cultivation_expanded -> settlement_stabilized`

Later monthly farm output can improve food position and feed into future hardship recovery or settlement growth.

### Migration Chain

`migration_pressure -> migration`

`migration -> local_tension`

`migration -> higher settlement momentum`

### Fragmentation Chain

`schism_risk / fragmentation pressure -> fragmentation -> polity_founded`

### Discovery Versus Learned Capability

The code now distinguishes capability gain from world revelation. A polity can learn Agriculture, and the actual consequences appear later through cultivation, settlement, and food events instead of being collapsed into one event.

The same terminology rule now applies to hunting:

- discovering that `Stonehorn Elk` are edible is `Discovery`
- discovering that `Redcap Mushroom` is toxic is `Discovery`
- learning improved hunting tactics would be `Learned`

## Player-Facing Formatting Rule

Species references remain in structured event data, but watch-mode chronicle lines do not append species to each polity name. The focal polity species is shown in the fixed top status panel instead.
Watch-mode coloring is also conservative: actor names, place names, known knowledge names, and explicit crisis or status phrases may be colored, but incidental descriptive prose should remain plain text.

Watch mode also separates:

- `Discoveries`
  - world knowledge the polity has discovered
- `Learned`
  - capability-granting advancements the polity has learned

## Chronicle Priority

Usually visible:

- `migration`
- `settlement_founded`
- `settlement_consolidated`
- `learned_advancement`
- `food_stress`
- `food_stabilized`
- `fragmentation`
- `polity_founded`
- `stage_changed`
- `polity_collapsed`
- rare `species_population_major_mutation`
- rare `species_population_evolutionary_turning_point`

Usually structured-only unless escalated:

- `migration_pressure`
- `starvation_risk`
- `cultivation_expanded`
- `settlement_stabilized`
- `schism_risk`
- `local_tension`
- most trade bookkeeping
- `species_population_mutated`
- `species_population_isolated`
- most `species_population_adapted_to_region`

Adaptation events are one-time milestones per regional population, not recurring telemetry.
If a later adaptation event appears, it should represent a stronger milestone rather than the same adaptation beat repeated.
