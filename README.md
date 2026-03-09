LivingWorld

LivingWorld is a procedural world simulation engine where species evolve, societies form, civilizations emerge, and economies develop over long spans of simulated time.

The project focuses on emergent systems, where complex historical outcomes arise naturally from simple interacting rules.

LivingWorld is currently in early development and focuses on building a robust simulation foundation before adding gameplay layers.

Core Philosophy

LivingWorld is designed around a few guiding principles:

Emergence over scripting

Civilizations, migration, famine, trade, and conflict should arise naturally from simulation systems rather than scripted events.

Ecology drives history

Food production and ecological capacity determine population growth, migration, and societal stability.

sunlight
→ plants
→ animals
→ food
→ population
→ societies
→ civilizations
Autonomous systems

Most systems operate autonomously. The player guides strategic direction rather than micromanaging individual actions.

Long timescale simulation

The world can simulate hundreds or thousands of years before the player even begins.

World Model

LivingWorld uses a pure region-based map.

World
 └ Regions
     ├ Ecology
     ├ Resources
     ├ Species
     └ Societies

Regions store abstract environmental data:

fertility

water availability

plant biomass

animal biomass

climate

biome

Regions generate ecological resources that societies depend on for survival.

Life and Species

The world begins with primitive life.

Over time:

primitive life
→ species evolve
→ intelligent species emerge
→ societies form

Species define biological traits such as:

intelligence

cooperation

aggression

adaptability

reproduction rate

These traits influence how societies behave and develop.

Societies and Civilizations

LivingWorld models social development as a single evolving entity.

Society → Civilization → Nation → Empire

A Society represents a cohesive social group.

A Civilization emerges when a society develops sufficient complexity, such as:

permanent settlements

food surplus

labor specialization

leadership structures

multiple communities

The same lineage evolves through these stages rather than switching entities.

Population Model

Population is modeled using aggregated counts, not individual agents.

Population exists on:

societies

settlements (later stages)

Example:

Stone River Society
Population: 48

Future versions may add demographic cohorts:

young
adult
old

This allows simulation of labor, military capacity, and demographic dynamics without simulating individuals.

Food and Ecology

Regions generate biomass, which societies harvest for food.

Biomass includes:

plant biomass

animal biomass

Food systems drive:

population growth

migration

famine

societal stability

Seasonal cycles influence ecological production:

Spring → growth
Summer → peak biomass
Autumn → harvest
Winter → scarcity
Simulation Time

LivingWorld uses monthly simulation ticks.

1 tick = 1 month
12 ticks = 1 year

Seasonal logic is layered on top of monthly ticks.

The simulation loop currently includes:

1. Advance Time
2. Region Ecology Update
3. Food Gathering
4. Food Consumption
5. Population Update
6. Yearly Report
Current Project Structure
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
Running the Simulation

This project is currently a console simulation.

Run the program and the engine will:

Generate a world

Generate species

Generate starting societies

Run the simulation month-by-month

Print yearly summaries of world state

Each year pauses so changes can be reviewed.

Roadmap

The project is currently focused on building core simulation systems.

Current systems

Region ecology

Species generation

Societies

Food gathering

Population dynamics

Simulation loop

World generation

Planned systems

Migration and expansion

Society splitting and colony formation

Settlement creation

Advancement / knowledge system

Society → civilization transitions

Trade networks

Diplomacy and conflict

Species evolution

Long-term ecological change

Long-Term Goal

The long-term vision of LivingWorld is a fully autonomous historical simulation where:

ecosystems evolve

species emerge

societies form

civilizations rise and fall

trade networks develop

cultures diverge

Every world generated should produce unique histories driven by environmental conditions and systemic interactions.

Development Status

LivingWorld is in early simulation engine development.

The current focus is:

validating core simulation systems

balancing ecological and population models

building stable architecture for future systems