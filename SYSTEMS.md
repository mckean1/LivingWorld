# LivingWorld Simulation Systems

This document defines the **simulation systems** used in the LivingWorld engine.

Each system is responsible for updating a specific part of the world state during each simulation tick.

The purpose of this document is to:

- define system responsibilities
- describe system inputs and outputs
- prevent overlapping responsibilities
- maintain clean architecture as the simulation grows

---

# System Design Rules

All systems in LivingWorld must follow these rules:

### Single Responsibility

Each system should perform one task.

Example:

```
FoodSystem → food gathering and consumption
PopulationSystem → population growth and decline
```

---

### No Direct System Dependencies

Systems must not call each other directly.

Incorrect:

```
FoodSystem → PopulationSystem
```

Correct:

```
Simulation
 ├ FoodSystem
 ├ PopulationSystem
```

---

### Systems Operate on World State

Systems should only read and write data stored in the `World` object.

```
World
 ├ Regions
 ├ Species
 ├ Societies
 └ Resources
```

---

# Current Systems

The following systems are currently implemented.

---

# Ecology System

## Purpose

Simulates ecological regeneration within regions.

Regions generate biological resources that support life.

---

## Inputs

- region fertility
- region water availability
- seasonal effects
- current biomass levels

---

## Outputs

Updates:

- plant biomass
- animal biomass

---

## Responsibilities

- regenerate ecological resources
- enforce ecological capacity limits
- simulate seasonal growth patterns

---

# Food System

## Purpose

Allows societies to gather food from regional ecological resources.

---

## Inputs

- region plant biomass
- region animal biomass
- society population
- species traits (later)

---

## Outputs

Updates:

- society food stores
- regional biomass

---

## Responsibilities

- gather food from regions
- reduce biomass
- store food for societies

---

# Population System

## Purpose

Updates population based on food availability and natural growth.

---

## Inputs

- society population
- food consumption
- species reproduction rate (future)
- starvation conditions

---

## Outputs

Updates:

- population growth
- population decline
- demographic changes

---

## Responsibilities

- simulate births
- simulate deaths
- handle starvation effects

---

# Migration System (Planned)

## Purpose

Allows societies to move between regions when conditions become unfavorable.

Migration is driven by **migration pressure**.

---

## Inputs

- food shortages
- overcrowding
- ecological collapse
- environmental opportunity

---

## Outputs

Updates:

- society region location
- population distribution across regions

---

## Responsibilities

- calculate migration pressure
- evaluate neighboring regions
- move societies when pressure is high

---

# Settlement System (Planned)

## Purpose

Allows societies to establish permanent settlements.

Settlements represent stable population centers.

---

## Inputs

- population size
- food surplus
- environmental stability

---

## Outputs

Creates:

- settlements
- localized population centers

---

## Responsibilities

- settlement creation
- settlement growth
- settlement abandonment

---

# Advancement System (Planned)

## Purpose

Models technological and cultural development.

Advancements represent new **capabilities**, not simple bonuses.

---

## Advancement Examples

- fire use
- food storage
- agriculture
- pottery
- metalworking
- writing
- administration

---

## Inputs

- societal needs
- environmental exposure
- knowledge prerequisites
- time

---

## Outputs

Unlocks new capabilities such as:

- agriculture
- settlement expansion
- trade networks

---

# Economy System (Future)

## Purpose

Simulates production, consumption, and trade of resources.

---

## Inputs

- resource production
- population demand
- geographic access
- trade routes

---

## Outputs

Creates:

- trade networks
- economic specialization
- market systems

---

# Diplomacy System (Future)

## Purpose

Handles relationships between societies and civilizations.

---

## Possible interactions

- alliances
- trade agreements
- conflict
- cultural exchange

---

# Warfare System (Future)

## Purpose

Simulates military conflict between political entities.

---

## Possible mechanics

- territorial control
- conquest
- raids
- military organization

---

# Culture System (Future)

## Purpose

Models cultural divergence and identity formation.

---

## Possible features

- language divergence
- traditions
- religious development
- cultural diffusion

---

# System Execution Order

The simulation systems must run in a specific order.

```
1. Time Update
2. Ecology System
3. Food System
4. Population System
5. Migration System
6. Settlement System
7. Advancement System
8. Economy System
9. Diplomacy System
10. Warfare System
11. Reporting
```

This order ensures that each system receives correct inputs from previous systems.

---

# System Expansion Strategy

New systems should be added gradually and integrated into the simulation loop.

Recommended development order:

1. MigrationSystem
2. SettlementSystem
3. AdvancementSystem
4. EconomySystem
5. TradeSystem
6. DiplomacySystem
7. WarfareSystem

This progression allows the simulation to evolve organically.

---

# Long-Term System Goals

The ultimate goal is a layered simulation where systems interact naturally.

```
Ecology
   ↓
Food Production
   ↓
Population
   ↓
Migration
   ↓
Settlements
   ↓
Civilizations
   ↓
Economies
   ↓
Diplomacy & Warfare
   ↓
History
```

Each layer builds upon the previous one.

---

# Summary

The LivingWorld engine is built around **modular simulation systems** operating on shared world data.

This system-based design enables:

- emergent historical simulation
- scalable world models
- extensible architecture
- long-term simulation stability