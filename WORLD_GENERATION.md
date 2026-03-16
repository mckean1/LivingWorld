# LivingWorld World Generation

World generation now creates the starting primitive ecological state first.
The default startup no longer assumes named civilization-ready species and initial polities already exist.

The agreed startup path is:

1. biological world foundation
2. evolution and divergence
3. sentience and social formation
4. polity start and player entry

This document describes the current default startup output, which now includes Pass 1 ecological foundation, Pass 2 evolutionary history bootstrap, Pass 3 civilizational emergence bootstrap, and Pass 4 player-entry evaluation before active play begins.

## Default Scale

The current startup targets live in `LivingWorld/Generation/WorldGenerationSettings.cs`:

- `RegionCount = 36`
- `InitialSpeciesCount = 7`
- `InitialPolityCount = 0`
- `ContinentWidth = 6`
- `ContinentHeight = 6`
- `PhaseAMinimumBootstrapMonths = 18`
- `PhaseAMaximumBootstrapMonths = 60`
- `PhaseBMinimumBootstrapYears = 180`
- `PhaseBMaximumBootstrapYears = 900`
- `PhaseCMinimumBootstrapYears = 120`
- `PhaseCMaximumBootstrapYears = 480`
- readiness thresholds for occupied regions, producer coverage, consumer coverage, and predator coverage
- readiness thresholds for mature lineage count, speciation count, ancestry depth, divergence maturity, sentience-capable count, and stable regions
- readiness thresholds for organic social trajectories, societies, settlements, polities, viable focal candidates, historical density, and final player-entry candidate filtering/diversity
- social-maturation knobs for bootstrap sentience-capable branch count, per-lineage social trajectory limits, growth/decline rates, polity-emergence thresholds, and candidate diversity weighting
- Phase B richness knobs for mutation-potential pressure, founder-isolation bonus, ecology-distance divergence, descendant-retention carry-over, and sentience-complexity/root-breadth progression
- startup-selection knobs for healthy-candidate score, emergency fallback labeling, and startup regeneration attempts
- startup progress presentation fields on `PrehistoryRuntimeStatus`, which drive player-facing phase labels, activity text, transition text, and world-age context while generation is still running

These values are intentionally centralized so density tuning can happen without rewriting generation logic.

## Generation Steps

1. generate regions with biome-shaped fertility, water, biomass, and carrying-capacity context
2. derive and cache regional ecology profiles
3. connect regions for founder movement paths
4. generate primitive lineage templates as early ecological archetypes
5. assign each primitive lineage a suitability-based clustered starting range
6. initialize regional primitive populations from those ranges
7. run an internal Phase A ecological bootstrap loop until readiness is achieved or the bootstrap month cap is reached
8. initialize explicit evolutionary lineage records from the surviving primitive carriers
9. run an internal Phase B evolutionary bootstrap loop with mutation, divergence, speciation, extinction, and sentience-capability progression until readiness is achieved or the bootstrap year cap is reached
10. run an internal Phase C social bootstrap loop with sentient activation, society formation, settlement pressure, polity formation, and candidate tracking until readiness is achieved or the bootstrap year cap is reached
11. run the canonical player-entry evaluation layer using startup age presets, observer snapshots, `WorldReadinessReview` checkpoints, candidate composition, and weak-world regeneration until the world truthfully resolves to `FocalSelection` or `GenerationFailure`
12. store `PhaseAReadinessReport`, `PhaseBReadinessReport`, `PhaseCReadinessReport`, and `WorldReadinessReport` for inspection
13. store startup outcome diagnostics for organic/fallback counts, candidate rejections, bottlenecks, and regeneration causes
14. if viable starts exist, freeze the world in `FocalSelection` with compact candidate summaries ready for player choice; otherwise keep the world frozen in `GenerationFailure`
15. render a dedicated startup progress panel during those steps so the player can see `World Seeding`, `Biological Divergence`, `Social Emergence`, and `World Readiness Review` progress, age, readiness-window context, and compact live metrics without chronicle spam

The default startup now intentionally reaches a playable prehistory handoff rather than stopping before player-entry logic exists.

## Region Model

The fuller starting world still aims to read as one coherent early continent rather than scattered noise.

- regions are generated on a `6 x 6` continent grid
- connectivity starts from orthogonal neighbors, then adds a small amount of diagonal and river-corridor reinforcement
- each generated region receives a `RegionBiome`
- biome profiles shape fertility, water availability, plant biomass, animal-biomass capacity, and carrying capacity

Current biome mix is lightweight by design and covers:

- forest
- plains
- mountains
- river valley
- coast
- dryland

## Region Ecology Profiles

Each region now caches a compact derived ecology profile so later systems do not repeatedly recompute the same environmental summary from raw biome, fertility, and water values.

Current derived values:

- `BasePrimaryProductivity`
- `HabitabilityScore`
- `MigrationEase`
- `EnvironmentalVolatility`
- supporting climate axes such as derived `Temperature`, `Moisture`, and `TerrainHarshness`

These values are designed to be tunable and easy to inspect in debug output and tests.

## Primitive Lineage Distribution Rules

Primitive lineages are no longer treated as globally present by default.

- each primitive lineage template declares niche, habitat preferences, temperature/moisture tolerance, resilience, migration tendency, and starting spread weight
- generation scores every region x lineage pairing using ecology profile values plus biome and biomass fit
- each lineage receives a clustered `InitialRangeRegionIds` set rather than random global scatter
- producers seed broadly across habitable clusters
- grazers/foragers and scavenger-omnivores seed more narrowly and unevenly
- predators begin sparse and prey-grounded
- later founder migration complements this seed state rather than replacing it

This keeps the opening world biologically alive without making every region ecologically identical.
The target outcome is broad producer presence, meaningful prey coverage, sparse predator presence, and visible regional unevenness.

## Phase A Bootstrap And Readiness

Pass 1 runs an internal ecological bootstrap after initial seeding.
That bootstrap uses the monthly/seasonal ecology cadence to stabilize regional populations before later startup passes are allowed to proceed.

Readiness is not time-based only.
`PhaseAReadinessReport` currently tracks:

- occupied region percentage
- producer coverage
- consumer coverage
- predator coverage
- biodiversity count
- stable region count
- collapsing region count
- failure reasons

Later startup passes should use that report to decide whether the ecological foundation is ready to hand off.

## Phase B Bootstrap And Readiness

After Phase A succeeds, startup now creates explicit lineage records and runs an evolutionary bootstrap.

That layer adds:

- ancestry-preserving `EvolutionaryLineage` records
- regional mutation and divergence accumulation
- founder-isolation payoff and ecology-distance pressure so partially isolated populations in different ecological pockets diverge more legibly over long timescales
- contact moderation so connected sibling populations diverge more slowly without collapsing all partial-contact branches into one flat lineage
- founder-driven speciation only after sustained isolation, viability, and persistence
- local and global extinction history retention
- extinction-opening replacement pressure so nearby/related lineages can recolonize or replace locally vanished populations
- emergent habitat/trait adaptation summaries
- rare sentience-capability progression from ecological pressure plus continuity, with bootstrap handoff that prefers broader root-branch coverage before repeating the same biological root
- `PhaseBDiagnostics` for ancestry depth, branch count, divergence maturity, adapted-biome spread, extinction/replacement texture, and sentience-capable root breadth

`PhaseBReadinessReport` currently tracks:

- mature lineage count
- speciation count
- extinct lineage count
- max ancestry depth
- mature regional divergence count
- sentience-capable lineage count
- stable ecosystem region count
- failure reasons

## Phase C Bootstrap And Readiness

After Phase B succeeds, startup now activates viable sentience-capable branches into actual sentient groups and lets them try to persist, settle, and organize.

That layer adds:

- sentient group activation from viable sentience-capable lineage populations
- persistent group continuity with cohesion, continuity years, identity strength, shared knowledge, and real annual population growth/decline pressure
- society formation with mobility and subsistence identity plus ecology/storage/carrying-support-driven maturation
- pressure-based settlement founding plus abandonment/failure handling, including latent support for first-settlement creation so viable societies do not stall forever below polity formation
- first polity formation from only the strongest settlement-backed societies, plus early multi-settlement polity spread when population pressure and local support justify it
- multiple regionally separated social trajectories per lineage when continuity, support, and spatial separation justify them
- focal-candidate viability tracking for later player-entry selection
- explicit fallback-origin tracking on groups, societies, settlements, and polities so rescued paths stay inspectable instead of blending into organic history

`PhaseCReadinessReport` currently tracks:

- sentient group count
- organic versus fallback group counts
- persistent society count
- organic versus fallback society counts
- settlement count
- organic versus fallback settlement counts
- viable settlement count
- polity count
- organic versus fallback polity counts
- viable focal-candidate count
- organic versus fallback focal-candidate counts
- average polity age
- historical event density
- failure reasons

## Pass 4 Player Entry Layer

Pass 4 is now implemented. It adds:

- `StartupWorldAgeConfiguration` presets (`YoungWorld`, `StandardWorld`, `AncientWorld`) with min/target/max prehistory years, readiness strictness, and candidate-count targets
- the canonical `PrehistoryRuntimePhase` ladder (`BootstrapWorldFrame`, `PrehistoryRunning`, `ReadinessCheckpoint`, `FocalSelection`, `ActivePlay`, `GenerationFailure`) so startup state is explicit instead of inferred from active play assumptions
- `WorldReadinessReport` that evaluates Biological, Social Emergence, World Structure, Candidate, Variety, and Agency readiness from observer evidence
- canonical candidate surfacing that filters, scores, and composes real simulated polities into a compact player-facing pool
- current-polity candidate profiling so subsistence, settlement-network shape, and current condition come from what the polity actually became by startup stop, not only from its founder society
- moderated polity settlement expansion so healthy starts stop converging into identical late-bootstrap settlement spam before candidate selection
- strict fallback order: normal readiness stop, final candidate search at max age, weaker but still simulated emergency candidates with explicit fallback labels, and generation failure if the world still produces nobody worth starting as
- alignment between `PhaseCReadinessReport` and `WorldReadinessReport` so fallback-only polities or candidate pools do not pass one layer while failing another
- startup regeneration preference for biology-weak, fallback-only, or one-candidate weak worlds instead of silently surfacing them as ordinary starts
- `StartupOutcomeDiagnostics` for organic/fallback counts, emergency admissions, candidate rejection reasons, bottlenecks, and regeneration causes
- preservation of prehistory as structured history only; the live chronicle begins only after focal selection, handoff packaging, and player binding
- truthful active-play handoff from the selected end-of-month state, with active play beginning paused

## Output Model After Generation

After generation:

- the world is frozen in `FocalSelection` when viable starts exist, or left frozen in `GenerationFailure` when the world truthfully produces none
- while generation is running, a separate startup progress panel owns the console and refreshes in place with phase-specific metrics such as occupied regions, lineage/speciation history, social actors, polities, and viable candidates
- default player-facing output is a candidate-selection watch screen first, then the live chronicle watch view after a polity is chosen
- structured history records prehistory underneath, but the live chronicle begins only after the explicit handoff marker

World generation does not produce a separate player-facing yearly report path.

Regional primitive populations now exist before any later-stage society logic could run.
Those populations now also carry founder/contact/divergence state, so mutation/speciation and sentience-capability progression grow from real ecological history rather than replacing the startup model.
Pass 3 then turns only viable sentience-capable branches into social actors, lets those actors actually grow or decline under pressure, accumulates early cultural discoveries, forms durable societies, founds early settlements, and promotes only the strongest organically maturing societies into pre-player polities.
Pass 4 then evaluates whether that world is mature enough for player entry, ranks distinct simulated starts, rejects fallback-only or one-candidate weak worlds more aggressively, and preserves prehistory as summary/history context rather than as live chronicle noise.

For repeated-run startup validation, the repo now keeps a compact deterministic sweep profile (`YoungWorld`, `4x4`, reduced bootstrap caps) for representative seed families.
During this pass that sweep improved from `5/9` accepted worlds to `7/9`, accepted worlds kept `0` fallback candidates, and sentience-capable root breadth in accepted runs moved from mostly `1` root to `2-3` roots.

## Phase 13/14 Generation Support

World generation now also seeds plant cultivation potential more explicitly through species templates.

- producer species can define `CultivationAffinity`
- animal species still define `DomesticationAffinity`
- those affinities do not guarantee domestication, but they make later historical outcomes emerge from the seed world rather than from hardcoded event scripts

This means fertile river valleys and mixed grassland regions are more likely to support early cultivation stories, while harsh or low-affinity ecologies remain slower to domesticate.
