# LivingWorld

LivingWorld is a command-line autonomous world simulation where ecosystems, species, societies, and civilizations emerge over time.

The simulation generates a procedural world and runs centuries of history before the player enters. During this time species spread, societies migrate, settlements form, knowledge is discovered, and civilizations begin to emerge.

The goal of the project is to produce **emergent history**, where complex civilizations arise naturally from interacting systems such as ecology, population dynamics, migration, resource pressure, and knowledge discovery.

---

# Core Design Principles

LivingWorld prioritizes **emergent simulation** rather than scripted gameplay.

The world evolves through interacting systems where:

* species adapt to environments
* societies respond to ecological pressure
* migration redistributes populations
* settlements anchor civilization growth
* knowledge emerges from conditions
* history develops naturally through simulation

The simulation models **processes that create history**, rather than scripting historical events.

---

# Simulation Overview

The simulation runs in **monthly ticks**, with seasonal logic layered on top.

Each simulated year roughly processes:

```
Ecological growth
Resource harvesting
Food consumption
Population growth or decline
Migration decisions
Settlement development
Knowledge discovery
Polity events
Historical logging
```

The world is simulated for approximately **1000 years before the player begins**, allowing civilizations to develop naturally.

---

# World Model

The world is composed of **regions rather than grid cells or tiles**.

Each region stores abstract environmental and ecological information.

Region properties may include:

* climate
* fertility
* water access
* ecological biomass
* species presence
* settlements
* societies

Regions generate **biomass**, representing plant and animal life available for consumption.

---

# Species

Species represent biological populations inhabiting the world.

Species influence:

* ecological efficiency
* environmental adaptation
* behavioral tendencies
* survival traits

Societies are always composed of members of a **single species**.

---

# Societies and Polities

The primary social unit in the simulation is a **Polity**.

A polity represents an organized group of a species acting as a social and economic unit.

Polities may:

* migrate between regions
* found settlements
* split into new groups
* discover knowledge
* experience famine or surplus

Over time, polities may evolve into **civilizations** as their complexity increases.

---

# Population Model

Population is tracked as **aggregated counts** rather than individual agents.

Population is typically stored at the polity or settlement level.

Population change is influenced by:

* food availability
* environmental conditions
* migration opportunities
* societal stability

Future versions may introduce simplified age cohorts such as:

* young
* adult
* old

---

# Ecology and Food System

Regions generate **ecological biomass** each season.

This biomass represents food accessible through activities such as:

* hunting
* gathering
* fishing
* agriculture

Food dynamics include:

* seasonal growth
* harvesting
* storage
* famine events

Food availability strongly influences migration, settlement stability, and population growth.

---

# Settlements

Settlements represent **permanent population centers** founded by societies.

Settlements track:

* population
* founding year
* region location
* food production

Settlements allow societies to transition from nomadic groups into more complex social structures.

They serve as the foundation for civilization development.

---

# Knowledge and Advancement

LivingWorld uses a **probabilistic knowledge discovery system** rather than a rigid technology tree.

Knowledge discovery depends on factors such as:

* environmental exposure
* societal needs
* prerequisite knowledge
* available surplus
* time

Examples of knowledge include:

* agriculture
* pottery
* animal domestication
* construction techniques

Knowledge unlocks **new capabilities in the simulation**, affecting food production, settlement development, and societal complexity.

---

# Migration and Societal Change

Societies dynamically respond to environmental pressures.

Migration may be triggered by:

* food scarcity
* population pressure
* ecological opportunity
* internal societal tension

Societies may also split into new groups when internal pressures become too large.

This produces natural expansion and cultural divergence across regions.

---

# Historical Event Logging

The simulation records important events as a chronological history.

Logs are intentionally short and readable, resembling historical records.

Example:

```
Year 412

Red River Clan migrated to Northern Plains
Stone Ford discovered Agriculture
Oak Valley Society founded River Camp
```

Only **notable events** are included in the default output to maintain readability.

---

# Player Entry

After the autonomous world simulation completes, the player selects an existing society.

The player then guides the strategic development of that society as it grows into a civilization.

The underlying simulation continues to run throughout gameplay.

---

# Architecture Overview

The simulation is organized into several interacting systems:

```
World
 ├─ Regions
 ├─ Species
 ├─ Polities
 ├─ Settlements
 ├─ Ecology System
 ├─ Population System
 ├─ Food System
 ├─ Knowledge System
 └─ Historical Logging
```

These systems interact to generate emergent world history.

---

# Development Roadmap

Planned systems for future development include:

### Knowledge Diffusion

Spread of discoveries between societies through migration, proximity, trade, and conflict.

### Trade Networks

Exchange of goods and resources between settlements and civilizations.

### Cultural Divergence

Development of distinct cultural identities.

### Warfare and Territorial Conflict

Competition between civilizations for land and resources.

### Governance Systems

Internal political structures within civilizations.

### Dynamic Economy

Supply and demand systems influencing production and trade.

---

# Project Goals

LivingWorld explores questions such as:

* How do civilizations emerge from ecological pressure?
* How do migration and scarcity shape history?
* How does knowledge spread between societies?
* What conditions lead to societal collapse?

By simulating these processes, LivingWorld aims to produce **dynamic and believable world histories**.
