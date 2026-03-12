# LivingWorld Cause And Effect

LivingWorld is designed around deterministic cause-and-effect simulation rather than scripted events.

The guiding rule remains:

> Nothing happens without a reason.

## Core Model

Every meaningful change follows this pattern:

`state transition -> canonical event -> propagation -> downstream state or event`

Example:

`food shortage -> food_stress -> migration_pressure -> migration -> local tension or settlement momentum`

## Why Propagation Exists

Without propagation, systems can truthfully log their own state changes but still feel isolated.

Propagation lets LivingWorld express:

- visible causes
- structured follow-up consequences
- traceable ancestry in JSONL history
- concise player-facing chronicle output

## Current Pressure Categories

### Food Pressure

Low food satisfaction, weak reserves, and repeated starvation are the strongest current historical drivers.

Implemented chain:

`food_stress -> migration_pressure`

`food_stress -> starvation_risk`

`food_recovery or trade relief -> food_stabilized`

### Settlement Pressure

Learned Agriculture and stable settlement context can now create downstream cultivation and settlement stabilization events.

Implemented chain:

`learned_advancement (Agriculture) -> cultivation_expanded -> settlement_stabilized`

### Migration Pressure

Migration is now both a result and a cause.

Implemented chain:

`migration_pressure -> migration`

`migration -> local_tension`

`migration -> higher settlement chance`

### Ecology Pressure

Regional species populations now create a second pressure network beneath polity history.

Implemented chains:

`seasonal habitat + carrying capacity -> regional population growth or decline`

`producer abundance -> herbivore support -> predator support`

`prey collapse -> predator food stress -> predator decline or ecosystem collapse`

`regional pressure -> species migration -> new regional population establishment`

`all local populations gone -> global extinction`

### Evolutionary Pressure

Mutation and divergence now grow out of repeated simulation pressure rather than flavor-only randomness.

Implemented chain:

`repeated food stress -> mutation pressure toward diet flexibility, endurance, or smaller size`

`repeated predation pressure -> mutation pressure toward endurance, aggression, or sociality`

`repeated hunting pressure -> mutation pressure toward harder, more dangerous, or leaner prey populations`

`same-season species exchange into harsher terrain + ancestral habitat mismatch -> mutation pressure toward climate tolerance and broader diet`

`prolonged isolation -> higher divergence pressure and rare low-pressure drift`

`crowding near carrying capacity -> mutation pressure toward smaller size, lower fertility, or broader resource use`

`accumulated divergence -> mutation event -> ecological and hunting effects -> possible evolutionary turning point`

`ancestral mismatch + sustained mismatch pressure + trait-driven fit improvement + persistence -> regional adaptation milestone`

### Hunting Pressure

Settlement hunting is now driven by regional species populations rather than abstract animal biomass.

Implemented chains:

`regional prey abundance + polity hunting knowledge -> target selection`

`hunt success -> food stores + edible discovery + prey decline`

`dangerous prey -> hunter casualties + dangerous-prey knowledge`

`toxic prey -> toxic-food discovery + behavioral avoidance`

`repeated hunting pressure -> overhunting -> local extinction -> ecosystem instability`

Discovery outcomes now persist as explicit cultural knowledge on the polity rather than only as one-off events or scattered runtime flags.

Advancement outcomes remain separate capability gains that later systems can consume through `Capabilities`.

### Fragmentation Pressure

Internal strain can be made visible before or during a split.

Implemented chain:

`fragmentation threshold -> schism_risk`

`fragmentation -> polity_founded`

## Event Ancestry

Follow-up events preserve causal structure through:

- `parentEventIds`
- `rootEventId`
- `propagationDepth`

This means a later outcome can still be traced back to the original trigger event.

## State-Transition Rule

LivingWorld does not emit follow-up events every tick.

That rule still applies to ecology and hunting: most population churn stays structured/internal until it crosses into collapse, extinction, disaster, or other major transition.

Instead it favors:

- threshold crossings
- condition entry
- condition worsening
- condition improvement
- durable recovery
- accumulated pressure crossing mutation thresholds
- divergence milestones that suggest a future speciation branch

This is why yearly hardship transitions remain important: they summarize meaningful changes without narrating every month of unchanged suffering.

## Safeguards

- handler reactions are deterministic
- propagation is capped by depth
- propagation is capped by event count per source event
- identical follow-ups within a step are deduped
- chronicle presentation still filters and cools down repeated visible beats

## Player Experience

The chronicle still mainly shows effects, but those effects now come from a stronger underlying cause-and-effect chain. Structured history retains the intermediate steps for debugging and future history views.
