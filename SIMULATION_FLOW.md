# SIMULATION_FLOW.md

# LivingWorld Simulation Flow

This document describes the order of operations used during the LivingWorld simulation.

The simulation operates using **monthly ticks**, with seasonal and yearly systems layered on top.

---

# Simulation Timeline

```
Month
 ├─ Resource updates
 ├─ Food harvesting
 ├─ Food consumption
 ├─ Population changes
 ├─ Migration checks
 └─ Settlement updates

Season
 ├─ Ecological growth cycles
 └─ Harvest periods

Year
 ├─ Settlement expansion
 ├─ Polity splits
 ├─ Knowledge discovery
 └─ Historical event recording
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

### 4. Population Adjustment

Population changes based on food availability.

Possible outcomes:

* growth
* stability
* decline

### 5. Migration Evaluation

Polities evaluate whether to migrate.

Triggers may include:

* food shortages
* population pressure
* better neighboring regions

### 6. Settlement Updates

Settlements update their population and food production.

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
Settlement expansion
Migration outcomes
Polity fragmentation
Knowledge discovery
Historical event generation
```

These events represent the most visible changes in the simulation history.

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
