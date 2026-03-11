# LivingWorld System Interactions

LivingWorld systems interact through shared state and the structured event pipeline rather than by writing directly to the player chronicle.

## Interaction Pattern

Each major system:

- reads world state
- evaluates pressure or opportunity
- updates its own domain state
- emits canonical events on meaningful transitions
- lets other systems and sinks react afterward

## Current Major Systems

- food and ecology
- agriculture
- trade
- migration
- population
- settlement
- fragmentation
- polity stage progression
- advancement

## Chronicle Separation

Systems may emit many meaningful events in one year, but only `Major` and `Legendary` turning points are shown in the default chronicle.

That separation keeps the architecture clean:

- simulation systems stay honest about causality
- structured history preserves the full event stream
- chronicle presentation stays readable

Population change is a good example: the event still matters to the simulation and the structured record, but it is not part of the default live chronicle unless another stronger turning point makes the historical change visible.

## Focal-Line Ownership

Chronicle ownership is handled centrally:

- systems emit events without deciding whether they belong in the live chronicle
- `ChronicleFocus` determines whether an event belongs to the active focal line
- focus handoff events keep continuity explicit across fragmentation, collapse, and lineage fallback
- `ChroniclePresentationPolicy` then applies severity, eligibility, and cooldown rules

## Common Interaction Chains

```text
Food stress
  -> migration
  -> settlement change
  -> stage change or fragmentation
```

```text
Stable settlement
  -> advancement discovery
  -> farming output
  -> stronger food position
  -> larger historical turning point later
```

```text
Trade relief
  -> hardship easing
  -> possible famine recovery
```

Trade transfers themselves are often structured-only, while the resulting major transition can still surface in the chronicle.

## Design Standard

Systems should emit events for state transitions, not for repeated unchanged conditions.
