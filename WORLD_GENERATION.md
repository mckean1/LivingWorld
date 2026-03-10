# LivingWorld World Generation

World generation creates the starting simulation state: regions, species, and initial polities.

## Generation Steps

1. generate regions with fertility, water, and ecology values
2. connect regions for movement paths
3. generate species
4. generate starting polities

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
