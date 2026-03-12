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

`broader suitable herbivore ranges + stronger ecological seeding -> richer early prey base`

`prey collapse -> predator food stress -> predator decline or ecosystem collapse`

`regional pressure -> species migration -> new regional population establishment`

`surviving neighboring population + open suitable habitat -> wildlife recolonization`

`healthy herbivore source + suitable adjacent producer-rich region -> founder population -> later local food web growth`

`prey-established frontier + predator prey support -> predator follow migration -> deeper regional food web`

`all local populations gone -> global extinction`

Those ecology chains now begin from biome-shaped initial ranges instead of every species starting everywhere, so migration and collapse have clearer geographic meaning from the opening years.
Fertile regions also now seed consumer populations from habitat suitability and ecological capacity more aggressively, so early producer-rich biomes usually carry visible herbivore life before predators and hunting begin trimming that abundance.

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

`further fit gain + stronger divergence + stronger persistence -> stronger adaptation milestone`

### Hunting Pressure

Settlement hunting is now driven by regional species populations rather than abstract animal biomass.
It is also settlement-local rather than polity-region-multiplied.
Starting polities now begin with a real home settlement anchor, so this hunting chain can operate from the opening months of the simulation.

Implemented chains:

`regional prey abundance + polity hunting knowledge -> target selection`

`settlement in region X -> local target selection in region X -> hunt success -> food stores + edible discovery + prey decline in region X`

`dangerous prey -> hunter casualties + dangerous-prey knowledge`

`toxic prey -> toxic-food discovery + behavioral avoidance`

`repeated hunting pressure -> overhunting -> local extinction -> ecosystem instability`

`neighboring surviving prey population -> recolonization migration -> prey return in a previously emptied region`

Discovery outcomes now persist as explicit cultural knowledge on the polity rather than only as one-off events or scattered runtime flags.

Advancement outcomes remain separate capability gains that later systems can consume through `Capabilities`.

Generic monthly food gathering no longer consumes `Region.AnimalBiomass`.
That removed the previous double-pressure bug where abstract animal harvesting and species-level hunting both depleted the same ecological layer.
The current balance pass keeps that architecture intact and instead raises early wildlife through seeding and growth tuning.
The next layer of wildlife recovery now comes from seasonal founder migration rather than from regeneration shortcuts: neighboring populations must exist, habitat must fit, and the founder population must then survive normal ecology rules.

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
- adaptation milestone transitions rather than repeated reaffirmation of the same adaptation state

This is why yearly hardship transitions remain important: they summarize meaningful changes without narrating every month of unchanged suffering.
The fuller-world opening is therefore tuned by improving real local anchors, homeland support, and viability, not by inventing disconnected drama.

## Safeguards

- handler reactions are deterministic
- propagation is capped by depth
- propagation is capped by event count per source event
- identical follow-ups within a step are deduped
- chronicle presentation still filters and cools down repeated visible beats
- hot-path systems now prefer cached lookup snapshots and clearer invariant failures so broken ids surface as explicit simulation problems rather than generic LINQ crashes

## Player Experience

The chronicle still mainly shows effects, but those effects now come from a stronger underlying cause-and-effect chain. Structured history retains the intermediate steps for debugging and future history views.
The new watch inspection UI reads that same state and event history without adding extra causes of its own. Pausing or changing views affects presentation timing only, not simulation causality.
Responsive watch input follows the same rule: the loop now separates input polling, timed month advancement, and redraw invalidation so player interaction does not distort or accelerate underlying cause-and-effect.
