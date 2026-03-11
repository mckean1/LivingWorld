# LivingWorld Simulation Flow

LivingWorld runs the full world in monthly ticks. Player-facing output is live chronicle playback, not a yearly report.

## Monthly Flow

1. region ecology update
2. seasonal regional species population update on season boundaries
3. seasonal ecosystem predation and migration on season boundaries
4. seasonal settlement hunting on season boundaries
5. wild food gathering
6. settlement farming output
7. trade evaluation and food redistribution
8. food consumption and starvation tracking
9. migration evaluation and relocation
10. structured events emitted immediately into the canonical event pipeline
11. watch mode formats and displays qualifying focal-polity chronicle entries

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

## Default Watch Output

Default player mode shows:

- a fixed status panel at the top
- focal polity species in that panel
- a chronicle viewport beneath it sized from the available console height
- newest messages first
- concise `Major` and `Legendary` history only

Chronicle lines themselves remain short and do not append species after every polity name.

It does not show:

- yearly stat blocks
- broad annual diagnostics
- routine monthly bookkeeping
- most trade and telemetry internals
- most ecosystem and hunting bookkeeping
- repeated status reminders that do not mark a new historical transition

## Debug Output

`OutputMode.Debug` still prints yearly developer summaries and the full yearly event list for diagnostics and balancing work.
