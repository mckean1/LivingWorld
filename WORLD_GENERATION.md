# LivingWorld World Generation

World generation creates the starting simulation state: regions, species, and initial polities.

## Generation Steps

1. generate regions with fertility, water, and ecology values
2. connect regions for movement paths
3. generate sapient and wildlife species with trophic roles, habitats, migration traits, and hunting traits
4. initialize regional species populations from habitat suitability and carrying capacity
5. generate starting polities from sapient species only

World generation still creates only baseline species definitions. Mutation, divergence, and regional adaptation now begin from those starting populations during simulation rather than being pre-baked into world generation.
That means regional adaptation later measures how far a local population has moved away from its ancestral fit in that region, not whether the generated species started there fully adapted.
World generation still starts polities without durable settlement records. The settlement layer is created by simulation when settlement formation actually occurs, which keeps early locality history causal rather than pre-assumed.

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
Those starting regional populations also now have clean divergence state slots, so future mutation, speciation, and domestication phases can build historical lineage change forward from generation year zero.
