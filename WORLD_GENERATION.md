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
- readiness thresholds for sentient groups, societies, settlements, polities, viable focal candidates, historical density, and final player-entry candidate filtering/diversity

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
11. run an internal Phase D player-entry evaluation loop using startup age presets, periodic readiness checks, and focal-candidate generation until readiness passes or max age forces a stop
12. store `PhaseAReadinessReport`, `PhaseBReadinessReport`, `PhaseCReadinessReport`, and `WorldReadinessReport` for inspection
13. freeze the world in `FocalSelection` with compact candidate summaries ready for player choice

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
- contact moderation so connected sibling populations diverge more slowly
- founder-driven speciation only after sustained isolation, viability, and persistence
- local and global extinction history retention
- emergent habitat/trait adaptation summaries
- rare sentience-capability progression from ecological pressure plus continuity

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
- persistent group continuity with cohesion, continuity years, identity strength, and shared knowledge
- society formation with mobility and subsistence identity
- pressure-based settlement founding plus abandonment/failure handling
- first polity formation from only the strongest settlement-backed societies
- focal-candidate viability tracking for later player-entry selection
- a narrow fallback safeguard so startup does not end in a socially dead world if every otherwise-viable branch narrowly misses polity formation

`PhaseCReadinessReport` currently tracks:

- sentient group count
- persistent society count
- settlement count
- viable settlement count
- polity count
- viable focal-candidate count
- average polity age
- historical event density
- failure reasons

## Deferred To Later Passes

The startup path still does not yet implement:

## Pass 4 Player Entry Layer

Pass 4 adds:

- `StartupWorldAgeConfiguration` presets (`YoungWorld`, `StandardWorld`, `AncientWorld`) with min/target/max prehistory years, readiness strictness, and candidate-count targets
- `PrehistoryRuntimeStatus` / `PrehistoryRuntimeState` so startup state is explicit instead of inferred from active play assumptions
- `WorldReadinessReport` that combines biological continuity, social maturity, civilizational maturity, candidate quality, and stability sanity
- `PlayerEntryCandidateGenerator` that filters, ranks, and diversity-trims real simulated polities into a compact player-facing pool
- strict fallback order: normal readiness stop, final candidate search at max age, weaker but still simulated emergency candidates, and generation failure if the world still produces nobody worth starting as
- preservation of prehistory as structured history only; the live chronicle begins after focal selection and player binding

## Output Model After Generation

After generation:

- the world is frozen in `FocalSelection`, not already running active time
- default player-facing output is a candidate-selection watch screen first, then the live chronicle watch view after a polity is chosen
- structured history records prehistory underneath, but the live chronicle begins only after the explicit handoff marker

World generation does not produce a separate player-facing yearly report path.

Regional primitive populations now exist before any later-stage society logic could run.
Those populations now also carry founder/contact/divergence state, so mutation/speciation and sentience-capability progression grow from real ecological history rather than replacing the startup model.
Pass 3 then turns only viable sentience-capable branches into social actors, accumulates early cultural discoveries, forms durable societies, founds early settlements, and promotes only the strongest societies into pre-player polities.
Pass 4 then evaluates whether that world is mature enough for player entry, ranks distinct simulated starts, and preserves prehistory as summary/history context rather than as live chronicle noise.

## Phase 13/14 Generation Support

World generation now also seeds plant cultivation potential more explicitly through species templates.

- producer species can define `CultivationAffinity`
- animal species still define `DomesticationAffinity`
- those affinities do not guarantee domestication, but they make later historical outcomes emerge from the seed world rather than from hardcoded event scripts

This means fertile river valleys and mixed grassland regions are more likely to support early cultivation stories, while harsh or low-affinity ecologies remain slower to domesticate.
