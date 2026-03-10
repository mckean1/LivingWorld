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

## Step 9 – Evaluate Migration
