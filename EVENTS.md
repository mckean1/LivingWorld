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
- `predator_pressure`
- `prey_collapse`
- `hunting_success`
- `hunting_disaster`
- `dangerous_prey_killed_hunters`
- `toxic_food_discovered`
- `edible_species_discovered`
- `overhunting_pressure`
- `local_species_extinction`
- `global_species_extinction`
- `trade_transfer`
- `trade_relief`

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
- fragmentation, collapse, polity founding, and focus handoff beats

## Anti-Spam Rules

- systems emit events on state transitions, not repeated unchanged conditions
- the propagation coordinator dedupes identical follow-up events inside one step
- propagation depth is capped
- total events per source event are capped
- chronicle cooldowns still suppress repeated visible beats for the same actor scope

## Example JSONL Record

```json
{"eventId":42,"rootEventId":40,"parentEventIds":[41],"propagationDepth":2,"year":118,"month":12,"season":"Winter","type":"food_stabilized","severity":"Major","scope":"Polity","narrative":"Red River Clan stabilized after hardship.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"hardshipTier":"Famine"},"after":{"hardshipTier":"Stable"},"metadata":{}}
```
