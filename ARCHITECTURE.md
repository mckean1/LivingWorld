# ARCHITECTURE.md

# LivingWorld Architecture

This document describes the high-level architecture of the LivingWorld simulation and how the major systems interact.

LivingWorld is built as a **systems-driven simulation** where independent systems interact to produce emergent world history.

---

# Core Simulation Structure

The simulation is centered around a **World** object that contains all entities and systems.

High-level structure:

```
World
 ├─ Regions
 ├─ Species
 ├─ Polities
 ├─ Settlements
 ├─ Ecology System
 ├─ Food System
 ├─ Population System
 ├─ Migration System
 ├─ Knowledge System
 └─ Historical Event System
```

Each system updates the world state during the simulation loop.

The yearly loop now includes a dedicated polity stage pass that advances polities through:

* Band
* Tribe
* Settled Society
* Civilization

---

# Core Entities

## World

The World object acts as the root container for the simulation.

Responsibilities:

* stores all regions
* stores species definitions
* stores all polities
* coordinates the simulation loop
* records historical events

---

## Region

Regions represent geographic areas of the world.

The simulation uses **regions rather than grid cells**.

Regions store environmental properties that affect societies and ecosystems.

Typical region data includes:

* climate
* fertility
* water availability
* biomass production
* species present
* settlements in the region
* polities occupying the region

Regions generate **biomass**, which represents available food resources.

---

## Species

Species represent biological populations.

Species influence how societies interact with the environment.

Species traits may affect:

* hunting efficiency
* agricultural potential
* environmental adaptability
* migration tendencies

Each polity belongs to exactly **one species**.

---

## Polity

A polity represents a cohesive social group.

Examples:

* clans
* tribes
* early civilizations

Polities are responsible for:

* managing population
* founding settlements
* migrating between regions
* discovering knowledge
* splitting into new societies through yearly fragmentation checks

Over time, polities may transition into **civilizations** as their complexity grows.

Each polity stores a persistent stage value, so progression is explicit state rather than inferred only from settlement status.

---

## Settlement

Settlements represent permanent population centers.

A settlement typically contains:

* population
* founding year
* region location
* food production
* stored food

Settlements anchor societies geographically and enable long-term growth.

Multiple settlements within one polity represent increasing societal complexity.

---

# Core Systems

## Ecology System

The ecology system generates environmental resources.

Responsibilities include:

* generating biomass
* seasonal ecological growth
* determining regional productivity

---

## Food System

The food system converts ecological resources into usable food.

Processes include:

* harvesting biomass
* capability-modified harvest efficiency
* food storage
* capability-modified spoilage
* food consumption
* famine detection

Food availability directly influences population growth and migration.

---

## Agriculture System

The agriculture system computes crop production as a distinct process from wild harvesting.

Processes include:

* settlement-anchored farming eligibility checks
* cultivated land allocation constrained by regional arable capacity
* gradual cultivation expansion for established settlements
* seasonal crop yield calculations
* annual notable cultivation and harvest events

Agriculture output is tracked separately from gathered biomass and then added to polity food stores.

---

## Population System

Population is tracked as **aggregated counts** rather than individual agents.

Population changes include:

* growth during surplus
* decline during famine
* redistribution through migration
* fragmentation during polity splits

---

## Migration System

Migration allows societies to relocate when conditions change.

Migration may occur due to:

* food scarcity
* population pressure
* ecological opportunity
* social instability

Migration spreads societies across regions.

Migration handles **whole-polity relocation**. It does not create new polities.

---

## Fragmentation System

The fragmentation system evaluates each active polity once per year.

Responsibilities include:

* calculating a yearly `FragmentationPressure` value
* tracking repeated food-stress years
* applying anti-chain-splitting cooldowns
* creating child polities when pressure triggers a split
* conserving transferred population and stored food
* recording short historical split events

The first implementation focuses on **colony/offshoot fragmentation**.

This system intentionally replaces the earlier standalone colony expansion pass.
Expansion through relocation remains part of the migration system, while branching expansion is handled here.

Its pressure model is intentionally simple and tunable. Current inputs are:

* polity population size
* starvation months and annual food stress
* regional crowding in the home region
* migration strain from movement pressure and repeated moves

When a split succeeds, the child polity:

* keeps the parent species
* starts in a connected target region
* inherits some of the parent's advancements
* receives a modest share of the parent population and food stores
* records lineage through `ParentPolityId`
* starts without concrete settlement state until the settlement system founds one

This version remains **region-based** rather than using discrete branch settlements.
Settlement-related polity fields continue to represent actual established settlement presence, not abstract colony intent.

---

## Knowledge System

Knowledge represents discovered capabilities.

LivingWorld uses a **probabilistic discovery system** instead of a fixed tech tree.

Discovery may depend on:

* environmental exposure
* societal need
* prerequisite knowledge
* available surplus

Each advancement now carries one or more structured capability effects.
Each polity derives an active capability profile from discovered advancements.
Simulation systems consume derived capability flags and numeric modifiers instead of relying on one-off advancement checks.

Current first-pass capability effects include:

* Fire: lowers effective food need and slightly improves food use
* Stone Tools: improves harvest efficiency
* Storage: reduces spoilage losses
* Agriculture: enables settlement-anchored farming and cultivated land growth

---

## Polity Stage System

The polity stage system evaluates each active polity yearly and advances stages when thresholds are met.

Its v1 inputs are intentionally simple and tunable:

* total population
* polity longevity
* settlement durability
* annual food stability
* advancement count
* sustained stress indicators

This version is advancement-only (no automatic regression) and emits short historical transition events.
Civilization now requires a multi-settlement base (at least two settlements).

---

## Historical Event System

The historical event system records notable events.

Events are stored as chronological records representing world history.

Examples include:

* migrations
* settlement founding
* knowledge discoveries
* polity splits
* famine events
