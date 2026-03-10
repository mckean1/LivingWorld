# SIMULATION_ROADMAP.md

# LivingWorld Simulation Roadmap

This roadmap describes planned evolution of simulation depth while preserving full-world emergent behavior.

---

## Current Foundation

Implemented core includes:

- world generation (regions, species, starting polities)
- ecology and food systems
- migration, settlement, population, fragmentation
- advancement and capability effects
- polity stage progression
- canonical structured event model
- focused society chronicle output
- append-only JSONL event history output

---

## Near-Term Priorities

1. Player lineage focus
Replace initial first-polity focus with explicit player-tracked lineage and better fallback behavior after collapse/absorption.

2. Chronicle quality tuning
Improve event deduping and weighting so migration-heavy years still surface the most meaningful beats.

3. Event taxonomy expansion
Add richer event types (absorption, diplomacy, conflict precursors) while maintaining concise chronicle rendering.

4. History tooling
Add simple post-run analyzers over JSONL (severity distributions, collapse causes, migration pressure trends).

---

## Mid-Term Systems

- knowledge diffusion
- trade networks
- cultural divergence
- warfare and territorial conflict

All should emit structured events through the same canonical pipeline.

---

## Long-Term Vision

- civilizations rise and fall organically
- player follows a readable lineage history
- debugging and balancing use structured event history at scale
- output remains readable without reducing simulation scope
