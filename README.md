# LivingWorld

LivingWorld is a command-line world simulation where ecosystems, species, societies, and polities emerge over time.

The player-facing experience is chronicle-first: the console follows one focal line of history, while the full simulation and structured event history continue underneath.

Default world generation now starts from a simulated prehistory foundation rather than a civilization-ready snapshot:

- `36` connected regions on one early-continent landmass
- primitive ecological lineages seeded first, then extended through simulated evolution, social emergence, and player-entry evaluation
- startup now stops at a truthful focal-selection checkpoint instead of dropping straight into live play

The current simulation phase now treats ecology, hunting, and polity history as one connected layer:

- regions track explicit per-species populations with carrying capacity, suitability, migration pressure, and recent ecological pressure
- seasonal ecosystem processing runs food-web interactions between producers, herbivores, omnivores, predators, and apex species
- monthly wild gathering now forages plant biomass only, while animal food comes only from species-level hunting
- `Region.AnimalBiomass` is now a derived ecological summary of current non-producer populations rather than a separate consumable meat pool
- world generation now seeds broader herbivore and omnivore coverage, while ecosystem initialization scales early wildlife from habitat suitability and ecological capacity instead of tiny flat starts
- early producer abundance now gives herbivores more room to establish and grow before predator pressure becomes dominant
- world generation also now protects against fauna-empty fertile regions by expanding the nearest plausible herbivore cluster into those regions instead of leaving plant-only dead zones
- seasonal fauna migration now lets healthy neighboring populations found real new regional populations over time, so producer-only regions can develop fuller food webs decades after generation
- predator and apex migration now follows prey-supported frontiers rather than jumping blindly into empty regions
- predator founders now either establish into meaningful local populations when prey support is strong or collapse back out when support is weak, instead of lingering as permanent tiny seeds
- settlement hunting draws food from those same regional populations, can discover edible or toxic prey, and can create overhunting, recolonization pressure, or legendary hunts
- settlement hunting now executes from actual settlements in their own regions instead of multiplying one polity-region hunt by settlement count
- settlement farming now allocates real regional arable capacity across actual settlements, so multiple settlements in one region share land instead of double counting it
- starting polities now begin with a real home settlement anchor, so settlement-grounded hunting and early locality pressure exist from year zero
- regional populations now also accumulate mutation pressure, isolation, divergence, and local trait offsets rather than mutating the global species baseline directly
- descendant species now begin with a stabilization period and heavily damped inherited divergence/isolation readiness, so speciation remains a rare long-horizon outcome instead of a recursive doubling wave
- speciation now also requires species age, meaningful global population, sustained readiness, and durable isolation/divergence rather than only one threshold-crossing season
- isolated regions now also enforce root-lineage crowding pressure and a per-region lineage cooldown so the same ancestral branch cannot keep splitting every few decades forever
- neighboring wildlife populations can now recolonize empty suitable regions through the existing migration system instead of waiting for magical respawns
- mutation pressure comes from repeated food stress, predation, hunting, ancestral habitat mismatch, seasonal species-exchange shock, crowding near carrying capacity, and prolonged isolation
- strongly isolated high-divergence regional populations can still produce descendant species with tracked parentage, origin region/time, and inherited local ecological traits, but those descendant populations must first stabilize before they can ever branch again
- local extinction and global extinction are now explicit species-population outcomes with recolonization flowing back through the same neighboring founder-migration path
- evolved regional traits now feed back into hunting danger and difficulty, predator-prey outcomes, habitat fit, migration capability, and reproduction/survival rates
- region species storage is now sparse by default: meaningful active or historically relevant regional populations are tracked, while never-established region-species pairs are not materialized every season
- watch mode shows the focal polity species in the fixed status panel
- watch mode also separates `Discoveries` from `Learned` advancements in that fixed status panel
- watch mode now uses a shared knowledge snapshot so chronicle, region, species, polity, and world-overview screens all read from the same discovery-aware visibility rules
- visible chronicle lines keep polity names short and do not append species by default
- hot-path systems now prefer cached id lookups and explicit invariant errors over raw LINQ `First(...)` crashes
- generation defaults are centralized in `WorldGenerationSettings`, while curated biome/name/species templates live in `WorldGenerationCatalog`
- ecology spread pacing is now centralized in `EcosystemSettings` so founder size, migration thresholds, prey support, and cooldowns can be tuned without rewriting the pipeline
- early-world liveliness is now tuned through centralized homeland-support, polity-spacing, focal-viability, and starting-anchor settings rather than scattered magic numbers

## Runtime Architecture

LivingWorld now enters play through the canonical `PrehistoryRuntimePhase` ladder: `WorldSeeding`, `BiologicalDivergence`, `SocialEmergence`, `WorldReadinessReview`, `FocalSelection`, `SimulationEngineActivePlay`, and `GenerationFailure`. `StartupProgressRenderer` owns the console through startup and prehistory, keeping initialization separate from the live chronicle. If the world produces real viable starts, time freezes in `FocalSelection` while the player reviews the surfaced candidate pool. If it does not, runtime stops honestly in `GenerationFailure` instead of inventing a start.

The startup handoff is built from the exact selected end-of-month prehistory state. `ActivePlayHandoffState` preserves identity, current condition, routes, settlements, neighbors, discoveries, learned capabilities, visibility truth, unresolved shocks, and a compact inherited prehistory summary. `World.BeginActiveSimulation` starts the live chronicle only after that handoff package is recorded, and the `SimulationEngine` begins paused so the inherited start can be inspected before time resumes.

Readiness and candidate selection stay evaluator-owned. Observer artifacts such as `PeopleHistoryWindowSnapshot`, `RegionEvaluationSnapshot`, and `NeighborContextSnapshot` hold facts only. `WorldReadinessReport` resolves `ContinuePrehistory`, `EnterFocalSelection`, `ForceEnterFocalSelection`, or `GenerationFailure` from those facts, and `PrehistoryCandidateSelectionEvaluator` surfaces viable starts without weakening hard truth or blurring `Discoveries` with `Learned`.

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
- `simulationPhase`
- `origin`
- `type`
- `severity`
- `scope`
- `year`, `month`, `season`
- polity, related polity, species, region, and settlement references
- `reason`, `narrative`, `details`
- `before`, `after`, `metadata`

This keeps causal ancestry visible in structured history without forcing every follow-up event into the player chronicle.
Bootstrap-created baseline events are explicitly tagged as `Bootstrap` phase state, and events now also carry an `origin` that distinguishes bootstrap baseline setup from true live transitions. That keeps startup seeding available to internal history/debugging without narrating it as live chronicle history.

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

## Active-Play Handoff Contract

PR-6 now treats `ActiveControl` as an intentional runtime/player-control overlay on top of the underlying polity-backed `SimulationEngine` model.

- canonical focal-selection entry always runs through the full handoff builder path
- the selected start is the exact end-of-month prehistory state the player chose; handoff conversion does not advance another month
- the `SimulationEngine` begins paused so inherited context can be inspected before time resumes
- the handoff package is the authoritative bootstrap source for entry discoveries, learned capabilities, and known regions/species/polities
- discoveries remain world knowledge, while learned capabilities remain gained advancements
- `Society` / `Polity` control conversion and `Network` / `AnchoredHomeRange` / `TerritorialCore` spatial interpretation are descriptive, not strength-inflating rewrites of simulation truth
- player-facing watch screens consume `ActiveControl` as the runtime control boundary, while polity lookups remain the backing simulation data underneath that overlay
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
Chronicle presentation now also uses semantic state signatures for noisy families such as hardship, recovery, migration, settlement stabilization, and regional ecology turns. The live feed can surface a changed state sooner than a repeated same-state reminder, while identical or near-identical beats remain in structured history only.
Visible families that do not yet have a custom semantic profile still pass through a fallback chronicle state key built from actor scope plus normalized narrative, so exact repeated lines do not leak into the player feed year after year.
Visible major-event summaries now apply the same player-facing dedupe identity as the live chronicle, so recent-event panels do not repeat the same settlement recovery or crisis beat multiple times in one year unless the visible state is meaningfully different.
Chronicle presentation now also ignores bootstrap-tagged baseline events. Initialization can establish shortage, trade-good, specialization, convoy-failure, or hardship state internally, but only post-bootstrap transitions are eligible for the live chronicle and recent major-event summaries.
Bootstrap seeding also initializes prior economy/material identity deeply enough that the first active comparison is against seeded baseline state, not against an empty tracker that would mistake old conditions for new history.
As a final safety net, non-live event origins are also refused by player-facing chronicle admission so bootstrap-derived economy/material/reputation lines cannot leak through grouped or summary presentation even if an upstream producer misbehaves.

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
- `Left` / `Right` page through chronicle/detail scrollback or jump faster through list screens
- `Enter` inspects the selected list item or the current focal region when supported
- `Enter` on `My Polity` intentionally keeps the current screen, because `My Polity` is already the focal polity's expanded player-facing view and should never drill into a less detailed generic polity screen
- `Esc` returns from a detail screen to the previous list

Phase 1 visibility rules are intentionally conservative and grounded:

- `Known Regions` uses the focal polity's settlement regions, its current center region, and directly connected neighboring regions
- `Known Species` uses species currently present in those known regions
- `Known Polities` uses active polities occupying those known regions

These views are observational only. They do not create simulation events or allow direct control over the world.
Foreign polity detail intentionally hides that polity's private discoveries and learned capabilities unless it is the focal polity.
The focal polity never loses visibility by drilling into detail: `My Polity` retains full player-facing access to discoveries, learned advancements, food, pressure, and settlement information.
World Overview now summarizes only known regions, known species, known polities, and visible major events inside the focal polity's current horizon.
`Known Species` detail now also surfaces player-visible lineage/origin context, compact mutation totals, divergence signals, and per-region fit/capacity summaries from the real regional-population records.

Watch-loop responsiveness notes:

- input is polled continuously while watch mode is active
- monthly simulation steps now run on a timed cadence instead of monopolizing the loop
- chronicle pacing no longer sleeps inside event recording, so view switching stays responsive while time is running
- unpausing resumes normal cadence from the current moment and does not burst through queued catch-up ticks

## Propagation Safeguards

- systems emit on meaningful transitions, not every tick
- the coordinator dedupes identical follow-up events within a propagation step
- propagation depth is capped
- total emitted events per source event are capped
- chronicle cooldowns still apply after storage
- chronicle cooldowns are now family-specific and semantic: they key off actor scope plus the visible state change, not just raw event type
- source systems such as mutation also suppress repeated emissions when no new adaptation or divergence milestone has been reached
- structured history writing is buffered in small batches instead of flushing every event immediately
- year-end focus resolution now uses the current year's rolling event cache instead of rescanning the full world history list

This keeps the world explainable without turning the chronicle into telemetry.

## Structured History

Important events are stored as append-only JSONL records.

Default path:

- `logs/history-{timestamp}.jsonl`

The JSONL history includes causal ids and propagation depth, so downstream debugging can trace:

`effect -> parent event -> root event`

World generation and prehistory also write a separate append-only text log at:

- `logs/worldgen-{timestamp}.txt`

That log file is created as soon as generation starts, updated continuously during prehistory, and left behind with partial progress if a run is interrupted before completion.

## Runtime Options

Default mode is watch mode. Useful flags:

- `--fast` for no playback delay
- `--delay-ms <n>` for slower chronicle playback
- `--buffer-size <n>` to raise the retained chronicle history floor
- `--focus-polity <id>` to watch a specific polity
- `--debug` to restore developer-oriented yearly summaries and raw yearly event listings
- `--perf` to print lightweight yearly performance counters in debug mode

Manual pause via `Space` is available during normal watch mode. It pauses simulation advancement while still allowing navigation across watch views.

Example:

```powershell
dotnet run --project LivingWorld -- --years 120 --delay-ms 250
```
## Phase 12 - Regional Trade and Resource Exchange

Phase 12 adds the first settlement-level logistics layer. Each monthly tick now classifies every settlement into `Surplus`, `Stable`, `Deficit`, or `Starving` after food production, consumption, and store allocation are resolved.

Settlements inside the same polity can redistribute food before downstream hardship systems react. Aid routes prioritize the same region first, then neighboring regions, then the closest reachable regions by hop distance. A sender can export at most 25% of its current surplus in a month, and each regional hop removes 5% of the shipment as transport loss.

Large convoys, starvation relief, and failed aid attempts are recorded as structured events. Only `Major` and `Legendary` famine-relief outcomes surface in the chronicle; smaller transfers remain in structured history and inspection views.

Settlement starvation beats are transition-based. The chronicle records when starvation begins, worsens meaningfully, or ends; it does not repeat the same unresolved hardship every tick or year.

## Phase 13/14 - Domestication And Early Agriculture Expansion

Phase 13/14 adds a managed-food layer between hunting/foraging and mature agricultural stability.

- repeated hunting, prolonged local coexistence, and suitable traits can reveal animal domestication candidates
- repeated plant use, settlement food pressure, fertile regions, and planning capability can reveal cultivable wild plants
- `Discovery` still means learning that a species is useful or manageable
- `Learned` still means gaining organized capability such as `SeasonalPlanning`, `FoodStorage`, or `Agriculture`

Successful domestication creates lightweight settlement-level `ManagedHerd` and `CultivatedCrop` records. These do not simulate individual genetics, but they do improve food reliability, feed into settlement food state resolution, and create high-signal chronicle moments such as herd establishment, crop establishment, domestication spread, and the first transition into an established managed-food economy.

## Phase 17 - Material Economy And Production Chains

Phase 17 adds a first-pass physical economy without introducing money, prices, merchants, or foreign trade.

- settlements now extract raw materials from regional abundance
- each settlement keeps stockpiles, reserve targets, pressure states, and monthly or yearly production totals
- short recipes convert raw inputs into useful goods such as pottery, rope, textiles, simple tools, and preserved food
- tools, pottery, and preserved food now feed back into farming, hunting, spoilage, and seasonal resilience
- settlements inside the same polity can redistribute critical materials using the same route-priority logic as food aid, with distance loss and major convoy events when relief is meaningful
- repeated output plus geographic fit now create emergent specialization tags such as timber work, pottery, preservation, or toolmaking

The main chronicle still stays high-signal. Routine extraction, per-material shortage churn, and convoy bookkeeping remain in structured history and inspection views. The visible chronicle now prefers grouped settlement-level material turns such as a broader material crisis beginning, worsening, or easing, alongside major milestones such as preservation established, sustained toolmaking, and settlement craft specialization.

## Phase 18 - Economy Interactions And Market Behavior

Phase 18 keeps the economy simulation internal and pressure-based rather than exposing raw numeric prices to the player.

- settlements now track hidden need, availability, value, opportunity, external-pull, and production-focus signals per material
- production and extraction no longer stay mostly static: settlements now lean toward goods they need, goods they can support locally, and goods that keep proving valuable
- scarcity and surplus now do more than label stockpiles; they push redistribution priority, bottleneck behavior, specialization drift, and trade-good identity
- player-facing watch screens stay readable through compact summaries such as `Shortage`, `Stable`, `Surplus`, `Highly Valued`, `Trade Good`, and `Locally Common`
- new economy-turn event families remain structured-first unless they become true major historical beats such as a material becoming highly valued or a settlement becoming known for a trade good
- startup baseline economy passes now seed those states during explicit bootstrap, so the live chronicle begins from world change rather than narrating initialization output
- visible economy identity lines are now stricter than the internal economy signals beneath them: a settlement must be mature enough, strong enough, and stable for long enough before the chronicle says it `became known for` a craft or trade good
- related identity beats also share anti-stacking rules, so specialization and trade-good milestones for the same settlement-material pair do not usually surface as back-to-back early chronicle lines

The canonical roadmap now points next to:

- Phase 19 - External Trade, Trade Routes, and Inter-Polity Exchange
- Phase 20 - Settlement Infrastructure & Construction
- Phase 21 - Diplomacy, Raiding, and Conflict Foundations

Chronicle dedupe tuning and later `Discoveries` / `Learned` full-list cleanup remain secondary UI follow-up work rather than replacing that next core simulation sequence.
