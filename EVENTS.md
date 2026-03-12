# LivingWorld Event System

LivingWorld uses a canonical structured event model for simulation events, then routes those events into separate storage, propagation, and presentation paths.

## Event Model

Core fields:

- `eventId`
- `rootEventId`
- `parentEventIds`
- `propagationDepth`
- `year`, `month`, `season`
- `type`, `severity`, `scope`
- `narrative`, `details`, `reason`
- `polityId`, `polityName`
- `relatedPolityId`, `relatedPolityName`
- `speciesId`, `speciesName`
- `regionId`, `regionName`
- `settlementId`, `settlementName`
- `before`, `after`, `metadata`

## Severity

- `Debug`
- `Minor`
- `Notable`
- `Major`
- `Legendary`

Structured history keeps the full event stream. Default chronicle playback only surfaces `Major` and `Legendary` events for the current focal line.

## Scope

Supported scopes:

- `Local`
- `Regional`
- `Polity`
- `World`

Handlers should only react to events relevant to their domain and scope.

Examples:

- settlement founding: `Local`
- migration between connected regions: `Regional`
- food hardship for one polity: `Polity`
- future climate shifts: `World`

## Storage Versus Propagation Versus Presentation

`World.AddEvent(...)` is the source of truth.

- `World.Events` remains append-only
- `EventPropagationCoordinator` processes follow-up events
- `HistoryJsonlWriter` persists the structured stream
- `ChronicleEventFormatter` applies player-facing filtering
- `ChronicleWatchRenderer` only renders what survives presentation filtering

Player-facing species context remains available, but it lives in the fixed watch-mode status panel rather than being appended to every chronicle line.
The same watch-mode panel also separates cultural discoveries from learned advancements instead of collapsing them into one ambiguous knowledge field.
Settlement references are now more often real execution sites rather than fabricated polity-region placeholders, especially for hunting, farming follow-through, and trade endpoints.
Watch navigation and pause controls are presentation-only concerns. Switching views or pausing the watch UI does not emit canonical `WorldEvent` records.
Chronicle pacing is also presentation-only now: delaying visible playback no longer blocks the canonical event path or input polling.
Likewise, the denser default seed world does not emit synthetic setup events just to announce extra regions, species, or polities; the chronicle remains focused on consequential transitions after simulation begins.
The same rule applies to starting home settlements: they exist as initial world state so early locality systems can function, but they do not backfill fake founding events.

Suppressed chronicle events still remain available in structured history with their metadata and causal ancestry.

## Causal Links

Propagation adds:

- direct parent links through `parentEventIds`
- root ancestry through `rootEventId`
- step depth through `propagationDepth`

This allows history/debug tooling to follow:

`root event -> follow-up event -> later consequence`

## Current Important Event Types

Major chronicle-facing types:

- `migration`
- `settlement_founded`
- `settlement_consolidated`
- `learned_advancement`
- `food_stress`
- `food_stabilized`
- `legendary_hunt`
- severe `ecosystem_collapse`
- `fragmentation`
- `polity_founded`
- `stage_changed`
- `polity_collapsed`
- focus handoff events

Structured-first follow-up types:

- `migration_pressure`
- `starvation_risk`
- `cultivation_expanded`
- `settlement_stabilized`
- `schism_risk`
- `local_tension`
- `species_population_established`
- can also represent wildlife recolonization into an emptied neighboring region when migration restores a local population
- can also represent a predator or apex founder successfully opening a new prey-supported ecological frontier when that shift is historically meaningful
- `species_population_mutated`
- `species_population_isolated`
- `species_population_adapted_to_region`
- `predator_pressure`
- `prey_collapse`
- `hunting_success`
- usually points at the settlement that actually hunted in that region
- `hunting_disaster`
- `dangerous_prey_killed_hunters`
- `toxic_food_discovered`
- `edible_species_discovered`
- `overhunting_pressure`
- `local_species_extinction`
- `global_species_extinction`
- `trade_transfer`
- `trade_relief`

Settlement-grounded systems should prefer true settlement references where available so structured history can answer not just which polity acted, but where the local action actually happened.
The same ecology rule now applies to food sourcing: generic foraging events describe plant gathering pressure, while animal food gains should always be traceable to an actual hunted species population.
Because richer regions now begin with a sturdier prey layer, hunting and wildlife-recovery events are more likely to reflect meaningful regional abundance shifts instead of one tiny starting population blinking in and out.
Seasonal fauna migration now records those shifts in structured history through `species_population_established`, but the main chronicle should still only surface the rare expansions that materially change a region or lineage context.
The same restraint applies to predator founder success or failure: ordinary colony sorting should stay in structured/debug history unless it clearly changes the focal historical story.

Major biology-turn types that may surface when the focused line is meaningfully affected:

- `species_population_major_mutation`
- `species_population_evolutionary_turning_point`

`species_population_adapted_to_region` remains structured-first by default even though it is historically meaningful. It becomes chronicle-worthy only when severity and focal-line relevance justify it.
When it does surface, chronicle cooldown uses a dedicated scoped key built from species, region, reason, and any adaptation milestone metadata so repeated reaffirmations of the same adaptation state do not reappear as duplicate history beats.

## Chronicle Filtering Rules

Normal player-facing watch mode:

- follows one focal polity line and its handoffs over time
- shows concise historical beats rather than telemetry
- keeps storage chronological but renders the visible watch buffer newest-first
- suppresses noisy follow-up events by default

The main chronicle favors:

- migration and relocation turning points
- settlement founding and durable consolidation
- learned capability breakthroughs such as agriculture
- major hardship transitions such as shortages beginning and recovery
- memorable hunts and major ecological collapses
- rare evolutionary turns with strong local historical consequences
- fragmentation, collapse, polity founding, and focus handoff beats

Player-facing coloring is intentionally narrower than event semantics. Watch mode colors explicit semantic units such as years, actor names, places, knowledge items, and severe status words, but leaves ordinary descriptive prose uncolored.

## Anti-Spam Rules

- systems emit events on state transitions, not repeated unchanged conditions
- the propagation coordinator dedupes identical follow-up events inside one step
- propagation depth is capped
- total events per source event are capped
- chronicle cooldowns still suppress repeated visible beats for the same actor scope
- source systems may also suppress repeat emissions before they ever reach chronicle presentation when no new milestone has been crossed

## Example JSONL Record

```json
{"eventId":42,"rootEventId":40,"parentEventIds":[41],"propagationDepth":2,"year":118,"month":12,"season":"Winter","type":"food_stabilized","severity":"Major","scope":"Polity","narrative":"Red River Clan stabilized after hardship.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"hardshipTier":"Famine"},"after":{"hardshipTier":"Stable"},"metadata":{}}
```
