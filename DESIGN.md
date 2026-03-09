# LivingWorld Simulation Design

This document defines the **core simulation design principles and systems** that drive LivingWorld.

The purpose of this document is to serve as the **long-term design reference** for the project so that new features remain consistent with the simulation architecture.

---

# Core Simulation Philosophy

LivingWorld is designed as an **emergent historical simulation**.

Rather than scripting events or behaviors, the simulation produces outcomes from interacting systems.

Examples of emergent behavior include:

- famine caused by ecological collapse
- migration caused by population pressure
- technological advancement driven by necessity
- civilization formation caused by surplus production

The simulation attempts to follow a fundamental chain of cause and effect:

```
environment
→ ecology
→ food production
→ population
→ societies
→ civilizations
→ economies
→ history
```

---

# Simulation Time

The simulation uses **discrete monthly ticks**.

```
1 tick = 1 month
12 ticks = 1 year
```

Seasonal effects are layered on top of monthly ticks.

### Seasons

```
Winter
Spring
Summer
Autumn
```

Seasonal cycles influence:

- ecological growth
- food availability
- migration pressure
- population stability

---

# World Model

The LivingWorld map is composed entirely of **regions**.

The design intentionally avoids cell grids to maintain abstraction and scalability.

```
World
 └ Regions
      ├ Ecology
      ├ Resources
      ├ Species
      └ Societies
```

Each region stores environmental attributes such as:

- fertility
- water availability
- plant biomass
- animal biomass
- biome
- climate

Regions represent **large ecological zones**, not individual tiles.

---

# Ecology System

Regions generate biological resources that support life.

### Primary ecological resources

- plant biomass
- animal biomass

Plant biomass represents:

- vegetation
- crops (later)
- forage

Animal biomass represents:

- wild animals
- huntable wildlife

Biomass regenerates based on:

- fertility
- water availability
- seasonal effects
- current biomass levels

Ecology forms the **foundation of the entire simulation**.

---

# Species

Species represent biological populations capable of forming societies.

Species have defining traits:

- intelligence
- cooperation
- aggression
- adaptability
- reproduction rate
- curiosity

These traits influence:

- social development
- technological advancement
- conflict behavior
- population growth

Species initially emerge through **evolutionary processes** within the simulation.

---

# Population Model

Population is represented using **aggregated counts**, not individual agents.

Population exists on:

- societies
- settlements (later stages)

Example:

```
Stone River Society
Population: 48
```

This approach allows simulation of large populations without excessive computational cost.

Future extensions may introduce **demographic cohorts**:

```
Young
Adult
Old
```

These cohorts enable modeling of:

- workforce
- military potential
- population aging
- birth and death rates

---

# Societies

A **Society** represents a cohesive population group belonging to a species.

Societies have attributes such as:

- population
- food stores
- cohesion
- leadership
- migration pressure
- region location

Societies are the earliest form of organized social structure.

---

# Civilization Progression

LivingWorld models political development as a **single evolving lineage**.

```
Society → Civilization → Nation → Empire
```

These are not separate entities but stages of development.

### Society

Small population groups with limited organization.

### Civilization

Emerges when a society develops:

- permanent settlements
- food surplus
- labor specialization
- leadership structures
- multiple population centers

### Nation / Empire

Later stages of large-scale political organization.

---

# Food System

Food production is derived from ecological resources.

Societies obtain food through:

- foraging
- hunting
- fishing
- agriculture (later)

Food production determines:

- population growth
- survival
- migration
- societal stability

Food shortages create **migration pressure**.

---

# Migration System

Migration occurs when societies experience pressure such as:

- food shortages
- ecological collapse
- overcrowding
- conflict
- environmental opportunity

Migration allows societies to move between connected regions.

Migration is one of the primary drivers of historical development.

---

# Settlement System (Future)

Societies may establish **permanent settlements** when conditions allow.

Settlement formation requires:

- stable food supply
- sufficient population
- environmental suitability

Settlements introduce new dynamics:

- local economies
- infrastructure
- agriculture
- trade

---

# Advancement / Knowledge System

Technological development is modeled using a **capability-based knowledge system**.

Advancements emerge probabilistically based on:

- prerequisites
- environmental exposure
- societal need
- surplus capacity
- time

Advancements unlock new capabilities rather than simple bonuses.

Examples:

- fire use
- food storage
- agriculture
- pottery
- metalworking
- writing
- administration

Knowledge may also spread through:

- trade
- cultural contact
- migration
- conquest

Some knowledge may be **lost after societal collapse**.

---

# Economic System (Future)

Economies emerge as societies develop surplus production.

Future economic mechanics may include:

- resource production
- supply and demand
- trade networks
- market development

Economic systems should emerge naturally from:

- geography
- resource distribution
- population density

---

# Simulation Loop

The simulation runs systems in a deterministic order each tick.

```
1. Advance Time
2. Ecology Update
3. Food Gathering
4. Food Consumption
5. Population Update
6. Migration Pressure Update
7. Society Update
8. Yearly Report
```

Maintaining a consistent update order prevents circular dependencies.

---

# Design Goals

The LivingWorld simulation is built to achieve the following goals:

### Scalability

The simulation should handle large populations and long timescales.

### Emergence

Historical outcomes should arise naturally from interacting systems.

### Modularity

Simulation systems should remain loosely coupled.

### Extensibility

New systems should be easy to integrate without rewriting existing ones.

---

# Current Implementation

The current prototype includes:

- region ecology
- species generation
- societies
- food gathering
- population dynamics
- simulation loop

Future work will expand these systems and introduce new ones.

---

# Long-Term Vision

The ultimate goal of LivingWorld is to simulate a dynamic world where:

- ecosystems evolve
- species appear and disappear
- societies form organically
- civilizations emerge naturally
- trade networks develop
- cultures diverge
- empires rise and fall

Each generated world should produce **unique emergent histories**.