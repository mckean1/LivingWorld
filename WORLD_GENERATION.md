# WORLD_GENERATION.md

# LivingWorld World Generation

This document describes how the LivingWorld simulation generates its world and initializes the pre-player simulation.

World generation creates the environmental and societal conditions that allow history to emerge naturally.

Before the player begins, the simulation runs for approximately **1000 years**, allowing species to spread, societies to form, and civilizations to begin emerging.

---

# Goals of World Generation

The world generation system is designed to:

* create a believable ecological landscape
* distribute species across environments
* allow societies to emerge naturally
* produce varied historical outcomes
* ensure no two worlds are identical

The world should feel **alive before the player arrives**.

---

# World Structure

The world is composed of **regions**, not grid tiles.

Regions represent large geographic areas with environmental properties that influence ecological productivity and societal development.

Typical region properties include:

```
Name
Climate
Fertility
WaterAvailability
Biomass
NeighboringRegions
SpeciesPresent
Settlements
Polities
```

Regions act as the fundamental geographic unit for the simulation.

---

# World Generation Process

World generation occurs in several stages.

High-level process:

```
Generate regions
Assign environmental properties
Place species
Initialize early societies
Begin historical simulation
```

Each stage prepares the world for the next.

---

# Region Generation

The first step is creating the world’s regions.

Regions may represent areas such as:

* plains
* forests
* river valleys
* mountains
* coastal regions
* deserts

Regions are connected to neighboring regions to allow migration and expansion.

Important regional attributes include:

Climate
Determines seasonal cycles and ecological growth.

Fertility
Influences agricultural potential and biomass generation.

Water Availability
Impacts settlement viability and food production.

Biomass Capacity
Represents the maximum ecological productivity of the region.

These environmental factors shape the development of societies.

---

# Species Distribution

Once regions are created, species are distributed across the world.

Species placement considers environmental suitability.

Example factors:

* climate compatibility
* available food sources
* terrain adaptability

Species begin as **biological populations**, not organized societies.

Over time, populations may form social groups.

---

# Early Societies

As the simulation begins, populations gradually organize into **early societies**.

These societies eventually become **polities**.

Early societies typically begin as:

* small hunter-gatherer groups
* mobile populations following food sources
* loosely organized clans

These groups migrate frequently while exploring the world.

At initialization, polities begin at the `Band` stage. Later stage transitions are evaluated during yearly simulation updates as conditions improve.

---

# Pre-Player Simulation

After world generation completes, the simulation runs autonomously for approximately:

**1000 years**

During this time:

* societies migrate across regions
* settlements form
* knowledge is discovered
* societies grow or collapse
* historical events accumulate

This creates a unique historical backdrop before the player begins.

---

# Settlement Emergence

Settlements begin forming when societies remain in a region long enough to establish permanent habitation.

Conditions for settlement formation may include:

* stable food supply
* sufficient population
* suitable environmental conditions

Settlements anchor societies geographically and enable long-term growth.

---

# Early Knowledge Development

During the pre-player simulation, societies may begin discovering early knowledge such as:

* agriculture
* pottery
* domestication
* construction techniques

These discoveries allow societies to grow more complex over time.

---

# Historical Record Creation

As the simulation progresses, important events are recorded in the historical log.

Examples:

```
Year 124
Red River Clan migrated to Southern Plains

Year 173
Stone Ford was founded by the Red River Clan

Year 201
Stone Ford discovered Agriculture
```

This historical record provides context for the world the player enters.

---

# Variation Between Worlds

Each generated world may develop very differently.

Factors influencing variation include:

* regional environmental conditions
* species distribution
* migration patterns
* knowledge discovery timing
* societal stability

As a result, every generated world produces a **unique historical timeline**.

---

# Future Enhancements

Future improvements to world generation may include:

Additional region types
More detailed climate modeling

River systems
Waterways influencing settlement placement and trade

Resource diversity
Different types of natural resources beyond biomass

Species evolution
Species adapting to environmental pressures over long periods

These improvements will further increase the depth and realism of the generated world.

---

# Summary

World generation establishes the foundation of the LivingWorld simulation.

By generating regions, distributing species, and simulating centuries of history, the system ensures that the player enters a world that already feels dynamic and alive.

Every world has its own unique geography, history, and emerging civilizations.
