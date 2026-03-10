# SIMULATION_FLOW.md

# LivingWorld Simulation Flow

This document describes the order of operations used during the LivingWorld simulation.

The simulation operates using **monthly ticks**, with seasonal and yearly systems layered on top.

---

# Simulation Timeline

```
Month
 |- Resource updates
 |- Food harvesting
 |- Food consumption
 |- Migration checks
 `- Settlement updates

Season
 |- Ecological growth cycles
 `- Harvest periods

Year
 |- Population adjustment
 |- Knowledge discovery
 |- Settlement progression
 |- Fragmentation checks
 |- Polity stage progression
 `- Historical event recording
```

---

# Monthly Processes

Each monthly tick updates core survival systems.

### 1. Biomass Update

Regions regenerate ecological biomass depending on climate and fertility.

### 2. Food Harvesting

Polities harvest biomass from their region.

This may represent:

* hunting
* gathering
* fishing
* agriculture

### 3. Food Consumption

Population consumes available food.

Food deficits may cause starvation or famine.

### 4. Migration Evaluation

Polities evaluate whether to migrate.

Triggers may include:

* food shortages
* population pressure
* better neighboring regions

Migration moves an existing polity as a whole. It does not create child polities.

### 5. Settlement Updates

Settlement status remains part of yearly progression, but monthly movement and food conditions shape whether a polity is stable enough to settle later.

---

# Seasonal Processes

Seasonal systems influence ecological productivity.

Typical seasonal events include:

* biomass growth
* harvest periods
* food storage changes

Seasonal cycles strongly affect food supply.

---

# Yearly Processes

Each year the simulation processes larger societal changes.

Typical yearly events include:

```
Population growth or decline
Knowledge discovery
Settlement expansion
Migration outcomes
Polity fragmentation
Polity stage transitions
Historical event generation
```

These events represent the most visible changes in the simulation history.

Yearly fragmentation details:

* each active polity calculates a `FragmentationPressure` score
* pressure is driven by population size, food stress, regional crowding, and migration strain
* only polities above a minimum population and outside their split cooldown can fragment
* successful splits create a child polity in a connected region
* the child receives transferred population, food, and some inherited knowledge
* the child starts without settlement state until the settlement system later founds one
* child names stay readable through simple uniqueness suffixes such as `II` and `III`
* a short history entry is emitted for the split

Yearly polity stage details:

* each active polity is evaluated once per year after settlement and fragmentation updates
* progression is advancement-only in v1 (no automatic regression)
* stage checks use population, longevity, settlement durability, annual food stability, and advancement count
* Civilization specifically requires a multi-settlement polity (at least two settlements)
* stage transitions emit short historical entries such as `Riverwatch Clan became a Tribe`

---

# Pre-Player Simulation

Before the player enters the world, the simulation runs for approximately:

**1000 years**

During this time:

* societies migrate
* settlements form
* knowledge is discovered
* civilizations begin emerging

This creates a dynamic historical backdrop for gameplay.
