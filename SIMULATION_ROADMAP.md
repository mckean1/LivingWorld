# SIMULATION_ROADMAP.md

# LivingWorld Simulation Roadmap

This roadmap describes planned evolution of simulation depth while preserving full-world emergent behavior.

---

## Current Foundation

Implemented core includes:

- world generation (regions, species, starting polities)
- ecology and food systems
- migration, settlement, population, fragmentation
- food-first regional trade redistribution between nearby polities
- advancement and capability effects
- polity stage progression
- canonical structured event model
- focused society chronicle output
- player lineage focus with deterministic focus handoff across fragmentation and collapse
- major milestone banner highlights for rare history-defining focal events
- append-only JSONL event history output

---

## Near-Term Priorities

1. Chronicle quality tuning
Improve event deduping and weighting so migration-heavy years still surface the most meaningful beats.

2. Trade expansion
Extend the refined food-first hybrid model into broader resource exchange (timber/stone/metal/livestock/crafted goods), stronger true settlement-level logistics, and eventual route infrastructure.

3. Event taxonomy expansion
Add richer event types (absorption, diplomacy, conflict precursors) while maintaining concise chronicle rendering.

4. History tooling
Add simple post-run analyzers over JSONL (severity distributions, collapse causes, migration pressure trends).

---

## Mid-Term Systems

- knowledge diffusion
- advanced trade networks (specialization, supply-demand, market-like behavior)
- cultural divergence
- warfare and territorial conflict

All should emit structured events through the same canonical pipeline.

---

## Long-Term Vision

- civilizations rise and fall organically
- player follows a readable lineage history
- debugging and balancing use structured event history at scale
- output remains readable without reducing simulation scope
