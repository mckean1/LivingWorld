# LivingWorld Simulation Roadmap

This roadmap describes how LivingWorld should deepen the simulation while keeping the chronicle-first player experience intact.

---

## Guiding Direction

LivingWorld is now prioritizing the Prehistory Rework above all later civilization-era roadmap phases.

The current primitive-life-first startup baseline is implemented and useful, but it is not the final intended startup architecture. The next major push is to replace that baseline with the fuller prehistory design so the path from primitive life into active play is more truthful, more simulation-driven, and more narratively coherent.

The chronicle remains the main player-facing experience.
The player should enter an already-living world through an honest simulated prehistory, not through a thin or overly artificial startup wrapper.

---

## Current Foundation

Implemented core currently includes:
- world generation
- fuller default seed-world scale with centralized generation settings
- primitive-life-first startup foundation across ecology, evolution, social emergence, and player-entry evaluation
- ecology and food systems
- regional species populations and seasonal ecosystem interactions
- settlement hunting tied to regional wildlife
- plant gathering separated cleanly from animal food so wildlife is pressured only through the species layer
- mutation, regional divergence, and first-pass speciation foundations
- migration, settlement, population, and fragmentation
- advancement and capability effects
- polity stage progression
- canonical structured event model
- chronicle-first watch mode with a fixed status panel
- newest-first live chronicle playback
- configurable chronicle playback delay
- append-only JSONL history output
- lineage-aware focus handoff across fragmentation and collapse
- lightweight debug and performance instrumentation for long-run balancing and regression detection
- watch-mode polity, region, species, polity-list, and world-overview screens
- shared discovery-aware visibility and consistent keyboard navigation
- domestication and early agriculture expansion
- material economy and production chains
- economy interactions and market-behavior foundations

---

## Highest Priority Now - Prehistory Rework

### Why this is the priority

The current startup path already proves that primitive-life-first world generation can work, but it still needs to be turned into the final long-term architecture for:
- honest prehistory stop conditions
- observer-facing evaluation artifacts
- viable candidate truth
- richer focal selection
- clean active-play handoff

This is now the most important program in LivingWorld and should be completed before the roadmap resumes later civilization-era growth systems.

### Program Goal

Turn prehistory into the canonical player-entry architecture so that:
- primitive life grows into sentient groups and early societies through the main simulation
- readiness is evaluated from real evidence rather than thin summary shortcuts
- viable starts are surfaced honestly
- weak worlds are handled honestly
- the selected start carries real prehistory, real pressures, and real opportunities into active play

### PR-1 - Prehistory Runtime and Evaluation Architecture

Implemented:
- keep the same universal monthly simulation pipeline in both prehistory and active play
- keep raw simulation truth separate from evaluator-owned startup decisions
- treat prehistory as the canonical startup path into active play
- preserve honest failure states when the world does not produce true viable starts
- formalize checkpoint coordination with `PrehistoryCheckpointCoordinator`, `LegacyCheckpointCompatibilityAdapter`, and `LegacyPlayerEntryOutcomeEvaluatorAdapter` so reader-facing readiness/candidate logic cannot mutate the core world state and GenerationFailure remains explicit
- group startup/runtime ownership under `World.Prehistory`, with `PrehistoryEvaluationSnapshot.LegacyCompatibility` and `PrehistoryEvaluationSnapshot.CandidateSelection` separating transitional legacy artifacts from candidate-pool state
- drive startup rendering from `PrehistoryRuntimeDetailView` rather than the old pass ladder
- resolve regeneration attempts from checkpoint/runtime outcomes and keep exhausted attempts as explicit `GenerationFailure` worlds

### PR-2 - Observer Snapshot Layer

Implemented:
- `PrehistoryObserverState` now retains recent monthly `PeopleMonthlySnapshot` truth
- `PrehistoryObserverService` now projects `PeopleHistoryWindowSnapshot`
- `PrehistoryObserverService` now projects `RegionEvaluationSnapshot`
- `PrehistoryObserverService` now projects `NeighborContextSnapshot`
- snapshots keep evaluator conclusions out of the artifacts themselves and stay descriptive/value-bearing only
- current-month trade and movement truth are captured explicitly instead of leaking yearly counters into observer evidence

### PR-3 - Readiness and Stop Logic

Implemented:
- preset-driven minimum, target, and maximum prehistory ages now drive the stop window
- `WorldReadinessReport` now owns the canonical PR-3 readiness layer between observer truth and runtime transition
- readiness categories now resolve with `Pass` / `Warning` / `Blocker` status across:
  - Biological
  - Social Emergence
  - World Structure
  - Candidate
  - Variety
  - Agency
- checkpoints now resolve through the canonical outcomes:
  - `ContinuePrehistory`
  - `EnterFocalSelection`
  - `ForceEnterFocalSelection`
  - `GenerationFailure`
- evidence windows now follow the canonical `current / 6 / 12 / 24` month model with `3 / 6 / 12` month shock windows
- max-age honesty is now enforced:
  - viable but weak or thin worlds can be forced into focal selection
  - max-age worlds with zero viable candidates fail honestly
  - hard candidate viability truth is never weakened to dodge failure

### PR-4 - Candidate Viability, Maturity Bands, and Pool Composition

Planned:
- implement candidate viability gates from observer artifacts
- implement maturity-band mapping from real simulated condition
- score only viable candidates
- score for coherent strength and playability rather than abstract advancement
- use seed + diversify + fill pool composition
- preserve honest thin-world handling without inventing candidates

### PR-5 - Focal Selection Experience

Planned:
- present real viable starts as a player-facing screen rather than a thin debug picker
- surface qualification reason, evidence sentence, maturity band, stability mode, strengths, warnings, risks, and opportunity and pressure context
- use descriptive score tiers by default
- support ready, thin, forced, and weak-world framing without hiding truth

### PR-6 - Active-Play Handoff

Planned:
- hand off directly from the selected end-of-month prehistory state into active play
- do not advance an extra month during handoff
- preserve real current condition, discoveries, learned capabilities, neighbors, routes, settlements, support, continuity, and unresolved shocks
- start active play paused
- preserve discovery-versus-learned truth
- convert the selected prehistory people into a truthful active control wrapper rather than a fake upgrade

### PR-7 - Documentation and Roadmap Sync

Planned:
- keep `IMPLEMENTED_SYSTEMS_LIST.md` as the canonical implementation ledger
- update planning and architecture docs as Prehistory Rework implementation decisions land
- keep the roadmap aligned with the agreed design baseline throughout implementation

### Explicitly Deferred for Now

Not in the current Prehistory Rework critical path:
- directive system
- standing posture system
- planning UX
- month-result review and planning flow

---

## Secondary Priorities After Prehistory Rework

These are still valuable, but they are no longer the top priority.

### Chronicle Quality Tuning

Keep improving event weighting, transition detection, grouping, and suppression so the live chronicle stays readable during busy simulation periods.

The denser world and richer startup path make this important, but it should now support the Prehistory Rework rather than displace it.

### History Views

Build richer lineage and event-history views over the stored event stream without changing core simulation rules.

The first lightweight inspection layer is already in place through watch-mode polity, region, species, polity-list, and world-overview screens.

### Multiple Perspectives

Allow the same stored history to be rendered through different focal filters or narrative lenses.

### Domestication and Ecology Follow-Through

Build on hunting pressure, edible discovery, domestication interest, and settlement locality so repeatedly used species can become stronger long-run domestication candidates tied to real settlement networks.

Continue balancing wildlife richness and frontier opening through ecology, recolonization, prey webs, habitat fit, and migration pressure instead of adding abstract animal-resource shortcuts.

### Discovery / Contact Visibility Refinement

Replace first-pass visibility approximations with deeper knowledge gating for regions, species, and foreign polities as contact systems mature.

Current visibility should remain honest and lightweight until deeper simulation-side contact memory exists.

### Speciation Follow-Through

Continue improving descendant-species naming, cultural encounter and discovery around descendant fauna, and long-horizon biological history tooling.

The goal is to deepen the existing biological-history layer, not replace the regional-population model.

---

## Deferred Civilization-Era Phases

These phases remain planned, but they are deferred until after the Prehistory Rework.

### Phase 19 - External Trade, Trade Routes, and Inter-Polity Exchange

Planned:
- inter-polity exchange
- foreign routes
- imports and exports
- dependency and logistics pressure across polity boundaries

### Phase 20 - Settlement Infrastructure and Construction

Planned:
- infrastructure as a long-term sink for the economy
- construction-driven regional development
- built-environment differentiation across settlement networks

### Phase 21 - Diplomacy, Raiding, and Conflict Foundations

Planned:
- diplomacy foundations
- raiding and coercive pressure
- conflict systems grounded in logistics, dependency, and supply disruption

### Later Follow-Through

Planned:
- knowledge diffusion
- cultural divergence
- richer history-view tooling
- later cultural, religious, political, archaeological, and wonder systems

All of these should continue to emit structured canonical events first.

---

## Implemented Milestone Snapshots

### Primitive-Life-First Startup Baseline

Implemented baseline currently includes:
- Pass 1 biological world foundation
- Pass 2 evolution and divergence
- Pass 3 sentience and social formation
- Pass 4 polity start and player entry

This is the current implemented startup architecture, but it is now the baseline that the Prehistory Rework will replace.

### Phase 13 and 14 Status

Domestication and early agriculture expansion now fill the missing layer between hunting and foraging and mature settlement growth.

Delivered scope:
- animal domestication candidates from repeated local interaction
- plant cultivation discoveries from familiarity and settlement pressure
- settlement-level managed herds and cultivated crops
- managed-food integration with farming, hardship, propagation, chronicle, and watch inspection views

Natural later follow-through:
- differentiated herd uses such as pack, labor, milk, or fiber
- crop failure and blight tied to ecology and climate pressure
- managed-food diffusion across polities through contact rather than only internal spread
- deeper settlement specialization once cross-region polity networks arrive

### Phase 17 Status

Material economy and production chains now extend the food-centered survival loop into a first-pass physical economy.

Delivered scope:
- settlement stockpiles for raw materials and processed goods
- abundance-based extraction tied to settlement labor, capability, and hardship
- short production chains for tools, pottery, rope, textiles, cut stone, lumber, and preserved food
- same-polity material redistribution with convoy events and distance friction
- emergent specialization from repeated output and geographic fit
- watch-mode visibility for surpluses, shortages, production centers, and strategic resource hotspots
- grouped settlement-level material crisis beats so the main chronicle stays historical instead of operational

Natural later follow-through:
- Phase 18 economy interactions and market behavior
- Phase 19 external trade and foreign routes
- Phase 20 infrastructure and construction
- Phase 21 diplomacy, raiding, and conflict grounded in logistics and supply disruption

### Phase 18 Status

Economy interactions and market behavior now sit on top of the material stockpile model without introducing coin, raw prices, or a player-facing market screen.

Delivered scope:
- hidden settlement-level need, availability, value, opportunity, and production-focus signals
- scarcity- and surplus-driven production shifts with smoothing
- stronger redistribution and specialization behavior driven by value pressure and local fit
- trade-good and highly valued identity emergence as structured economy outcomes
- player-facing watch summaries built from readable labels such as `Shortage`, `Highly Valued`, and `Trade Good`

Natural later follow-through:
- Phase 19 inter-polity exchange, foreign routes, imports, exports, and dependency
- Phase 20 infrastructure and construction as long-term sinks for the reactive economy
- Phase 21 diplomacy, raiding, and conflict built on route pressure, supply disruption, and dependency

---

## Long-Term Vision

- the chronicle is the main game experience
- players follow a living lineage rather than yearly diagnostics
- prehistory becomes the true simulation-driven bridge into active play
- richer history tools and multiple perspectives are layered over the same append-only event foundation
- later civilization-era systems deepen the same world rather than replacing its core storytelling model
