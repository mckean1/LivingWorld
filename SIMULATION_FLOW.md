# LivingWorld Simulation Flow

LivingWorld runs the full world in monthly ticks. Player-facing output is live chronicle playback, not a yearly report.
The watch UI can now swap between chronicle and inspection screens without changing the simulation state it observes.

The default seed world feeding that loop is now denser by default: `36` regions, `28` species, and `10` starting polities.
Those entities are still range-limited and region-grounded before month one begins.

## Monthly Flow

1. region ecology update
2. seasonal regional species population update on season boundaries
3. seasonal ecosystem predation and regional species exchange on season boundaries
4. seasonal settlement hunting on season boundaries
   - each settlement hunts in its own region
5. seasonal mutation and divergence update on season boundaries using the just-resolved species exchange state
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
- `ChronicleEventFormatter` applies chronicle severity filtering and cooldown suppression
- `ChronicleWatchRenderer` redraws the watch console with newest entries first

Simulation emits and records events before chronicle presentation. The chronicle is not the source of truth.
Chronicle pacing is now non-blocking: visible-event recording no longer sleeps inside the event path, so input responsiveness does not depend on whether time is paused.

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

Chronicle lines themselves remain short and do not append species after every polity name.
The intended fuller-world opening is now more active in years `0-20`: home-anchor hunting, early food relief or hardship, migration under real pressure, and faster first consolidations for viable lineages.

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
