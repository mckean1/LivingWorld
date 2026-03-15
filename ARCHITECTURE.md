# LivingWorld Architecture

LivingWorld keeps simulation logic, event storage, propagation, formatting, and console playback separate.

The canonical runtime architecture is now driven entirely by `PrehistoryRuntimePhase`. Bootstrap and prehistory ticks happen inside `BootstrapWorldFrame` and `PrehistoryRunning`, decision points are owned by `ReadinessCheckpoint`, the startup handoff freezes inside `FocalSelection`, and live simulation runs under `ActivePlay` unless it stops early via `GenerationFailure`. `PrehistoryRuntimeStatus` captures the orchestration metadata (phase labels, subphase text, activity summaries, checkpoint results, and runtime detail view) while the raw world data (regions, species, polities, and events) remains the simulation truth that evaluation and presentation overlay. Legacy `WorldStartupStage` labels survive only as transitional generator and diagnostic labels, not as the source of run-control.

Startup presentation now follows that runtime truth. `StartupProgressRenderer` owns the console until `FocalSelection`, `ChronicleWatchRenderer` freezes the world while your candidate pool is displayed, a `Handoff summary` describes the selected start, and `ActivePlayHandoffState` now records the canonical PR-6 handoff package rather than a thin polity-summary record. That package preserves the exact handoff month, converted control wrapper, spatial control truth, visibility truth, inherited summary, and unresolved risks before `World.BeginActiveSimulation` stamps the `LiveChronicleStartYear`. Active play begins paused so the inherited state can be inspected before time resumes. `GenerationFailure` is now an explicit terminal state that surfaces an honest failure message instead of faking a viable start.

Checkpoint evaluation now sits on top of that runtime highway through `PrehistoryCheckpointCoordinator`, but PR-3 moved the authoritative stop logic out of the legacy adapters. The coordinator enters `ReadinessCheckpoint`, invokes the canonical `PrehistoryCheckpointEvaluationAdapter`, applies the returned `PrehistoryCheckpointEvaluationResult`, and records `PrehistoryCheckpointOutcome` results directly from `WorldReadinessReport.FinalCheckpointResolution` before handing control back to the raw simulation. `WorldReadinessReport` is now the evaluator-owned world-level artifact for PR-3: it carries age-gate status, final resolution, per-category `Pass` / `Warning` / `Blocker` reports, candidate-pool summary, global blockers and warnings, weak-world and thin-world flags, and concise summary text for startup rendering. PR-4 now adds `PrehistoryCandidateSelectionEvaluator` as the canonical layer above observer truth and readiness verdicts: it owns candidate viability, maturity-band mapping, viable-only scoring, and seed + diversify + fill pool composition. `PrehistoryEvaluationSnapshot.CandidateSelection` still owns the surfaced pool, rejection reasons, and pool snapshot, while `PrehistoryEvaluationSnapshot.LegacyCompatibility` remains only as a narrow compatibility seam for older artifacts and tests. PR-6 now consumes that surfaced candidate plus the factual observer output to build the active-play handoff package and truthful control conversion without mutating the observer artifacts beneath it. This keeps raw geography, species, and polity truth untouched while the checkpoint layer decides whether to continue prehistory, stop for selection, force selection, or resolve `GenerationFailure`.

The startup direction is now explicitly primitive-life-first:

1. biological world foundation
2. evolution and divergence
3. sentience and social formation
4. polity start and player entry

Those passes now live inside a dedicated runtime orchestration layer rather than defining the outer flow itself. Generation begins in a `BootstrapWorldFrame`, moves into a shared `PrehistoryRunning` pipeline (where the familiar biological/evolutionary/social descriptors serve as subphase text), pauses in a `ReadinessCheckpoint` whenever evaluator logic inspects the latest simulation facts, and finally resolves into `FocalSelection`, `ActivePlay`, or a new `GenerationFailure` state through explicit `PrehistoryCheckpointOutcome` results (ContinuePrehistory / EnterFocalSelection / ForceEnterFocalSelection / GenerationFailure). This keeps the universal monthly simulation pipeline shared between prehistory and active play while letting evaluator decisions, candidate composition, and focal selection sit on top of the raw world truth instead of rewriting it.
The corrective startup-stabilization pass tightened that contract further: normal starts are expected to come from multiple organic candidate polities, while fallback-created or emergency-admitted worlds are explicitly labeled, diagnosed, and usually regenerated instead of being surfaced as healthy defaults.
The follow-up startup-richness pass tightened Pass 2 through Pass 4 in a different way: it improves weak Phase B seed families by deepening biological branching/replacement history and makes later candidate summaries/ranking depend on current polity state instead of frozen founder-origin labels.

## Core Structure

`World` contains:

- regions
- species
- evolutionary lineages and structured biological history
- sentient groups, societies, social settlements, focal-candidate summaries, and structured civilizational history
- polities
- polity-owned settlements
- time
- current simulation phase (`Bootstrap` vs `Active`)
- canonical world events

`World` now groups startup/runtime ownership under `World.Prehistory`:

- `World.Prehistory.Runtime` owns orchestration state
- `World.Prehistory.Evaluation.LegacyCompatibility` owns transitional readiness and diagnostic artifacts
- `World.Prehistory.Evaluation.CandidateSelection` owns surfaced candidate-pool state
- `World.Prehistory.ActivePlayHandoff` owns the canonical PR-6 active-play handoff package
- `World.Prehistory.FocalSelectionPresentation` keeps future presentation hints

Compatibility forwarding properties remain on `World` for the PR-1 transition, but the durable ownership boundary is now explicit. The new `PrehistoryRuntimePhase` flow and `PrehistoryCheckpointOutcomeKind` results now sit beside an implemented PR-2 observer layer: `PrehistoryObserverState` retains recent monthly `PeopleMonthlySnapshot` truth, and `PrehistoryObserverService` can project `PeopleHistoryWindowSnapshot`, `RegionEvaluationSnapshot`, and `NeighborContextSnapshot` artifacts without mutating the base world. PR-4 is now the evaluator-owned bridge between readiness and the surfaced candidate pool: it consumes those observer artifacts plus `CandidateReadinessEvaluation`, then writes structured surfaced candidate summaries without mutating the observer artifacts beneath it. PR-5 focal-selection presentation and PR-6 active-play handoff conversion now sit on top of that shared simulation truth, with PR-6 mapping starts into truthful `Society` or `Polity` control and `Network`, `AnchoredHomeRange`, or `TerritorialCore` spatial interpretation only when the evidence supports it.
Major systems:

- world generation
- food / ecology
- regional species populations
- ecosystem interactions
- settlement hunting
- mutation, divergence, and early speciation foundations
- agriculture
- trade
- migration
- population
- advancement
- settlement
- fragmentation
- polity stage progression

Only some of those systems are active in the default startup stage.
`World.StartupStage` now provides generator-facing subphase labels (Pass 1 through Pass 4) for diagnostics and presentation without gating the runtime path. The canonical phase flow (`PrehistoryRuntimePhase`) now determines when monthly ticks run, when checkpoints fire, and when `FocalSelection` or `GenerationFailure` pauses the pipeline.

## Settlement-Grounded Locality

`Polity` now owns lightweight `Settlement` records.
Those records are the local execution points for hunting, farming, and settlement-aware trade endpoints.
The polity-level `RegionId` still exists as the polity's current center of movement and presentation, but food-production systems no longer assume one abstract settlement multiplied by `SettlementCount`.

`WorldLookup` is the current shared lookup layer for hot-path systems.
It builds cached dictionaries for regions, species, polities, active populations by region, and settlements by region so systems can:

- avoid repeated linear scans in inner loops
- recover gracefully where a missing reference can be ignored
- throw clearer invariant errors where corruption would otherwise surface as an opaque LINQ exception

`Region` also now keeps direct indexing for species-population entries, which removes another repeated lookup hotspot from seasonal ecology and hunting code.
Those entries are sparse by default now: regions keep active or historically meaningful populations instead of materializing every species in every region.

## Generation Architecture

World seeding is now split into a few explicit pieces so density tuning stays debuggable:

- `WorldGenerationSettings` holds the main numeric knobs for region, species, and polity counts
- `WorldGenerationCatalog` provides curated biome layout, name pools, and species templates
- `WorldGenerator` now turns those settings and templates into connected regions, ecology profiles, primitive lineage templates, weighted seeded ranges, and a Phase A ecological bootstrap
- `WorldGenerator` now also initializes explicit lineage records, runs an internal evolutionary bootstrap, and stores a `PhaseBReadinessReport` before active simulation begins
- `WorldGenerator` now also runs a social-emergence bootstrap with real population growth/decline, first-settlement pressure, early polity settlement spread, and multi-trajectory lineage activation before storing a `PhaseCReadinessReport`
- `WorldGenerator` now treats fallback polity seeding as an explicit last-resort mode instead of an unconditional Phase C rescue, and the default path now prefers honest regeneration when organic readiness never materializes
- `WorldGenerator` now also stores `PhaseBDiagnostics`, uses root-diverse sentience bootstrap handoff, and prefers capable branches that widen adapted-biome/root coverage before repeating one lineage branch
- producer coverage is intentionally broad while consumer and predator spread is narrower and more uneven, so the world opens biologically alive without becoming uniform
- `EcosystemSettings` now centralizes the long-run fauna spread knobs that take over after generation: migration thresholds, founder-population sizing, prey-support gates, predator establishment windows, and cooldown pacing
- `PhaseAReadinessEvaluator` summarizes whether the generated world has broad, uneven, functioning ecological foundations rather than relying on a fixed time cutoff
- `PhaseBReadinessEvaluator` summarizes whether the post-ecology world has enough branching, extinction, divergence maturity, and sentience-capable potential to hand off into later social emergence
- `PhaseCReadinessEvaluator` now measures organic social trajectories, settlements, polities, and focal candidates separately from fallback-created actors so readiness cannot drift into "healthy on paper, rescued in practice"
- `StartupOutcomeDiagnosticsEvaluator` provides organic-vs-fallback startup counts, emergency candidate counts, candidate rejection totals, bottleneck reasons, and regeneration reasons for debug inspection and startup balancing
- `PhaseBDiagnosticsEvaluator` provides deeper inspection for shallow-seed failures: ancestry depth, branch count, divergence maturity, adapted-biome spread, extinction/replacement texture, and sentience-capable root breadth
- `PrehistoryRuntimeStatus` now also carries player-facing startup phase, subphase, activity, and transition text so long-running bootstrap work can be rendered without exposing raw internal logs

This keeps the startup world explicit rather than burying scale changes in scattered magic numbers.

## Canonical Event Architecture

The canonical flow is now:

`simulation systems -> World.AddEvent -> EventPropagationCoordinator -> World.Events + EventRecorded -> output sinks`

### Event Source Of Truth

`World.AddEvent(...)` is still the only supported entry point for canonical events. It is responsible for:

- assigning event ids
- stamping time
- stamping the current simulation phase so setup and live history remain distinguishable
- copying structured payloads
- recording chronological history
- invoking the propagation coordinator
- publishing `EventRecorded`
- leaving any year-local control caches to higher-level simulation logic rather than mixing them into canonical storage

### EventPropagationCoordinator

The coordinator is a lightweight in-process dispatcher. It:

- records the initial event
- routes it to subscribed handlers
- enqueues deterministic follow-up events
- preserves parent-child causal links
- enforces per-step dedupe
- enforces max propagation depth
- enforces max events per source event

Current default limits:

- max depth: `4`
- max events per step: `64`

### Current Handler Subscriptions

- `FoodStressPropagationHandler`
  - listens to `food_stress`, `trade_relief`
  - can emit `migration_pressure`, `starvation_risk`, `food_stabilized`
- `AgriculturePropagationHandler`
  - listens to `learned_advancement`, `cultivation_expanded`
  - can emit `cultivation_expanded`, `settlement_stabilized`
- `MigrationPropagationHandler`
  - listens to `migration_pressure`, `migration`
  - can emit `schism_risk`, `local_tension`
- `FragmentationPropagationHandler`
  - listens to `fragmentation`
  - emits `polity_founded`

## Canonical Event Model

`WorldEvent` stores:

- `eventId`
- `rootEventId`
- `parentEventIds`
- `propagationDepth`
- `simulationPhase`
- `origin`
- `year`, `month`, `season`
- `type`, `severity`, `scope`
- `narrative`, `details`, `reason`
- polity / related polity / species / region / settlement references
- `before`, `after`, `metadata`

`scope` is used to express how far an event matters:

- `Local`
- `Regional`
- `Polity`
- `World`

## Player-Facing Chronicle Path

Default player output still uses watch mode rather than yearly reports.

Watch mode is built from:

- `ChronicleEventFormatter`
- `ChronicleWatchRenderer`
- `ChronicleColorWriter`
- `WatchViewCatalog`
- `WatchUiState`
- `WatchInputController`
- `WatchInspectionData`
- `WatchKnowledgeSnapshot`
- `WatchScreenBuilder`

Important traits:

- structured history remains chronological and append-only
- the visible chronicle buffer is rendered newest-first
- only `Major+` turning points are shown in normal player mode
- internal propagation events remain structured-first unless they are promoted into genuine historical beats
- bootstrap-created baseline events remain canonical/internal but are not eligible for the player chronicle or recent-major-event summaries
- prehistory history before player binding remains structured-only and is exposed through candidate/origin summaries rather than the live chronicle
- `World.LiveChronicleStartYear/Month` marks the boundary where visible chronicle eligibility begins
- event origin now distinguishes bootstrap baseline/setup context from true live transitions, which gives the chronicle pipeline a final safety guard against bootstrap-derived leaks
- render ownership is now stricter across startup states, so focal-selection cards, diagnostics, status summaries, and chronicle entries are redrawn as separate panes rather than reusing stale text
- economy identity families now use a clearer two-layer model: internal specialization / trade-good state can respond quickly, while visible `known for` milestones require settlement maturity, sustained confirmation, and stricter promotion thresholds
- related specialization and trade-good turns for the same settlement-material pair now also share one presentation family key, which gives the chronicle a final anti-stacking guard even if an upstream producer emits both
- chronicle presentation now uses per-family visibility profiles with semantic scope keys, state signatures, and separate same-state versus changed-state cooldown gaps
- when a visible event family has no custom state signature yet, chronicle presentation falls back to actor scope plus normalized narrative so exact repeated lines are still suppressed
- the fixed top panel shows focal polity context such as species
- the fixed top panel now also shows `RUNNING` / `PAUSED` and the active watch view
- the fixed top panel separates discoveries from learned advancements
- chronicle lines do not append species to every polity name
- syntax coloring is applied after formatting, with structured status-line parsing first and boundary-aware semantic matching for narrative lines

## Watch UI Architecture

The watch UI is now a thin observation layer over the simulation rather than a chronicle-only screen.

- `WatchUiState` stores active view, pause state, per-view selection, per-view scroll offsets, and the detail back stack
- `WatchViewCatalog` centralizes top-level screen labels, numbering, and control mapping
- `WatchInputController` translates key presses into shared list/detail navigation, paging, and pause behavior
- `WatchKnowledgeSnapshot` is the shared player-knowledge projection for one render/input pass
- `WatchInspectionData` builds that knowledge snapshot from the focal polity's visible horizon and discovery records
- `WatchScreenBuilder` renders top-level and detail inspection screens from that filtered world view instead of from omniscient raw state
- `ChronicleWatchRenderer` remains responsible for low-flicker console drawing, the fixed top panel, chronicle retention, and viewport slicing
- `StartupProgressRenderer` is a separate low-flicker console owner used only during world generation; it renders phase/subphase labels, world-age progress, and compact phase-specific metrics without touching chronicle entries

When `FocalSelection` arrives, `StartupProgressRenderer` clears its frame and `ChronicleWatchRenderer` takes over. The launch pad freezes time, shows the candidate pool, and displays an explicit `Handoff summary` before PR-6 builds the canonical active-play handoff package from the exact selected end-of-month state. That package converts the selected prehistory people into the truthful active control wrapper, preserves routes, settlements, region relations, discoveries, learned capabilities, and inherited prehistory context, and then leaves the watch loop paused inside `ActivePlay` until the player resumes time. `ActiveControl` is intentionally a runtime/player-control overlay over the polity-backed simulation model: watch and entry UI treat it as the authoritative control boundary, while polity objects remain the backing simulation truth underneath. If the checkpoint instead reports `GenerationFailure`, the panel surfaces a clear failure summary and the world remains paused instead of pretending a viable start exists.

- `Simulation` now acts as the watch-loop coordinator: it polls input every iteration, advances months only when the next step time has arrived, and renders only when invalidated
- initial focus selection now runs after regional populations are initialized, so the selector can prefer a live starting polity rather than blindly taking the first id

Simulation advancement remains independent from the active screen. The UI reads world state, while `Space` explicitly gates whether monthly ticks continue.
Left/Right paging is view-agnostic now: list screens page selection, while chronicle and detail screens page scroll offsets.
`My Polity` is also a special focal-polity view rather than just a shortcut into generic polity detail. `Enter` intentionally leaves the player there so focal-only information cannot be downgraded by a foreign-polity-safe renderer path.
Simulation now also performs an explicit bootstrap seeding pass before active play begins. The default bootstrap is now four layers deep: world generation runs primitive seeding and Phase A stabilization, initializes lineage history and runs an internal evolutionary pass until `PhaseBReadinessReport` is evaluated, runs a social-emergence pass until `PhaseCReadinessReport` is evaluated, then runs a dedicated player-entry evaluation pass that builds `WorldReadinessReport`, generates/ranks focal candidates, and stops in `FocalSelection`.
Visible time no longer resets to year 0 after bootstrap. Instead, the chosen polity enters active play at the real simulated world age, while the live chronicle starts at an explicit boundary marker so prehistory stays in structured history and summaries rather than leaking into the active watch feed.
Weak-world startup outcomes are now filtered more aggressively too: max-age worlds that only produce fallback-quality starts after serious readiness failures are regenerated instead of being surfaced as normal player starts.
Debug startup summaries now also expose organic groups/societies/settlements/polities, fallback counts, emergency candidate admissions, candidate rejection totals, and regeneration causes so repeated startup sweeps can be judged on simulation health instead of only on whether a world technically produced one start.
Before that handoff, the player now sees a dedicated startup panel instead of an empty console: world generation updates that panel in place at major phase boundaries and periodic deep-time checkpoints, and the panel is cleared before focal selection or active play takes ownership of the watch viewport.

## Focus And Continuity

`ChronicleFocus` stores the currently watched polity and lineage.

`LineagePolityFocusSelector` handles:

1. initial focus selection
2. deterministic year-end handoff when the watched polity fragments, collapses, or disappears

Focus handoff events are still canonical `WorldEvent` records.

## Simulation Loop Notes

Monthly:

- region plant biomass refresh
- seasonal regional species population update on season boundaries
- seasonal ecosystem food-web processing and species exchange on season boundaries
- seasonal settlement hunting on season boundaries
- those seasonal hunts iterate actual settlements in actual regions
- regional animal biomass is derived during seasonal ecosystem sync from surviving consumer populations and is not harvested directly
- seasonal mutation, divergence, and speciation processing after same-season species exchange
- descendant-species creation now feeds into a stabilization window and readiness rebuild rather than inheriting near-ready speciation state
- seasonal extinction cleanup and biomass sync after mutation processing
- plant gathering, farming, trade redistribution, consumption, migration
- propagation state bonuses tick down
- systems emit canonical events on meaningful transitions
- follow-up events are processed immediately through the same event pipeline

In startup Pass 1, the active monthly path intentionally stops after ecological systems.
In startup Pass 2, the active monthly path runs ecology plus mutation/divergence/speciation and extinction history, but still stops before hunting, economy, migration, settlement, and polity-year-end systems.
After Pass 3 bootstrap, the generated world reaches pre-social/political continuity. Pass 4 then evaluates readiness, generates candidates, freezes in `FocalSelection`, and only after the player binds a polity does the existing polity, settlement, advancement, and fragmentation loop continue in `ActivePlay`.

The food architecture is intentionally asymmetric now:

- `FoodSystem` gathers only plant biomass from regions
- `HuntingSystem` is the only system that converts wildlife into animal food
- `EcosystemSystem` owns consumer population seeding, recovery, decline, migration, predator founder establishment/collapse, recolonization bookkeeping, and derived `AnimalBiomass`
- `Region.AnimalBiomass` exists for ecology context, watch screens, migration heuristics, and advancement weighting, not as an independent meat store
- early herbivore establishment now comes from range-limited worldgen plus carrying-capacity-driven initialization and producer-supported growth rather than from any abstract animal stock
- ongoing fauna spread now also lives in `EcosystemSystem`: adjacent-region founder migration happens after seasonal food-web pressure is known and before mutation consumes same-season exchange markers
- ecology processing is now sparse and active-population driven: suitability and carrying-capacity refresh happens for existing regional populations, while empty region-species pairs are evaluated only when an explicit founder or recolonization attempt targets them

The later monthly `MigrationSystem` still handles polity relocation after food resolution. Mutation does not read polity movement directly; it reads seasonal species-exchange state on `RegionSpeciesPopulation`.
`MutationSystem` now owns the regional evolution pass on those same records: pressure accumulation, local trait drift, divergence pressure, contact moderation, adaptation milestones, sentience-capability progression, and descendant-species creation for long-isolated viable populations.
That evolutionary pass now lets founder isolation, ecology mismatch, and partial contact produce richer divergence histories, while descendant species keep enough retained momentum to create deeper lineage trees without immediate recursive speciation.
Speciation is now additionally gated by descendant-species age, global species population, sustained readiness, and stabilization rules so evolutionary storytelling remains visible without turning into recursive geometric growth.
`EcosystemSystem` now also treats recent local extinctions as ecological openings for related-lineage replacement, so extinction history feeds later biological texture instead of disappearing into a flat survivor snapshot.
When polity migration does occur, settlement records are relocated with the polity so settlement-grounded systems keep a coherent local model until a later phase introduces true cross-region polity settlement networks.

Year-end:

- population
- learned advancements
- settlement progression
- fragmentation
- stage progression
- annual agriculture and trade review
- annual hardship transition events
- focus validation and handoff
- optional perf instrumentation snapshots

## Design Direction

The architecture continues to prioritize:

- one structured event stream
- explainable cause-and-effect
- concise chronicle presentation
- regional ecology and hunting as shared simulation state rather than isolated subsystems
- population-level biological divergence layered on regional populations rather than rewritten species definitions
- the first lineage-aware descendant-species path now exists; later phases can deepen naming, deep-history, and domestication variants without replacing the population-level model
- future alternate history views without rewriting simulation systems
- source-side milestone guards plus chronicle cooldown keys so visible history beats stay about transitions rather than repeated reaffirmation
- sparse regional-population tracking and descendant-species stabilization as first-class performance safeguards
- a stricter presentation split where structured history may still keep repeated pressure/follow-up events even when the live chronicle hides them
- startup-stage honesty, so the generated world only contains layers that have actually been simulated into existence
## Phase 12 - Settlement Aid Layer

Regional food exchange now sits between settlement production/consumption bookkeeping and later social-pressure systems. The new monthly pass is settlement-centric: it derives each settlement's local food requirement, production share, and stored food share from the current polity-level totals, classifies the settlement food state, and then performs intra-polity redistribution.

This layer does not introduce markets, prices, currency, or diplomacy. It is a logistical redistribution system whose main responsibilities are:
- classify settlement food pressure
- move food along region-aware routes inside a polity
- apply transport loss by regional distance
- emit high-signal aid and famine-relief events through the normal world event pipeline

## Phase 13/14 - Managed Food Architecture

Domestication and cultivation now extend the same settlement-grounded architecture rather than creating a disconnected economy layer.

- `DomesticationSystem` evaluates local animal and plant familiarity after gathering
- `AgricultureSystem` still owns cultivated land and farm output, while cultivated crops now modify that output
- settlement-managed food remains local first, gradual, and event-driven
- `DomesticationPropagationHandler` turns meaningful domestication success into later stabilization follow-ups through the normal event pipeline
- annual managed-food stabilization is now a polity state transition, not a yearly steady-state reminder

The new managed-food entities stay population-level:

- `ManagedHerd` tracks a domesticated animal variant, reliability, growth, and managed yield for one settlement
- `CultivatedCrop` tracks a cultivated plant variant, yield multiplier, resilience, and stability effect for one settlement

This keeps the model cheap, explicit, and extensible for later pack animals, labor animals, milk/fiber outputs, and selective breeding without replacing the current architecture.

## Phase 17 - Material Economy Architecture

The new material layer extends the same settlement-grounded model instead of creating a parallel economy simulator.

- `Region` remains a capacity provider through per-material abundance values rather than a finite inventory
- `Settlement` is the core material actor and now owns stockpiles, reserve targets, pressure states, annual production history, tool tier, and specialization tags
- `MaterialEconomySystem` runs monthly after gathering and domestication familiarity, then before farm output and later hardship resolution
- extraction, production, pressure classification, redistribution, and specialization all operate on the same settlement records
- `MaterialEconomyPropagationHandler` turns meaningful preservation, toolmaking, and convoy success into downstream stability effects through the normal event pipeline
- grouped material-crisis events are emitted in addition to lower-level per-material shortage events so player-facing history stays settlement-scale and readable while structured history keeps the exact material breakdown

This preserves LivingWorld's existing architecture rules:

- cause-and-effect remains explicit
- structured events stay canonical
- routine operational state remains inspectable without spamming the chronicle
- later construction, infrastructure, and inter-polity trade can build on the same stockpile model
- player-facing major-event views now share a common dedupe identity so the live chronicle and visible-major-event summaries do not diverge on duplicate suppression

The canonical next planned sequence after this material-economy foundation is:

Phase 18 is now implemented on top of that stockpile model:

- the economy remains hybrid and pressure-based internally rather than becoming a player-facing price screen
- settlements now keep smoothed need, value, opportunity, production-focus, and external-pull readiness signals
- those signals feed extraction, recipe choice, redistribution priority, bottleneck handling, and specialization drift
- player-facing summaries stay label-based and readable instead of exposing raw market math

The canonical next planned sequence is now:

- Phase 19 - external trade, trade routes, and inter-polity exchange beyond same-polity redistribution
- Phase 20 - settlement infrastructure and construction as durable material sinks and capability multipliers
- Phase 21 - diplomacy, raiding, and conflict foundations grounded in logistics, dependency, and supply disruption

Chronicle dedupe polish and later `Discoveries` / `Learned` list cleanup remain supporting presentation follow-through, not the primary next architecture phases.
