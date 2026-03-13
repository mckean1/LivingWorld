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

Settlement hardship follows the same pattern at a smaller scale:

`food deficit persists -> starvation stage changes -> aid failure or recovery event`

No chronicle beat should be emitted for a settlement that is merely still starving in the same way it was before. The event belongs to the transition, not the steady state.
The same rule now applies one step earlier in time: initialization-created baseline state is not chronicle history. Bootstrap can seed settlement pressure, shortage, trade-good, specialization, and starvation baselines internally, and only later world change should narrate those conditions as history.

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
`strong prey support + suitable habitat -> predator founder establishment -> stable regional predator population`

`broader suitable herbivore ranges + stronger ecological seeding -> richer early prey base`

`weak prey support or poor habitat -> predator founder failure -> local predator collapse`

`prey collapse -> predator food stress -> predator decline or ecosystem collapse`

`regional pressure -> species migration -> new regional population establishment`

`surviving neighboring population + open suitable habitat -> wildlife recolonization`

`healthy herbivore source + suitable adjacent producer-rich region -> founder population -> later local food web growth`

`prey-established frontier + predator prey support -> predator follow migration -> predator founder population`

`predator founder population + sustained prey support -> predator establishment`

`predator founder population + repeated food shortage -> founder collapse`

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

`sustained isolation + durable divergence + viable local population + readiness + species maturity -> descendant species appears in one region`

`new descendant species -> stabilization window + reset readiness -> later independent divergence path`

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
- chronicle-visible hardship, recovery, migration, and ecology beats only when the tracked visible band actually changes or enough quiet time has passed
- as a final presentation safeguard, exact repeated visible lines for the same actor are also suppressed even when a family has not been given a custom semantic state key yet

This is why yearly hardship transitions remain important: they summarize meaningful changes without narrating every month of unchanged suffering.
The fuller-world opening is therefore tuned by improving real local anchors, homeland support, and viability, not by inventing disconnected drama.

## Safeguards

- handler reactions are deterministic
- propagation is capped by depth
- propagation is capped by event count per source event
- identical follow-ups within a step are deduped
- chronicle presentation still filters and cools down repeated visible beats
- bootstrap-tagged setup events are filtered from player-facing chronicle surfaces even when they remain part of canonical/internal history
- hot-path systems now prefer cached lookup snapshots and clearer invariant failures so broken ids surface as explicit simulation problems rather than generic LINQ crashes
- sparse ecology storage, buffered history writes, and current-year event caches are now also explicit performance safeguards so long-run cause-and-effect remains traceable without late-game quadratic overhead

## Player Experience

The chronicle still mainly shows effects, but those effects now come from a stronger underlying cause-and-effect chain. Structured history retains the intermediate steps for debugging and future history views.
The new watch inspection UI reads that same state and event history without adding extra causes of its own. Pausing or changing views affects presentation timing only, not simulation causality.
Responsive watch input follows the same rule: the loop now separates input polling, timed month advancement, and redraw invalidation so player interaction does not distort or accelerate underlying cause-and-effect.
The Phase 8 visibility pass keeps that same boundary: screen content now flows through a shared focal-polity knowledge snapshot, so hiding unknown regions, species, and polity knowledge is a presentation rule built on existing state rather than a new simulation-side mechanic.
## Phase 12 Cause and Effect

Settlement aid follows a strict causal sequence:
1. settlements receive monthly production and store shares from already-resolved food systems
2. each settlement computes a local food balance
3. only true surplus settlements can export aid
4. aid first targets the closest needy settlements inside the same polity
5. distance removes part of the convoy through transport loss
6. the receiver's post-aid food state determines whether relief succeeded or failed

This keeps stories grounded. Nearby breadbasket settlements can keep a frontier camp alive through winter, while remote camps may still starve if distance friction or insufficient surplus limits what arrives.

## Phase 13/14 Cause and Effect

Domestication and cultivation follow the same grounded chain:

1. settlements repeatedly interact with nearby species through hunting, gathering, and coexistence
2. only suitable species accumulate enough familiarity to become candidates
3. discoveries reveal that a species is manageable or cultivable
4. already learned capability determines whether that knowledge can be organized into herding or cultivation
5. managed food then changes later settlement food balance, hardship, migration pressure, and stabilization outcomes
6. the chronicle only records the transition into an established managed-food state, not each later year that state continues unchanged

This preserves the terminology split:

- discovering a useful species is `Discovery`
- organizing herding or agriculture is `Learned` capability plus local implementation

## Phase 17 Cause and Effect

Material economy now follows the same grounded chain:

1. a region provides abundance for specific physical materials
2. a settlement with enough labor and learned capability extracts those materials
3. local stockpiles are converted into useful goods through simple recipes
4. tools, pottery, and preserved food change later farming, hunting, spoilage, and hardship outcomes
5. only true surplus moves through same-polity convoys, with distance reducing what arrives
6. detailed per-material shortages and convoy failures are still recorded as operational history
7. the main chronicle sees grouped settlement-level crisis turns only when the broader material state actually changes

This keeps the simulation concrete:

- no prices decide outcomes
- no merchant actor is invented to explain movement
- convoy relief, craft emergence, and shortage pressure all come from stockpiles, capability, geography, and distance
- player-facing major-event views may collapse equivalent same-year outcomes into one visible beat, but the underlying structured event chain remains intact for debugging and history tools

## Phase 18 Cause and Effect

Economy interactions now follow the same grounded chain:

1. settlement stockpiles, reserve targets, and recent usage establish hidden need and availability pressure
2. local abundance, downstream usefulness, and sustained surplus build internal value and opportunity signals
3. those signals shift extraction, recipe choice, and convoy priority without introducing money or explicit market screens
4. smoothing and confirmation rules prevent one noisy month from fully flipping a settlement's production identity
5. persistent value or surplus can create major turns such as a material becoming highly valued or a settlement becoming known for a trade good
6. missing inputs can bottleneck favored output, leaving a visible explanation for why later resilience or productivity weakened
7. bootstrap seeding establishes baseline economy identity without treating initial computed state as a historical turning point

This preserves LivingWorld's core rule:

- the simulation remains concrete and pressure-based
- the UI shows readable summaries rather than modern-market dashboards
- every major economy turn still traces back to stockpiles, need, geography, capability, and logistics
