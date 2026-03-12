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
- per-population trait offsets for Intelligence, Sociality, Aggression, Endurance, Fertility, DietFlexibility, ClimateTolerance, and Size
- accumulated mutation pressure by cause: food stress, predation, hunting, habitat mismatch, isolation, crowding, and low-pressure drift
- divergence tracking: `DivergenceScore`, `IsolationSeasons`, mutation counts, and milestone markers
- adaptation tracking: `LastAdaptationMilestone` plus compatibility `RegionAdaptationRecorded`
- seasonal exchange markers so isolation and migration shock can be resolved cleanly
- ancestral-fit versus adapted-fit tracking so regional adaptation can compare baseline species suitability against evolved local suitability

These entries are population-level only. LivingWorld still does not simulate individual animals or genetics.
The global `Species` definition remains the ancestral baseline. Mutation and divergence now happen on `RegionSpeciesPopulation` so one regional lineage can adapt without rewriting the parent species everywhere else.

## Region

`Region` now also carries lightweight biome identity through `Biome`.

That biome is used by:

- world generation profiles
- seeded species-range selection
- habitat suitability scoring
- starting-polity placement heuristics

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
- `WatchInputController`
- `WatchInspectionData`
- `WatchScreenBuilder`
- `HistoryJsonlWriter`

`WatchUiState` is UI-only state. It tracks:

- active watch view
- paused/running state
- remembered selected index per list view
- remembered scroll offset per scrollable view
- current inspected region/species/polity ids for detail pages
- a lightweight back stack for `Esc` navigation

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
- divergence state is intentionally lightweight so future speciation or domesticated variants can promote an existing regional population instead of replacing the architecture
- settlement-local execution is intentionally lightweight so later settlement-specialization and cross-region trade can reuse the same records instead of reintroducing polity-level shortcuts
- cached lookup snapshots plus direct region species-population indexing are intentional infrastructure for both safety and performance in hot paths
