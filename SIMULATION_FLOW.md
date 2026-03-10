# SIMULATION_FLOW.md

# LivingWorld Simulation Flow

The simulation runs in monthly ticks with yearly aggregation. The full world always simulates; only default presentation is focused.

---

## Monthly Flow

1. Region ecology update
2. Wild food gathering
3. Settlement farming output
4. Food consumption and starvation tracking
5. Migration evaluation and relocation
6. Structured events emitted when notable outcomes occur

---

## Year-End Flow (Month 12)

1. Increment polity age
2. Population update
3. Advancement discovery
4. Settlement progression
5. Fragmentation checks
6. Polity stage progression
7. Annual agriculture events
8. Annual food-stress events
9. Remove collapsed polities (`Population <= 0`)
10. Render yearly focused chronicle (narrative mode) or debug summary (debug mode)
11. Reset annual food stats

---

## Event Pipeline During Flow

`systems -> World.AddEvent -> World.Events + EventRecorded`

Sinks:

- focused chronicle renderer (year-end)
- JSONL writer (immediate append)

---

## Focused Chronicle Output (Default)

Each year prints:

- header for focal polity (region, population delta, food, status, knowledge)
- `This Year` focal events (usually 1-5)
- optional `Notable Changes`
- optional `World Notes` (0-2 rare outside events)

---

## Debug Output

Debug mode keeps broad world summary and full yearly event list for simulation diagnostics.
