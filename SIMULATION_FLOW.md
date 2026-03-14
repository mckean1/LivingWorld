# LivingWorld Simulation Flow

LivingWorld runs the full world in monthly ticks. Player-facing output is live chronicle playback, not a yearly report.
The watch UI can now swap between chronicle and inspection screens without changing the simulation state it observes.

The default seed world feeding that loop is now denser by default: `36` regions, `31` species, and `10` starting polities.
Those entities are still range-limited and region-grounded before month one begins.

## Monthly Flow

1. region ecology update
2. seasonal regional species population update on season boundaries
3. seasonal ecosystem predation, founder migration, predator founder establishment/collapse, and regional species exchange on season boundaries
4. seasonal settlement hunting on season boundaries
   - each settlement hunts in its own region
5. seasonal mutation, divergence, and speciation update on season boundaries using the just-resolved species exchange state
   - speciation now requires sustained readiness and descendant-species stabilization, so new species do not immediately create another synchronized burst
6. seasonal extinction cleanup and biomass sync on season boundaries
7. wild plant gathering
8. settlement farming output
   - each settlement farms in its own region and shares that region's arable capacity with all other local settlements
9. trade evaluation and food redistribution
10. food consumption and starvation tracking
11. migration evaluation and relocation
12. structured events emitted immediately into the canonical event pipeline
13. watch mode formats and displays qualifying focal-polity chronicle entries
14. watch input can change views at any time
15. watch mode advances the next month only when its timed step cadence is due
16. rendering occurs on invalidation rather than after every loop pass; when paused, input continues while monthly advancement is held

Because starting polities now begin with a home settlement anchor, step 4 can produce real local hunting and discovery pressure from the opening season instead of waiting for a later founding roll.
Animal food no longer enters at step 7. It enters only at step 4 through species-level hunting, while step 7 is now plant-foraging only.
The same opening pass now relies on stronger wildlife seeding and herbivore growth rather than on any abstract regional animal-food reserve, so region screens should show healthier `AnimalBiomass` because real consumer populations are healthier.

The migration step at item 11 is polity migration, not the regional species exchange consumed by mutation at item 5.
For now, polity migration relocates the polity's settlement records as one network so settlement-grounded systems remain coherent.
Seasonal species exchange at items 2-6 is also the main long-run recovery path for locally depleted wildlife, because recolonization now rebuilds real populations rather than refilling a separate animal pool.
That same seasonal pass is now also where descendant species can appear, only after local pressure, exchange, persistence, and stabilization gates are already known.
That ecology-side migration is role-specific: herbivores and omnivores can open suitable adjacent frontiers first, while predators and apex populations generally follow only once prey support exists in the destination region.
Predator founders then pass through a short establishment window where strong prey support lets them grow into real local populations, while weak support makes them collapse back out naturally.

## Year-End Flow

When `Month == 12`:

1. increment polity age
2. update population
3. roll advancement discovery
4. update settlement progression
5. resolve fragmentation
6. evaluate polity stage progression
7. apply annual agriculture events
8. apply annual trade maintenance
9. evaluate annual hardship transitions and emit food-stress events
10. remove collapsed polities
11. validate lineage focus and emit any handoff event
12. persist resolved year-end food-state snapshots
13. refresh the watch-mode status panel
14. reset annual counters

## Event Pipeline

`systems -> World.AddEvent -> World.Events + EventRecorded`

Sinks:

- `HistoryJsonlWriter` writes append-only structured history
- `HistoryJsonlWriter` now batches flushes to reduce write amplification while keeping append-only semantics
- `ChronicleEventFormatter` applies chronicle severity filtering, visibility weighting, and semantic cooldown suppression
- `ChronicleWatchRenderer` redraws the watch console with newest entries first

Simulation emits and records events before chronicle presentation. The chronicle is not the source of truth.
Chronicle pacing is now non-blocking: visible-event recording no longer sleeps inside the event path, so input responsiveness does not depend on whether time is paused.
The chronicle formatter now distinguishes between repeated same-state reminders and real changed-state transitions for noisy event families, which keeps live playback readable during busy eras without thinning structured history.
That formatter also has a fallback narrative-based state key for visible families without a custom semantic signature, so exact repeated lines are still treated as the same visible state.
Year-end focus resolution now also consumes the current year's rolling event cache instead of filtering the full historical event list every year.

## Default Watch Output

Default player mode shows:

- a fixed status panel at the top
- explicit `RUNNING` / `PAUSED` state in that panel
- focal polity species in that panel
- the currently active watch view in that panel
- separate `Discoveries` and `Learned` rows in that panel
- a chronicle viewport beneath it sized from the available console height
- newest messages first
- concise `Major` and `Legendary` history only
- key-driven inspection screens for polity, region, species, polity lists, and world overview
- shared discovery-aware visibility across all watch inspection screens
- `My Polity` remains the focal polity's expanded screen; pressing `Enter` there does not route into the generic foreign-polity detail path

Chronicle lines themselves remain short and do not append species after every polity name.
The intended fuller-world opening is now more active in years `0-20`: home-anchor hunting, early food relief or hardship, migration under real pressure, and faster first consolidations for viable lineages.
Chronicle scrollback now retains a deeper in-memory buffer, and Left/Right paging is available for both chronicle history and list-heavy inspection screens.

It does not show:

- yearly stat blocks
- broad annual diagnostics
- routine monthly bookkeeping
- most trade and telemetry internals
- most ecosystem and hunting bookkeeping
- most mutation pressure accumulation, isolation tracking, and minor trait drift
- repeated status reminders that do not mark a new historical transition

## Debug Output

`OutputMode.Debug` still prints yearly developer summaries and the full yearly event list for diagnostics and balancing work.
## Phase 12 Monthly Food Redistribution

Monthly food resolution now has an extra settlement step:
1. food gathering and farming update polity stores
2. consumption resolves monthly polity shortages
3. settlement food states are calculated from produced food, stored food, and required food
4. surplus settlements send limited aid to deficit or starving settlements in the same polity
5. migration and other downstream pressures see the post-aid settlement state

This keeps cause and effect local: a breadbasket settlement can relieve a nearby shortage, while remote settlements may still remain starving after transport loss.

## Phase 13/14 Monthly Managed-Food Flow

The monthly food layer now includes an earlier domestication and cultivation step:

1. wild gathering updates immediate plant supply
2. local familiarity with useful nearby plants and animals increases
3. domestication candidates and cultivable plants can be discovered from repeated interaction
4. settlement farming resolves with cultivated crops modifying yield and seasonal resilience
5. managed herds add reliable local food before later hardship effects
6. trade, aid, consumption, and migration react to that updated food position

This creates a historical transition path instead of a single jump: foraging and hunting -> managed local species -> early agriculture -> more stable settlement life.

## Phase 17 Monthly Material Flow

The monthly material layer now follows this order inside a polity:

1. discover region-linked useful materials when local abundance is high enough
2. extract raw materials from settlement regions using labor, capability, hardship, and tool modifiers
3. convert stockpiles through short production recipes
4. preserve part of food surplus when salt and storage capability exist
5. apply monthly wear to durable goods
6. classify each material into deficit, stable, or surplus states
7. redistribute critical material surplus within the polity using same-region, adjacent-region, then closest routing
8. record detailed per-material shortages and convoy failures in structured history
9. emit grouped settlement-level crisis turns when multiple related material shifts happen together
10. surface only major material transitions such as a broader crisis beginning, worsening, easing, or a settlement becoming known for a craft
11. apply a final player-facing dedupe identity when recent visible major-event summaries are collected, so repeated same-year lines do not reappear through a different view path
12. keep economy identity chronicle turns stricter than internal economy labels: `known for` and trade-good milestones require mature settlements, sustained monthly confirmation, and stronger escalation than the hidden pressure model itself

This keeps the physical economy legible:

`Regional Abundance -> Extraction -> Local Stockpile -> Production -> Consumption / Bonuses -> Pressure Classification -> Redistribution`

Phase 18 now extends that flow with a hybrid economy layer:

- internal pressure scoring evaluates need, availability, value, opportunity, and export-readiness after stockpile targets are known
- extraction and recipe choice use those smoothed signals so settlements react to scarcity, surplus, and local opportunity without thrashing every month
- redistribution still moves real material stockpiles, but receiver priority now reflects economic value pressure as well as reserve deficit
- player-facing screens summarize the result through readable labels rather than raw price numbers
- visible identity-style economy events now sit one layer higher: bootstrap baselines remain hidden, legitimate post-bootstrap Year 0 milestones are still allowed, but only after age, persistence, and anti-stacking gates confirm that the settlement has earned a durable reputation

The canonical next roadmap sequence is:

- Phase 19 - extend exchange across polity boundaries through explicit trade routes and dependencies
- Phase 20 - convert material output into lasting settlement infrastructure and construction sinks
- Phase 21 - let those routes, dependencies, and shortages feed diplomacy, raiding, and conflict events

Player-facing chronicle dedupe cleanup and later `Discoveries` / `Learned` full-list cleanup remain secondary UI follow-up work alongside those deeper simulation phases.
