# DATA_MODEL.md

# LivingWorld Data Model

This document describes the primary data structures used in the LivingWorld simulation.

The simulation uses **aggregated entities rather than individual agents**, which allows the world to simulate centuries of history efficiently.

---

# Core Entities

The simulation is built around several core data structures:

```
World
Region
Species
Polity
Settlement
Knowledge
HistoricalEvent
```

These entities interact to produce the evolving simulation.

---

# World

The `World` object is the root container for the simulation.

It stores all entities and coordinates the simulation loop.

Typical properties:

```
Regions
Species
Polities
Settlements
HistoricalEvents
CurrentYear
CurrentMonth
```

Responsibilities:

* advancing the simulation
* coordinating system updates
* recording historical events

---

# Region

Regions represent geographic areas of the world.

The simulation uses **abstract regions rather than tiles or grid cells**.

Regions provide the environmental context for societies and ecosystems.

Typical properties:

```
Name
Climate
Fertility
WaterAvailability
Biomass
SpeciesPresent
Settlements
PolitiesPresent
```

Responsibilities:

* generating ecological biomass
* providing food resources
* hosting species and societies

---

# Species

Species represent biological populations capable of forming societies.

Typical properties:

```
Name
Traits
EnvironmentalAdaptability
HuntingEfficiency
AgriculturalPotential
```

Responsibilities:

* influencing how societies interact with the environment
* shaping migration and survival behaviors

Each polity belongs to exactly **one species**.

---

# Polity

A `Polity` represents a cohesive social group.

Examples include:

* clan
* tribe
* early civilization

Typical properties:

```
Name
Species
Population
Settlements
CurrentRegion
KnownKnowledge
YearsSinceFounded
ParentPolityId
FragmentationPressure
FoodStressYears
SplitCooldownYears
```

Responsibilities:

* managing population
* founding settlements
* migrating between regions
* discovering knowledge
* splitting into new groups

Polities evolve over time and may transition into civilizations.

Fragmentation-related notes:

* `ParentPolityId` records simple parent-child lineage for split-off polities
* `FragmentationPressure` stores the current yearly split-pressure score for inspection and tuning
* `FoodStressYears` tracks sustained shortage pressure across years
* `SplitCooldownYears` prevents immediate repeat fragmentation

---

# Settlement

Settlements represent permanent population centers.

Typical properties:

```
Name
Region
Population
FoundingYear
FoodProduction
FoodStorage
ParentPolity
```

Responsibilities:

* producing food
* supporting population growth
* anchoring societies geographically

Multiple settlements within a polity indicate increasing societal complexity.

---

# Knowledge

Knowledge represents discovered capabilities.

LivingWorld uses a **probabilistic discovery system** rather than a rigid technology tree.

Typical properties:

```
Name
Category
Prerequisites
DiscoveryConditions
Effects
```

Examples:

* Agriculture
* Pottery
* Animal Domestication
* Construction

Knowledge unlocks new simulation behaviors.

---

# HistoricalEvent

Historical events record notable occurrences during the simulation.

Typical properties:

```
Year
EventType
Description
Polity
Settlement
Region
```

Examples of event types:

* Migration
* SettlementFounded
* KnowledgeDiscovered
* PolitySplit
* Famine

Events form a readable historical record of the world's development.

---

# Design Philosophy

The LivingWorld data model prioritizes:

* **simplicity**
* **scalability**
* **emergent behavior**

Entities represent aggregated groups rather than individuals.

This allows the simulation to run for **thousands of years** without excessive computational cost.

---

# Future Data Model Extensions

Future versions of the simulation may introduce additional entities:

```
TradeRoute
Culture
Economy
War
Diplomacy
ResourceTypes
```

These systems will expand the simulation from early societies into full civilizations.
