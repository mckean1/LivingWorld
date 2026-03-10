# WORLD_GENERATION.md

# LivingWorld World Generation

World generation creates the starting world state (regions, species, and initial polities). Simulation then runs the full world over time.

---

## Generation Steps

1. Generate regions with fertility/water/ecology values
2. Connect regions for migration pathways
3. Generate species
4. Generate starting polities

---

## Starting Focus for Chronicle

For current focused chronicle mode, the initial focal polity defaults to the first starting polity (lowest initial polity id), unless overridden in options.

This is a temporary stable selector designed to evolve into player lineage tracking.

---

## Pre-Player Simulation Output Model

Even though all polities simulate fully, default console output is a yearly focal chronicle plus rare world notes.

Important events from all polities are still written to append-only JSONL history.
