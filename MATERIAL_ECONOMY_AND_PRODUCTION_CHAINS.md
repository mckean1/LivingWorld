# Phase 15 — Material Economy & Production Chains

## Implementation Status

Implemented in the current codebase as the first-pass material survival economy:

- settlement stockpiles and pressure states
- regional abundance-based extraction
- short production chains for early goods
- tool, storage, and preserved-food feedback into survival systems
- same-polity material convoys with distance loss
- emergent settlement specialization
- structured material events plus chronicle-safe major milestones
- watch-mode material summaries and hotspots
- grouped player-facing material-crisis chronicle behavior layered over detailed structured events

## Purpose
Phase 15 expands LivingWorld from a mostly food-centered survival simulation into a physical material economy. Settlements should extract raw materials from their regions, convert them into useful goods, store them locally, and redistribute them across the polity when needed.

This phase should remain grounded, settlement-based, and cause-and-effect driven. It should **not** introduce money, prices, merchants, or abstract market simulation. The focus is on real materials, real shortages, real production, and visible downstream consequences.

---

## Design Goals

Phase 15 should make the following true:

- Regions with different resource abundance matter economically.
- Settlements can have meaningful non-food shortages and surpluses.
- Materials improve existing systems such as farming, hunting, storage, and survival resilience.
- Internal logistics expands from food-only redistribution into broader material redistribution.
- Geography causes settlements to specialize naturally over time.
- The chronicle gains major economic turning points without becoming noisy.

---

## Scope

### Included
- Raw material extraction
- Settlement material stockpiles
- Basic production recipes and conversion chains
- Material consumption and simulation effects
- Same-polity redistribution of materials
- UI summaries for material state
- Structured event/history integration
- Major material turning points in the player-facing chronicle
- Grouped settlement-level material crisis chronicle behavior

### Excluded
- Currency
- Prices and market systems
- Individual merchants or caravans as actors
- Foreign trade between polities
- Detailed construction/building trees
- Deep metallurgy simulation
- Diplomacy-driven trade systems

---

## Core Simulation Loop

The core material loop should be:

**Regional Abundance → Extraction → Local Stockpile → Production Conversion → Consumption / Bonuses → Shortage / Surplus Classification → Redistribution**

This should mirror the existing food/logistics structure as much as possible so the material economy feels like an extension of the current simulation, not a disconnected subsystem.

---

## Material Model

## Raw Materials
Start with a compact set of foundational materials:

- Wood
- Stone
- Clay
- Fiber
- Salt
- Copper Ore
- Iron Ore

These are grounded in the existing regional resource system and create strong immediate economic differences between regions.

## Processed Goods
Start with a small set of processed goods that matter immediately:

- Lumber
- Stone Blocks
- Pottery
- Rope
- Textiles
- Simple Tools
- Preserved Food

This is enough to make the system meaningful without exploding complexity too early.

---

## Settlement Data Model

Material state should live at the settlement level, because LivingWorld already anchors food production, hunting, domestication, and redistribution to real settlements.

### Recommended Additions
- `Dictionary<MaterialType, float> MaterialStockpiles`
- `Dictionary<MaterialType, float> MonthlyMaterialProduced`
- `Dictionary<MaterialType, float> MonthlyMaterialConsumed`
- `Dictionary<MaterialType, float> TargetMaterialReserve`
- `Dictionary<MaterialType, MaterialPressureState> MaterialPressure`
- `Dictionary<WorkRole, float> LaborAllocation`
- `List<ProductionCapability>` or lightweight production flags
- `List<SettlementWorkshop>` only if needed later

### New Enums / Types
- `MaterialType`
- `ProcessedGoodType` (or reuse `MaterialType`)
- `WorkRole`
- `ProductionRecipe`
- `MaterialPressureState`
- `TransferPriorityReason`

---

## Regional Model

Regions should continue acting as **capacity providers**, not direct material inventories.

The region determines how much of a material can be extracted efficiently based on abundance and suitability. Settlements convert that regional capacity into actual stockpiled material through labor and capability.

### Regional Design Rules
- Keep resources as environmental capacity, not loot piles.
- Do not introduce hard depletion for most materials in v1.
- Ore extraction may later gain long-horizon pressure or depletion behavior.
- Seasonal modifiers should only exist where they clearly matter.

### Extraction Output Should Depend On
- Regional abundance
- Settlement population
- Labor allocation
- Learned capabilities
- Tool availability
- Settlement disruption/hardship

---

## Extraction Rules

Each month, settlements should allocate part of their labor to material extraction.

### Extraction Categories
- **Woodcutting** from Wood abundance
- **Quarrying** from Stone abundance
- **Clay Gathering** from Clay abundance
- **Fiber Gathering** from fertile / producer-supporting regions
- **Salt Harvesting** from Salt abundance
- **Ore Extraction** from Copper / Iron abundance, gated by capability

### Extraction Output Factors
Extraction should scale with:
- Adults available
- Labor share assigned
- Regional abundance level
- Tool modifiers
- Learned capability modifiers
- Settlement stress penalties

This keeps material extraction consistent with the settlement-grounded philosophy already used by farming and hunting.

---

## Production Chains

Production should be short, simple, and meaningful.

## Tier 1 — Universal Early Production
- Wood → Lumber
- Stone → Stone Blocks
- Clay → Pottery
- Fiber → Rope
- Fiber → Textiles

## Tier 2 — Survival and Productivity Goods
- Wood + Stone → Simple Tools
- Salt + Food Surplus → Preserved Food

## Tier 3 — Early Metal Groundwork
- Wood + Copper Ore + capability → Copper Tools
- Wood + Iron Ore + stronger capability → Iron Tools

For the first implementation, Copper Tools and Iron Tools can simply act as stronger versions of generic tool bonuses rather than requiring a fully simulated metallurgy tree.

---

## Capability Gates

This phase should follow the established terminology model:

- **Discovery** = learning that something exists or is useful
- **Learned** = gaining organized capability to exploit or produce with it

### Examples
- Discover local clay usefulness
- Learned pottery making
- Discover salt preservation potential
- Learned organized food preservation
- Discover workable copper
- Learned early smelting/toolmaking

This keeps the system aligned with LivingWorld’s Discovery vs Learned distinction.

---

## Material Effects on Existing Systems

This phase only matters if materials feed back into current simulation systems.

## Tools
Tools should improve:
- Farming output
- Extraction throughput
- Hunting efficiency
- Later transport efficiency if expanded

## Pottery
Pottery should improve:
- Food storage efficiency
- Spoilage reduction
- Reserve stability

## Rope / Textiles
Rope and textiles can improve:
- Carrying or transfer efficiency
- Future trapping/hunting support
- Future managed herd handling

## Lumber / Stone Blocks
These should initially support:
- Settlement stability
- Settlement growth modifiers
- Future construction/building prerequisites

## Preserved Food
Preserved food should improve:
- Seasonal resilience
- Famine buffering
- Long-distance aid usefulness

The first version should prioritize simple, visible effects over realism for its own sake.

---

## Material Pressure States

Each key material should be classified into a simple pressure state.

### Recommended First Version
- `Deficit`
- `Stable`
- `Surplus`

This should be enough for:
- UI summaries
- Logistics prioritization
- Chronicle threshold checks
- Structured event generation

A more detailed pressure model can come later if needed.

---

## Internal Redistribution

Phase 15 should extend the existing same-polity food logistics foundation into non-food material logistics.

### First-Pass Redistribution Rules
- Only same-polity transfers
- Same region first, then adjacent, then nearest reachable
- Transfer only from true surplus
- Emergency needs outrank reserve growth
- Material transfer loss can reuse or adapt food transfer loss rules

### Recommended Priority Order
1. Tool shortages affecting food production
2. Pottery/storage shortages affecting survival resilience
3. Preservation materials before seasonal hardship
4. General reserve balancing
5. Workshop feedstock supply

This keeps logistics practical, understandable, and strongly tied to survival outcomes.

---

## Specialization

Specialization should emerge from geography and repeated production, not from manual assignment.

### Example Specializations
- Forest settlement → Lumber center
- Clay-rich river settlement → Pottery center
- Salt basin settlement → Preservation center
- Ore-rich settlement → Toolmaking center

### Specialization Score Factors
A settlement’s specialization can be derived from:
- Sustained material output share
- Match between local abundance and production
- Repeated production history
- Current learned capabilities
- Workshop/processing presence if modeled later

Specialization should be something the player notices through outcomes and chronicle events, not something chosen from a menu.

---

## Events and History

All material activity should go through the structured event pipeline first. Most material actions should stay in structured history/debug data. Only major turning points should appear in the visible chronicle.

## Structured Event Families
- `material_discovered`
- `material_extraction_started`
- `material_shortage_started`
- `material_shortage_worsened`
- `material_shortage_resolved`
- `production_started`
- `production_milestone`
- `material_convoy_sent`
- `material_convoy_failed`
- `settlement_specialized`
- `preservation_established`
- `toolmaking_established`

## Chronicle-Worthy Events
Only major beats should surface in the main chronicle, such as:
- First pottery tradition established
- First sustained toolmaking
- First preserved-food economy
- Major timber shortage slowing growth
- Settlement becoming known for a craft
- Large rescue convoy of critical materials

This keeps the player-facing chronicle consistent with the current major-event philosophy.

---

## UI Integration

Phase 15 should extend the existing navigation/UI system rather than introduce a separate interface model.

## My Polity
Add:
- Top material surpluses
- Top critical shortages
- Leading production settlements
- Material convoy / aid summary
- Tool, storage, and preservation status

## Current Region
Add:
- Extractable resources
- Active local extraction
- Local production tags
- Region-linked settlement outputs

## Known Regions
Where known, show:
- Notable resources
- Major exports or specialization tags
- Material significance if discovered

## Known Polities
Keep foreign material knowledge limited by existing visibility rules. Show only what the focal polity should reasonably know, such as broad specialization or visible material strength, not exact internal stockpiles.

## World Overview
Add:
- Strategic material hotspots
- Shortage hotspots
- Major same-polity production centers

---

## Recommended Implementation Order

## Phase 15.1 — Material Foundations
- Add `MaterialType`
- Add settlement material stockpiles
- Add material pressure states
- Add basic debug/UI summaries

## Phase 15.2 — Extraction
- Add labor allocation for extraction
- Add abundance-based extraction rules
- Add discovered vs usable material logic

## Phase 15.3 — Production Chains
- Implement core recipes
- Add capability gates
- Add stockpile conversion logic

## Phase 15.4 — Material Effects
- Tools affect food/extraction
- Pottery affects storage/spoilage
- Preserved food affects hardship buffering

## Phase 15.5 — Redistribution
- Extend same-polity logistics from food to materials
- Prioritize shortages
- Add convoy transfer logic and events

## Phase 15.6 — Specialization and History
- Add specialization scoring/tags
- Add major economic chronicle beats
- Add structured milestones and resolution events

---

## Recommended First-Pass Simplifications

To keep the phase manageable:

- No building placement system
- No per-item crafting queues
- No artisan individuals
- No foreign trade
- No price system
- No hard depletion for most resources
- No full metallurgy tree

This phase should feel like **material survival economy**, not a full grand-strategy market simulator.

---

## Success Criteria

Phase 15 is successful when:

- Two settlements in the same polity can have meaningfully different material roles
- Material shortages visibly affect survival capability
- Non-food convoys happen for understandable reasons
- Economic stories emerge naturally through chronicle-worthy milestones
- The player can inspect material state without being overwhelmed

---

## Future Expansion Hooks

Phase 15 should naturally support later systems such as:

- Construction and buildings
- Workshops and infrastructure
- Deeper metallurgy
- Inter-polity trade
- Diplomatic/resource conflict
- Contact-based economic knowledge diffusion

This makes Phase 15 a foundational bridge phase rather than a dead-end subsystem.
