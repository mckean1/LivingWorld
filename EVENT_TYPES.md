# LivingWorld – Event Types Documentation

## Purpose

LivingWorld uses events to represent meaningful state changes in the simulation.

Events serve three major purposes:

1. **Drive system-to-system propagation**
2. **Generate player-facing chronicle entries**
3. **Preserve structured historical/debug records**

Events are not arbitrary notifications. They are the formal expression of **cause-and-effect changes** in the world.

Every event should answer:

- **What changed?**
- **Where did it happen?**
- **Who was involved?**
- **What caused it?**
- **What other systems may react to it?**

---

# Core Event Design Principles

## 1. Events Must Represent Real State Changes

An event should only be emitted when something meaningful changes.

Good examples:

- a polity enters food shortage
- a migration begins
- a settlement is founded
- a new advancement is discovered
- a species population collapses in a region

Bad examples:

- repeating the same shortage every month with no state transition
- logging a value that changed insignificantly with no gameplay consequence
- emitting narrative spam without downstream meaning

---

## 2. Events Must Be Causal

Every event should have a reason.

Example:

- `FoodShortageStartedEvent` should be caused by low harvest, rising population, spoilage loss, drought, or similar factors
- `MigrationStartedEvent` should be caused by food pressure, ecological collapse, conflict, or overpopulation
- `AdvancementDiscoveredEvent` should be caused by need, exposure, surplus, and prerequisites

If the cause cannot be explained, the event should not exist.

---

## 3. Events Must Be Propagatable

Events are the bridges between systems.

Example chain:

```text
LowHarvestEvent
    ↓
FoodShortageStartedEvent
    ↓
MigrationPressureIncreasedEvent
    ↓
MigrationStartedEvent
    ↓
SettlementFoundedEvent
```

Each event should make it possible for other systems to react in a logical way.

---

## 4. Events Must Support Both Chronicle and Debug History

Each event has two outputs:

- **Chronicle form** — short, readable, player-facing history
- **Structured form** — detailed data for debugging, analytics, and future history views

Example:

Chronicle:

```text
Year 203 — Red Fang Tribe (Wolfkin) migrates into the Eastern Plains.
```

Structured:

```text
Type: MigrationStarted
Actors: Red Fang Tribe
Species: Wolfkin
OriginRegion: Western Hills
DestinationRegion: Eastern Plains
Causes: [FoodShortageStarted, PopulationPressureIncreased]
Scope: Polity
Severity: Moderate
```

---

# Standard Event Structure

All LivingWorld events should follow a common conceptual structure.

```text
Event
{
    Id
    Type
    Time
    Scope
    Location
    Actors
    Tags
    Causes
    Severity
    Data
}
```

## Field Definitions

### Id
Unique identifier for the event instance.

### Type
Canonical event type name, such as `SettlementFounded` or `FoodShortageStarted`.

### Time
Simulation time when the event occurred.

### Scope
The scale at which the event matters:

- Local
- Regional
- Polity
- World

### Location
Primary location or region where the event occurred.

### Actors
Relevant entities involved in the event:

- polity
- settlement
- species
- region
- population group

### Tags
Optional classification tags used for filtering and future history views.

Examples:

- food
- migration
- conflict
- ecology
- knowledge
- settlement
- species
- prosperity
- catastrophe

### Causes
List of parent events or system-state causes.

### Severity
A rough intensity indicator:

- Minor
- Moderate
- Major
- Critical

### Data
Extra structured payload specific to the event type.

---

# Event Categories

LivingWorld event types are grouped by simulation domain.

Major categories:

1. Ecology Events
2. Species Events
3. Population Events
4. Food and Resource Events
5. Migration Events
6. Settlement Events
7. Polity Events
8. Advancement Events
9. Trade Events
10. Conflict Events
11. World Events

---

# 1. Ecology Events

Ecology events represent environmental and ecosystem state changes.

## BiomassChangedEvent
Emitted when regional biomass changes significantly enough to matter.

**Possible causes:**
- seasonal growth
- drought
- fertility change
- overharvesting
- herbivore pressure

**Possible reactions:**
- hunting success changes
- food pressure changes
- migration pressure rises or falls

**Chronicle use:** Usually not shown directly unless especially important.

---

## BiomassCollapseEvent
Emitted when a region’s usable biomass falls below a critical threshold.

**Possible causes:**
- prolonged drought
- heavy harvesting
- ecological imbalance
- herbivore overconsumption

**Possible reactions:**
- food shortages
- species decline
- migration pressure
- settlement decline

**Example chronicle:**

```text
Year 188 — Game and forage collapse in the Dry Plains.
```

---

## SeasonalShiftEvent
Emitted when a season changes.

**Possible causes:**
- simulation calendar transition

**Possible reactions:**
- agriculture update
- biomass growth update
- harvest resolution
- migration condition changes

**Chronicle use:** Usually not shown directly.

---

## DroughtStartedEvent
A region enters drought conditions.

**Possible reactions:**
- plant growth reduced
- food instability
- herbivore decline
- migration pressure
- advancement pressure toward irrigation or agriculture

**Example chronicle:**

```text
Year 241 — Drought grips the Lower Riverlands.
```

---

## DroughtEndedEvent
A drought condition ends.

**Possible reactions:**
- biomass recovery
- reduced food pressure
- settlement stabilization

---

# 2. Species Events

Species events represent changes to regional populations, mutations, divergence, and extinction.

## SpeciesPopulationIncreasedEvent
A species population grows significantly in a region.

**Possible causes:**
- abundant food
- strong habitat suitability
- low predator pressure

**Possible reactions:**
- more hunting opportunities
- more predator support
- higher migration pressure into nearby regions

---

## SpeciesPopulationDeclinedEvent
A species population falls significantly in a region.

**Possible causes:**
- predation
- climate mismatch
- overhunting
- food web collapse

**Possible reactions:**
- reduced hunting success
- predator decline
- possible extinction pressure

**Example chronicle:**

```text
Year 176 — Deer herds dwindle in the Northern Forest.
```

---

## SpeciesLocallyExtinctEvent
A species disappears from a specific region.

**Possible causes:**
- sustained decline
- habitat collapse
- overpredation
- overhunting

**Possible reactions:**
- food web disruption
- hunting loss
- predator migration
- domestication opportunity lost

**Example chronicle:**

```text
Year 214 — Wild cattle vanish from the Red Grasslands.
```

---

## SpeciesMutationEvent
A regional population undergoes a notable mutation.

**Possible causes:**
- long-term isolation
- environmental pressure
- rare mutation roll

**Possible reactions:**
- trait changes
- ecological shifts
- divergence pressure
- chronicle-worthy biological history

**Example chronicle:**

```text
Year 327 — A hardy strain of river grazer emerges in the marshlands.
```

---

## SpeciesSpeciationEvent
A divergent regional population becomes a new species.

**Possible causes:**
- prolonged isolation
- accumulated mutation
- habitat divergence

**Possible reactions:**
- new food web relationships
- new domestication possibilities
- new sentient species branches in the distant future

**Example chronicle:**

```text
Year 612 — The Stonehorn branches from its mountain ancestors.
```

---

## SpeciesExtinctionEvent
A species goes globally extinct.

**Possible causes:**
- total habitat loss
- world-scale ecological collapse
- hunting pressure
- competitive displacement

**Possible reactions:**
- ecosystem restructuring
- loss of future domestication option
- cultural memory in civilizations

**Example chronicle:**

```text
Year 901 — The last ember stag disappears from the world.
```

---

# 3. Population Events

Population events represent demographic changes for societies, settlements, and species-linked groups.

## PopulationGrowthEvent
A polity or settlement population grows meaningfully.

**Possible causes:**
- food surplus
- stable settlement
- favorable climate
- reduced mortality

**Possible reactions:**
- labor increase
- migration pressure later
- settlement expansion
- increased consumption demand

---

## PopulationDeclineEvent
A polity or settlement population declines meaningfully.

**Possible causes:**
- famine
- conflict
- migration outflow
- ecological decline

**Possible reactions:**
- labor shortage
- settlement weakening
- collapse risk

**Example chronicle:**

```text
Year 293 — The Stone Elk Clan shrinks after years of hardship.
```

---

## PopulationPressureIncreasedEvent
Population approaches or exceeds local carrying capacity.

**Possible causes:**
- growth outpacing food production
- lack of arable land
- insufficient hunting territory

**Possible reactions:**
- migration
- expansion
- food shortage
- conflict
- innovation pressure

---

## PopulationCollapseEvent
A population drops catastrophically.

**Possible causes:**
- famine
- plague-style future systems
- war
- ecological disaster

**Possible reactions:**
- polity collapse
- settlement abandonment
- migration fragments
- historical memory events later

**Example chronicle:**

```text
Year 447 — The Ash Walker people collapse under famine.
```

---

# 4. Food and Resource Events

These are some of the most important events in LivingWorld because food pressure is one of the main drivers of history.

## FoodSurplusStartedEvent
A polity or settlement enters sustained food surplus.

**Possible causes:**
- strong harvest
- abundant biomass
- agriculture success
- favorable climate

**Possible reactions:**
- growth
- stability
- settlement expansion
- advancement progress
- trade exports

**Example chronicle:**

```text
Year 202 — The Red Fang Tribe enjoys a season of abundance.
```

---

## FoodSurplusEndedEvent
A previous food surplus condition ends.

**Possible reactions:**
- reduced growth
- reduced trade potential
- early warning before shortage

---

## FoodShortageStartedEvent
A polity or settlement enters a meaningful food shortage state.

**Possible causes:**
- failed harvest
- biomass decline
- spoilage
- rapid growth
- migration stress

**Possible reactions:**
- migration pressure
- population decline
- unrest
- advancement pressure
- settlement collapse risk

**Example chronicle:**

```text
Year 188 — Red Fang Tribe (Wolfkin) faces food shortages in the Western Hills.
```

---

## FoodShortageEndedEvent
A polity or settlement recovers from food shortage.

**Possible causes:**
- successful harvest
- migration to richer land
- trade imports
- reduced population pressure

**Possible reactions:**
- recovery
- resumed growth
- reduced migration pressure

**Example chronicle:**

```text
Year 191 — Food stores recover among the Red Fang Tribe.
```

---

## FamineStartedEvent
A severe food shortage becomes catastrophic.

**Possible causes:**
- prolonged shortage
- multiple failed harvests
- ecological collapse
- isolation from trade

**Possible reactions:**
- deaths
- migration waves
- settlement abandonment
- polity fragmentation

**Example chronicle:**

```text
Year 305 — Famine spreads through the Lower Valley settlements.
```

---

## FamineEndedEvent
Famine conditions end.

**Possible reactions:**
- stabilization
- cultural memory potential
- recovery phase

---

## HarvestFailedEvent
A seasonal or agricultural harvest fails badly.

**Possible causes:**
- drought
- climate mismatch
- insufficient labor
- low cultivated land productivity

**Possible reactions:**
- food shortage
- famine
- migration
- advancement pressure

**Example chronicle:**

```text
Year 304 — The harvest fails along the Silver River.
```

---

## HarvestBountifulEvent
A harvest significantly exceeds normal output.

**Possible reactions:**
- surplus
- storage growth
- trade export
- prosperity chronicle event

**Example chronicle:**

```text
Year 206 — Fields along the Silver River yield a rich harvest.
```

---

# 5. Migration Events

Migration events are core historical movers in LivingWorld.

## MigrationPressureIncreasedEvent
Pressure to leave a region or settlement increases.

**Possible causes:**
- food shortage
- overpopulation
- predator pressure
- conflict
- ecological collapse

**Possible reactions:**
- migration attempt
- colony founding
- fragmentation

---

## MigrationStartedEvent
A polity, clan, or population group begins migration.

**Possible causes:**
- shortage
- conflict
- opportunity
- settlement overcrowding

**Possible reactions:**
- settlement founding
- regional spread
- founder populations
- future divergence

**Example chronicle:**

```text
Year 189 — Several Red Fang clans migrate south.
```

---

## MigrationArrivedEvent
A migrating group reaches a destination.

**Possible reactions:**
- settlement establishment
- competition
- conflict
- food pressure transfer

---

## MigrationFailedEvent
A migration attempt collapses or fails.

**Possible causes:**
- destination unsuitable
- food exhaustion
- conflict
- severe travel losses

**Possible reactions:**
- population decline
- return migration
- fragmentation

**Example chronicle:**

```text
Year 190 — A wandering branch of the Red Fang fails to survive the crossing.
```

---

# 6. Settlement Events

Settlement events mark the transition from movement to rooted civilization.

## SettlementFoundedEvent
A new settlement is established.

**Possible causes:**
- migration arrival
- favorable region
- stable food access
- agricultural opportunity

**Possible reactions:**
- permanent growth center
- civilization development
- resource extraction
- trade routes later

**Example chronicle:**

```text
Year 191 — Red Fang Tribe (Wolfkin) founds a settlement along the Silver River.
```

---

## SettlementExpandedEvent
A settlement grows significantly in size or importance.

**Possible causes:**
- sustained surplus
- population growth
- trade success

**Possible reactions:**
- higher complexity
- civilization advancement
- increased resource demand

---

## SettlementAbandonedEvent
A settlement is deserted.

**Possible causes:**
- famine
- ecological decline
- migration
- conflict
- collapse

**Possible reactions:**
- polity weakening
- regional population shift
- historical ruin marker for future systems

**Example chronicle:**

```text
Year 332 — The settlement at Cold Ford is abandoned.
```

---

## SettlementBecameCoreEvent
A settlement becomes the central hub of a polity.

**Possible causes:**
- population concentration
- strong production
- strategic location

**Possible reactions:**
- increased cohesion
- stronger civilization identity
- future political tension if rival centers emerge

---

# 7. Polity Events

Polity events represent major social and civilizational changes.

## SocietyFormedEvent
A persistent organized group becomes a recognized society.

**Possible causes:**
- social cohesion
- sustained population
- stable territory

**Possible reactions:**
- future settlement building
- culture drift
- polity lineage begins

---

## CivilizationFormedEvent
A society crosses complexity thresholds and becomes a civilization.

**Possible causes:**
- large population
- surplus
- settlements
- specialization
- leadership structure

**Possible reactions:**
- expanded political development
- greater trade
- more complex fragmentation patterns

**Example chronicle:**

```text
Year 256 — The River Horn people rise as a true civilization.
```

---

## PolityFragmentedEvent
A polity splits into multiple successor groups.

**Possible causes:**
- distance
- hunger
- leadership tension
- cultural drift
- collapse stress

**Possible reactions:**
- daughter societies
- migration
- conflict
- lineage continuation changes

**Example chronicle:**

```text
Year 341 — The River Horn civilization fractures into rival branches.
```

---

## PolityCollapsedEvent
A polity loses enough structure to cease functioning as a coherent entity.

**Possible causes:**
- famine
- depopulation
- abandonment
- conflict
- cascading failures

**Possible reactions:**
- settlement decline
- successor fragments
- knowledge decay
- chronicle lineage transfer if applicable

**Example chronicle:**

```text
Year 348 — The old River Horn order collapses.
```

---

## LeadershipShiftEvent
Leadership changes in a meaningful way.

**Possible causes:**
- succession
- crisis
- internal power shift

**Possible reactions:**
- policy-like future behavior changes
- civil tension
- stabilization

**Chronicle use:** Optional depending on importance.

---

# 8. Advancement Events

Advancement events represent emergent knowledge and capability gain.

## AdvancementProgressEvent
A society makes meaningful progress toward a capability.

**Possible causes:**
- exposure
- need
- time
- surplus
- experimentation

**Possible reactions:**
- eventual discovery
- chronicle hints if needed

**Chronicle use:** Usually hidden unless especially important.

---

## AdvancementDiscoveredEvent
A polity discovers a new advancement.

**Possible causes:**
- prerequisites met
- environmental need
- surplus capacity
- repeated exposure

**Possible reactions:**
- new systems unlocked
- agriculture begins
- improved survival
- new chronicle branch

**Example chronicle:**

```text
Year 197 — Red Fang Tribe (Wolfkin) discovers agriculture.
```

---

## AdvancementSpreadEvent
Knowledge spreads from one polity to another.

**Possible causes:**
- proximity
- trade
- migration
- contact

**Possible reactions:**
- accelerated development
- regional convergence
- broader historical shifts

---

## AdvancementLostEvent
A polity loses practical access to an advancement.

**Possible causes:**
- collapse
- depopulation
- loss of specialists
- environmental unsuitability

**Possible reactions:**
- reduced productivity
- vulnerability
- historical regression

**Example chronicle:**

```text
Year 409 — The floodplain craft of irrigation is forgotten after the collapse.
```

---

# 9. Trade Events

Trade is a planned major system and should have event types defined early.

## TradeRouteEstablishedEvent
A recurring exchange path is established between settlements.

**Possible causes:**
- surplus and deficit pairing
- proximity
- stable relations

**Possible reactions:**
- deficit relief
- increased prosperity
- cultural contact
- dependence risk

**Example chronicle:**

```text
Year 522 — Grain begins to flow between Stone Ford and the Lower River.
```

---

## TradeShipmentSentEvent
A shipment is dispatched.

**Chronicle use:** Usually hidden unless significant.

---

## TradeShipmentReceivedEvent
A shipment successfully arrives.

**Possible reactions:**
- shortage relief
- prosperity
- growth

---

## TradeShortageRelievedEvent
Trade materially resolves food or resource pressure.

**Possible causes:**
- successful transfer from surplus settlement

**Possible reactions:**
- famine avoided
- migration delayed
- settlement stabilized

**Example chronicle:**

```text
Year 525 — Imported grain spares the Lower River from hunger.
```

---

## TradeRouteCollapsedEvent
A trade route stops functioning.

**Possible causes:**
- sender shortage
- conflict
- settlement collapse
- distance cost

**Possible reactions:**
- renewed shortages
- instability
- migration pressure

---

# 10. Conflict Events

Conflict may expand later, but event scaffolding should exist now.

## TensionIncreasedEvent
Conflict pressure rises between groups.

**Possible causes:**
- territorial overlap
- food competition
- migration arrival
- cultural split

---

## ConflictStartedEvent
Open conflict begins.

**Possible reactions:**
- deaths
- migration
- abandonment
- polity fragmentation

**Example chronicle:**

```text
Year 601 — Fighting erupts between the hill clans and the river settlers.
```

---

## ConflictEndedEvent
A conflict ends.

**Possible reactions:**
- stabilization
- weakened participants
- territorial shifts

---

## TerritoryLostEvent
A polity loses control of an important place.

---

## TerritoryGainedEvent
A polity secures control of a new place.

---

# 11. World Events

World events represent globally or regionally historic changes.

## MajorClimateShiftEvent
The world or a major area undergoes significant climate change.

**Possible reactions:**
- habitat changes
- species migration
- biomass changes
- civilizational stress

---

## GreatExtinctionEvent
A major ecological die-off occurs.

**Possible reactions:**
- food web restructuring
- civilizational collapse risk
- long-term recovery

---

## AgeTransitionEvent
A future high-level historical era shift.

Examples:
- early agrarian age
- trade age
- imperial age

This is optional and mostly for future historical framing.

---

# State Transition Event Pairs

To reduce spam and improve readability, LivingWorld should prefer **state transition events** instead of repeated condition logs.

Recommended pattern:

- `FoodShortageStartedEvent`
- `FoodShortageEndedEvent`

Instead of:

- “still in shortage”
- “still in shortage”
- “still in shortage”

Other recommended pairs:

- `FoodSurplusStartedEvent` / `FoodSurplusEndedEvent`
- `FamineStartedEvent` / `FamineEndedEvent`
- `DroughtStartedEvent` / `DroughtEndedEvent`

This keeps the chronicle meaningful and supports your preferred cleaner watch mode.

---

# Event Priority for Chronicle Display

Not all events should be shown equally.

## High Priority
Usually shown in the chronicle:

- SettlementFoundedEvent
- CivilizationFormedEvent
- PolityFragmentedEvent
- PolityCollapsedEvent
- AdvancementDiscoveredEvent
- SpeciesSpeciationEvent
- SpeciesExtinctionEvent
- FoodShortageStartedEvent
- FamineStartedEvent
- HarvestFailedEvent
- MigrationStartedEvent
- SettlementAbandonedEvent
- TradeShortageRelievedEvent
- ConflictStartedEvent

## Medium Priority
Shown when impactful:

- PopulationDeclineEvent
- HarvestBountifulEvent
- AdvancementLostEvent
- DroughtStartedEvent
- SpeciesPopulationDeclinedEvent

## Low Priority
Usually structured/debug only unless escalated:

- BiomassChangedEvent
- SeasonalShiftEvent
- TradeShipmentSentEvent
- AdvancementProgressEvent
- SpeciesPopulationIncreasedEvent

---

# Event Naming Conventions

Use consistent names.

## Recommended format

```text
[Noun/Domain][Action/EventState]Event
```

Examples:

- `FoodShortageStartedEvent`
- `SettlementFoundedEvent`
- `MigrationStartedEvent`
- `SpeciesMutationEvent`
- `AdvancementDiscoveredEvent`

## Guidelines

- Use **Started/Ended** for state transitions
- Use **Founded/Collapsed/Discovered/Abandoned** for decisive historical changes
- Use **Increased/Declined** for directional changes
- Prefer explicit names over vague ones

Good:

- `FoodShortageStartedEvent`

Bad:

- `BadFoodEvent`

---

# Suggested Minimum Event Set for Current Implementation

Given LivingWorld’s current and near-future systems, the minimum high-value event set should be:

## Food / Survival
- FoodSurplusStartedEvent
- FoodSurplusEndedEvent
- FoodShortageStartedEvent
- FoodShortageEndedEvent
- FamineStartedEvent
- FamineEndedEvent
- HarvestFailedEvent
- HarvestBountifulEvent

## Migration / Settlement
- MigrationPressureIncreasedEvent
- MigrationStartedEvent
- MigrationArrivedEvent
- MigrationFailedEvent
- SettlementFoundedEvent
- SettlementExpandedEvent
- SettlementAbandonedEvent

## Polity
- SocietyFormedEvent
- CivilizationFormedEvent
- PolityFragmentedEvent
- PolityCollapsedEvent

## Knowledge
- AdvancementDiscoveredEvent
- AdvancementSpreadEvent
- AdvancementLostEvent

## Ecology / Species
- BiomassCollapseEvent
- DroughtStartedEvent
- DroughtEndedEvent
- SpeciesPopulationDeclinedEvent
- SpeciesMutationEvent
- SpeciesSpeciationEvent
- SpeciesLocallyExtinctEvent
- SpeciesExtinctionEvent

## Future Trade
- TradeRouteEstablishedEvent
- TradeShortageRelievedEvent
- TradeRouteCollapsedEvent

---

# Example Full Event Definitions

## Example: FoodShortageStartedEvent

```text
Type: FoodShortageStarted
Scope: Polity
Tags: [food, hardship]
Severity: Moderate
Actors: [Polity]
Location: Region or Settlement
Causes:
 - HarvestFailedEvent
 - BiomassCollapseEvent
 - PopulationPressureIncreasedEvent
Possible Propagation:
 - MigrationPressureIncreasedEvent
 - PopulationDeclineEvent
 - FamineStartedEvent
Chronicle Example:
 - Year 188 — Red Fang Tribe (Wolfkin) faces food shortages in the Western Hills.
```

---

## Example: SettlementFoundedEvent

```text
Type: SettlementFounded
Scope: Local
Tags: [settlement, expansion]
Severity: Major
Actors: [Polity, Settlement]
Location: Destination Region
Causes:
 - MigrationStartedEvent
 - FoodSurplusStartedEvent
 - FavorableRegionEvaluation
Possible Propagation:
 - PopulationGrowthEvent
 - CivilizationFormedEvent
 - TradeRouteEstablishedEvent
Chronicle Example:
 - Year 191 — Red Fang Tribe (Wolfkin) founds a settlement along the Silver River.
```

---

## Example: AdvancementDiscoveredEvent

```text
Type: AdvancementDiscovered
Scope: Polity
Tags: [knowledge, progress]
Severity: Major
Actors: [Polity]
Location: Primary Settlement or Region
Causes:
 - EnvironmentalExposure
 - SocietalNeed
 - SurplusCapacity
 - PrerequisiteKnowledge
Possible Propagation:
 - FoodSurplusStartedEvent
 - SettlementExpandedEvent
 - CivilizationFormedEvent
Chronicle Example:
 - Year 197 — Red Fang Tribe (Wolfkin) discovers agriculture.
```

---

# Final Rule

Events are the backbone of LivingWorld’s historical simulation.

They should make the world feel:

- causal
- explainable
- emergent
- readable
- historically alive

A good LivingWorld event is not just a log line.

It is a **meaningful historical turning point** that can be traced backward to its causes and forward to its consequences.
