# LivingWorld Data Model

LivingWorld uses aggregated entities rather than individual agents so long historical runs remain practical.

## Core Entities

- `World`
- `Region`
- `Species`
- `RegionSpeciesPopulation`
- `Polity`
- `PolityStage`
- `WorldEvent`
- `ChronicleFocus`

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
- settlements: `SettlementStatus`, `SettlementCount`, `YearsSinceFirstSettlement`
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
- `HabitatSuitability`
- `MigrationPressure`
- recent predation, hunting, and food-stress markers
- `SeasonsUnderPressure`
- per-population trait offsets for Intelligence, Sociality, Aggression, Endurance, Fertility, DietFlexibility, ClimateTolerance, and Size
- accumulated mutation pressure by cause: food stress, predation, hunting, habitat mismatch, isolation, crowding, and low-pressure drift
- divergence tracking: `DivergenceScore`, `IsolationSeasons`, mutation counts, and milestone markers
- seasonal exchange markers so isolation and migration shock can be resolved cleanly

These entries are population-level only. LivingWorld still does not simulate individual animals or genetics.
The global `Species` definition remains the ancestral baseline. Mutation and divergence now happen on `RegionSpeciesPopulation` so one regional lineage can adapt without rewriting the parent species everywhere else.

## Presentation And Persistence Types

- `ChronicleFocus`
- `ChronicleFocusSelection`
- `ChronicleFocusTransition`
- `IPolityFocusSelector`
- `LineagePolityFocusSelector`
- `ChroniclePresentationPolicy`
- `ChronicleEventFormatter`
- `ChronicleWatchRenderer`
- `HistoryJsonlWriter`

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
