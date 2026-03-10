# LivingWorld – Cause and Effect Systems

## Philosophy

LivingWorld is designed around **deterministic cause-and-effect simulation** rather than scripted events.

Every historical development in the simulation must originate from:

- environmental pressures
- species traits
- resource availability
- societal needs
- prior historical events

Events exist to **expose the chain of causality** to the player through the chronicle system.

The guiding rule:

> **Nothing happens without a reason.**

If a system change cannot be traced to a clear cause, the design should be reconsidered.

---

# Core Cause-Effect Model

Every simulation change follows this pattern:

```
Simulation State Change
        ↓
Pressure or Condition Detected
        ↓
System Reaction
        ↓
Event Emitted
        ↓
Event Propagation
        ↓
Consequences in Other Systems
```

Example:

```
Food shortage
    ↓
Population stress
    ↓
Migration attempt
    ↓
Migration event
    ↓
Settlement founded in neighboring region
```

---

# Event Object Structure

Events represent **observable consequences** of system state changes.

Example structure:

```
Event
{
    Type
    Time
    Location
    Actors
    Tags
    Causes
    Severity
}
```

Example:

```
FoodShortageEvent
{
    Type: FoodShortage
    Time: Year 312
    Location: River Valley
    Actors: Wolf Tribe (Gray Wolves)
    Causes: [LowHarvest, PopulationGrowth]
    Severity: Moderate
}
```

Chronicle output:

```
Year 312 — Wolf Tribe (Gray Wolves) faces food shortages in the River Valley.
```

---

# Event Propagation

Events are not the end of the chain.

Systems subscribe to events and may produce **secondary consequences**.

Example:

```
FoodShortageEvent
        ↓
MigrationSystem reacts
        ↓
MigrationEvent
        ↓
SettlementSystem reacts
        ↓
SettlementFoundedEvent
```

This produces **historical chains of events**.

Example chronicle:

```
Year 312 — Wolf Tribe faces food shortages.
Year 313 — Several clans migrate east.
Year 314 — A new settlement is founded in the Green Plains.
```

---

# Core Pressure Categories

## Environmental Pressure

Caused by climate, fertility, geography, or ecosystems.

Examples:

- drought
- poor soil fertility
- harsh winters
- predator pressure

Effects:

```
Low biomass
    → reduced hunting success
    → food shortages
    → migration pressure
```

---

## Population Pressure

Occurs when population exceeds local carrying capacity.

Example:

```
Population > Available Food
```

Effects:

```
food shortages
migration
settlement expansion
conflict
```

---

## Resource Pressure

Triggered when societies require resources they cannot obtain locally.

Examples:

```
lack of food
lack of arable land
lack of animals to hunt
```

Effects:

```
migration
trade attempts
territorial expansion
agriculture adoption
```

---

## Social Pressure

Generated internally within societies.

Examples:

```
population fragmentation
distance from core settlements
cultural drift
leadership conflict
```

Effects:

```
clan splits
new societies
civilization fragmentation
```

---

## Knowledge Pressure

Societies attempt to solve problems through discovery.

Example triggers:

```
food shortages
stable settlements
long-term surplus
environmental interaction
```

Possible outcomes:

```
Agriculture discovered
Animal domestication
Improved tools
```

---

# Cross-System Cause Chains

LivingWorld systems interact through cascading events.

Example chain:

```
Drought
    ↓
Reduced plant growth
    ↓
Herbivore population decline
    ↓
Reduced hunting success
    ↓
Food shortages
    ↓
Migration
    ↓
Settlement founding
```

This produces visible historical narrative.

---

# Example Historical Chain

Example simulation timeline:

```
Year 187 — Harsh winters reduce game populations.
Year 188 — Wolf Tribe struggles to find enough food.
Year 189 — Several clans migrate south.
Year 191 — A new settlement is founded along the Silver River.
Year 197 — The tribe discovers agriculture.
```

Every step results from **previous pressures**.

---

# System Responsibilities

## Ecology System

Responsible for:

- plant growth
- biomass generation
- ecosystem balance

Causes:

```
climate
fertility
species population dynamics
```

Effects:

```
food availability
animal populations
hunting success
```

---

## Population System

Responsible for:

- births
- deaths
- demographic pressure

Effects:

```
population growth
migration pressure
labor availability
```

---

## Migration System

Triggered when pressure thresholds exceed tolerance.

Triggers:

```
food shortage
population pressure
environmental collapse
```

Effects:

```
population movement
regional colonization
founder populations
future speciation opportunities
```

---

## Settlement System

Creates and maintains settlements.

Triggers:

```
migration
stable food supply
agriculture adoption
```

Effects:

```
permanent population centers
civilization formation
resource extraction
```

---

## Advancement System

Represents knowledge discovery.

Triggers:

```
environmental exposure
societal need
time and experimentation
surplus capacity
```

Example:

```
Food instability
    ↓
Need for reliable food
    ↓
Agriculture discovery
```

---

# Species Cause-Effect

Species traits influence outcomes.

Example:

```
High Intelligence
    → higher chance of discovery

High Sociality
    → larger societies

High Aggression
    → more conflict events

High Fertility
    → faster population growth
```

Species evolution can therefore **change the trajectory of civilizations**.

---

# Design Rule

Every new system added to LivingWorld must answer:

1. **What pressures does this system detect?**
2. **What state changes does it create?**
3. **What events does it emit?**
4. **What systems react to those events?**

If a feature cannot be expressed through cause-and-effect chains, it should not be implemented.

---

# Debugging Cause-Effect

The structured event history file records full causal chains.

Example:

```
Event: Migration
Causes:
 - FoodShortageEvent
 - PopulationPressureEvent
```

This allows developers to trace:

```
Effect → Cause → Root Cause
```

Ensuring the simulation remains **explainable and emergent**.

---

# Player Experience

Players observe the simulation through the chronicle.

The chronicle shows **effects**, while the simulation internally tracks **causes**.

Example:

Chronicle:

```
Year 412 — The Red Antler Tribe discovers agriculture.
```

Internal causal chain:

```
Food instability
    + fertile river valley
    + permanent settlement
    → Agriculture discovery
```

---

# Final Principle

LivingWorld should feel like **watching real history unfold**.

History is not random.

It is the **inevitable result of pressures acting on societies over time**.
