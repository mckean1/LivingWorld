```md
# LivingWorld - IMPLEMENTED_SYSTEMS_LIST

This document is the canonical source of truth for:
- what has been implemented
- what is currently in progress
- what comes next

A feature is not fully complete until:
1. the code is implemented
2. tests are updated and passing where applicable
3. this file is updated
4. relevant documentation is updated

---

## Highest Priority Program - Prehistory Rework

**Status:** Planned as highest-priority implementation program

This is now the top roadmap priority for LivingWorld.

The earlier primitive-life-first startup path remains the current implemented foundation, but it is no longer the final intended startup architecture. The Prehistory Rework replaces that baseline with the fuller long-term design for simulated prehistory, readiness evaluation, focal selection, and active-play handoff.

All later roadmap phases are deferred until this program is complete enough to become the new canonical player-entry path.

### Purpose

- preserve primitive-life-first simulation as the foundation
- rebuild prehistory around factual observer-facing artifacts instead of thin bootstrap summaries
- make readiness, candidate evaluation, focal selection, and active-play handoff truth-preserving and simulation-driven
- handle weak worlds honestly without inventing readiness or fabricating viable starts
- make prehistory the real bridge into active play rather than a thin wrapper around startup rescue logic

### PR-1 - Prehistory Runtime and Evaluation Architecture

**Status:** Implemented as the canonical startup/runtime architecture for PR-1

Planned:
- keep prehistory as the canonical startup path into active play
- keep the universal monthly simulation pipeline shared between prehistory and active play
- separate raw simulation truth from evaluator-owned startup decisions
- ensure stop logic, candidate evaluation, focal selection, and player handoff sit above simulation facts rather than rewriting them
- preserve honest failure states when the world does not produce true viable starts
- make `PrehistoryRuntimePhase` and `PrehistoryRuntimeStatus` the single source of runtime truth (BootstrapWorldFrame → PrehistoryRunning → ReadinessCheckpoint → FocalSelection → ActivePlay → GenerationFailure), while `World` retains the simulation truth and legacy `WorldStartupStage` stays purely a transitional subphase label and generator diagnostic artifact.
- add `PrehistoryCheckpointCoordinator` plus the transitional `LegacyCheckpointCompatibilityAdapter` and `LegacyPlayerEntryOutcomeEvaluatorAdapter` so the checkpoint layer, candidate pool, and failure handling sit above the world rather than mutating simulation truth.
- align `StartupProgressRenderer`, `ChronicleWatchRenderer`, and `ActivePlayHandoffState` so the canonical phases drive the player view: the startup panel shows the phase text and metrics, `FocalSelection` pauses monthly ticks while the candidate pool is shown, `ActivePlay` only begins once the handoff is recorded, and `GenerationFailure` surfaces an honest failure summary.

Implemented cleanup:
- `World` now groups startup/runtime ownership under `World.Prehistory`, with compatibility forwarding properties kept only as a transitional surface.
- `PrehistoryEvaluationSnapshot` now separates `LegacyCompatibility` artifacts from `CandidateSelection` state so transitional legacy readiness/diagnostic data is isolated from the surfaced candidate pool.
- `LegacyCheckpointCompatibilityAdapter` now returns a `PrehistoryCheckpointEvaluationResult` instead of mutating `World`, which keeps the checkpoint coordinator as the canonical controller.
- `WorldGenerator` now resolves regeneration attempts from canonical checkpoint/runtime outcomes instead of calling the old direct focal-selection acceptance path after generation.
- `PrehistoryRuntimeDetailView` now drives startup metric and presentation selection, so `WorldStartupStage` is no longer required as canonical runtime truth.
- exhausted regeneration now resolves to an explicit `GenerationFailure` world state that stays frozen without starting the live chronicle boundary.

Legacy `WorldStartupStage` labels now exist only for generator-level diagnostics and presentation-friendly subphase summaries. Runtime orchestration decisions, presentation labels, and evaluation checkpoints all rely on the `PrehistoryRuntimePhase` flow.

### Transitional seams intentionally kept

- `LegacyCheckpointCompatibilityAdapter` still runs legacy readiness and candidate-generation code, but only as a one-way result bridge into `World.Prehistory.Evaluation`.
- `LegacyPlayerEntryOutcomeEvaluatorAdapter` continues to run the historical candidate-outcome rules until the new readiness module replaces them in PR-3.
- `WorldStartupStage` and `StartupOutcomeDiagnostics` remain populated for generator diagnostics and balancing while they stop gating canonical runtime behavior.

### PR-2 - Observer Snapshot Layer

**Status:** Implemented

Implemented:
- `PrehistoryObserverState` now retains recent monthly `PeopleMonthlySnapshot` history
- `PrehistoryObserverService` now builds `PeopleHistoryWindowSnapshot`
- `PrehistoryObserverService` now builds `RegionEvaluationSnapshot`
- `PrehistoryObserverService` now builds `NeighborContextSnapshot`
- snapshots are factual, observer-facing, and non-mutating
- current-month movement and trade-contact truth are captured explicitly rather than inferred from yearly counters
- rolled health summaries, shock markers, region relationships, and neighbor exchange/pressure context are now available as descriptive evidence for later readiness logic

Canonical snapshot direction now implemented:
- `PeopleHistoryWindowSnapshot` is the rolled evaluator-ready people history artifact built from monthly truth
- `RegionEvaluationSnapshot` is the factual region-scoped observer layer with global region truth plus people-relative regional truth
- `NeighborContextSnapshot` is the factual people-scoped neighbor, opportunity, pressure, and contact context layer
- these artifacts contain evidence and state only, not evaluator conclusions such as selection score, qualification result, or recommendations

### PR-3 - Readiness and Stop-Condition System

**Status:** Implemented

Implemented:
- canonical preset-driven `MinPrehistoryYears`, `TargetPrehistoryYears`, and `MaxPrehistoryYears` now drive the player-entry stop window
- `WorldReadinessReport` is now the authoritative PR-3 readiness artifact between factual observer truth and runtime phase transition
- checkpoint resolution now consumes the canonical stop outcomes directly:
  - `ContinuePrehistory`
  - `EnterFocalSelection`
  - `ForceEnterFocalSelection`
  - `GenerationFailure`
- readiness now evaluates the six canonical categories:
  - Biological Readiness
  - Social Emergence Readiness
  - World Structure Readiness
  - Candidate Readiness
  - Variety Readiness
  - Agency Readiness
- category reports now use explicit `Pass` / `Warning` / `Blocker` semantics, with strictness differences materially affecting stop behavior
- evaluator-owned candidate readiness now derives support stability/recovery, demographic viability, movement coherence, rootedness, continuity, settlement durability, political durability, and recent shocks from observer evidence instead of pushing those conclusions into observer snapshots
- hard current-month vetoes now surface explicitly in blocker output:
  - severe unsupported current month
  - active identity break
  - catastrophically scattered current footprint
  - population below minimum demographic viability
  - catastrophic unresolved displacement
- candidate truth floors now stay hard at all ages:
  - current support must pass
  - continuity must be at least `Established`
  - movement coherence must be at least `Coherent` or rootedness must reach the minimum rooting floor
- evidence windows now follow the canonical model:
  - current month for vetoes
  - last `6` months for recent condition and recovery
  - last `12` months for readiness baseline
  - last `24` months for deep-history confirmation
  - shock windows of `3`, `6`, and `12` months
- weak-world and thin-world states now surface honestly inside `WorldReadinessReport`
- maximum-age honesty is now explicit:
  - viable but weak/thin worlds can `ForceEnterFocalSelection`
  - max-age worlds with zero viable candidates now resolve to `GenerationFailure`
  - the system never invents candidates or weakens hard viability truth to dodge failure

Authoritative PR-3 result flow:
- runtime now consumes readiness results from the canonical PR-3 evaluator path through `PrehistoryCheckpointEvaluationAdapter`
- `LegacyCheckpointCompatibilityAdapter` remains only as a narrow compatibility seam for older tests and legacy diagnostics; it is no longer the source of readiness or stop decisions

### PR-4 - Candidate Viability, Maturity Bands, and Pool Composition

**Status:** Implemented

Implemented:
- `PrehistoryCandidateSelectionEvaluator` now owns the canonical PR-4 layer above PR-2 observer artifacts and PR-3 readiness results
- surfaced candidates in the checkpoint/runtime path are no longer built from legacy `FocalCandidateProfile` shortcuts
- candidate viability now applies explicit hard gates and preserves PR-3 veto truth:
  - current support must pass
  - continuity must be `Established` or `Deep`
  - movement coherence must be `Coherent` or `Strong`, or rootedness must be `Rooted` or `DeeplyRooted`
  - current-month catastrophic vetoes still block viability outright
- maturity bands now use the canonical set grounded in social/spatial/political condition:
  - `Mobile`
  - `Anchored`
  - `Settling`
  - `EmergentPolity`
- viable-only scoring now uses explicit evaluator dimensions:
  - Survival Strength
  - Continuity Depth
  - Spatial Identity
  - Agency and Internal Organization
  - External Entanglement
  - Strategic Opportunity
  - limited Fragility penalty
- pool composition now follows seed + diversify + fill with soft diversity caps, near-duplicate suppression, and honest thin-world handling
- surfaced candidate summaries now retain PR-5-ready structured evaluator data:
  - viability result
  - maturity band
  - stability mode
  - archetype summary
  - qualification reason
  - evidence sentence
  - strengths, warnings, risks
  - score breakdown
  - diversity tags

Canonical truth floor remains hard:
- no candidate should be surfaced as viable unless the hard viability gates actually pass

### PR-5 - Focal Selection Presentation Contract

**Status:** Implemented

Implemented:
- focal selection now renders as a player-facing presentation contract instead of falling back to generic Phase D startup metrics or a thin debug picker
- the presentation consumes surfaced PR-4 candidate summaries directly rather than recomputing viability, maturity, ranking, or evaluator conclusions in the UI
- each surfaced candidate now shows:
  - polity, species, and home region identity
  - maturity band, stability mode, and archetype summary
  - population band, settlement count, subsistence style, and current condition
  - qualification reason and evidence sentence
  - pressure or opportunity context plus recent historical note where useful
  - visible strengths, warnings, and risks
  - structured sections for Identity and Form, Homeland and Movement, Neighbors and Pressure, Opportunity and Risk, and Why This Start Qualified
- score presentation is now descriptive by default in player-facing focal selection, using score-tier language instead of raw numeric totals
- truthful banner framing now surfaces ready, thin, forced, and weak-world states without hiding weakness
- single-candidate pools still render through the full focal selection contract instead of collapsing into a stub

### PR-6 - Active-Play Handoff and Control Conversion

**Status:** Implemented

Implemented:
- focal selection now hands off from the exact selected end-of-month prehistory state without advancing another month during conversion
- `ActivePlayHandoffState` now stores a canonical structured handoff package instead of a thin polity-summary record
- the package now separates:
  - player ownership state
  - starting control state
  - chronicle handoff state
  - knowledge / visibility state
  - origin record
  - warnings / unresolved-risk state
- active play now begins paused after handoff, so the inherited start can be inspected before time resumes
- handoff packages now preserve:
  - selected people/species/home-region identity
  - exact handoff month
  - current condition, support, continuity, maturity, and stability truth
  - routes, occupied regions, settlement truth, and region-relation truth
  - neighbor / pressure / opportunity context
  - unresolved shocks, warnings, and risks
  - real discoveries and real learned capabilities
  - visibility-scoped known regions, species, and polities
  - compact inherited prehistory summary lines for active-play entry
- control conversion is now descriptive and truth-preserving:
  - default `Society`
  - `Mobile` and `Anchored` starts remain `Society`
  - `Settling` starts convert to `Polity` only if the full polity gate passes
  - `EmergentPolity` starts fall back to `Society` when structured-authority evidence is too thin
- spatial control conversion now maps starts truthfully into `Network`, `AnchoredHomeRange`, or `TerritorialCore`
- `ActiveControl` is now the explicit runtime/player-control overlay, while polity objects remain the backing simulation state beneath that control boundary
- watch/UI entry surfaces now show the inherited start, converted control type, and compact handoff summary without dumping raw handoff fields into the chronicle

### PR-7 - Documentation and Canonical Roadmap Sync

**Status:** Planned

Planned:
- keep this file as the canonical ledger for the Prehistory Rework
- sync roadmap and planning docs so the Prehistory Rework clearly sits above later civilization-era phases
- update startup and simulation docs to reflect the new prehistory architecture
- keep design decisions aligned with roadmap and planning docs as implementation proceeds

### Explicitly Deferred / Out of Scope for This Program

Deferred unless later reintroduced:
- directive system
- standing posture system
- planning UX
- month-result review and planning flow

These are intentionally not part of the current critical path for the Prehistory Rework.

---

## Current Implemented Foundation

The following is the current implemented foundation that the Prehistory Rework will replace and absorb.

## Startup Architecture - Primitive-Life-First 4-Pass Baseline

This startup path is the current implemented baseline.

The world no longer assumes a static civilization-ready start.

### Pass 1 - Biological World Foundation

**Status:** Implemented foundation slice

Implemented:
- region ecological profiles with derived productivity, habitability, migration ease, and volatility values
- primitive lineage templates for producers, grazers and foragers, scavenger-omnivores, and predators
- suitability-based uneven ecological seeding instead of "everything everywhere"
- aggregated regional primitive populations with carrying capacity, food and support pressure, reproduction pressure, migration pressure, and trend and stress diagnostics
- early ecological simulation loop with producer support, consumer pressure, predator pressure, founder spread, and local extinction cleanup
- founder-population migration into adjacent viable regions
- Phase A ecological readiness and stability reporting
- bootstrap gating so the default startup path hands off from ecological readiness into the next pre-social evolution layer instead of assuming civilization-ready species or polities

### Pass 2 - Evolution and Divergence

**Status:** Implemented foundation slice plus startup-richness stabilization pass

Implemented:
- explicit `EvolutionaryLineage` records with ancestry, origin, extinction, lineage stage, trait and adaptation summaries, and sentience-capability state
- startup-stage activation of mutation, divergence, founder-isolation pressure, speciation, and extinction history after Phase A ecology stabilizes
- population-level mutation history first, with regional divergence and contact tracking and speciation only after sustained viable isolation
- structured evolutionary history events for mutation, divergence milestones, speciation, adaptation, local extinction, global extinction, and sentience-capability milestones
- lineage adaptation summaries and rare sentience-capability progression that can produce pre-social `Capable` branches without creating societies yet
- `PhaseBReadinessReport` so startup handoff is based on branching history, divergence maturity, extinction history, and sentience-capable potential rather than time alone
- richer founder-isolation payoff, ecology-distance divergence pressure, descendant momentum retention, and partial-contact damping
- local-extinction opening bonuses and related-lineage replacement pressure
- `PhaseBDiagnostics` with ancestry depth, branching counts, divergence maturity, adapted-biome spread, extinction and replacement texture, sentience-capable root breadth, and weakness reasons for shallow-seed inspection
- sentience-capability progression and bootstrap handoff that prefer broader root-branch coverage and adapted-biome novelty before repeating the same lineage branch

### Pass 3 - Sentience and Social Formation

**Status:** Implemented corrective stabilization pass

Implemented:
- activation of actual sentient population groups from viable sentience-capable lineages
- persistent group continuity with cohesion, identity strength, survival knowledge, migration pattern, and stress tracking
- society formation as the first durable social unit with predecessor links, mobility mode, subsistence mode, cultural knowledge, and settlement pressure
- early cultural discovery accumulation around edible species, dangerous animals, fertile and harsh regions, and reliable water
- pressure-based settlement founding plus abandonment and failure handling through `SocialSettlement`
- first society-to-polity transition foundations, including limited early `Learned` capability seeding where needed for continuity
- continuity-preserving group and society fragmentation plus collapse history
- focal-candidate viability tracking for later player starts
- `PhaseCReadinessReport` so startup handoff is based on actual social and political maturity rather than time alone
- grounded annual population growth, stagnation, decline, and collapse for sentient groups, societies, settlement populations, and early polities
- latent settlement support, storage support, ecological carrying support, and subsistence mode feeding first-settlement founding
- polity expansion adding grounded secondary settlements and using settlement-distributed fragmentation pressure
- same-lineage social emergence allowing multiple regionally separated trajectories when support and continuity justify them
- bootstrap sentience handoff preserving multiple viable sentience-capable branches instead of funneling the whole world through one lineage
- explicit fallback-origin continuity on groups, societies, settlements, and polities so downstream startup logic can distinguish rescued paths from organic ones

### Pass 4 - Polity Start and Player Entry

**Status:** Implemented corrective stabilization pass plus startup-richness and differentiation follow-up

Implemented:
- startup world-age presets with variable prehistory duration, target age as a soft centerpoint, and readiness strictness and candidate-count tuning
- explicit prehistory runtime state flow across biological foundation, evolutionary history, social emergence, player-entry evaluation, focal selection, and active play
- `WorldReadinessReport` for player-entry handoff using biological, social, civilizational, candidate, and stability categories instead of raw age alone
- focal candidate generation from real simulated post-prehistory polities with viability filters, score-plus-diversity ranking, and weak-world emergency fallback thresholds
- compact player-facing candidate summaries covering lineage and species, region, age, settlement depth, subsistence style, current condition, discoveries, learned capability, and a recent historical note
- dedicated `FocalSelection` watch and UI state that freezes time until the player binds to a chosen polity
- player binding and handoff fields on `World` for selected polity, entry year, polity-age context, stop reason, summary snapshot, and live-chronicle start marker
- strict chronicle boundary enforcement so prehistory remains structured history and summary material instead of leaking into the live chronicle buffer
- stricter weak-world handling so max-age, fallback-only, and biologically weak outcomes are rejected more often and rerolled instead of being surfaced as normal starts
- selection-screen cleanup plus chronicle viewport sanitation so player-facing startup text is narrative-first and stale status or summary fragments cannot leak into the chronicle pane
- candidate fallback labeling that tracks fallback-created origins and emergency admissions directly
- Phase C and Pass 4 readiness alignment on organic social and political depth
- stronger expectation that normal startup produces multiple organic candidates
- startup diagnostics exposing organic and fallback counts, emergency candidate admissions, candidate rejection reasons, startup bottlenecks, and regeneration causes
- deterministic startup reroll seed derivation for stable organic-versus-fallback measurement
- candidate summaries classifying starts from current polity state instead of founder-origin labels
- polity settlement expansion tuned around subsistence mode, network age, and fragmentation pressure
- diversity trimming that preserves richer current-polity summaries and more regional differentiation
- dedicated startup progress rendering showing world-frame, ecology, evolution, society, and player-entry phases while prehistory runs
- startup progress text isolated cleanly from the live chronicle path

### Meaning of the Current Baseline

This 4-pass primitive-life-first startup is the current implemented baseline, not the final target architecture.

The Prehistory Rework above is the new highest-priority program that will replace this baseline as the canonical player-entry path.

---

## Additional Implemented Supporting Systems

These systems are already part of the broader implemented foundation and remain in place while the Prehistory Rework moves forward.

Implemented:
- world generation
- fuller default seed-world scale with centralized generation settings
- ecology and food systems
- regional species populations and seasonal ecosystem interactions
- settlement hunting tied to regional wildlife
- plant gathering separated cleanly from animal food so wildlife is pressured only through the species layer
- migration, settlement, population, and fragmentation simulation
- advancement and capability effects
- polity stage progression
- canonical structured event model
- chronicle-first watch mode with a fixed status panel
- newest-first live chronicle playback
- configurable chronicle playback delay
- append-only JSONL history output
- lineage-aware focus handoff across fragmentation and collapse
- lightweight debug and performance instrumentation for long-run balancing and regression detection
- watch-mode polity, region, species, polity-list, and world-overview inspection screens
- shared discovery-aware visibility and consistent keyboard navigation
- domestication and early agriculture expansion
- material economy and production chains
- economy interactions and market-behavior foundations

---

## Deferred Until After Prehistory Rework

These remain planned, but they are not the current priority.

### Phase 19 - External Trade, Trade Routes, and Inter-Polity Exchange

**Status:** Deferred until after Prehistory Rework

Planned:
- inter-polity exchange
- foreign trade routes
- imports, exports, and dependency
- external logistics pressure as a foundation for later diplomacy and conflict

### Phase 20 - Settlement Infrastructure and Construction

**Status:** Deferred until after Prehistory Rework

Planned:
- infrastructure and construction as long-term sinks for the economy
- durable development layers tied to material throughput and settlement specialization
- stronger region-shaping built environment over time

### Phase 21 - Diplomacy, Raiding, and Conflict Foundations

**Status:** Deferred until after Prehistory Rework

Planned:
- diplomacy and relationship handling across polities
- raiding and coercive pressure on routes and border regions
- conflict foundations grounded in logistics, dependency, and supply disruption

### Later Follow-Through

**Status:** Deferred until after Prehistory Rework

Planned:
- knowledge diffusion
- cultural divergence
- richer history-view tooling
- deeper focal-candidate inspection and compare views
- dedicated civilization, species, and world-history screens built on preserved structured prehistory
- later cultural, religious, political, archaeological, and wonder systems

---

## Working Rule Going Forward

For LivingWorld, the Prehistory Rework is now the highest-priority roadmap program.

No later deferred phase should displace it unless the roadmap is intentionally changed and this file is updated to reflect that decision.
```
