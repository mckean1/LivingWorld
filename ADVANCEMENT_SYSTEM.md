# ADVANCEMENT_SYSTEM.md

# LivingWorld Advancement System

Advancement discovery remains probabilistic and condition-driven. The system now emits structured canonical events that feed both focused chronicle output and JSONL history.

---

## Discovery Loop

Year-end, for each polity:

1. build advancement context
2. evaluate undiscovered definitions with satisfied prerequisites
3. roll discovery probability
4. on success:
   - add advancement
   - refresh capabilities
   - emit `knowledge_discovered` world event (structured)

---

## Inputs to Discovery Probability

- population and social scale
- annual food conditions
- reserves
- regional crowding and ecology context
- movement pressure
- prerequisite advancements

---

## Outputs

1. Capability changes consumed by simulation systems
2. Structured event record containing:
   - type/severity
   - polity/species/region references
   - advancement metadata and probability
   - concise narrative for chronicle rendering

---

## Event Integration

Advancement events are canonical simulation events, not direct console writes.

`AdvancementSystem -> World.AddEvent(knowledge_discovered) -> renderer/writer`

This keeps capture independent from presentation.
