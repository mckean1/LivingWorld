# LivingWorld

LivingWorld is a command-line autonomous world simulation where ecosystems, species, and polities evolve over time.

The player-facing experience is chronicle-first: the console follows one focal line of history, while the full simulation and structured event history continue underneath.

The current simulation phase now treats ecology, hunting, and polity history as one connected layer:

- regions track explicit per-species populations with carrying capacity, suitability, migration pressure, and recent ecological pressure
- seasonal ecosystem processing runs food-web interactions between producers, herbivores, omnivores, predators, and apex species
- settlement hunting draws food from those same regional populations, can discover edible or toxic prey, and can create overhunting or legendary hunts
- settlement hunting now executes from actual settlements in their own regions instead of multiplying one polity-region hunt by settlement count
- settlement farming now allocates real regional arable capacity across actual settlements, so multiple settlements in one region share land instead of double counting it
- regional populations now also accumulate mutation pressure, isolation, divergence, and local trait offsets rather than mutating the global species baseline directly
- mutation pressure comes from repeated food stress, predation, hunting, ancestral habitat mismatch, seasonal species-exchange shock, crowding near carrying capacity, and prolonged isolation
- evolved regional traits now feed back into hunting danger and difficulty, predator-prey outcomes, habitat fit, migration capability, and reproduction/survival rates
- watch mode shows the focal polity species in the fixed status panel
- watch mode also separates `Discoveries` from `Learned` advancements in that fixed status panel
- visible chronicle lines keep polity names short and do not append species by default
- hot-path systems now prefer cached id lookups and explicit invariant errors over raw LINQ `First(...)` crashes

## Core Principles

- Full-world simulation, focused player presentation
- Chronicle lines over yearly diagnostics
- Structured append-only history under every visible event
- Cause-and-effect propagation between systems
- Low-noise output that favors meaningful historical turning points

## Event Pipeline

LivingWorld now uses a propagation-aware event pipeline:

`simulation systems -> World.AddEvent -> EventPropagationCoordinator -> subscribed handlers -> World.Events + EventRecorded -> ChronicleEventFormatter + HistoryJsonlWriter`

`World.AddEvent(...)` remains the canonical entry point. Every event is timestamped, assigned an id, recorded in chronological order, and then made available to output sinks. Follow-up events are emitted through the same path, so chronicle lines and JSONL history share one source of truth.

## Structured Event Model

`WorldEvent` stores:

- `eventId`
- `rootEventId`
- `parentEventIds`
- `propagationDepth`
- `type`
- `severity`
- `scope`
- `year`, `month`, `season`
- polity, related polity, species, region, and settlement references
- `reason`, `narrative`, `details`
- `before`, `after`, `metadata`

This keeps causal ancestry visible in structured history without forcing every follow-up event into the player chronicle.

## Current Propagation Examples

- Food stress transitions can emit migration pressure, starvation risk, and food stabilization follow-ups.
- Learned agriculture can create cultivation expansion events and settlement stabilization momentum.
- Migration can create local tension or settlement momentum in the destination region.
- Fragmentation can emit a downstream `polity_founded` event for the child polity.

These are deterministic follow-ups based on state transitions, not random story injections.

## Chronicle Behavior

Default watch mode still shows:

- a fixed polity status panel docked at the top
- a reverse-chronological chronicle beneath it
- only `Major` and `Legendary` turning points by default
- key-driven inspection views layered over the same watch console
- an explicit paused/running state in the status panel

The status panel carries secondary context such as the focal polity species so chronicle lines can stay concise and story-like.
It also separates cultural discoveries from learned advancements so the player-facing UI matches the simulation terminology.
Watch-mode syntax coloring is structure-first and token-aware: it colors years, actor names, regions, knowledge items, and true status outcomes, but it does not color arbitrary narrative prose from substring matches inside larger phrases.

The main chronicle continues to favor:

- migration and relocation beats
- learned advancements such as agriculture
- settlement founding and durable consolidation
- food hardship entry, escalation, and recovery
- legendary hunts, major overhunting, and severe ecosystem collapses
- rare major mutation lineages, strong regional adaptation, and true evolutionary turning points when they matter to the focused historical line
- fragmentation, collapse, and focus handoff events

Internal follow-up events such as migration pressure, starvation risk, cultivation growth, local tension, minor mutation drift, and isolation milestones remain structured-first unless they rise to the level of a true historical turning point.
Mutation reacts to same-season regional species exchange from the ecology pipeline, not to the later monthly polity migration step.
Polity migration still relocates the whole polity network for now, but it now relocates the polity's actual settlement records too so settlement-grounded systems stay coherent.
Regional adaptation events now emit on meaningful adaptation milestones rather than on repeated reaffirmation of the same condition, and chronicle presentation applies a dedicated adaptation cooldown key so the same species-region adaptation beat does not spam the live feed.

## Watch Controls

Watch mode now supports lightweight inspection without leaving the live simulation view:

- `Space` toggles `RUNNING` / `PAUSED`
- `1` Chronicle
- `2` My Polity
- `3` Current Region
- `4` Known Regions
- `5` Known Species
- `6` Known Polities
- `7` World Overview
- `Tab` cycles the main top-level views
- `Up` / `Down` move list selection or scroll the active screen where relevant
- `Enter` inspects the selected list item or the current focal polity / region when supported
- `Esc` returns from a detail screen to the previous list

Phase 1 visibility rules are intentionally conservative and grounded:

- `Known Regions` uses the focal polity's settlement regions, its current center region, and directly connected neighboring regions
- `Known Species` uses species currently present in those known regions
- `Known Polities` uses active polities occupying those known regions

These views are observational only. They do not create simulation events or allow direct control over the world.

## Propagation Safeguards

- systems emit on meaningful transitions, not every tick
- the coordinator dedupes identical follow-up events within a propagation step
- propagation depth is capped
- total emitted events per source event are capped
- chronicle cooldowns still apply after storage
- source systems such as mutation also suppress repeated emissions when no new adaptation or divergence milestone has been reached

This keeps the world explainable without turning the chronicle into telemetry.

## Structured History

Important events are stored as append-only JSONL records.

Default path:

- `logs/history-{timestamp}.jsonl`

The JSONL history includes causal ids and propagation depth, so downstream debugging can trace:

`effect -> parent event -> root event`

## Runtime Options

Default mode is watch mode. Useful flags:

- `--fast` for no playback delay
- `--delay-ms <n>` for slower chronicle playback
- `--buffer-size <n>` to raise the retained chronicle history floor
- `--focus-polity <id>` to watch a specific polity
- `--debug` to restore developer-oriented yearly summaries and raw yearly event listings

Manual pause via `Space` is available during normal watch mode. It pauses simulation advancement while still allowing navigation across watch views.

Example:

```powershell
dotnet run --project LivingWorld -- --years 120 --delay-ms 250
```
