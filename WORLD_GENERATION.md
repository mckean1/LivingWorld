# LivingWorld World Generation

World generation creates the starting simulation state: regions, species, and initial polities.

## Default Scale

The current fuller-world targets live in `LivingWorld/Generation/WorldGenerationSettings.cs`:

- `RegionCount = 36`
- `InitialSpeciesCount = 28`
- `InitialPolityCount = 10`
- `ContinentWidth = 6`
- `ContinentHeight = 6`
- `MinimumStartingPolityRegionSpacing = 1`
- `HomelandSupportRadius = 1`
- `MinimumAccessibleHomelandSupportSpecies = 2`
- `StartPolitiesWithHomeSettlements = true`

These values are intentionally centralized so density tuning can happen without rewriting generation logic.

## Generation Steps

1. generate regions with biome-shaped fertility, water, biomass, and carrying-capacity context
2. connect regions for movement paths
3. generate sapient and wildlife species with trophic roles, habitat preferences, migration traits, and hunting traits
4. assign each species a clustered initial range over viable regions
5. initialize regional species populations from habitat suitability, carrying capacity, and seeded range limits
6. generate starting polities from sapient species only

World generation still creates only baseline species definitions. Mutation, divergence, and regional adaptation now begin from those starting populations during simulation rather than being pre-baked into world generation.
That means regional adaptation later measures how far a local population has moved away from its ancestral fit in that region, not whether the generated species started there fully adapted.
World generation now gives each starting polity one grounded home settlement anchor in its starting region.
That anchor is intentionally lightweight: it enables settlement-grounded hunting, trade endpoints, migration locality, and focal inspection from year zero without fabricating extra setup events.

## Region Model

The fuller starting world still aims to read as one coherent early continent rather than scattered noise.

- regions are generated on a `6 x 6` continent grid
- connectivity starts from orthogonal neighbors, then adds a small amount of diagonal and river-corridor reinforcement
- each generated region receives a `RegionBiome`
- biome profiles shape fertility, water availability, plant biomass, animal-biomass capacity, and carrying capacity

Current biome mix is lightweight by design and covers:

- forest
- plains
- mountains
- river valley
- coast
- dryland

## Species Distribution Rules

Species are no longer treated as globally present by default.

- each generated species can declare `PreferredBiomes`
- generation scores regions for suitability using fertility, water, biomass, and biome fit
- each species receives a clustered `InitialRangeRegionIds` set rather than random global scatter
- target range size varies by trophic role so producers and herbivores seed more broadly than apex predators
- herbivores and omnivores now also use slightly broader viable-range thresholds than before, which helps fertile regions start with multiple real consumer niches instead of one token prey species
- ecosystem initialization only seeds starting populations inside that initial range
- after the first clustered pass, world generation now patches fauna-empty fertile regions into the nearest plausible herbivore cluster so strong producer regions do not start as plant-only ecological dead ends
- predator and apex ranges are then trimmed back to herbivore-supported regions, keeping predator presence limited and prey-grounded

This keeps the opening world denser without making every region ecologically identical.
The target outcome is regional variation with a healthier prey baseline: some rich regions, some moderate regions, and some sparse ones, rather than globally thin wildlife.

## Starting Polity Placement

Starting polities are seeded from sapient species only and now prefer viable, reasonably separated starting regions.

- candidate regions come from that sapient species' initial range
- settlement suitability favors fertile land, water, biomass support, and biome-specific settlement bonuses
- settlement suitability also favors homeland regions with accessible support species in the local corridor
- starting regions must respect `MinimumStartingPolityRegionSpacing` when practical
- starting regions now mildly prefer connected corridor regions over isolated dead ends
- the generator therefore leaves meaningful empty space for later migration, expansion, and fragmentation

The current default density is roughly one polity per three to four regions, which keeps the early world active without stacking most polities into the same small cluster.
Starting polities also begin with a single home settlement record in that region, usually in a `SemiSettled` state, so early settlement-grounded systems have a real execution point immediately.

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
The denser seed world is intentionally still range-limited and biome-shaped so early chronicle output gains context without turning into random clutter.
Starting-polity homeland scoring also now prefers nearby support species coverage, so focal starts are less likely to open in a dead ecological pocket.
Initial `AnimalBiomass` values are best read as starting ecological context for species seeding and region summaries; once the simulation begins, animal biomass is derived from real consumer populations rather than harvested as an independent food pool.
Initial consumer populations are now seeded from carrying capacity and habitat fit strongly enough that fertile producer-rich regions can support substantially larger herbivore starts.
Predator coverage remains narrower, so early worlds usually establish a herbivore foundation before predator suppression becomes a major constraint.
