# ADVANCEMENT_SYSTEM.md

# LivingWorld Advancement System

The advancement system models how societies discover new capabilities over time.

Rather than using a rigid technology tree, LivingWorld uses a **probabilistic discovery system** driven by environmental conditions, societal needs, and historical development.

This approach encourages **emergent technological progress** that varies between civilizations.

---

# Design Goals

The advancement system is designed to:

* allow knowledge to emerge naturally
* respond to environmental pressures
* reward societal stability and surplus
* allow civilizations to develop differently
* support long-term historical progression

The goal is to simulate **how societies historically developed new techniques and technologies**.

---

# Core Concept

Knowledge is discovered when certain **conditions are satisfied** and a probabilistic discovery check succeeds.

Discovery is influenced by:

* environmental exposure
* population size
* settlement stability
* food surplus
* prerequisite knowledge
* time

Societies experiencing different environments and pressures will discover knowledge at different times.

---

# Knowledge Representation

Knowledge is stored as structured data.

Typical fields:

```
Name
Category
Prerequisites
DiscoveryConditions
Effects
```

Example:

```
Name: Agriculture
Category: Food Production
Prerequisites: None
DiscoveryConditions: Fertile region, stable settlement
Effects: Increased food production
```

---

# Discovery Conditions

Each knowledge entry defines conditions that influence discovery probability.

Possible conditions include:

* living in a fertile region
* access to water
* presence of animals suitable for domestication
* population above a threshold
* existence of a permanent settlement
* prolonged food scarcity
* existing prerequisite knowledge

These conditions simulate the **historical pressures that drove innovation**.

---

# Discovery Algorithm

Knowledge discovery typically occurs during **yearly simulation updates**.

Basic discovery process:

```
For each polity:

    For each undiscovered knowledge:

        If prerequisites are satisfied:

            Evaluate discovery conditions

            Calculate discovery probability

            Perform random discovery roll

            If successful:
                add knowledge to polity
                generate historical event
```

This produces gradual and unpredictable technological development.

---

# Probability Factors

Discovery probability may be influenced by several factors.

Common influences include:

Population size
Larger societies have more opportunities for experimentation.

Food surplus
Stable food supply allows specialization and experimentation.

Environmental exposure
Certain knowledge requires exposure to relevant environments.

Settlement stability
Permanent settlements allow accumulation of knowledge.

Time
Some discoveries become more likely as time passes.

---

# Knowledge Effects

Knowledge unlocks new capabilities in the simulation.

Examples include:

Agriculture
Improves food production in fertile regions.

Pottery
Allows food storage and improves famine resistance.

Animal Domestication
Provides additional food sources and transportation potential.

Construction Techniques
Improves settlement stability and expansion.

These effects modify how societies interact with the world.

---

# Knowledge Storage

Knowledge is tracked at the **polity level**.

Example structure:

```
Polity
 ├─ Name
 ├─ Species
 ├─ Population
 ├─ Settlements
 └─ KnownKnowledge
```

This allows different civilizations to develop unique technological profiles.

---

# Knowledge Diffusion (Future System)

Future versions of LivingWorld may allow knowledge to spread between societies.

Possible diffusion mechanisms:

Migration
Migrating populations bring knowledge with them.

Trade
Trade networks spread ideas between settlements.

Proximity
Neighboring societies influence each other.

Conquest
Victorious societies absorb knowledge from defeated ones.

Knowledge diffusion will produce more realistic technological spread across the world.

---

# Historical Event Logging

When knowledge is discovered, a historical event is recorded.

Example:

```
Stone Ford discovered Agriculture
```

These discoveries become part of the world's historical narrative.

---

# Example Development Timeline

Example progression for a hypothetical society:

```
Year 112
Red River Clan founded Stone Ford

Year 147
Stone Ford discovered Agriculture

Year 182
Stone Ford discovered Pottery

Year 230
Stone Ford domesticated herd animals
```

Different societies may follow completely different advancement paths depending on environment and historical conditions.

---

# Design Philosophy

The advancement system emphasizes:

* emergent progression
* environmental influence
* societal pressure
* non-linear technological growth

Civilizations do not advance along a fixed path.

Instead, their development reflects the **unique conditions of their environment and history**.
