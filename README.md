# LivingWorld

LivingWorld is a **procedural world simulation engine** where species evolve, societies form, civilizations emerge, and complex histories unfold over long spans of simulated time.

The project focuses on **emergent systems**, where large-scale historical outcomes arise naturally from simple interacting rules.

---

# Project Goals

LivingWorld aims to simulate:

- ecological systems
- species evolution
- society formation
- civilization development
- migration
- resource competition
- long-term historical change

Rather than scripting events, LivingWorld relies on **system interactions** to produce history.

---

# Core Philosophy

## Emergence Over Scripting

Civilizations, trade networks, migration, famine, and conflict should arise naturally from simulation systems.

## Ecology Drives History

Food production and ecological capacity drive population growth and migration.

```
sunlight
→ plants
→ animals
→ food
→ population
→ societies
→ civilizations
```

## Autonomous Systems

Most systems operate autonomously.  
The player will eventually guide strategic direction rather than micromanaging individual actors.

## Long Timescale Simulation

The world can simulate **hundreds or thousands of years** before the player begins interacting with it.

---

# World Model

LivingWorld uses a **pure region-based world map**.

```
World
 └ Regions
     ├ Ecology
     ├ Resources
     ├ Species
     └ Societies
```

Regions store environmental values such as:

- fertility
- water availability
- plant biomass
- animal biomass
- biome
- climate

Regions act as **ecological containers** that societies interact with.

---

# Species

Species emerge from primitive life through evolutionary processes.

Species define biological traits that influence behavior:

- intelligence
- cooperation
- aggression
- adaptability
- reproduction rate
- curiosity

These traits influence how societies grow and develop.

---

# Societies and Civilizations

LivingWorld models social development as a **single evolving entity**.

```
Society → Civilization → Nation → Empire
```

A **Society** represents a cohesive social group.

A **Civilization** emerges when a society develops sufficient complexity, such as:

- permanent settlements
- food surplus
- labor specialization
- leadership structures
- multiple communities

The same lineage evolves through these stages.

---

# Population Model

Population is modeled using **aggregated counts**, not individuals.

Population exists on:

- societies
- settlements (later)

Example:

```
Stone River Society
Population: 48
```

Future versions may introduce demographic cohorts:

```
Young
Adult
Old
```

---

# Ecology and Food

Regions generate **biomass**:

- plant biomass
- animal biomass

Societies harvest biomass for food.

Food systems drive:

- population growth
- migration
- famine
- societal stability

Seasonal cycles influence ecological production:

```
Spring → growth
Summer → peak biomass
Autumn → harvest
Winter → scarcity
```

---

# Simulation Time

LivingWorld uses **monthly simulation ticks**.

```
1 tick = 1 month
12 ticks = 1 year
```

Seasonal logic is layered on top of monthly ticks.

Current simulation loop:

```
1. Advance Time
2. Region Ecology Update
3. Food Gathering
4. Food Consumption
5. Population Update
6. Yearly Report
```

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

# Running the Simulation

Currently the project runs as a **console simulation**.

The program will:

1. Generate a world
2. Generate species
3. Generate starting societies
4. Run the simulation month-by-month
5. Print yearly summaries

The simulation pauses each year so results can be reviewed.

---

# Roadmap

## Current Systems

- Region ecology
- Species generation
- Societies
- Food gathering
- Population dynamics
- Simulation loop

## Planned Systems

- Migration and expansion
- Society splitting
- Settlement creation
- Advancement / knowledge system
- Society → civilization transitions
- Trade networks
- Diplomacy and conflict
- Species evolution
- Long-term ecological change

---

# Development Status

LivingWorld is currently in **early simulation engine development**.

The current focus is building a stable simulation foundation before introducing gameplay systems.

---

# Long-Term Vision

The long-term goal is a **fully autonomous historical simulation** where:

- ecosystems evolve
- species emerge
- societies form
- civilizations rise and fall
- cultures diverge
- economies develop

Each generated world should produce **unique histories** driven entirely by system interactions.