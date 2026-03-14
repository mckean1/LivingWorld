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

## Startup Architecture - Primitive-Life-First 4-Pass Plan

This startup plan now sits above the later civilization-era feature phases.
The world should no longer assume a static civilization-ready start.

### Pass 1 - Biological World Foundation
**Status:** Implemented foundation slice

Implemented:
- region ecological profiles with derived productivity, habitability, migration ease, and volatility values
- primitive lineage templates for producers, grazers/foragers, scavenger-omnivores, and predators
- suitability-based uneven ecological seeding instead of "everything everywhere"
- aggregated regional primitive populations with carrying capacity, food/support pressure, reproduction pressure, migration pressure, and trend/stress diagnostics
- early ecological simulation loop with producer support, consumer pressure, predator pressure, founder spread, and local extinction cleanup
- founder-population migration into adjacent viable regions
- Phase A ecological readiness and stability reporting
- bootstrap gating so the default startup path now hands off from ecological readiness into the next pre-social evolution layer instead of assuming civilization-ready species or polities

Intentionally deferred to later startup passes:
- society and polity creation
- focal polity selection and player-entry runtime assumptions
- active-play chronicle boundary changes

### Pass 2 - Evolution and Divergence
**Status:** Implemented foundation slice plus startup-richness stabilization pass

Implemented:
- explicit `EvolutionaryLineage` records with ancestry, origin, extinction, lineage stage, trait/adaptation summaries, and sentience-capability state
- startup-stage activation of mutation, divergence, founder-isolation pressure, speciation, and extinction history after Phase A ecology stabilizes
- population-level mutation history first, with regional divergence/contact tracking and speciation only after sustained viable isolation
- structured evolutionary history events for mutation, divergence milestones, speciation, adaptation, local extinction, global extinction, and sentience-capability milestones
- lineage adaptation summaries and a rare sentience-capability progression that can produce pre-social `Capable` branches without creating societies yet
- `PhaseBReadinessReport` so startup handoff to later social emergence is based on branching history, divergence maturity, extinction history, and sentience-capable potential rather than time alone
- richer founder-isolation payoff, ecology-distance divergence pressure, descendant momentum retention, and partial-contact damping so weak seed families accumulate deeper branching biological history without turning into speciation spam
- local-extinction opening bonuses and related-lineage replacement pressure so biological turnover leaves visible recolonization/replacement texture instead of flat survivor-only ecology
- `PhaseBDiagnostics` with ancestry depth, branching counts, divergence maturity, adapted-biome spread, extinction/replacement texture, sentience-capable root breadth, and weakness reasons for shallow-seed inspection
- sentience-capability progression and bootstrap handoff now prefer broader root-branch coverage and adapted-biome novelty before repeating the same lineage branch

Intentionally deferred to later startup passes:
- full sentient societies, settlements, and polity creation from capable lineages
- focal polity candidate generation and player-entry runtime
- active-play chronicle changes for bootstrap biological history

### Pass 3 - Sentience and Social Formation
**Status:** Implemented corrective stabilization pass

Implemented:
- activation of actual sentient population groups from viable sentience-capable lineages
- persistent group continuity with cohesion, identity strength, survival knowledge, migration pattern, and stress tracking
- society formation as the first durable social unit with predecessor links, mobility mode, subsistence mode, cultural knowledge, and settlement pressure
- early cultural discovery accumulation around edible species, dangerous animals, fertile/harsh regions, and reliable water
- pressure-based settlement founding plus abandonment/failure handling through `SocialSettlement`
- first society-to-polity transition foundations, including limited early `Learned` capability seeding where needed for continuity
- continuity-preserving group and society fragmentation plus collapse history
- focal-candidate viability tracking for later player starts
- `PhaseCReadinessReport` so startup handoff is based on actual social/political maturity rather than time alone
- grounded annual population growth, stagnation, decline, and collapse for sentient groups, societies, settlement populations, and early polities
- latent settlement support, storage support, ecological carrying support, and subsistence mode now feed first-settlement founding so viable societies are not trapped below polity formation forever
- polity expansion now adds grounded secondary settlements and uses settlement-distributed fragmentation pressure instead of treating raw total population as an automatic instability trap
- same-lineage social emergence now allows multiple regionally separated trajectories when support and continuity justify them, while still capping lineage spam
- bootstrap sentience handoff can preserve multiple viable sentience-capable branches instead of funneling the whole world through one lineage
- fallback-origin continuity is now explicit on groups, societies, settlements, and polities so downstream startup logic can distinguish rescued paths from organic ones

Intentionally deferred to Pass 4:
- final focal polity selection UI
- player-entry runtime and stop-logic handoff
- active-play chronicle boundary changes for bootstrap history

### Pass 4 - Polity Start and Player Entry
**Status:** Implemented corrective stabilization pass plus startup-richness/differentiation follow-up

Implemented:
- startup world-age presets with variable prehistory duration, target age as a soft centerpoint, and readiness strictness / candidate-count tuning
- explicit prehistory runtime state flow across biological foundation, evolutionary history, social emergence, player-entry evaluation, focal selection, and active play
- `WorldReadinessReport` for player-entry handoff using biological, social, civilizational, candidate, and stability categories instead of raw age alone
- focal candidate generation from real simulated post-prehistory polities with viability filters, score-plus-diversity ranking, and weak-world emergency fallback thresholds
- compact player-facing candidate summaries covering lineage/species, region, age, settlement depth, subsistence style, current condition, discoveries, learned capability, and a recent historical note
- dedicated `FocalSelection` watch/UI state that freezes time until the player binds to a chosen polity
- player binding / handoff fields on `World` for selected polity, entry year, polity-age context, stop reason, summary snapshot, and live-chronicle start marker
- strict chronicle boundary enforcement so prehistory remains structured history and summary material instead of leaking into the live chronicle buffer
- stricter weak-world handling so max-age / fallback-only / biologically weak outcomes are rejected more often and rerolled instead of being surfaced as normal starts
- selection-screen cleanup plus chronicle viewport sanitation so player-facing startup text is narrative-first and stale status/summary fragments cannot leak into the chronicle pane
- candidate fallback labeling now tracks fallback-created origins and emergency admissions directly instead of relying on one narrow score condition
- Phase C and Pass 4 readiness now agree on organic social/political depth: fallback-only polities, fallback-only candidate pools, and zero-organic-polity worlds no longer count as healthy starts
- normal startup now expects multiple organic candidates; single-candidate and fallback-only worlds bias toward honest regeneration instead of silent acceptance
- startup diagnostics now expose organic/fallback counts, emergency candidate admissions, candidate rejection reasons, startup bottlenecks, and regeneration causes
- startup reroll seed derivation is now deterministic, so repeated startup sweeps and tests measure stable organic-vs-fallback outcomes instead of process-randomized retries
- candidate summaries now classify starts from current polity state instead of founder-origin labels, so subsistence, settlement-network shape, and present pressure reflect what the polity actually became by player entry
- polity settlement expansion now respects subsistence mode, network age, and fragmentation pressure so healthy starts stop flattening into oversized late-bootstrap settlement spreads
- diversity trimming now works on richer current-polity summaries, making lineage/root breadth and regional differentiation more likely to survive the final candidate cut
- dedicated startup progress rendering now shows world-frame, ecology, evolution, society, and player-entry phases in place while prehistory runs, including world age, readiness window context, and phase-appropriate live metrics
- startup progress text is now isolated from the live chronicle path: the generator renders its own panel, the handoff clears before watch/focal-selection ownership takes over, and startup status lines cannot linger in the active chronicle viewport

Intentionally deferred after Pass 4:
- richer player-entry presets and custom world-age editing
- deeper focal-candidate inspection UI and compare views
- dedicated civilization/species/world history screens built on the preserved structured prehistory

This completes the intended primitive-life-first startup path end to end: Pass 1 creates a living ecology, Pass 2 creates branching biological history, Pass 3 creates early social/political actors with real maturation pressure, and Pass 4 now expects normal starts to come from organic simulated outcomes rather than repeated fallback-polity rescue behavior.
World generation also no longer appears blank during long startup runs: the player now sees a compact in-place progress panel instead of waiting on an empty console.

Post-Pass-4 startup-richness validation now also has a compact deterministic sweep (`YoungWorld`, `4x4`, reduced bootstrap caps) for seeds `31, 32, 33, 34, 35, 7, 11, 13, 43`.
During this pass that sweep improved from `5/9` accepted worlds to `7/9`, weak seeds `31` and `11` moved from rejection into organic acceptance, accepted worlds kept `0` fallback candidates, and sentience-capable root breadth improved from mostly `1` root to `2-3` roots in most accepted runs.

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
- Startup-stage metadata so the simulation can stop at ecological foundation instead of always assuming civilization-ready activation

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
**Status:** Complete and now re-grounded as startup Pass 1 foundation

Implemented:
- Species as a core simulation pillar
- Regional per-species population tracking
- Habitat suitability and species-range logic
- Food web and ecological niche foundation
- Producer, herbivore, predator, and apex ecosystem relationships
- Regional extinction and global extinction support
- Seasonal ecology simulation basis
- Primitive-life-first startup now uses this layer as the canonical first world state instead of treating it as support for already-existing societies
- Ecological profile caching, primitive lineage templates, and Phase A readiness reporting now make this layer inspectable and tunable as the base of the whole startup pipeline

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
- Explicit evolutionary lineage records, structured biological history, sentience-capability progression, and Phase B startup readiness now build on this phase as the canonical Pass 2 bootstrap layer

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
- Economy identity chronicle milestones such as `became known for ...` and `... as a trade good` now sit on a stricter narrative layer than the internal economy: they require minimum settlement age, sustained multi-month confirmation, stronger visible thresholds, sticky earned identity, and related-signal anti-stacking before they appear as history

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

## Phase 22 - Traditions
**Status:** Planned

Planned:
- Tradition system for recurring customs, rites, feast practices, burial customs, taboos, sacred foods, and sacred species
- Traditions that emerge from environment, food sources, migration, disasters, survival pressures, and prior history
- Regionally distinct traditions that help differentiate otherwise similar polities
- Tradition-linked chronicle hooks for cultural turning points, continuity, and loss
- Tradition pressure and continuity tracking so customs can persist, adapt, weaken, or be abandoned over time

---

## Phase 23 - Historical Memory
**Status:** Planned

Planned:
- Historical memory system for remembered famines, migrations, betrayals, foundings, collapses, disasters, and golden ages
- Memory records derived from real simulation events rather than arbitrary flavor generation
- Behavioral consequences where remembered events influence storage behavior, diplomacy, migration, expansion, and risk tolerance
- Intergenerational memory retention and fading over time
- Chronicle-visible memory callbacks that make current decisions feel rooted in lived history

---

## Phase 24 - Dynamic Religion
**Status:** Planned

Planned:
- Religion system distinct from generic culture and distinct from mythology
- Early religions that are fluid, local, reactive, and highly shaped by environment and major events
- Gradual stabilization over time as doctrine, institutions, sacred sites, and orthodoxy harden
- Emergent reform, schism, syncretism, local cult behavior, and sacred geography
- Religious behavior influencing food rules, diplomacy, legitimacy, ritual life, and conflict pressures

---

## Phase 25 - Mythology
**Status:** Planned

Planned:
- Mythology system separate from formal religion
- Origin stories, heroic tales, ancestor legends, sacred beasts, migration stories, and disaster narratives
- Myth-making fed by real simulation events, famous figures, species encounters, ruins, and environmental shocks
- Mythology that can persist across regime change and even outlast shifts in formal religion
- Mythology acting as a storytelling and identity layer that informs traditions, religion, and polity self-understanding

---

## Phase 26 - Named Great People
**Status:** Planned

Planned:
- Lightweight named major figures without shifting the simulation into full individual-level character modeling
- Founders, prophets, reformers, explorers, architects, war leaders, lawgivers, and beast tamers
- Great people emerging from real pressure contexts and major turning points
- Chronicle anchors that make eras easier for the player to remember
- Post-death legacy support through memory, mythology, tradition, and legitimacy effects

---

## Phase 27 - Internal Politics and Factions
**Status:** Planned

Planned:
- Internal faction pressures such as elders, warriors, traders, priests, settlers, regional elites, and core-versus-frontier blocs
- Reform resistance, policy disputes, succession friction, and competing internal priorities
- More believable fragmentation, instability, and state-direction changes
- Internal pressure tied to economy, religion, legitimacy, memory, and external threat
- Political event chains that remain grounded in LivingWorld's cause-and-effect simulation rules

---

## Phase 28 - Diplomatic Identity
**Status:** Planned

Planned:
- Diplomatic identity layer beyond simple relationships or trade links
- Emergent identities such as ally, rival, kin-polity, trusted trade partner, tributary, old enemy, or feared raider
- Relationship identity formed through repeated interaction, shared history, conflict, religion, and trade dependency
- Diplomatic memory and identity feeding into later negotiation, retaliation, trust, and alliance systems
- Chronicle hooks for long-running interstate narratives

---

## Phase 29 - Exploration Expeditions
**Status:** Planned

Planned:
- Organized scouting and exploratory expeditions into less-known or unknown regions
- Route-finding, distant-contact, species-reporting, ruin-finding, and fossil-finding hooks
- Risk of failure, loss, disappearance, or misleading reports
- Exploration outcomes that directly expand map knowledge, species knowledge, and cultural knowledge
- Foundations for deeper frontier stories, contact stories, and discovery-driven myth-making

---

## Phase 30 - Species Reputation
**Status:** Planned

Planned:
- Reputation layer for species beyond simple edibility, danger, or domestication suitability
- Species becoming sacred, cursed, noble prey, vermin, famine food, omen-bringers, prestige animals, or taboo creatures
- Reputation formation shaped by hunting history, religion, mythology, ecological impact, and rare events
- Behavioral consequences for hunting, domestication, diplomacy, ritual, and cultural identity
- Chronicle and UI summaries that help the player understand why a species matters beyond raw utility

---

## Phase 31 - Ruins, Fossils, and Archaeology
**Status:** Planned

Planned:
- Persistent ruin sites from collapsed settlements, destroyed infrastructure, abandoned roads, burial grounds, and lost projects
- Fossil and extinct-species traces that preserve deep-time world history
- Archaeological discovery of real prior simulation events, extinct species, and fallen civilizations
- Reuse, scavenging, rediscovery, legitimacy claims, and myth distortion around old remains
- Strong ties into exploration, mythology, memory, religion, and deep-history storytelling

---

## Phase 32 - Functional Wonders
**Status:** Planned

Planned:
- Multi-year prestige and infrastructure projects such as temples, monuments, observatories, canals, roads, granaries, and fortresses
- Wonders with real simulation consequences rather than cosmetic prestige only
- Wonder construction as a major polity commitment shaped by material capacity, religion, legitimacy, ambition, and geography
- Chronicle-defining long projects with interruption, completion, inheritance, and ruin outcomes
- Late-game historical landmarks that anchor memory, diplomacy, religion, and archaeology

---

# Design Rules and Roadmap Notes

## Canonical Planning Rules
- This file is both the canonical implementation ledger and the canonical forward-looking phase plan.
- Future implementation prompts should always review this file first.
- No feature is fully complete until this file and relevant documentation are updated.

## Chronicle and Initialization Rules
- If an event exists because the simulation was initialized, do not chronicle it.
- If an event exists because the world changed after initialization, chronicle it.
- Player-facing chronicle output should remain focused on meaningful state changes rather than setup state.

## Knowledge and Terminology Rules
- Use `Discovery` for revealing resources, geography, species knowledge, or understanding what exists in the world.
- Use `Learned` for advancements that grant capability.
- Cultural knowledge and polity capability remain separate systems.

## Economy Presentation Rules
- Internal economy logic should remain simulation-facing and pressure-based.
- Player-facing UI should prefer compact human-readable summaries rather than raw market-number presentation.

## Culture, Religion, and Storytelling Rules
- Traditions are the formal system name for recurring customs, rites, taboos, and cultural practices.
- Religion and mythology are separate systems.
- Religion should be dynamic and fluid early, then become more stable and institutional over time.
- Mythology should be able to outlast formal religious change more easily than doctrine-bound religion.
- Great People, traditions, religion, mythology, and historical memory should all emerge from simulation cause-and-effect rather than arbitrary flavor generation.

## UI Follow-Up Notes
- Continue player-facing chronicle dedupe tuning and visible major-event cleanup as more event families come online.
- Continue watch UI cleanup around `Discoveries` and `Learned`, including later fuller list and detail presentation improvements.
- Future UI work should explicitly decide and document sorting rules for compact summaries and full-list views.
- A dedicated place in My Polity and inspect/drill-in views should later expose the full `Discoveries` and full `Learned` lists.

## Additional Later Follow-Up Areas
- Additional later phases should be added here only after the canonical roadmap is intentionally extended and synced across docs
