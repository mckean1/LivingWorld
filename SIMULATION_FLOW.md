# SIMULATION_FLOW.md

# LivingWorld Simulation Flow

The simulation runs in monthly ticks with yearly aggregation. The full world always simulates; only default presentation is focused.

---

## Monthly Flow

1. Region ecology update
2. Wild food gathering
3. Settlement farming output
4. Trade evaluation and food redistribution (nearby partners)
5. Food consumption and starvation tracking
6. Migration evaluation and relocation
7. Structured events emitted when notable outcomes occur

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
9. Annual trade dependency and link maintenance pass
10. Remove collapsed polities (`Population <= 0`)
11. Persist resolved year-end food-state snapshot for each active polity
12. Render yearly focused chronicle (narrative mode) or debug summary (debug mode)
13. Reset annual food stats

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
- `This Year` focal events (up to 3, collapsed summaries)
- optional `Notable Changes`
- optional `World Notes` (0-2 rare outside events)

Food transitions in `Notable Changes` compare persisted prior-year food state to current year-end resolved food state, so reset annual counters do not distort January start snapshots.
Chronicle rendering collapses multi-step migration paths into one yearly migration line and collapses food stress into one worst-condition yearly line.
Population micro-events are summarized via yearly population delta / notable change lines.
Trade stays food-first in v1: normal monthly transfers are mostly hidden from player output, while notable trade milestones (new link, shortage relief, annual dependency) can appear.
Current refinement details:

- internal-priority matching is evaluated before external trade
- reachability uses constrained multi-hop local networks
- continuity of existing links affects partner selection
- relief metrics include partial and full shortage mitigation

---

## Debug Output

Debug mode keeps broad world summary and full yearly event list for simulation diagnostics.
