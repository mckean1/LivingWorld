# LivingWorld Architecture

This document describes the **technical architecture** of the LivingWorld simulation engine.

It defines how the simulation is structured, how systems interact with data, and the rules for safely extending the engine as new features are added.

---

# Architectural Principles

LivingWorld follows several key architectural principles:

### Deterministic Simulation

Given the same seed and inputs, the simulation should produce the same results.

This allows:

- reproducibility
- debugging
- testing
- world regeneration

---

### System-Based Simulation

LivingWorld uses a **system-based simulation model**.

Systems operate on shared world data during each simulation tick.

```
World State
   ↓
Simulation Systems
   ↓
Updated World State
```

Examples of systems include:

- EcologySystem
- FoodSystem
- PopulationSystem
- MigrationSystem
- EconomySystem
- DiplomacySystem

Each system performs a **single responsibility**.

---

### Centralized Simulation Loop

All simulation updates occur within the **Simulation loop**.

Systems must **never trigger other systems directly**.

```
Simulation
 ├ AdvanceTime
 ├ EcologySystem
 ├ FoodSystem
 ├ PopulationSystem
 ├ MigrationSystem
 ├ SocietySystem
 └ Reporting
```

This ensures:

- predictable execution order
- easier debugging
- prevention of circular dependencies

---

# Core Data Model

The world simulation is built around a **shared world state object**.

```
World
 ├ Regions
 ├ Species
 ├ Societies
 ├ Resources
 └ Simulation Time
```

The `World` object acts as the central container for all simulation data.

Systems receive the world state and mutate it.

---

# Data Ownership

To maintain clean architecture, each data type has a clear owner.

| Data | Owner |
|-----|-----|
| World time | World |
| Regions | World |
| Species | World |
| Societies | World |
| Population | Societies |
| Biomass | Regions |
| Food stores | Societies |

Systems operate on this data but **do not own it**.

---

# Project Structure

```
LivingWorld/
 ├ Core/
 │   ├ World.cs
 │   ├ WorldTime.cs
 │   └ Simulation.cs
 │
 ├ Generation/
 │   └ WorldGenerator.cs
 │
 ├ Life/
 │   └ Species.cs
 │
 ├ Map/
 │   └ Region.cs
 │
 ├ Societies/
 │   └ Polity.cs
 │
 ├ Systems/
 │   ├ FoodSystem.cs
 │   └ PopulationSystem.cs
 │
 └ Program.cs
```

---

# Core Components

## World

`World` is the central container for all simulation state.

It contains:

- regions
- species
- societies
- simulation time

Systems read and mutate data inside the world object.

---

## WorldTime

`WorldTime` manages the simulation calendar.

```
1 Tick = 1 Month
12 Ticks = 1 Year
```

WorldTime also determines the current season.

---

## Simulation

`Simulation` controls the simulation loop.

Responsibilities include:

- advancing time
- running simulation systems
- generating reports
- maintaining system update order

---

# Simulation Systems

Simulation systems perform updates to world state.

Each system should follow these rules:

- perform one responsibility
- avoid interacting with other systems directly
- only read/write world data

### Example System

```
FoodSystem
 ├ Gather food from regions
 ├ Update food stores
 └ Reduce ecological biomass
```

---

# System Execution Order

System order matters because later systems depend on earlier ones.

```
1. Time Update
2. Ecology Update
3. Food Gathering
4. Food Consumption
5. Population Update
6. Migration Pressure
7. Society Updates
8. Reporting
```

Future systems will be inserted carefully into this pipeline.

---

# Randomness and Seeds

All randomness should originate from a **controlled seed**.

Example:

```
WorldGenerator(seed)
```

Using seeded randomness ensures:

- reproducible worlds
- consistent debugging
- deterministic simulations

---

# Extending the Simulation

When adding new systems, follow these guidelines.

### 1. Create a new system class

Example:

```
MigrationSystem.cs
```

---

### 2. Define a single responsibility

A system should only handle one domain.

Examples:

- migration
- diplomacy
- economy
- warfare

---

### 3. Integrate into the simulation loop

Add the system to the `Simulation` update order.

```
Simulation
 ├ EcologySystem
 ├ FoodSystem
 ├ PopulationSystem
 ├ MigrationSystem
```

---

### 4. Avoid cross-system dependencies

Systems should not call each other.

Incorrect:

```
FoodSystem → PopulationSystem
```

Correct:

```
Simulation → FoodSystem
Simulation → PopulationSystem
```

---

# Performance Strategy

LivingWorld prioritizes **scalable simulation design**.

Key techniques include:

### Aggregated Population

Population is stored as counts rather than individual agents.

### Region Abstraction

Regions represent large areas rather than grid tiles.

### System Separation

Each system processes only the data it requires.

---

# Testing Strategy

Future development should include:

- deterministic simulation tests
- system-level unit tests
- simulation stability tests

Example test scenarios:

- famine collapse
- migration waves
- population booms

---

# Future Architectural Extensions

As the simulation expands, new systems may include:

- MigrationSystem
- SettlementSystem
- AdvancementSystem
- EconomySystem
- TradeSystem
- DiplomacySystem
- WarfareSystem
- CultureSystem

Each new system must follow the architecture rules described above.

---

# Long-Term Engine Structure

The long-term architecture may evolve toward a more structured simulation framework:

```
World
 ├ Core Data
 ├ Simulation Systems
 ├ Generation Systems
 └ Reporting Systems
```

This structure allows the engine to grow without sacrificing maintainability.

---

# Summary

LivingWorld uses a **system-driven simulation architecture** built around shared world state and deterministic updates.

Key characteristics include:

- deterministic simulation
- modular systems
- centralized update loop
- clear data ownership
- scalable design

This architecture supports the long-term goal of simulating **complex emergent world histories**.