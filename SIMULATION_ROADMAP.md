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

## Near-Term Priorities

1. Chronicle quality tuning
Keep improving event weighting, transition detection, and suppression so the live chronicle stays readable during busy simulation periods.
The denser default world makes that especially important because more simultaneous societies and ecosystems now exist from year zero.
The current opening pass is meant to create more real early turning points, not more filler.

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
The first descendant-species pipeline now exists. Follow-up work should focus on better naming, richer cultural encounter/discovery around descendant fauna, and deeper long-horizon history tools rather than replacing the regional-population model.

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
