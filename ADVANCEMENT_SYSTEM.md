# LivingWorld Advancement System

Advancement discovery remains probabilistic and condition-driven. Successful discoveries emit structured canonical events that feed both structured history storage and the live player chronicle.

## Discovery Loop

At year-end, for each polity:

1. build advancement context
2. evaluate undiscovered definitions with satisfied prerequisites
3. roll discovery probability
4. on success:
   - add advancement
   - refresh capabilities
   - emit a `knowledge_discovered` `WorldEvent`

## Inputs to Discovery Probability

- population and social scale
- annual food conditions
- food reserves
- regional crowding and ecology context
- movement pressure
- prerequisite advancements

## Outputs

1. capability changes consumed by simulation systems
2. canonical structured event data containing:
   - event type and severity
   - polity/species/region references
   - advancement metadata and probability
   - concise narrative text

## Event Integration

Advancement events are not written directly to the console.

They flow through:

`AdvancementSystem -> World.AddEvent(knowledge_discovered) -> ChronicleEventFormatter / HistoryJsonlWriter`

This keeps discovery logic independent from player presentation while preserving a future path for alternate history views.
