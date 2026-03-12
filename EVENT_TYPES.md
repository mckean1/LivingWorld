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
  - may also mark wildlife recolonization when a neighboring surviving population returns to an empty suitable region
  - founder-population establishment now also covers slow ecological frontier opening from adjacent fauna-rich regions
  - most predator founder arrivals should remain structured-first unless they grow into a materially important regional shift
- `species_population_recolonized`
  - a region regained a species after true local loss
  - distinct from first-time establishment so extinction and recovery history stay readable
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
  - animal food should now always come from this path, not from abstract regional animal biomass harvesting
- `hunting_disaster`
  - a hunt failed badly enough to become a historical turning point
- `dangerous_prey_killed_hunters`
  - prey danger inflicted notable casualties
- `overhunting_pressure`
  - repeated hunting drove a local species population toward collapse
- `prey_collapse` and `species_population_established`
  - now more often reflect real regional food-web swings because world generation seeds a broader early herbivore base and seasonal migration can rebuild or extend that base over time
- `ecosystem_collapse`
  - regional food-web instability became severe
- `legendary_hunt`
  - rare high-risk major-prey hunt worthy of the main chronicle
- `species_population_mutated`
  - minor regional trait drift; usually structured/debug-first
  - now respects stricter emission cooldowns so repeated small shifts do not spam structured history every active year
- `species_population_major_mutation`
  - rare major lineage-level change with stronger ecological consequences
- `species_population_isolated`
  - prolonged separation without meaningful exchange
  - now emitted on broader isolation milestones rather than every repeated short interval
- `species_population_adapted_to_region`
  - regional lineage overcame ancestral habitat mismatch through sustained pressure, divergence, and improved effective fit
  - emitted on adaptation-stage transitions rather than repeated reaffirmation of the same regional condition
- `species_population_evolutionary_turning_point`
  - divergence milestone that may become chronicle-worthy
- `new_species_appeared`
  - a long-isolated viable regional population diverged enough to become a descendant species
  - now also requires descendant-species age, meaningful global population, sustained readiness, and stabilization guards before another split can happen
  - usually structured-first unless it lands at `Major` severity inside the focused historical line

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

The same sourcing rule now applies to food:

- plant food can come from ordinary wild gathering
- animal food must come from species-level hunting
- `AnimalBiomass` is an ecological summary metric, not a generic gatherable pool
- early `AnimalBiomass` differences should now mostly read as differences in real seeded consumer populations and their growth, not as missing or hidden food stock logic

## Player-Facing Formatting Rule

Species references remain in structured event data, but watch-mode chronicle lines do not append species to each polity name. The focal polity species is shown in the fixed top status panel instead.
Watch-mode coloring is also conservative: actor names, place names, known knowledge names, and explicit crisis or status phrases may be colored, but incidental descriptive prose should remain plain text.
The watch inspection controls are UI-only and do not introduce new event types.
The same is true for watch-loop pacing changes: responsive input and timed stepping change presentation behavior, not event semantics.
The fuller seed world also does not introduce generation-only spam event types; denser starting state should surface through ordinary ecology, migration, settlement, and polity history.
Predator founder failure and success are expected to be common structured ecology outcomes, but only unusually consequential arrivals or collapses should surface to the main chronicle.
Anchored starting settlements are also generation state, not synthetic `settlement_founded` events. Chronicle-visible settlement beats still come from later real transitions such as consolidation.
The completed watch inspection UI does not introduce event types for pausing, paging, screen switching, or other presentation-only actions.

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
- rare `new_species_appeared`

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
Chronicle presentation now applies the same idea more broadly to other noisy families: hardship, recovery, migration, and some ecology turns are filtered by semantic state change rather than by raw event repetition alone.
## Phase 12 Food Redistribution Events

New structured event types:
- `food_aid_sent`: significant internal food convoy between settlements
- `famine_relief`: aid prevented a starving settlement from remaining in famine
- `aid_failed`: a starving settlement remained unaided after redistribution

`aid_failed` is transition-based for settlement hardship. Use it when starvation begins or worsens meaningfully, not as a recurring reminder while the same starving state persists.

Expected severity:
- `Minor`: ordinary internal aid transfer
- `Major`: large convoy or starvation prevented
- `Legendary`: massive famine rescue

Each aid event carries location, actors, cause, distance, transport loss, and settlement food-state metadata so event propagation and structured history preserve the cause-and-effect chain.

## Phase 13/14 Domestication And Cultivation Events

New structured event types:

- `species_domestication_candidate_identified`
  - discovery that a nearby animal species appears manageable
- `plant_cultivation_discovered`
  - discovery that a wild plant can be cultivated
- `animal_domesticated`
  - a settlement established a managed herd
- `crop_established`
  - a settlement established a cultivated crop
- `domestication_spread`
  - the same herd or crop pattern spread across multiple settlements in one polity
- `agriculture_stabilized_food_supply`
  - managed food first covered enough yearly consumption to establish a durable managed-food base

Default visibility intent:

- discovery beats may stay structured-first unless they reach `Major`
- herd establishment, crop establishment, spread, and first-time food stabilization are the main chronicle-facing turning points

## Phase 17 Material Economy Events

New structured event types:

- `material_discovered`
  - a polity recognized useful local wood, stone, clay, salt, or ore
- `material_extraction_started`
  - a settlement entered sustained local extraction
- `material_shortage_started`
  - a settlement dropped below working reserve in a tracked material
- `material_shortage_worsened`
  - a tracked material shortage deepened into a more severe band
- `material_shortage_resolved`
  - a settlement recovered out of a tracked shortage band
- `material_crisis_started`
  - grouped player-facing settlement crisis beat for multiple related shortages beginning together
- `material_crisis_worsened`
  - grouped player-facing settlement crisis beat for a broader material downturn deepening
- `material_crisis_resolved`
  - grouped player-facing settlement crisis beat for a broader material recovery
- `production_started`
  - a settlement began regular production of a specific processed good
- `production_milestone`
  - a chronicle-worthy craft milestone such as pottery tradition or cut-stone work
- `material_convoy_sent`
  - a same-polity material convoy moved meaningful relief
- `material_convoy_failed`
  - a critical material shortage remained unaided at a meaningful transition
- `settlement_specialized`
  - a settlement became known for a sustained craft or extraction role
- `preservation_established`
  - a settlement began preserving food through salt-backed storage
- `toolmaking_established`
  - a settlement reached sustained higher-quality tool production

Default visibility intent:

- routine discovery, extraction, production, and per-material shortage bookkeeping remain mostly structured-first
- grouped `material_crisis_*` events, preservation, sustained toolmaking, and specialization turns are the main chronicle-facing material beats
