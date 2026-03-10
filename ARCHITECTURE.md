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
* splitting into new societies

Over time, polities may transition into **civilizations** as their complexity grows.

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
* food storage
* food consumption
* famine detection

Food availability directly influences population growth and migration.

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

---

## Knowledge System

Knowledge represents discovered capabilities.

LivingWorld uses a **probabilistic discovery system** instead of a fixed tech tree.

Discovery may depend on:

* environmental exposure
* societal need
* prerequisite knowledge
* available surplus

Knowledge unlocks new capabilities in the simulation.

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
