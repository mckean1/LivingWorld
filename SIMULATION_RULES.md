# LivingWorld Simulation Rules

This document defines the **core simulation rules and formulas** used in the LivingWorld engine.

It describes how ecological systems, food production, population growth, migration, and societal development are calculated.

The purpose of this document is to ensure that simulation mechanics remain **consistent, predictable, and well-documented** as the engine evolves.

---

# Simulation Time

LivingWorld operates on **discrete monthly simulation ticks**.

```
1 tick = 1 month
12 ticks = 1 year
```

Most systems update every tick, while some effects (such as population growth) may occur annually.

---

# Seasonal Model

Seasons influence ecological productivity.

```
Winter
Spring
Summer
Autumn
```

Seasonal impacts may affect:

- plant growth
- animal reproduction
- food availability
- migration pressure

Example seasonal modifiers:

| Season | Plant Growth Modifier |
|------|------|
| Winter | -50% |
| Spring | +50% |
| Summer | +25% |
| Autumn | 0% |

Exact values may be tuned during development.

---

# Ecology Rules

Regions generate biological resources that support life.

Each region contains:

- plant biomass
- animal biomass

These values represent the total ecological resources available.

---

## Plant Biomass Regeneration

Plant biomass regenerates each month based on region characteristics.

Example formula:

```
plant_growth =
    max_plant_biomass
    * fertility
    * water_availability
    * seasonal_modifier
```

Plant biomass cannot exceed the region's maximum capacity.

```
plant_biomass = min(max_plant_biomass, plant_biomass + plant_growth)
```

---

## Animal Biomass Regeneration

Animal biomass depends partly on plant biomass.

Example rule:

```
animal_growth =
    plant_biomass
    * reproduction_rate
```

Animal biomass also has a regional maximum.

---

# Food Gathering

Societies gather food from regional biomass.

Food sources include:

- plant biomass
- animal biomass

Food gathering capacity depends on population size.

Example formula:

```
food_gathered =
    population
    * gathering_efficiency
```

Where gathering efficiency may depend on:

- species traits
- technology level
- environmental conditions

---

## Biomass Consumption

Food gathering reduces regional biomass.

Example rule:

```
region.plant_biomass -= gathered_plants
region.animal_biomass -= gathered_animals
```

Biomass cannot fall below zero.

---

# Food Consumption

Each population consumes food every month.

Example formula:

```
monthly_food_required =
    population
    * food_per_person
```

If food stores are insufficient, starvation occurs.

---

# Starvation

Starvation occurs when food consumption requirements are not met.

Example starvation ratio:

```
food_ratio =
    food_consumed
    / food_required
```

Where:

| Food Ratio | Outcome |
|------|------|
| ≥ 1.0 | population stable |
| 0.75 - 1.0 | mild stress |
| 0.50 - 0.75 | moderate decline |
| < 0.50 | severe starvation |

Deaths increase as the food ratio decreases.

---

# Population Growth

Population growth depends primarily on food availability.

Example annual growth rule:

```
if food_ratio >= 1.0
    population_growth =
        population
        * reproduction_rate
```

Population decline occurs when starvation persists.

Example starvation deaths:

```
population_loss =
    population
    * starvation_factor
```

---

# Migration Pressure

Migration occurs when local conditions become unfavorable.

Migration pressure increases due to:

- food shortages
- ecological depletion
- overcrowding
- environmental opportunity elsewhere

Example migration pressure formula:

```
migration_pressure =
    food_shortage
    + overcrowding
    + environmental_difference
```

When migration pressure exceeds a threshold, societies may move to neighboring regions.

---

# Regional Carrying Capacity

Each region has an approximate carrying capacity based on ecological productivity.

Example estimate:

```
carrying_capacity =
    plant_biomass
    / food_per_person
```

If population exceeds carrying capacity, migration pressure increases.

---

# Society Splitting

Large societies may divide into smaller groups.

Example conditions:

```
if population > split_threshold
and migration_pressure > threshold
```

Then a new society may form.

Example:

```
population_a = 60
population_b = 40
```

Both groups continue independently.

---

# Settlement Formation

Settlements form when societies achieve stable food production.

Possible requirements:

- stable food surplus
- population above threshold
- suitable environment

Settlements allow:

- agriculture
- infrastructure
- economic specialization

---

# Advancement Discovery

Advancements represent new capabilities discovered by societies.

Discovery probability depends on:

- environmental exposure
- societal need
- knowledge prerequisites
- surplus labor
- time

Example conceptual formula:

```
discovery_chance =
    prerequisite_factor
    * environmental_exposure
    * societal_need
    * surplus_capacity
    * randomness
```

Advancements unlock new capabilities such as:

- agriculture
- pottery
- metalworking
- writing
- administration

---

# Knowledge Diffusion

Advancements can spread between societies through:

- trade
- migration
- conquest
- cultural contact

Diffusion increases discovery speed in neighboring societies.

---

# Knowledge Loss

Technological knowledge can be lost during societal collapse.

Knowledge loss may occur due to:

- population collapse
- loss of specialists
- cultural fragmentation
- isolation

Some knowledge is more resilient than others.

Examples:

Stable knowledge:

- fire use
- basic tools

Fragile knowledge:

- writing
- advanced metallurgy
- complex administration

---

# Economic Systems (Future)

Economic systems will emerge from surplus production.

Possible economic mechanics:

- resource production
- supply and demand
- trade networks
- regional specialization

Markets emerge as societies exchange resources.

---

# Simulation Stability Goals

Simulation rules should ensure that:

- ecosystems regenerate sustainably
- populations fluctuate realistically
- migration occurs naturally
- civilizations emerge gradually

The simulation should avoid:

- runaway population growth
- ecological collapse every cycle
- static worlds with no change

Balancing these rules will be an ongoing process during development.

---

# Future Expansion

As the engine evolves, this document will expand to include rules for:

- trade systems
- diplomacy
- warfare
- cultural divergence
- long-term ecological change

These systems will build upon the foundational mechanics described above.

---

# Summary

LivingWorld uses a layered simulation model where each system builds upon the previous layer.

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
History
```

By documenting these rules clearly, LivingWorld can evolve into a robust simulation capable of generating complex emergent histories.