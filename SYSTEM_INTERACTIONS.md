# LivingWorld – System Interactions Documentation

## Purpose

LivingWorld is built as a collection of interacting simulation systems rather than isolated mechanics.

Each system:

- reads world state
- applies its own rules
- changes simulation state
- emits events when meaningful transitions occur
- reacts to events emitted by other systems

This document defines how major systems interact so the simulation remains:

- causal
- explainable
- scalable
- event-driven
- historically coherent

The goal is to ensure that no major gameplay outcome appears arbitrary. Every meaningful outcome should emerge from interacting pressures across systems.

---

# Core Interaction Philosophy

LivingWorld does not use disconnected feature logic.

Instead, it follows this pattern:

```text
System observes state or receives event
    ↓
System evaluates pressure / condition
    ↓
System updates its own state
    ↓
System emits event
    ↓
Other systems react
```

This produces historical chains such as:

```text
DroughtStartedEvent
    ↓
EcologySystem reduces biomass
    ↓
FoodShortageStartedEvent
    ↓
MigrationSystem increases migration pressure
    ↓
MigrationStartedEvent
    ↓
SettlementSystem founds a new settlement
    ↓
AdvancementSystem gains agriculture pressure
```

---

# High-Level System Graph

```text
World Generation System
    ↓
Region / Climate / Fertility data
    ↓
Ecology System ↔ Species System
    ↓
Food and Resource System
    ↓
Population System
    ↓
Migration System
    ↓
Settlement System
    ↓
Polity System
    ↓
Advancement System
    ↓
Trade System
    ↓
Conflict System
    ↓
Chronicle / Event History System
```

In practice, these systems are not purely linear. Most are connected through event propagation.

---

# Interaction Model

Each system should define four things:

1. **Inputs**
2. **Internal responsibilities**
3. **Outputs**
4. **Subscribed events**

Template:

```text
System Name
- Inputs
- Responsibilities
- Outputs
- Reacts To
- Influences
```

---

# 1. World Generation System

## Responsibilities

The World Generation System creates the initial simulation state.

It defines:

- regions
- terrain quality
- climate
- fertility
- water access
- river valleys
- initial ecological conditions
- initial species distribution
- starting sentient societies after pre-simulation

## Inputs

- world generation parameters
- biome/climate rules
- geography generation rules
- species seeding rules

## Outputs

- initialized regions
- fertility values
- climate values
- region connectivity
- river and settlement-favorable regions
- initial species population placement
- initial societies/polities

## Influences

- Ecology System
- Species System
- Settlement System
- Migration System
- Advancement System

## Why It Matters

World generation is the root cause layer for almost all later history.

Example:

```text
River valley + fertile land
    ↓
high biomass
    ↓
strong food base
    ↓
early settlement stability
    ↓
agriculture pressure and civilization growth
```

---

# 2. Region System

## Responsibilities

The Region System stores the core environmental state of each region.

It acts as the shared spatial layer for most simulation systems.

Region data may include:

- fertility
- climate
- water access
- arable capacity
- ecological biomass
- species populations
- settlement presence
- polity influence
- neighboring regions

## Inputs

- world generation output
- ecology updates
- settlement updates
- migration changes
- species population changes

## Outputs

- region state queries for all systems
- suitability information
- carrying capacity context
- location for event scopes

## Influences

Almost every other system depends on region state.

## Notes

The Region System is not a driver system by itself. It is a shared state container and interaction surface.

---

# 3. Ecology System

## Responsibilities

The Ecology System models renewable environmental productivity.

It governs:

- plant growth
- biomass regeneration
- ecosystem productivity
- environmental decline
- seasonal ecological variation

## Inputs

- region fertility
- climate
- season
- drought and climate conditions
- herbivore consumption
- harvesting pressure
- species population pressures

## Outputs

- regional biomass changes
- biomass collapse states
- ecosystem recovery states
- ecology-related events

## Reacts To

- `SeasonalShiftEvent`
- `DroughtStartedEvent`
- `DroughtEndedEvent`
- `SpeciesPopulationIncreasedEvent`
- `SpeciesPopulationDeclinedEvent`
- harvesting-related state changes

## Emits

- `BiomassChangedEvent`
- `BiomassCollapseEvent`
- `HarvestBountifulEvent` in some cases
- ecology pressure causes used by food systems

## Influences

- Food and Resource System
- Species System
- Population System
- Migration System
- Settlement viability
- Advancement pressure

## Example Interaction

```text
DroughtStartedEvent
    ↓
EcologySystem reduces biomass growth
    ↓
BiomassCollapseEvent
    ↓
FoodShortageStartedEvent
    ↓
MigrationStartedEvent
```

---

# 4. Species System

## Responsibilities

The Species System models all life at the population level.

It governs:

- species regional populations
- habitat suitability
- migration between regions
- mutation
- divergence
- speciation
- local extinction
- global extinction
- ecological niche roles

## Inputs

- region climate and fertility
- biomass availability
- predator/prey relationships
- habitat suitability
- migration connectivity
- human/sentient hunting pressure
- domestication pressure in future systems

## Outputs

- regional species population changes
- mutation events
- speciation events
- extinction events
- ecology pressure shifts
- food availability changes for sentient societies

## Reacts To

- `BiomassChangedEvent`
- `BiomassCollapseEvent`
- climate shifts
- hunting pressure state changes
- migration opportunities
- domestication interactions in future systems

## Emits

- `SpeciesPopulationIncreasedEvent`
- `SpeciesPopulationDeclinedEvent`
- `SpeciesMutationEvent`
- `SpeciesSpeciationEvent`
- `SpeciesLocallyExtinctEvent`
- `SpeciesExtinctionEvent`

## Influences

- Ecology System
- Food and Resource System
- Migration System
- Advancement System
- Domestication systems later
- Chronicle history

## Example Interaction

```text
Herbivore population decline
    ↓
less hunting yield
    ↓
FoodShortageStartedEvent
    ↓
Migration pressure rises
```

---

# 5. Food and Resource System

## Responsibilities

The Food and Resource System converts ecological and agricultural production into survival pressure.

It governs:

- gathered food
- farmed food
- storage
- spoilage
- food need
- food surplus
- food shortage
- famine state

Later it may expand into broader resources.

## Inputs

- biomass availability
- species populations available for hunting
- cultivated land output
- harvest output
- storage levels
- polity population
- spoilage rules
- trade imports/exports

## Outputs

- food balance
- shortage/surplus transitions
- famine transitions
- resource pressure state
- food-related events

## Reacts To

- `BiomassCollapseEvent`
- `HarvestFailedEvent`
- `HarvestBountifulEvent`
- `TradeShipmentReceivedEvent`
- `SettlementFoundedEvent`
- `PopulationGrowthEvent`
- drought-related conditions

## Emits

- `FoodSurplusStartedEvent`
- `FoodSurplusEndedEvent`
- `FoodShortageStartedEvent`
- `FoodShortageEndedEvent`
- `FamineStartedEvent`
- `FamineEndedEvent`

## Influences

- Population System
- Migration System
- Settlement System
- Polity stability
- Advancement System
- Trade System
- Chronicle output

## Example Interaction

```text
Population grows faster than food supply
    ↓
FoodShortageStartedEvent
    ↓
PopulationDeclineEvent or MigrationPressureIncreasedEvent
```

---

# 6. Agriculture System

## Responsibilities

The Agriculture System is a specialized production layer within the broader food economy.

It governs:

- cultivated land
- seasonal planting/harvest logic
- farm productivity
- region arable capacity
- agricultural food output

## Inputs

- region fertility
- arable capacity
- season
- settlement presence
- labor availability
- agriculture advancement availability
- climate stress

## Outputs

- farmed food production
- harvest success or failure
- cultivated land growth
- agriculture-related pressure

## Reacts To

- `AdvancementDiscoveredEvent` for agriculture
- `SeasonalShiftEvent`
- `DroughtStartedEvent`
- `SettlementFoundedEvent`
- labor changes from population shifts

## Emits

- `HarvestFailedEvent`
- `HarvestBountifulEvent`
- production data consumed by Food System

## Influences

- Food and Resource System
- Settlement growth
- Population stability
- Trade potential
- Civilization complexity

## Example Interaction

```text
Agriculture discovered
    ↓
AgricultureSystem enabled for polity
    ↓
seasonal harvest output increases
    ↓
FoodSurplusStartedEvent
    ↓
Settlement growth and population growth
```

---

# 7. Population System

## Responsibilities

The Population System governs demographic change for societies and settlements.

It models:

- births
- deaths
- growth
- decline
- demographic pressure
- labor availability

## Inputs

- food balance
- famine state
- settlement stability
- migration inflow/outflow
- environmental conditions
- conflict losses later
- prosperity conditions

## Outputs

- population growth/decline
- labor changes
- carrying-capacity pressure
- collapse risk
- demographic events

## Reacts To

- `FoodSurplusStartedEvent`
- `FoodShortageStartedEvent`
- `FamineStartedEvent`
- `MigrationArrivedEvent`
- `MigrationStartedEvent`
- `SettlementAbandonedEvent`
- future conflict events

## Emits

- `PopulationGrowthEvent`
- `PopulationDeclineEvent`
- `PopulationPressureIncreasedEvent`
- `PopulationCollapseEvent`

## Influences

- Migration System
- Settlement System
- Food and Resource System
- Polity stability
- Advancement progress through labor and surplus capacity

## Example Interaction

```text
FoodSurplusStartedEvent
    ↓
Population growth accelerates
    ↓
PopulationPressureIncreasedEvent later
    ↓
migration or expansion pressure
```

---

# 8. Migration System

## Responsibilities

The Migration System moves population groups between regions.

It governs:

- migration pressure
- migration attempts
- destination evaluation
- founder group movement
- colonization waves
- failed migrations

## Inputs

- food shortage
- population pressure
- ecological collapse
- climate stress
- region suitability
- neighboring region connectivity
- settlement opportunities
- conflict pressure later

## Outputs

- migration movement
- founder populations
- destination transfer of pressure
- migration-related events

## Reacts To

- `FoodShortageStartedEvent`
- `FamineStartedEvent`
- `PopulationPressureIncreasedEvent`
- `BiomassCollapseEvent`
- `ConflictStartedEvent` later
- opportunity conditions such as rich neighboring land

## Emits

- `MigrationPressureIncreasedEvent`
- `MigrationStartedEvent`
- `MigrationArrivedEvent`
- `MigrationFailedEvent`

## Influences

- Settlement System
- Species divergence opportunities
- Polity fragmentation
- Population distribution
- Trade geography later
- Chronicle history

## Example Interaction

```text
FoodShortageStartedEvent
    ↓
MigrationSystem evaluates neighboring regions
    ↓
MigrationStartedEvent
    ↓
MigrationArrivedEvent
    ↓
SettlementFoundedEvent
```

---

# 9. Settlement System

## Responsibilities

The Settlement System governs permanent population centers.

It models:

- settlement founding
- settlement growth
- settlement abandonment
- settlement importance
- core settlement development

## Inputs

- migration arrival
- local food stability
- region suitability
- river access
- arable land
- population level
- polity support

## Outputs

- new settlements
- expanded settlements
- abandoned settlements
- settlement hierarchy changes

## Reacts To

- `MigrationArrivedEvent`
- `FoodSurplusStartedEvent`
- `FoodShortageStartedEvent`
- `FamineStartedEvent`
- `PopulationGrowthEvent`
- `PopulationCollapseEvent`

## Emits

- `SettlementFoundedEvent`
- `SettlementExpandedEvent`
- `SettlementAbandonedEvent`
- `SettlementBecameCoreEvent`

## Influences

- Polity System
- Agriculture System
- Trade System
- Advancement System
- Population stability
- Civilization formation

## Example Interaction

```text
MigrationArrivedEvent + favorable river valley
    ↓
SettlementFoundedEvent
    ↓
Agriculture potential increases
    ↓
civilization complexity grows
```

---

# 10. Polity System

## Responsibilities

The Polity System governs organized social groups through their lifecycle.

It models:

- societies
- civilization transition
- cohesion
- fragmentation
- collapse
- lineage continuity

## Inputs

- settlement count
- population
- food stability
- migration branching
- internal pressure
- distance between groups
- advancement level
- collapse stress

## Outputs

- polity stage changes
- fragmentation
- successor polities
- collapse states
- cohesion-related events

## Reacts To

- `SettlementFoundedEvent`
- `SettlementExpandedEvent`
- `FoodShortageStartedEvent`
- `FamineStartedEvent`
- `MigrationStartedEvent`
- `PopulationCollapseEvent`
- future leadership and conflict events

## Emits

- `SocietyFormedEvent`
- `CivilizationFormedEvent`
- `PolityFragmentedEvent`
- `PolityCollapsedEvent`
- `LeadershipShiftEvent` later if used

## Influences

- Advancement System
- Trade System
- Conflict System
- Chronicle lineage view
- long-term historical continuity

## Example Interaction

```text
Multiple stable settlements + surplus + leadership structure
    ↓
CivilizationFormedEvent
    ↓
trade and knowledge spread potential increase
```

---

# 11. Advancement System

## Responsibilities

The Advancement System models emergent knowledge and capability unlocks.

It governs:

- advancement progress
- prerequisite checking
- discovery chance
- knowledge spread
- knowledge decay after collapse

## Inputs

- environmental exposure
- societal need
- surplus capacity
- settlement permanence
- contact with other polities
- prior knowledge prerequisites
- polity complexity

## Outputs

- advancement discoveries
- spread events
- loss events
- capability unlocks

## Reacts To

- `FoodShortageStartedEvent`
- `SettlementFoundedEvent`
- `CivilizationFormedEvent`
- `TradeRouteEstablishedEvent`
- ecological or species pressures
- repeated environmental interaction

## Emits

- `AdvancementProgressEvent`
- `AdvancementDiscoveredEvent`
- `AdvancementSpreadEvent`
- `AdvancementLostEvent`

## Influences

- Agriculture System
- Food and Resource System
- Settlement growth
- Trade System
- civilization resilience
- historical progression

## Example Interaction

```text
Persistent food instability + fertile floodplain + settled life
    ↓
AdvancementDiscoveredEvent (Agriculture)
    ↓
AgricultureSystem expands production
    ↓
surplus and settlement growth
```

---

# 12. Trade System

## Responsibilities

The Trade System moves resources between settlements or polities.

It governs:

- surplus/deficit matching
- route establishment
- transfers
- route collapse
- dependency relationships

## Inputs

- settlement surpluses
- settlement shortages
- region distance/connectivity
- polity relations
- production stability
- route viability

## Outputs

- trade routes
- resource shipments
- shortage relief
- dependence and route failure pressure

## Reacts To

- `FoodSurplusStartedEvent`
- `FoodShortageStartedEvent`
- `SettlementFoundedEvent`
- `SettlementExpandedEvent`
- `PolityCollapsedEvent`
- future conflict events

## Emits

- `TradeRouteEstablishedEvent`
- `TradeShipmentSentEvent`
- `TradeShipmentReceivedEvent`
- `TradeShortageRelievedEvent`
- `TradeRouteCollapsedEvent`

## Influences

- Food and Resource System
- Population System
- Polity stability
- Advancement spread
- cultural contact
- future conflict potential

## Example Interaction

```text
One settlement surplus + another shortage
    ↓
TradeRouteEstablishedEvent
    ↓
TradeShipmentReceivedEvent
    ↓
FoodShortageEndedEvent
```

---

# 13. Conflict System

## Responsibilities

The Conflict System models organized tension and violence between groups.

This may be later in the roadmap, but the interaction layer should be planned now.

It may govern:

- territorial tension
- raids
- wars
- casualties
- territorial control changes

## Inputs

- food competition
- migration overlap
- territorial pressure
- polity fragmentation
- trade dependency failures
- aggression traits
- leadership instability

## Outputs

- conflict start/end
- casualties
- territorial gain/loss
- migration displacement
- settlement destruction

## Reacts To

- `MigrationArrivedEvent`
- `FoodShortageStartedEvent`
- `PolityFragmentedEvent`
- `TradeRouteCollapsedEvent`
- future diplomacy or relation events

## Emits

- `TensionIncreasedEvent`
- `ConflictStartedEvent`
- `ConflictEndedEvent`
- `TerritoryLostEvent`
- `TerritoryGainedEvent`

## Influences

- Population System
- Migration System
- Settlement System
- Polity System
- Chronicle output

## Example Interaction

```text
Migrating group arrives in occupied fertile valley
    ↓
TensionIncreasedEvent
    ↓
ConflictStartedEvent
    ↓
PopulationDeclineEvent + possible SettlementAbandonedEvent
```

---

# 14. Chronicle System

## Responsibilities

The Chronicle System transforms structured events into player-facing narrative history.

It governs:

- event-to-text rendering
- event prioritization
- filtering noisy events
- chronology display
- perspective-based future history views

## Inputs

- structured events from all systems
- event severity
- event scope
- actor data
- lineage focus rules

## Outputs

- player-facing chronicle lines
- filtered watch mode output
- future civilization history views

## Reacts To

- all chronicle-worthy events

## Emits

The Chronicle System usually does not emit simulation events. It is a presentation layer over structured history.

## Influences

- player understanding
- debugging visibility
- perceived historical continuity

## Notes

Chronicle text should present effects clearly while preserving implied causality.

Example:

```text
Year 197 — Red Fang Tribe (Wolfkin) discovers agriculture.
```

This is player-facing. The structured event preserves the full causal chain behind it.

---

# 15. Structured Event History System

## Responsibilities

This system stores the full append-only debug/history record.

It exists separately from the player-facing chronicle.

It preserves:

- all important events
- event metadata
- causes
- tags
- severity
- actor and location references
- future queryable history

## Inputs

- all structured event objects

## Outputs

- append-only history log
- debug inspection source
- future historical analysis tools
- future Civilization History view foundation

## Reacts To

- every event worth persisting

## Emits

Usually none. This is primarily a sink/storage system.

## Influences

- debugging
- post-simulation analysis
- future replay/history UI
- causal traceability

---

# Major Cross-System Interaction Chains

## 1. Ecology to Migration Chain

```text
DroughtStartedEvent
    ↓
EcologySystem reduces biomass
    ↓
BiomassCollapseEvent
    ↓
FoodSystem detects shortage
    ↓
FoodShortageStartedEvent
    ↓
Population stress rises
    ↓
MigrationPressureIncreasedEvent
    ↓
MigrationStartedEvent
```

---

## 2. Settlement to Civilization Chain

```text
MigrationArrivedEvent
    ↓
SettlementFoundedEvent
    ↓
stable food production
    ↓
PopulationGrowthEvent
    ↓
SettlementExpandedEvent
    ↓
PolitySystem evaluates complexity thresholds
    ↓
CivilizationFormedEvent
```

---

## 3. Need to Knowledge Chain

```text
repeated food instability
    ↓
settled life in fertile region
    ↓
AdvancementSystem accumulates agriculture pressure
    ↓
AdvancementDiscoveredEvent
    ↓
AgricultureSystem increases farm output
    ↓
FoodSurplusStartedEvent
```

---

## 4. Species to Civilization Chain

```text
Herbivore decline
    ↓
hunting yield falls
    ↓
FoodShortageStartedEvent
    ↓
Migration or agriculture pressure
    ↓
new settlement patterns or advancement discovery
```

---

## 5. Trade Stabilization Chain

```text
Settlement A food surplus
    ↓
Settlement B food shortage
    ↓
TradeRouteEstablishedEvent
    ↓
TradeShipmentReceivedEvent
    ↓
TradeShortageRelievedEvent
    ↓
FoodShortageEndedEvent
```

---

## 6. Collapse Chain

```text
FamineStartedEvent
    ↓
PopulationDeclineEvent
    ↓
SettlementAbandonedEvent
    ↓
PolityCollapsedEvent
    ↓
AdvancementLostEvent
```

---

# System Dependency Summary

## Foundational Systems

These create the base conditions other systems depend on:

- World Generation System
- Region System
- Ecology System
- Species System

## Survival Systems

These determine whether societies can endure:

- Food and Resource System
- Agriculture System
- Population System

## Expansion Systems

These determine how societies spread and root:

- Migration System
- Settlement System
- Polity System

## Development Systems

These determine how complexity increases:

- Advancement System
- Trade System
- Conflict System

## Presentation / History Systems

These expose simulation outcomes to player and developer:

- Chronicle System
- Structured Event History System

---

# Recommended Event Subscription Rules

To keep the architecture clean:

## 1. Systems should subscribe only to relevant events

Do not let every system react to every event.

Good:

- Migration System reacts to `FoodShortageStartedEvent`

Bad:

- Chronicle formatting logic inside Food System
- unrelated systems subscribing broadly to everything

---

## 2. Systems should emit events only on meaningful state transitions

Use transitions such as:

- started
- ended
- founded
- collapsed
- discovered
- abandoned

Avoid repetitive “still happening” style event spam.

---

## 3. Systems should own their domain logic

Examples:

- Food logic belongs in the Food and Resource System
- migration destination logic belongs in Migration System
- settlement founding belongs in Settlement System
- chronicle text belongs in Chronicle System

This keeps causality understandable and debugging manageable.

---

# Recommended Processing Flow Per Tick

A typical monthly or seasonal simulation flow may look like this:

```text
1. Apply time / season updates
2. Update ecology
3. Update species populations
4. Update agriculture production
5. Update food and resource balances
6. Update population growth/decline
7. Evaluate migration
8. Update settlements
9. Update polity cohesion / stage
10. Evaluate advancements
11. Resolve trade
12. Resolve conflict
13. Emit and propagate events
14. Record structured history
15. Render chronicle output
```

Exact ordering may evolve, but this structure preserves strong cause-and-effect flow.

---

# Scalability Guidance

When adding a new system, define:

## 1. What state does it own?
Example: domestication, religion, governance, disease, transport

## 2. What inputs does it require?
Which systems or event types feed it?

## 3. What events does it subscribe to?
What should make it react?

## 4. What events does it emit?
What meaningful state changes should it expose?

## 5. What systems does it influence?
How does it propagate consequences into the simulation?

If a new system cannot answer these clearly, it is not ready to be integrated.

---

# Future Systems That Fit This Model

The current interaction architecture can naturally expand to include:

- Domestication System
- Selective Breeding System
- Disease System
- Religion / Belief System
- Governance System
- Diplomacy System
- Memory / Cultural Tradition System
- Transportation System
- Warfare Logistics System

Each should plug into the same event-driven interaction framework.

---

# Final Principle

LivingWorld should feel like a world where history unfolds through interconnected pressures.

No system should feel isolated.

A drought should matter because it affects ecology.
Ecology should matter because it affects food.
Food should matter because it affects survival.
Survival should matter because it drives migration, settlement, collapse, discovery, and civilization.

That chain of interactions is what makes the simulation feel alive.
