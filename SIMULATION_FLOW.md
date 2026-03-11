# LivingWorld Simulation Flow

LivingWorld runs the full world in monthly ticks. Player-facing output is live chronicle playback, not a yearly report.

## Monthly Flow

1. region ecology update
2. wild food gathering
3. settlement farming output
4. trade evaluation and food redistribution
5. food consumption and starvation tracking
6. migration evaluation and relocation
7. structured events emitted immediately into the canonical event pipeline
8. watch mode formats and displays qualifying focal-polity chronicle entries

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
- a chronicle viewport beneath it sized from the available console height
- newest messages first
- concise `Major` and `Legendary` history only

It does not show:

- yearly stat blocks
- broad annual diagnostics
- routine monthly bookkeeping
- most trade and telemetry internals
- repeated status reminders that do not mark a new historical transition

## Debug Output

`OutputMode.Debug` still prints yearly developer summaries and the full yearly event list for diagnostics and balancing work.
