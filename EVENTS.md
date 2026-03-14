# LivingWorld Event System

LivingWorld uses a canonical structured event model for simulation events, then routes those events into separate storage, propagation, and presentation paths.

## Event Model

Core fields:

- `eventId`
- `rootEventId`
- `parentEventIds`
- `propagationDepth`
- `simulationPhase`
- `origin`
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
That includes bootstrap-created baseline events: initialization may register canonical setup transitions internally, but player-facing chronicle views treat them as setup context rather than as live history.
`origin` now makes that distinction explicit: bootstrap baseline/setup events are not the same thing as post-bootstrap live transitions.

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
- `species_population_recolonized`
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
- `new_species_appeared`
- `trade_transfer`
- `trade_relief`

Settlement-grounded systems should prefer true settlement references where available so structured history can answer not just which polity acted, but where the local action actually happened.
The same ecology rule now applies to food sourcing: generic foraging events describe plant gathering pressure, while animal food gains should always be traceable to an actual hunted species population.
Because richer regions now begin with a sturdier prey layer, hunting and wildlife-recovery events are more likely to reflect meaningful regional abundance shifts instead of one tiny starting population blinking in and out.
Seasonal fauna migration now records those shifts in structured history through `species_population_established`, but the main chronicle should still only surface the rare expansions that materially change a region or lineage context.
The same restraint applies to predator founder success or failure: ordinary colony sorting should stay in structured/debug history unless it clearly changes the focal historical story.
Phase 8 watch inspection does not add any new event types for UI navigation, pausing, paging, or screen changes. Those remain presentation-only.

Major biology-turn types that may surface when the focused line is meaningfully affected:

- `species_population_major_mutation`
- `species_population_evolutionary_turning_point`
- `new_species_appeared`

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
- rare descendant-species appearances tied to the focused historical line
- fragmentation, collapse, polity founding, and focus handoff beats

Player-facing coloring is intentionally narrower than event semantics. Watch mode colors explicit semantic units such as years, actor names, places, knowledge items, and severe status words, but leaves ordinary descriptive prose uncolored.
Most recolonization, local extinction, and minor mutation beats remain structured-first even when they now carry richer lineage/source metadata; only unusually consequential descendant-species appearances should surface live.
Biology families now also use stricter source-side milestone guards: prolonged isolation emits on wider milestone bands, minor mutation events observe year-level cooldowns, and descendant-species appearances require stabilization-aware speciation gates.
The chronicle formatter now also treats some event families as semantic transition bands rather than plain repeated messages. For example, repeated recovery into the same hardship state is cooled down more aggressively than a true hardship escalation or a later stable recovery.

## Anti-Spam Rules

- systems emit events on state transitions, not repeated unchanged conditions
- bootstrap setup is not itself a player-facing historical transition; baseline seeding can emit canonical events, but those events are filtered from live chronicle surfaces by simulation phase
- prior-state trackers should be seeded during bootstrap so the first active comparison does not turn established specialization, trade-good identity, or crisis normalization into fake new history
- economy identity milestones are also stricter than internal economy state: visible `settlement_specialized` and `trade_good_established` beats now require minimum settlement age, sustained monthly confirmation, and stronger visible thresholds before they become live chronicle history
- the propagation coordinator dedupes identical follow-up events inside one step
- propagation depth is capped
- total events per source event are capped
- chronicle cooldowns still suppress repeated visible beats for the same actor scope
- visible cooldowns are now semantic and family-specific, so repeated same-state events are suppressed more strongly than changed-state transitions
- visible families without an explicit semantic key still use a fallback normalized-narrative state key by actor scope, which suppresses exact repeated chronicle lines without hiding distinct turning points
- related economy identity families now also share one visible presentation family for the same settlement-material pair, which helps prevent back-to-back `known for` and trade-good lines from stacking in the same early window
- source systems may also suppress repeat emissions before they ever reach chronicle presentation when no new milestone has been crossed
- settlement aid failure follows the same rule: `aid_failed` and starvation recovery only emit when a settlement enters starvation, worsens to a deeper starvation stage, or exits starvation
- player-facing major-event summaries now also apply a final dedupe pass using the same visible event identity as chronicle presentation, so exact or equivalent same-year lines do not reappear in recent-event lists
- history writing is still append-only, but flushes are now batched so heavy biology years do not force a synchronous disk flush per event

## Example JSONL Record

```json
{"eventId":42,"rootEventId":40,"parentEventIds":[41],"propagationDepth":2,"year":118,"month":12,"season":"Winter","type":"food_stabilized","severity":"Major","scope":"Polity","narrative":"Red River Clan stabilized after hardship.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"hardshipTier":"Famine"},"after":{"hardshipTier":"Stable"},"metadata":{}}
```

## Phase 13/14 Event Notes

Domestication and cultivation events follow the same rules:

- they represent state changes, not monthly reminders
- they carry settlement, polity, region, and target-species context where relevant
- they remain eligible for propagation through the normal coordinator

New high-signal beats include discovery of manageable animals, discovery of cultivable plants, herd establishment, crop establishment, domestication spread, and the first annual transition into established managed-food stability. That stabilization beat should not repeat while the polity remains in the same established state.

## Phase 17 Event Notes

Material events follow the same transition-first rules:

- extraction and routine production can remain structured-first
- shortages only emit when a material crisis begins, worsens, or resolves
- convoy failure is keyed to the shortage transition, not repeated every cycle of the same unresolved deficit
- preservation, toolmaking, specialization, and critical convoy relief are the main chronicle-facing material milestones
- when several material shortages shift together for one settlement in the same tick, the player-facing chronicle now prefers one grouped `material_crisis_*` beat while the underlying per-material events remain in structured history

Material follow-up events can still propagate through the canonical coordinator. Preservation and critical material relief can support later food stabilization, while toolmaking and specialization can strengthen settlement stability without bypassing the event pipeline.

## Phase 18 Event Notes

Economy-interaction events keep the same hybrid rule:

- internal pressure, value, and opportunity math stays simulation-side
- player-facing output uses readable labels and rare historical turns rather than price telemetry
- structured detail still records why a material became important, why output shifted, or what bottleneck constrained it

New structured event types:

- `material_highly_valued`
  - a material crossed into a true high-value state for one settlement
- `production_focus_shifted`
  - a settlement's smoothed production focus moved toward a different good
- `production_bottleneck_hit`
  - an important output faltered because a key input stayed constrained
- `trade_good_established`
  - one settlement developed sustained surplus in a good that remained valuable beyond immediate local use

Default visibility intent:

- `production_focus_shifted` and `production_bottleneck_hit` are usually structured-first unless a later pass intentionally escalates them
- `material_highly_valued` and `trade_good_established` can surface when they become real major historical turns for the focused line
- duplicate-safe major-event presentation still applies, so the live chronicle and recent-major-event views should not repeat the same settlement-material turn in one year
- those same economy-turn families are now bootstrap-aware, so initial shortage, trade-good, highly valued, specialization, or convoy-failure state can be recorded internally without creating a startup chronicle dump
- `settlement_specialized` and `trade_good_established` now also rely on mature settlement age, multi-month persistence, and related-signal anti-stacking so the chronicle reserves `became known for ...` wording for durable identity rather than early fluctuations
