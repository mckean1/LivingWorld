# LivingWorld Simulation Roadmap

This roadmap describes how LivingWorld can deepen the simulation while keeping the chronicle-first player experience intact.

## Current Foundation

Implemented core includes:

- world generation
- ecology and food systems
- regional species populations and seasonal ecosystem interactions
- settlement hunting tied to regional wildlife
- mutation and regional divergence foundations
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

2. History views
Build a Civilization History view over the stored event stream without changing simulation systems.

3. Multiple perspectives
Allow the same stored history to be rendered through different focal filters or narrative lenses.

4. Domestication and ecology follow-through
Build on hunting pressure, edible discovery, and domestication interest so repeatedly hunted species can become deliberate domestication candidates.

5. Speciation follow-through
Build on regional divergence, isolation, and mutation history so strongly diverged populations can split into named descendant lineages without replacing the current population-level foundation.

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
