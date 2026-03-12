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
   - emit a `learned_advancement` `WorldEvent`

## Inputs to Discovery Probability

- population and social scale
- annual food conditions
- food reserves
- regional crowding and ecology context
- movement pressure
- prerequisite advancements

The ecology context now treats `Region.AnimalBiomass` as a derived wildlife-summary signal rather than a separate monthly food stock.
That means advancement weighting can still notice whether a region is rich in animal life without reintroducing direct abstract animal harvesting into the food model.
Because early wildlife seeding is now stronger in producer-rich regions, advancement rolls that care about ecological richness should also see a more believable early spread between animal-poor and animal-rich homelands.
Seasonal fauna migration now makes that ecological signal more dynamic over long runs, so a polity's homeland can become richer or poorer in animal life through neighboring founder spread and later predator follow-through rather than staying frozen near generation-era conditions.
Because predator founders now establish or fail based on prey support, local danger and hunting opportunity should increasingly reflect real mid-game ecology instead of a flat early-world predator baseline.

## Outputs

1. capability changes consumed by simulation systems
2. canonical structured event data containing:
   - event type and severity
   - polity / species / region references
   - advancement metadata and probability
   - concise narrative text

## Event Integration

Advancement events are not written directly to the console.

They flow through:

`AdvancementSystem -> World.AddEvent(learned_advancement) -> ChronicleEventFormatter / HistoryJsonlWriter`

Breakthrough discoveries such as fire, agriculture, leadership traditions, and craft specialization are classified as `Major`, so they can appear in the default chronicle. Lower-level discoveries remain in structured history even when they do not surface live.

This remains separate from biology milestones: mutation and regional adaptation events are ecology-driven population outcomes, not polity `Learned` capability gains.
Agriculture is also now consumed by a settlement-grounded farming layer: learned capability unlocks the ability to farm, but actual output depends on real settlements competing for real regional arable capacity.
