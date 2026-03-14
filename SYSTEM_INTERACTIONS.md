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

## Startup Direction

The startup path is now explicitly primitive-life-first:

1. biological world foundation
2. evolution and divergence
3. sentience and social formation
4. polity start and player entry

Pass 1 through Pass 4 are implemented now. The default generated world reaches ecological readiness, then evolutionary readiness, then social/political readiness, then a player-entry evaluation/readiness stop before active play starts.
The corrective startup-stabilization pass tightened the interaction rules between those layers: Phase C must now produce organic social depth that Pass 4 agrees is real, and fallback-only candidate pools are treated as regeneration cases rather than as ordinary starts.
The startup-richness follow-up then widened the contract again: Pass 2 must produce richer branch/adaptation/sentience breadth, and Pass 4 must preserve genuine current-polity differentiation instead of flattening all healthy starts into the same late-bootstrap profile.

## Current Major Systems

- food and ecology
- regional species populations
- ecosystem interactions
- settlement hunting
- mutation and divergence
- agriculture
- trade
- migration
- population
- settlement
- fragmentation
- polity stage progression
- advancement

---

## Phase 1 - Core World Simulation Foundations
**Status:** Complete

Bootstrap seeding is an explicit exception in presentation only: systems may emit canonical setup events while the world is in `Bootstrap`, but those baseline events do not enter player-facing chronicle surfaces until later live transitions occur in `Active` simulation.
That bootstrap pass should also seed prior-state trackers so the first active material and reputation comparisons do not reinterpret old settlement identity as fresh chronicle news.
Canonical events now also carry origin metadata, which lets chronicle admission reject non-live setup-derived economy/material/reputation events as a final safety guard.
Implemented:
- Core simulation loop foundation
- Monthly tick-based simulation structure with seasonal logic layered on top
- Society-to-civilization progression foundation
- Aggregated population model
- Early polity and society lifecycle foundations
- Single-continent world generation baseline

## Current Major Systems

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
- Region ecology profiles now cache derived productivity, habitability, migration ease, and volatility so seeding and simulation use the same environmental frame
- Primitive lineage templates now provide the Pass 1 startup roster for producers, grazers/foragers, scavenger-omnivores, and predators
- Phase A readiness reporting now summarizes whether ecology is broad, uneven, and stable enough for later startup passes
- Explicit lineage ancestry, divergence/contact state, mutation/speciation history, sentience-capability progression, `PhaseBDiagnostics`, and Phase B readiness now provide the Pass 2 handoff layer into later social emergence
- Sentient groups, societies, social settlements, candidate polities, civilizational history, and Phase C readiness now provide the Pass 3 handoff layer into later player-entry logic
- Startup world-age presets, `WorldReadinessReport`, focal-candidate generation/ranking, current-polity candidate profiling, `FocalSelection`, player binding, and live-chronicle boundary markers now provide the Pass 4 handoff from prehistory into active play
- Weak-world startup outcomes are now screened more aggressively so fallback-only, max-age, biologically weak worlds regenerate more often instead of being surfaced as ordinary starts
- Social-emergence actors now feed player-entry through explicit organic-vs-fallback origin tracking, emergency candidate labeling, and startup outcome diagnostics instead of letting fallback rescue paths disappear into the normal candidate pool
- Deterministic startup retry seeding now keeps worldgen rerolls stable across runs, which makes repeated-run tuning and startup-regression tests reflect real simulation changes instead of process-randomized retry variance
- Compact repeated-run validation during this pass improved from `5/9` accepted worlds to `7/9`, with accepted runs keeping `0` fallback candidates and usually reaching `2-3` sentience-capable roots

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
- Dedicated focal-selection state before active simulation starts
- Diagnostics can be toggled separately from the default focal-selection summary so player-facing startup text stays narrative-first
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
- Shared visible-event dedupe and chronicle filtering that still allow real post-bootstrap transitions, including legitimate Year 0 changes, to surface normally
- Economy identity milestones are now stricter than the internal economy layer itself: specialization and trade-good chronicle turns require minimum settlement age, multi-month persistence, and stronger visible thresholds before they are promoted into player-facing history
- Related specialization and trade-good milestones for the same settlement-material pair now use anti-stacking and escalation rules, so the chronicle prefers one earned historical reputation beat over two near-duplicate early lines

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

## Phase 22 - Internal Politics, Leadership, and Player Directives
**Status:** Planned

Planned:
- Player interaction layer that goes beyond watch-only simulation
- High-level player directives that influence priorities without turning the game into micro-management
- Leadership, pressure, and governance hooks that can respond to scarcity, trade, conflict, and expansion
- Policy-style guidance over settlement growth, military posture, economic focus, exploration, and stability
- Strong cause-and-effect integration so player directives shape pressures rather than force arbitrary outcomes

---

## Phase 23 - Historical Memory and Narrative Consequence Systems
**Status:** Planned

Planned:
- Historical Memory System where major events become lasting cultural memory
- Narrative event chaining so major events create logical downstream consequence events
- Memories of famine, migration, war, prosperity, and foundation that influence later choices
- Greater long-range storytelling continuity across generations
- Deeper causal continuity between chronicle events and polity behavior

---

## Phase 24 - Chronicle Expansion and History Views
**Status:** Planned

Planned:
- Dedicated Civilization History view built from chronicle and structured event history
- Multiple chronicle perspectives such as focal polity, foreign polity, and world-event views
- Better player-facing access to structured history without exposing internal debug noise
- Continued major-event deduplication and visible-event cleanup
- Preservation of concise chronicle style while allowing deeper drill-down

---

## Phase 25 - Knowledge and Discovery UI Expansion
**Status:** Planned

Planned:
- Dedicated full-list views for Discoveries and Learned items in My Polity and related screens
- Explicit sorting rules for compact summaries and full-list displays
- Better presentation of cultural knowledge versus polity capability
- Cleaner top-panel summaries following the agreed compact formatting rules
- More discoverability of what a polity knows about species, regions, resources, and practices

---

## Phase 26 - Monumental Projects and Prestige Works
**Status:** Planned

Planned:
- Civilization Wonder Project system for multi-year prestige megaprojects
- Wonders such as temples, monuments, observatories, canals, and other landmark constructions
- Large material, labor, and political commitments with long-horizon payoffs
- Chronicle-worthy world and polity events tied to construction milestones and completion
- Strong integration with economy, infrastructure, diplomacy, and cultural identity

---

## Phase 27 - Deep History, Fossils, and Ancient World Layer
**Status:** Planned

Planned:
- Prehistoric deep-history layer with ancient extinct species records
- Fossil sites and fossil discovery systems
- Ancient ecological and evolutionary traces discoverable by later civilizations
- Deeper world identity through visible forgotten natural history
- Long-range storytelling links between world generation, extinction, discovery, and culture

---

## Phase 28 - Advanced State Development and Large-Scale Civilization Systems
**Status:** Planned

Planned:
- Stronger nation/empire-scale governance and administration pressures
- Larger territorial coordination problems across regions and settlements
- Advanced logistics, political cohesion, and imperial overstretch pressures
- More mature late-game interactions between economy, conflict, memory, and identity
- Foundations for truly large-scale civilization arcs

---

# Ongoing Cross-Phase Cleanup

- Continue player-facing chronicle dedupe tuning and visible major-event cleanup as more event families come online
- Continue validating that bootstrap and initialization state never appears as false live chronicle history
- Continue UI readability cleanup for compact summaries, labels, and drill-in screens
- Continue reviewing chronicle output to ensure there are no duplicated or redundant player-facing chronicle messages
- Continue syncing roadmap and documentation whenever a design decision is made so planning stays aligned with agreed direction
