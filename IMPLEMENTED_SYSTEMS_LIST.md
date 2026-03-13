# LivingWorld - Implemented Systems List

This document is the canonical source of truth for:
- what has been implemented
- what is currently in progress
- what comes next

A feature is not fully complete until:
1. the code is implemented
2. tests are updated and passing
3. this list is updated
4. relevant documentation is updated

---

## Phase 1 - Core World Simulation Foundations
**Status:** Complete

Implemented:
- Core simulation loop foundation
- Monthly tick-based simulation structure with seasonal logic layered on top
- Society-to-civilization progression foundation
- Aggregated population model
- Early polity and society lifecycle foundations
- Single-continent world generation baseline

---

## Phase 2 - Settlement, Food, and Survival Foundations
**Status:** Complete

Implemented:
- Settlement-centered simulation foundation
- Food production and consumption basics
- Storage, shortage, and survival pressure foundations
- Seasonal food behavior groundwork
- Early migration and survival pressure integration

---

## Phase 3 - Knowledge, Advancement, and Discovery Foundations
**Status:** Complete

Implemented:
- Capability-based advancement system foundation
- `Learned` capability model
- Discovery model distinct from advancements
- Cultural knowledge versus polity knowledge distinction
- Discovery terminology and log wording direction
- Resource, species, and world knowledge foundations

---

## Phase 4 - Chronicle and Player-Facing History Foundations
**Status:** Complete

Implemented:
- Chronicle-style simulation output replacing generic yearly-report feel
- Short, player-facing historical event phrasing
- Focus on readable historical storytelling output
- Main chronicle as the primary player-facing simulation view

---

## Phase 5 - Event Architecture and Cause-and-Effect Systems
**Status:** Complete

Implemented:
- Structured event objects and metadata pipeline
- Cause-and-effect event architecture
- Event scopes and subscription model
- Event propagation coordinator and propagation handlers
- Causal ancestry support and safeguards
- Structured background debug and history record separate from player chronicle

---

## Phase 6 - Chronicle Filtering and Focus Systems
**Status:** Complete

Implemented:
- Major and Legendary emphasis for player-facing chronicle
- Reduced chronicle spam through filtering and cooldowns
- State-transition-style chronicle behavior
- Player lineage focus system
- Chronicle-first historical presentation direction

---

## Phase 7 - Watch Mode and Chronicle UI Foundations
**Status:** Complete

Implemented:
- Watch mode presentation improvements
- Fixed top status panel
- Chronicle viewport below status panel
- Newest chronicle entries shown at the top
- Reduced flicker and partial redraw direction
- Playback pacing improvements
- Focal polity status integration into the watch experience

---

## Phase 8 - Regional Resource System
**Status:** Complete

Implemented:
- Regional resource abundance model
- Resource categories such as wood, stone, clay, copper, iron, and salt
- Resources modeled as environmental capacity rather than simple inventories
- Discovery-aligned resource knowledge direction

---

## Phase 9 - Regional Species Population and Ecology System
**Status:** Complete

Implemented:
- Species as a core simulation pillar
- Regional per-species population tracking
- Habitat suitability and species-range logic
- Food web and ecological niche foundation
- Producer, herbivore, predator, and apex ecosystem relationships
- Regional extinction and global extinction support
- Seasonal ecology simulation basis

---

## Phase 10 - Hunting and Settlement-Grounded Food Interaction
**Status:** Complete

Implemented:
- Settlement-based hunting system
- Preferred prey targeting
- Hunting success affected by species traits
- Species population reduction from hunting
- Dangerous-animal hunting risk
- Hunting-based cultural discoveries
- Overhunting pressure support
- Legendary and major hunt storytelling hooks

---

## Phase 11 - Mutation, Divergence, and Speciation
**Status:** Complete

Implemented:
- Population-level mutation model
- Minor and major mutation tiers
- Regional divergence support
- Speciation from long-term isolated divergence
- Species lineage tracking direction
- Founder-population-based evolutionary spread
- Anti-cascade and cooldown safeguards for speciation behavior

---

## Phase 12 - Predator Ecology Improvements
**Status:** Complete

Implemented:
- Predator establishment improvements
- Predator growth tied to prey support
- Founder predator failure dynamics
- Ecology balancing improvements to avoid weak persistent predator remnants

---

## Phase 13 - World Visibility and Navigation UI
**Status:** Complete

Implemented:
- Live keypress-based screen navigation while simulation runs
- Pause and unpause support
- Direct screen switching and cycling behavior
- Views for Chronicle, My Polity, Current Region, Known Regions, Known Species, Known Polities, and World Overview
- Visibility-aware world data presentation
- Shared knowledge visibility rules foundation

---

## Phase 14 - Focal Polity UI and Routing Corrections
**Status:** Complete

Implemented:
- My Polity as focal-only player-facing screen
- Enter key no longer drills into generic polity detail from My Polity
- Focal-only fields preserved on My Polity
- Navigation behavior fixes and related tests

---

## Phase 15 - Regional Trade and Resource Exchange
**Status:** Complete

Implemented:
- Inter-settlement food surplus and deficit detection
- Monthly transfer and resource-exchange groundwork
- Early internal logistics between settlements
- Trade and resource-exchange foundation for broader economy systems

---

## Phase 16 - Domestication and Early Agriculture Expansion
**Status:** Complete

Implemented:
- Domestication candidates and domestication foundations
- Managed herds
- Cultivated crop foundations
- Managed food integration into the economy and survival loop
- Early agriculture expansion beyond simple foraging and hunting

---

## Phase 17 - Material Economy and Production Chains
**Status:** Complete

Implemented:
- Settlement material stockpiles and pressure states
- Regional abundance-driven extraction for wood, stone, clay, fiber, salt, copper ore, and iron ore
- Lightweight production chains for lumber, stone blocks, pottery, rope, textiles, simple tools, and preserved food
- Tool tiers and material bonuses feeding back into farming, hunting, spoilage, and seasonal resilience
- Same-polity material redistribution with distance loss, critical-priority routing, and convoy events
- Emergent settlement specialization from sustained output and regional fit
- Structured material event families plus grouped chronicle-safe material crisis milestones
- Watch and UI summaries for material surpluses, shortages, production centers, and resource hotspots

---

## Phase 18 - Economy Interactions and Market Behavior
**Status:** Complete

Implemented:
- Internal settlement-level economy pressure using need, availability, value, opportunity, production-focus, and external-pull signals
- Hybrid presentation model where internal pressure math stays hidden while player-facing watch screens use compact labels such as `Shortage`, `Stable`, `Surplus`, `Highly Valued`, `Trade Good`, and `Locally Common`
- Pressure-driven extraction, production, redistribution priority, bottleneck handling, and specialization drift with smoothing to reduce thrashing
- New economy-turn event families for highly valued goods, trade-good identity, production-focus shifts, and bottlenecks
- Explicit bootstrap-aware chronicle handling so initialization-created economy baselines remain internal state and setup history instead of appearing as live Year 0 narrative
- Bootstrap baseline seeding now initializes older settlements' economy and material-reputation state deeply enough that the first live tick compares against an established baseline instead of inventing new `known for`, trade-good, or false recovery history
- Event origin metadata now distinguishes bootstrap baseline setup from true live transitions, and the visible chronicle applies a final non-live-origin safety guard for these families
- Shared visible-event dedupe and chronicle filtering that still allow real post-bootstrap transitions, including legitimate Year 0 changes, to surface normally

---

# Next Planned Phases

## Phase 19 - External Trade, Trade Routes, and Inter-Polity Exchange
**Status:** Planned

Planned:
- Trade behavior that extends beyond same-polity redistribution into foreign exchange
- External trade links between polities with region-route consequences instead of abstract global access
- Import and export behavior for settlements and polities with uneven local resource positions
- Trade dependency pressures where important external goods can stabilize or expose a polity
- Route-level exchange outcomes and event hooks that can feed chronicle, diplomacy, and disruption systems

---

## Phase 20 - Settlement Infrastructure and Construction
**Status:** Planned

Planned:
- Long-term material sinks for settlement development so stockpiles convert into durable capability
- Construction of infrastructure such as warehouses, workshops, roads, irrigation, mines, defenses, and docks
- Settlement investment choices that shape storage, production reliability, logistics, extraction, and resilience
- Stronger ties between geography, construction, and long-horizon settlement specialization
- Foundations for later transport, defense, and economic scaling systems

---

## Phase 21 - Diplomacy, Raiding, and Conflict Foundations
**Status:** Planned

Planned:
- Early diplomacy and conflict systems grounded in economy, logistics, and inter-polity pressure
- Coercive resource competition between neighboring or connected polities
- Raids, border pressure, and supply disruption as material and trade consequences rather than isolated combat abstractions
- Conflict event chains that emerge from scarcity, dependency, convoy pressure, and territorial friction
- Foundations for later political negotiation, retaliation, and broader warfare systems

---

# Later Follow-Up Areas
- Continue player-facing chronicle dedupe tuning and visible major-event cleanup as more event families come online
- Continue watch UI cleanup around `Discoveries` and `Learned`, including later fuller list and detail presentation improvements
- Additional later phases should be added here only after the canonical roadmap is intentionally extended and synced across docs
