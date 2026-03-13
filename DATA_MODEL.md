# LivingWorld Data Model

LivingWorld uses aggregated entities rather than individual agents so long historical runs remain practical.

## Core Entities

- `World`
- `Region`
- `RegionBiome`
- `Species`
- `RegionSpeciesPopulation`
- `Polity`
- `Settlement`
- `PolityStage`
- `WorldEvent`
- `ChronicleFocus`
- `WatchUiState`
- `WatchViewType`

## World

`World` is the root simulation container.

Key properties:

- `Regions`
- `Species`
- `Polities`
- `Events`
- `Time`
- `SimulationPhase`

Event responsibilities:

- `AddEvent(...)` enriches and stores canonical event records
- `ConfigureEventPropagation(...)` attaches the propagation coordinator
- `EventRecorded` publishes each stored event to sinks

## Polity

`Polity` represents a social group.

Selected fields:

- identity: `Id`, `Name`, `SpeciesId`, `RegionId`, `LineageId`, `ParentPolityId`
- demographics: `Population`, `YearsSinceFounded`, `Stage`
- food: `FoodStores` and annual food aggregates
- migration: `MigrationPressure`, `MovedThisYear`, `MovesThisYear`
- fragmentation: `FragmentationPressure`, `FoodStressYears`, `SplitCooldownYears`
- settlements: `SettlementStatus`, `SettlementCount`, `YearsSinceFirstSettlement`, and owned `Settlements`
- discoveries: explicit cultural/world knowledge records in `Discoveries`
- learned advancements: `Advancements`, derived `Capabilities`
- hunting-specific runtime support: known edible species, known toxic species, dangerous prey knowledge, hunt success/failure tracking
- domestication groundwork: `DomesticationInterestBySpecies`
- year-boundary food snapshot: `LastResolvedFoodState`, `LastResolvedFoodStateYear`

Propagation support fields:

- `EventDrivenMigrationPressureBonus`
- `EventDrivenFragmentationPressureBonus`
- `EventDrivenSettlementChanceBonus`
- remaining-month counters for those bonuses
- `LastLearnedAgricultureEventId`

These are lightweight runtime fields used so one event can influence later system behavior without creating hidden randomness.

## Settlement

`Settlement` is a lightweight locality record owned by a `Polity`.

Selected fields:

- `Id`
- `PolityId`
- `RegionId`
- `Name`
- `CultivatedLand`
- `YearsEstablished`

Current design notes:

- hunting executes from each settlement's `RegionId`
- farming allocates regional capacity across settlements, not across abstract polity-region buckets
- trade endpoints prefer real settlement references when available
- polity migration currently relocates the polity's settlement records together
- starting polities can now begin with one home settlement anchor already present

### Polity Knowledge Split

LivingWorld now treats polity knowledge as two separate layers:

- `Discoveries`
  - cultural knowledge about the world
  - examples: edible species, toxic species, dangerous prey, resources, geography, environmental understanding
- `Advancements`
  - learned capability-granting practices and techniques
  - examples: Fire, Organized Hunting, Seasonal Planning, Agriculture

Discoveries can exist without any advancement, and advancements do not replace the discovery model.

## WorldEvent

`WorldEvent` is the canonical historical record.

It includes:

- time: `EventId`, `Year`, `Month`, `Season`
- phase: `SimulationPhase`
- origin: `Origin`
- causal ancestry: `RootEventId`, `ParentEventIds`, `PropagationDepth`
- classification: `Type`, `Severity`, `Scope`
- narrative fields: `Narrative`, `Details`, `Reason`
- entity references: polity, related polity, species, region, settlement ids and names
- visible-chronicle polity formatting support: primary and related polity species names
- context maps: `Before`, `After`, `Metadata`

## Species

`Species` now covers both polity-forming species and wildlife.

Selected ecology fields:

- `IsSapient`
- `TrophicRole`
- habitat preferences for fertility, water, and biomass
- `PreferredBiomes`
- `InitialRangeRegionIds`
- `DietSpeciesIds`
- `BaseCarryingCapacityFactor`
- `MigrationCapability`, `ExpansionPressure`
- seasonal reproduction and decline inputs
- hunting traits such as `MeatYield`, `HuntingDifficulty`, `HuntingDanger`, `IsToxicToEat`
- `DomesticationAffinity`
- `EarliestSpeciationYear`

## RegionSpeciesPopulation

Each `Region` now owns first-class regional population entries for species.

Selected fields:

- `SpeciesId`
- `RegionId`
- `PopulationCount`
- `CarryingCapacity`
- `BaseHabitatSuitability`
- `HabitatSuitability`
- `MigrationPressure`
- recent predation, hunting, and food-stress markers
- `SeasonsUnderPressure`
- seeded starting population is now derived from this entry's carrying capacity and habitat fit, so fertile regions can begin with meaningfully sized herbivore populations
- `MigrationCooldownSeasons` now paces repeated founder attempts so populations do not thrash between regions every season
- `FounderSeasonsRemaining` tracks short predator founder-establishment windows so new predator colonies can either mature or fail under normal ecology rules
- per-population trait offsets for Intelligence, Sociality, Aggression, Endurance, Fertility, DietFlexibility, ClimateTolerance, and Size
- accumulated mutation pressure by cause: food stress, predation, hunting, habitat mismatch, isolation, crowding, and low-pressure drift
- divergence tracking: `DivergencePressure`, `DivergenceScore`, `IsolationSeasons`, `SpeciationReadinessSeasons`, mutation counts, and milestone markers
- founder/source metadata: founder kind, source region/species, and founder year/month
- extinction bookkeeping: `HasEverExisted`, local-extinction markers, and last exit reason
- adaptation tracking: `LastAdaptationMilestone` plus compatibility `RegionAdaptationRecorded`
- seasonal exchange markers so isolation and migration shock can be resolved cleanly
- ancestral-fit versus adapted-fit tracking so regional adaptation can compare baseline species suitability against evolved local suitability

Important storage rule:

- `RegionSpeciesPopulation` is sparse by default
- regions do not maintain permanent entries for every species
- never-established empty region-species pairs should be absent unless an explicit migration/speciation/founder action creates one
- empty records can be pruned again when they never became meaningful state

These entries are population-level only. LivingWorld still does not simulate individual animals or genetics.
The global `Species` definition remains the ancestral baseline. Mutation and divergence now happen on `RegionSpeciesPopulation` so one regional lineage can adapt without rewriting the parent species everywhere else.
When divergence matures into speciation, a new `Species` record is created with parent/root lineage metadata, origin region/time, and inherited baseline traits derived from the evolved regional population.
That descendant population does not inherit full speciation readiness. Isolation progress, divergence readiness, and immediate re-branching pressure are reset or heavily damped so the new species must stabilize before it can ever speciate again.

## Region

`Region` now also carries lightweight biome identity through `Biome`.

That biome is used by:

- world generation profiles
- seeded species-range selection
- habitat suitability scoring
- starting-polity placement heuristics

Selected ecology/resource fields on `Region`:

- `PlantBiomass`
  - current forageable plant biomass
- `MaxPlantBiomass`
  - plant biomass capacity used by growth and carrying-capacity heuristics
- `AnimalBiomass`
  - derived summary of current non-producer regional populations
- `MaxAnimalBiomass`
  - ecological capacity input used by habitat and carrying-capacity calculations

Important model rule:

- `PlantBiomass` is still a directly gathered monthly resource
- `AnimalBiomass` is no longer a directly consumable food pool
- animal food enters polity stores only through successful species-level hunting
- wildlife recovery comes from `RegionSpeciesPopulation` reproduction, migration, and habitat fit, then flows back into `AnimalBiomass` during ecosystem sync
- early wildlife richness now comes from broader consumer seeding plus stronger herbivore ecological capacity in producer-rich regions, not from reintroducing an abstract animal reserve
- migration now creates real founder populations in neighboring regions rather than toggling abstract range flags, so later growth, collapse, hunting pressure, and mutation all act on the same population records
- predator founder populations use that same record model, with prey-support thresholds and a short establishment window rather than a separate scripted predator layer
- recolonization reuses those same regional records; empty regions regain fauna only through adjacent founder spread

## Presentation And Persistence Types

- `ChronicleFocus`
- `ChronicleFocusSelection`
- `ChronicleFocusTransition`
- `IPolityFocusSelector`
- `LineagePolityFocusSelector`
- `ChroniclePresentationPolicy`
- `ChronicleEventFormatter`
- `ChronicleWatchRenderer`
- `WatchUiState`
- `WatchViewType`
- `WatchViewCatalog`
- `WatchInputController`
- `WatchInspectionData`
- `WatchKnowledgeSnapshot`
- `WatchScreenBuilder`
- `HistoryJsonlWriter`

`WatchUiState` is UI-only state. It tracks:

- active watch view
- paused/running state
- remembered selected index per list view
- remembered scroll offset per scrollable view
- current inspected region/species/polity ids for detail pages
- a lightweight back stack for `Esc` navigation

`WatchKnowledgeSnapshot` is the current player-knowledge projection for one watch render/input pass.
It centralizes:

- known regions from settlements, current center, discovered regions, and immediate neighboring connections
- known species from visible populations, discovered species-use/safety knowledge, and known polity species
- known polities from currently known regions
- discovery-indexed region/species summaries for inspection screens

`ChroniclePresentationPolicy` now carries both family-specific semantic cooldown profiles and a fallback normalized-narrative state key for visible event families that do not yet have a dedicated semantic signature.
It also treats bootstrap-tagged events as canonical-but-non-player-facing setup history, so initialization baselines do not appear in the live chronicle or recent major-event summaries.

## Propagation Types

- `EventPropagationCoordinator`
- `IWorldEventHandler`
- handler implementations in `LivingWorld/Systems`

## Design Notes

- simulation behavior remains full-world
- event capture is source-of-truth and output-agnostic
- propagation extends the same event stream instead of replacing it
- storage order and display order are intentionally different
- lower-severity and chronicle-suppressed events remain structured even when hidden from the live chronicle
- divergence state stays lightweight, but it now promotes existing regional populations into descendant species instead of stopping at milestone-only tracking
- settlement-local execution is intentionally lightweight so later settlement-specialization and cross-region trade can reuse the same records instead of reintroducing polity-level shortcuts
- cached lookup snapshots plus direct region species-population indexing are intentional infrastructure for both safety and performance in hot paths
- simulation control may now also keep rolling year-local summaries such as current-year event caches and perf counters so late-game work does not require rescanning full historical storage

## Phase 13/14 Additions

`Settlement` now also carries:

- `ManagedHerds`
- `CultivatedCrops`
- `ManagedAnimalFoodThisMonth`
- `ManagedCropFoodThisMonth`
- `ManagedFoodThisYear`

`Polity` now also tracks:

- `CultivationFamiliarityBySpecies`
- `FoodManagedThisMonth`
- `AnnualFoodManaged`

New lightweight records:

- `ManagedHerd`
  - base species id
  - variant name
  - establishment year/month
  - herd size
  - reliability
  - breeding multiplier
- `CultivatedCrop`
  - base species id
  - crop name
  - establishment year/month
  - yield multiplier
  - stability bonus
  - seasonal resilience

`Species` also now includes `CultivationAffinity` alongside existing domestication suitability support.

## Phase 17 Additions

`Region` now also carries abundance values for:

- `WoodAbundance`
- `StoneAbundance`
- `ClayAbundance`
- `FiberAbundance`
- `SaltAbundance`
- `CopperOreAbundance`
- `IronOreAbundance`

`Settlement` now also carries:

- `ToolProductionTier`
- `MaterialStockpiles`
- `MaterialProducedThisMonth`
- `MaterialConsumedThisMonth`
- `MaterialProducedThisYear`
- `MaterialConsumedThisYear`
- `MaterialTargetReserves`
- `MaterialPressureStates`
- `LastRecordedMaterialShortageBands`
- `SpecializationScores`
- `SpecializationTags`
- `MaterialMilestonesRecorded`

`Polity` now also tracks:

- `MaterialMovedThisYear`

New economy types:

- `MaterialType`
- `MaterialPressureState`
- `ProductionRecipe`
- `SettlementSpecializationTag`

These remain lightweight settlement-level records rather than a full market model. Regions provide abundance, settlements convert that capacity into stockpiled materials, and the event stream records major material transitions.

## Phase 18 Additions

`Settlement` now also carries lightweight economy-signal state for each `MaterialType`:

- `MaterialNeedPressures`
- `MaterialAvailabilityScores`
- `MaterialValueScores`
- `MaterialOpportunityScores`
- `MaterialExternalPullReadiness`
- `MaterialProductionFocusScores`
- `LastRecordedHighlyValuedBands`
- `LastRecordedTradeGoodStates`
- `HighlyValuedMaterials`
- `TradeGoodMaterials`
- `LocallyCommonMaterials`
- `DominantProductionFocusMaterial`
- `CandidateProductionFocusMaterial`
- `CandidateProductionFocusMonths`
- `ProductionFocusShiftCooldownMonths`

New economy type:

- `EconomySummaryLabel`

These additions keep the model hybrid:

- internal numeric pressure stays simulation-side
- player-facing screens consume readable labels built from those signals
- no currency, coin inventory, or explicit buy/sell market table is added in this phase
