# LivingWorld World Generation

World generation creates the starting simulation state: regions, species, and initial polities.

## Generation Steps

1. generate regions with fertility, water, and ecology values
2. connect regions for movement paths
3. generate sapient and wildlife species with trophic roles, habitats, migration traits, and hunting traits
4. initialize regional species populations from habitat suitability and carrying capacity
5. generate starting polities from sapient species only

## Starting Chronicle Focus

Watch mode begins by selecting one focal polity.

Default behavior:

- use `SimulationOptions.FocusedPolityId` if provided
- otherwise follow the lowest-id starting polity

This keeps the initial chronicle deterministic while the lineage handoff system preserves continuity later.

## Output Model After Generation

After generation:

- the whole world simulates normally
- default player-facing output is the live chronicle watch view
- structured history records events for the broader world underneath

World generation does not produce a separate player-facing yearly report path.

Regional species populations now exist before the first polity season resolves, so the first hunting and ecology phase has concrete prey, predators, and producers to work with.
