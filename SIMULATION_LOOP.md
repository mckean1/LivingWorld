# LivingWorld – Simulation Loop Documentation

## Purpose

The simulation loop defines the order in which LivingWorld updates the world.

This order matters because LivingWorld is built on **strong cause-and-effect logic**. Systems must run in a sequence that allows pressures to form naturally, consequences to propagate cleanly, and chronicle events to reflect what actually happened.

The simulation loop should ensure that:

- environmental conditions influence ecology before food is evaluated
- food conditions influence population before migration is evaluated
- migration influences settlement before polity development is evaluated
- polity and environmental pressures influence advancement emergence
- all important state changes emit events
- events propagate cleanly across systems
- the player-facing chronicle reflects meaningful historical developments
- the structured history log preserves the full causal chain

---

# Core Time Model

LivingWorld uses a **monthly simulation tick** with **seasonal logic layered on top**.

This means:

- the simulation advances one month at a time
- some systems update every month
- some systems react more strongly during season boundaries
- agriculture and ecology depend heavily on seasonal timing
- economy, migration, and polity pressures can update monthly

This gives the simulation a good balance between:

- enough granularity for history to feel alive
- enough abstraction to remain manageable
- visible cause-and-effect over time

---

# High-Level Tick Flow

At a high level, each simulation tick should follow this pattern:

```text
1. Advance time
2. Determine seasonal state
3. Update environmental and ecological conditions
4. Update species populations and ecosystem relationships
5. Update agricultural production and harvest state
6. Update food and storage balances
7. Update population change
8. Evaluate migration pressure and movement
9. Update settlements
10. Update polity development, cohesion, fragmentation, and collapse
11. Evaluate advancement progress and discoveries
12. Resolve trade and resource exchange
13. Resolve conflict and territorial pressure
14. Propagate emitted events across subscribed systems
15. Record structured history
16. Render player-facing chronicle output
17. Refresh watch mode UI/state
```

This flow preserves the basic logic:

```text
environment
    ↓
food
    ↓
population
    ↓
movement
    ↓
settlement
    ↓
civilization
    ↓
knowledge
    ↓
trade/conflict
    ↓
history output
```

---

# Core Simulation Principles

## 1. Earlier systems create pressures for later systems

Example:

```text
Drought
    ↓
Biomass decline
    ↓
Food shortage
    ↓
Population stress
    ↓
Migration
    ↓
Settlement founding
```

The loop order should make this chain possible within a logical progression.

---

## 2. Systems should emit events on meaningful state transitions

The loop should not produce repeated noisy logs for unchanged hardship states.

Good:

- `FoodShortageStartedEvent`
- `FoodShortageEndedEvent`
- `FamineStartedEvent`
- `SettlementFoundedEvent`

Bad:

- repeating every month that a polity is still hungry unless something materially changed

---

## 3. Event propagation should reinforce causality, not obscure it

Events should be emitted as systems change state, then processed by downstream systems that logically react to them.

This makes the history traceable.

---

## 4. Chronicle output is not the simulation itself

The simulation creates structured events.
The chronicle presents selected high-value consequences to the player.

The structured event history remains the authoritative causal record.

---

# Detailed Monthly Tick Sequence

## Step 1 – Advance Time

### Purpose

Move the simulation forward by one month and update the time context.

### Responsibilities

- increment month counter
- roll year forward when needed
- determine current season
- expose time context to systems

### Outputs

- current year
- current month
- current season
- season transition state if applicable

### Example

```text
Year 187, Early Spring
```

---

## Step 2 – Detect Seasonal Transitions

### Purpose

Identify whether the current tick starts a new season.

This is important because multiple systems use seasonal timing.

### Responsibilities

- check for spring, summer, autumn, winter transitions
- emit a seasonal transition event if used
- mark season-sensitive systems for update behavior changes

### Possible Event

- `SeasonalShiftEvent`

### Why It Runs Early

Ecology and agriculture need the updated seasonal state before they compute production.

---

## Step 3 – Update Climate and Environmental Conditions

### Purpose

Apply environment-level pressures before ecology and food calculations.

### Responsibilities

- update drought state if applicable
- apply climate fluctuations
- apply region-level environmental modifiers
- determine broad ecological stress conditions

### Inputs

- region climate
- fertility
- water access
- seasonal state
- long-term climate variation rules

### Possible Events

- `DroughtStartedEvent`
- `DroughtEndedEvent`
- `MajorClimateShiftEvent` later if needed

### Example Chain

```text
Season changes to summer
    ↓
dry region enters drought
    ↓
ecology productivity reduced
```

---

## Step 4 – Update Ecology System

### Purpose

Calculate the current ecological productivity of each region.

### Responsibilities

- regenerate biomass
- reduce biomass where climate or overconsumption causes decline
- apply pressure from herbivore populations
- apply environmental recovery when conditions improve

### Inputs

- region fertility
- season
- climate conditions
- drought status
- species population pressure
- harvesting pressure

### Outputs

- updated regional biomass
- ecological stress states
- region productivity changes

### Possible Events

- `BiomassChangedEvent`
- `BiomassCollapseEvent`

### Why This Happens Before Food

Food gathering and ecosystem hunting depend on the state of biomass and ecology.

---

## Step 5 – Update Species System

### Purpose

Update regional species populations and ecosystem relationships.

### Responsibilities

- apply reproduction and mortality at the population level
- evaluate habitat suitability
- process species migration to neighboring regions
- process local decline or population growth
- process mutation and long-term divergence
- process local extinction or speciation thresholds

### Inputs

- region climate
- biomass availability
- habitat suitability
- predator/prey pressures
- migration connectivity
- hunting pressure from societies

### Outputs

- updated regional species populations
- food web changes
- biological history changes

### Possible Events

- `SpeciesPopulationIncreasedEvent`
- `SpeciesPopulationDeclinedEvent`
- `SpeciesMutationEvent`
- `SpeciesSpeciationEvent`
- `SpeciesLocallyExtinctEvent`
- `SpeciesExtinctionEvent`

### Why This Happens Before Food Collection

Sentient societies depend on species populations for hunting, domestication opportunities later, and ecosystem stability.

---

## Step 6 – Update Agriculture System

### Purpose

Resolve food production from cultivated land.

### Responsibilities

- update cultivated land use
- process planting/growth/harvest by season
- compute farm yields
- apply labor limits
- apply fertility and climate modifiers
- apply drought penalties
- distinguish farmed food from gathered food

### Inputs

- season
- agriculture advancement availability
- settlement presence
- cultivated land
- region fertility
- arable capacity
- labor availability
- drought/climate pressure

### Outputs

- farmed food production
- harvest success/failure state
- updated cultivated land productivity

### Possible Events

- `HarvestFailedEvent`
- `HarvestBountifulEvent`

### Notes

This step is seasonal in effect but can still run monthly with season-sensitive logic.

---

## Step 7 – Update Food and Resource System

### Purpose

Resolve survival economics for societies and settlements.

This is one of the most important steps in the loop.

### Responsibilities

- collect gathered food from ecology/species sources
- collect farmed food from agriculture
- combine with stored food
- apply spoilage
- compare available food to need
- update shortage/surplus/famine states

### Inputs

- gathered food
- farmed food
- storage
- spoilage rules
- polity and settlement populations
- trade imports from previous tick if applicable

### Outputs

- available food totals
- shortages
- surpluses
- famine transitions
- storage changes

### Possible Events

- `FoodSurplusStartedEvent`
- `FoodSurplusEndedEvent`
- `FoodShortageStartedEvent`
- `FoodShortageEndedEvent`
- `FamineStartedEvent`
- `FamineEndedEvent`

### Why This Step Is Central

Food links the environment to society.

```text
ecology + agriculture
    ↓
food balance
    ↓
population stability or crisis
```

---

## Step 8 – Update Population System

### Purpose

Apply births, deaths, and demographic pressure.

### Responsibilities

- compute population growth in stable or surplus conditions
- compute decline in shortage or famine conditions
- apply demographic momentum
- detect overpopulation relative to carrying capacity or food supply
- adjust labor availability

### Inputs

- food balance
- famine state
- settlement stability
- migration inflow/outflow from prior steps
- polity conditions

### Outputs

- updated population totals
- labor changes
- pressure indicators
- collapse risk for extreme cases

### Possible Events

- `PopulationGrowthEvent`
- `PopulationDeclineEvent`
- `PopulationPressureIncreasedEvent`
- `PopulationCollapseEvent`

### Why This Comes After Food

Population should respond to actual food conditions, not the other way around.

---

## Step 9 – Evaluate Migration Pressure

### Purpose

Determine whether groups feel compelled to move.

### Responsibilities

- evaluate migration pressure from shortage, overpopulation, ecological decline, or opportunity
- identify candidate populations for movement
- rank neighboring destinations
- prepare migrations

### Inputs

- food shortage states
- famine states
- population pressure
- species/ecology conditions
- neighboring region suitability
- distance and connectivity
- settlement opportunities

### Outputs

- migration decisions
- movement candidates
- founder group selection

### Possible Events

- `MigrationPressureIncreasedEvent`
- `MigrationStartedEvent`

### Example

```text
FoodShortageStartedEvent
    + neighboring fertile river valley
    ↓
migration begins
```

---

## Step 10 – Resolve Migration Movement

### Purpose

Move population groups and apply arrival/failure outcomes.

### Responsibilities

- transfer migrants
- resolve destination arrival
- handle failed migration outcomes
- create founder populations where appropriate
- create conditions for future polity splits or new settlement founding

### Inputs

- migration plans from previous step
- destination suitability
- movement survival rules
- travel difficulty

### Outputs

- migrant populations relocated
- new regional pressure patterns
- arrival or failure outcomes

### Possible Events

- `MigrationArrivedEvent`
- `MigrationFailedEvent`

### Notes

This step should happen before settlement updates because settlement founding may depend on newly arrived groups.

---

## Step 11 – Update Settlement System

### Purpose

Resolve permanent population centers after movement and population changes.

### Responsibilities

- found new settlements
- expand viable settlements
- identify declining settlements
- abandon failed settlements
- evaluate core settlement shifts

### Inputs

- migration arrivals
- population totals
- local food conditions
- river access
- fertility
- region suitability
- polity support

### Outputs

- settlement map updates
- core settlement changes
- permanent habitation shifts

### Possible Events

- `SettlementFoundedEvent`
- `SettlementExpandedEvent`
- `SettlementAbandonedEvent`
- `SettlementBecameCoreEvent`

### Why It Happens Here

Settlement logic should reflect the newly updated population and migration state.

---

## Step 12 – Update Polity System

### Purpose

Resolve society and civilization lifecycle changes.

### Responsibilities

- evaluate whether groups remain cohesive
- detect fragmentation pressure
- create successor groups when splits occur
- detect civilization formation thresholds
- detect polity collapse thresholds
- maintain lineage continuity for the player focus

### Inputs

- settlements
- population
- food stability
- migration branching
- distance between subgroups
- advancement/capability level
- recent hardship or collapse pressures

### Outputs

- polity state changes
- successor polities
- collapse outcomes
- civilization transitions

### Possible Events

- `SocietyFormedEvent`
- `CivilizationFormedEvent`
- `PolityFragmentedEvent`
- `PolityCollapsedEvent`

### Example Chain

```text
multiple stable settlements
    + food stability
    + population growth
    ↓
civilization forms
```

---

## Step 13 – Update Advancement System

### Purpose

Evaluate knowledge emergence, spread, and decay.

### Responsibilities

- accumulate advancement pressure from need, exposure, and surplus
- check prerequisites
- evaluate discovery chances
- spread knowledge via contact or trade
- remove or decay knowledge after collapse where appropriate

### Inputs

- environmental exposure
- food instability or stability
- settlement permanence
- polity complexity
- prior advancements
- inter-polity contact
- trade contact later

### Outputs

- advancement discoveries
- spread events
- knowledge loss

### Possible Events

- `AdvancementProgressEvent`
- `AdvancementDiscoveredEvent`
- `AdvancementSpreadEvent`
- `AdvancementLostEvent`

### Why It Happens After Polity and Settlement Updates

Advancement depends heavily on social stability, settlement permanence, and current pressures.

---

## Step 14 – Resolve Trade System

### Purpose

Move resources between settlements or polities.

This can begin with food surplus/deficit balancing.

### Responsibilities

- identify surplus nodes
- identify deficit nodes
- establish viable exchange paths
- transfer resources
- resolve shortage relief or route collapse

### Inputs

- settlement surpluses
- settlement shortages
- region connectivity
- polity relationships
- recent settlement changes

### Outputs

- resource transfers
- route creation or loss
- shortage relief
- trade dependencies

### Possible Events

- `TradeRouteEstablishedEvent`
- `TradeShipmentSentEvent`
- `TradeShipmentReceivedEvent`
- `TradeShortageRelievedEvent`
- `TradeRouteCollapsedEvent`

### Notes

Trade can resolve food pressure after local production has already been evaluated.

That sequencing preserves causality.

---

## Step 15 – Resolve Conflict System

### Purpose

Apply tension, clashes, and territorial struggle where appropriate.

### Responsibilities

- evaluate tension from overlap, scarcity, fragmentation, or migration
- resolve conflict starts and ends
- apply casualties and displacement
- apply territorial control shifts

### Inputs

- food pressure
- migration overlap
- polity fragmentation
- settlement competition
- trade collapse
- aggression traits later

### Outputs

- deaths
- territory shifts
- migration displacement
- settlement damage or abandonment

### Possible Events

- `TensionIncreasedEvent`
- `ConflictStartedEvent`
- `ConflictEndedEvent`
- `TerritoryLostEvent`
- `TerritoryGainedEvent`

### Notes

Conflict should happen after settlement and polity state is known for the tick.

---

## Step 16 – Propagate Events Across Systems

### Purpose

Allow emitted events to trigger downstream reactions.

### Responsibilities

- dispatch newly emitted events
- allow subscribed systems to react
- avoid infinite loops or event spam
- enforce scope-based propagation
- process only meaningful state-change consequences

### Inputs

- all events emitted during the tick

### Outputs

- follow-up events
- derived state transitions
- chained historical consequences

### Example

```text
FoodShortageStartedEvent
    ↓
MigrationSystem reacts
    ↓
MigrationPressureIncreasedEvent
```

### Important Rule

Event propagation should occur on state transitions, not every time a value is recalculated.

---

## Step 17 – Record Structured Event History

### Purpose

Persist the authoritative causal history of the world.

### Responsibilities

- append structured events to history log
- preserve causes, actors, tags, location, and severity
- ensure later debugging and history analysis are possible
- support future Civilization History view

### Inputs

- finalized structured events from the tick

### Outputs

- append-only historical/debug record

### Notes

This is distinct from chronicle output.

The structured log is comprehensive.
The chronicle is curated.

---

## Step 18 – Render Chronicle Output

### Purpose

Generate readable player-facing history from selected events.

### Responsibilities

- select chronicle-worthy events
- format concise narrative lines
- maintain lineage focus
- include species in polity references
- suppress repetitive noise
- prioritize meaningful historical changes

### Inputs

- structured event history for current tick
- player lineage focus rules
- chronicle visibility rules

### Outputs

- short narrative event lines for the player

### Example

```text
Year 197 — Red Fang Tribe (Wolfkin) discovers agriculture.
Year 198 — Several Red Fang clans migrate south.
Year 199 — A new settlement rises along the Silver River.
```

### Important UI Rule

Chronicle lines shown in watch mode should display:

- newest entries at the top
- older entries below

This improves readability and matches the intended player experience.

---

## Step 19 – Refresh Watch Mode UI

### Purpose

Update the console display without overwhelming the player.

### Responsibilities

- update fixed polity status panel
- redraw chronicle viewport
- minimize flicker through partial redraws where possible
- apply playback delay to event display
- keep newest chronicle entries at the top
- avoid repeated hardship spam through state-transition event filtering

### Inputs

- updated chronicle entries
- current focus polity status
- playback timing settings

### Outputs

- player-facing live simulation display

### Notes

This is a presentation step, not a world-simulation step.

---

# Event Propagation Model in the Loop

A useful conceptual model is:

```text
Phase A: Base systems update world state
Phase B: Systems emit meaningful events
Phase C: Subscribed systems react
Phase D: History is recorded
Phase E: Chronicle is rendered
```

This keeps the architecture understandable.

---

# Example Full Tick Chain

Here is an example of how one monthly tick might unfold:

```text
1. Time advances to Late Summer, Year 304
2. Drought continues in the Silver River region
3. EcologySystem reduces biomass
4. Herbivore populations decline further
5. AgricultureSystem resolves poor harvest output
6. FoodSystem detects severe deficit
7. FoodShortageStartedEvent emitted
8. PopulationSystem applies stress and decline
9. MigrationSystem begins evaluating nearby fertile regions
10. A migrating branch departs south
11. SettlementSystem marks one small settlement as unstable
12. PolitySystem increases fragmentation pressure
13. AdvancementSystem gains agriculture/irrigation pressure
14. TradeSystem fails to fully cover the shortage
15. Structured events are recorded
16. Chronicle displays the most important outcomes
```

Possible chronicle output:

```text
Year 304 — The harvest fails along the Silver River.
Year 304 — Red Fang Tribe (Wolfkin) faces food shortages.
Year 304 — Several Red Fang clans migrate south.
```

---

# Recommended Loop Granularity by System

## Monthly

These should normally update every month:

- time/calendar
- food balance
- storage/spoilage
- population
- migration pressure
- settlement viability
- polity cohesion checks
- trade transfers
- event propagation
- chronicle output

## Seasonal or Season-Weighted

These should run monthly but have stronger seasonal outcomes:

- ecology growth
- agriculture growth/harvest
- climate stress
- biomass swings

## Long-Horizon but Checked Monthly

These may accumulate slowly but should still be evaluated monthly:

- advancement progress
- species mutation/divergence
- polity fragmentation risk
- civilization formation thresholds
- extinction pressure

This gives the world continuity without requiring a separate long-interval loop.

---

# Processing Order Rationale

## Why ecology comes before food

Because societies cannot gather what does not exist.

## Why food comes before population

Because survival pressure shapes births, deaths, and labor.

## Why population comes before migration

Because migration is a response to pressure on the population.

## Why migration comes before settlement

Because settlement founding often emerges from arrival.

## Why settlement comes before polity change

Because complex polities depend on settlement structure.

## Why polity comes before advancement

Because complexity, stability, and need influence discoveries.

## Why trade comes after local production

Because trade redistributes outcomes rather than replacing local production logic.

## Why chronicle comes last

Because it should describe what actually happened after all state changes and event propagation are complete.

---

# Recommended Internal Loop Pattern Per System

Each system should ideally use a consistent structure:

```text
1. Read required input state
2. Evaluate pressures / conditions
3. Apply state changes
4. Emit events for meaningful transitions
5. Return results for downstream systems
```

This makes the codebase easier to reason about and debug.

---

# Anti-Patterns to Avoid

## 1. Systems changing unrelated domains directly

Bad:

- FoodSystem directly founding settlements
- ChronicleSystem changing simulation state

Good:

- FoodSystem emits shortage events
- Migration/Settlement systems react appropriately

---

## 2. Repeated event spam for unchanged conditions

Bad:

- emitting “still starving” every month

Good:

- emit `FoodShortageStartedEvent` once
- emit `FamineStartedEvent` if severity escalates
- emit `FoodShortageEndedEvent` when recovered

---

## 3. Discovery without causal context

Bad:

- random agriculture unlock with no relationship to environment or need

Good:

- settled life + fertile land + repeated food pressure + prerequisites
    → agriculture discovery

---

## 4. Chronicle-first logic driving simulation

The simulation should create reality first.
The chronicle should interpret it afterward.

---

# Pseudocode Example

```text
AdvanceTime();
DetectSeasonTransition();

UpdateClimateAndEnvironment();
UpdateEcology();
UpdateSpecies();
UpdateAgriculture();

UpdateFoodAndResources();
UpdatePopulation();

EvaluateMigrationPressure();
ResolveMigration();

UpdateSettlements();
UpdatePolities();
UpdateAdvancements();

ResolveTrade();
ResolveConflict();

PropagateEvents();

RecordStructuredHistory();
RenderChronicle();
RefreshWatchModeUI();
```

This is the intended conceptual order, even if implementation details evolve.

---

# Future Expansion Guidance

When new systems are added, place them according to what they depend on.

Examples:

## Domestication System
Likely between Species and Food/Agriculture, with ties into Advancement.

## Disease System
Likely after Population and Settlement, with consequences for Polity and Migration.

## Religion / Belief System
Likely after Polity and Advancement, possibly influenced by catastrophe events.

## Governance System
Likely inside or adjacent to Polity System.

## Cultural Memory System
Likely after structured event history, with future effects feeding back into Polity and Advancement.

New systems should never break the causal order without a strong reason.

---

# Final Principle

The simulation loop is the backbone of LivingWorld.

A good loop does more than update numbers.

It creates a believable chain where:

- land shapes ecology
- ecology shapes food
- food shapes survival
- survival shapes migration
- migration shapes settlement
- settlement shapes civilization
- civilization shapes discovery
- discovery reshapes the world
- all of it becomes history

That is what makes LivingWorld feel alive.
