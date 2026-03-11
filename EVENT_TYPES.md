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
- `settlement_stabilized`
  - follow-up event when cultivation improves settlement reliability

### Knowledge And Capability

- `learned_advancement`
  - capability gain such as Fire, Storage, Agriculture, or Leadership Traditions
- `knowledge_discovered`
  - reserved for world-facing discoveries rather than capability unlocks

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

Usually structured-only unless escalated:

- `migration_pressure`
- `starvation_risk`
- `cultivation_expanded`
- `settlement_stabilized`
- `schism_risk`
- `local_tension`
- most trade bookkeeping
