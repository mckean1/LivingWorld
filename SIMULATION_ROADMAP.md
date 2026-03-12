# LivingWorld Simulation Roadmap

This roadmap describes how LivingWorld can deepen the simulation while keeping the chronicle-first player experience intact.

## Current Foundation

Implemented core includes:

- world generation
- fuller default seed-world scale with centralized generation settings
- anchored starting polities and focal-start viability safeguards
- ecology and food systems
- regional species populations and seasonal ecosystem interactions
- settlement hunting tied to regional wildlife
- plant gathering separated cleanly from animal food so wildlife is pressured only through the species layer
- mutation, regional divergence, and first-pass speciation foundations
- migration, settlement, population, fragmentation
- advancement and capability effects
- polity stage progression
- canonical structured event model
- chronicle-first watch mode with a fixed status panel
- newest-first live chronicle playback
- configurable chronicle playback delay
- append-only JSONL history output
- lineage-aware focus handoff across fragmentation and collapse
- lightweight debug/perf instrumentation for long-run balancing and regression detection

## Near-Term Priorities

1. Chronicle quality tuning
Keep improving event weighting, transition detection, and suppression so the live chronicle stays readable during busy simulation periods.
The denser default world makes that especially important because more simultaneous societies and ecosystems now exist from year zero.
The current opening pass is meant to create more real early turning points, not more filler.
The current implementation now includes semantic chronicle profiles for noisy event families, but future passes should keep tuning weights and transition bands as new systems come online.

2. History views
Build richer lineage and event-history views over the stored event stream without changing simulation systems.
The first lightweight inspection layer is now in place through watch-mode polity, region, species, polity-list, and world-overview screens, with shared discovery-aware visibility and consistent keyboard navigation.

3. Multiple perspectives
Allow the same stored history to be rendered through different focal filters or narrative lenses.

4. Domestication and ecology follow-through
Build on hunting pressure, edible discovery, domestication interest, and now-real settlement locality so repeatedly hunted species can become deliberate domestication candidates tied to actual settlement networks.
Keep tuning early wildlife richness through ecological seeding, herbivore recovery, and recolonization strength rather than by adding new abstract animal resource layers.
Continue balancing long-run regional fauna migration so frontiers open believably over decades without flattening the world into globally uniform wildlife.
Current predator tuning assumes founder establishment and collapse are the main levers; future work should prefer prey-web and habitat tuning over reintroducing abstract predator spawning.

6. Discovery/contact visibility refinement
Replace first-pass generation-era visibility approximations with truer knowledge gating for regions, species, and foreign polities as simulation-side contact systems deepen.
Current Phase 8 completion still uses a lightweight horizon model based on settlements, nearby regions, and explicit discoveries; richer diplomacy/contact memory remains intentionally deferred rather than faked in the UI.

5. Speciation follow-through
The first descendant-species pipeline now exists with explicit stabilization and anti-cascade safeguards. Follow-up work should focus on better naming, richer cultural encounter/discovery around descendant fauna, and deeper long-horizon history tools rather than replacing the regional-population model or reopening recursive growth waves.

## Mid-Term Systems

- knowledge diffusion
- broader trade networks and specialization
- cultural divergence
- warfare and territorial conflict

All should continue to emit structured canonical events first.

## Long-Term Vision

- the chronicle is the main game experience
- players follow a living lineage rather than yearly diagnostics
- richer history tools and perspectives are layered over the same append-only event foundation

## Phase 13/14 Status

Domestication and early agriculture expansion now fills the missing layer between hunting/foraging and mature settlement growth.

Delivered scope:

- animal domestication candidates from repeated local interaction
- plant cultivation discoveries from familiarity and settlement pressure
- settlement-level managed herds and cultivated crops
- managed-food integration with farming, hardship, propagation, chronicle, and watch inspection views

Natural next steps after this phase:

- differentiated herd uses such as pack, labor, milk, or fiber
- crop failure and blight tied to ecology and climate pressure
- managed-food diffusion across polities through contact rather than only internal spread
- deeper settlement specialization once cross-region polity networks arrive

## Phase 17 Status

Material economy and production chains now extend the food-centered survival loop into a first-pass physical economy.

Delivered scope:

- settlement stockpiles for raw materials and processed goods
- abundance-based extraction tied to settlement labor, capability, and hardship
- short production chains for tools, pottery, rope, textiles, cut stone, lumber, and preserved food
- same-polity material redistribution with convoy events and distance friction
- emergent specialization from repeated output and geographic fit
- watch-mode visibility for surpluses, shortages, production centers, and strategic resource hotspots
- grouped settlement-level material crisis beats so the main chronicle stays historical instead of operational

Natural next steps after this phase:

- construction and building prerequisites using lumber and stone blocks
- deeper metallurgy beyond simple tool tiers
- contact-driven knowledge diffusion about foreign resource centers
- infrastructure and transport improvements that reduce convoy loss
