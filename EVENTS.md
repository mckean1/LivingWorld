# LivingWorld Event System

LivingWorld uses a canonical structured event model for simulation events, then routes those events into separate storage and presentation paths.

## Event Model

Core fields:

- `eventId`, `year`, `month`, `season`
- `type`, `severity`, `narrative`, `details`, `reason`
- `polityId`, `polityName`
- `relatedPolityId`, `relatedPolityName`
- `speciesId`, `speciesName`
- `regionId`, `regionName`
- `settlementId`, `settlementName`
- `before`, `after`, `metadata`

## Severity

- `Debug`
- `Minor`
- `Notable`
- `Major`
- `Legendary`

Structured history keeps the full event stream. Default chronicle playback only surfaces `Major` and `Legendary` events for the focused polity.

## Storage Versus Presentation

`World.AddEvent(...)` is the source of truth.

- `World.Events` remains append-only in chronological order
- `HistoryJsonlWriter` persists the structured stream
- `ChronicleEventFormatter` applies player-facing filtering
- `ChronicleWatchRenderer` only renders what survives presentation filtering

Suppressed chronicle events are still preserved in structured history with their metadata, causal context, and before/after state.

## Important Event Types

- `migration`
- `knowledge_discovered`
- `settlement_founded`
- `settlement_consolidated`
- `food_stress`
- `population_changed`
- `fragmentation`
- `stage_changed`
- `polity_collapsed`
- `focus_handoff_fragmentation`
- `focus_handoff_collapse`
- `focus_lineage_continued`
- `focus_lineage_extinct_fallback`
- `trade_transfer`
- `trade_link_started`
- `trade_relief`
- `trade_dependency`
- `trade_link_collapsed`
- `world_event`

## Chronicle Filtering Rules

Normal player-facing watch mode:

- follows one focal polity or lineage
- shows concise historical beats rather than telemetry
- keeps storage chronological but renders the visible watch buffer newest-first
- suppresses yearly report formatting entirely
- suppresses noisy bookkeeping and most non-focal telemetry

Current chronicle formatting favors:

- migration and relocation turning points
- settlement founding and durable consolidation
- stage changes and civilization formation
- breakthrough discoveries such as fire or agriculture
- major hardship transitions such as shortages beginning, famine striking, and famine recovery
- fragmentation, collapse, and lineage handoffs
- major population declines and large milestone growth

Lower-level reminders remain structured-only by default:

- repeated hardship persistence messages
- trade transfers and low-level relief events
- cultivation expansion bookkeeping
- smaller discoveries and ongoing status updates

## Chronicle Cooldowns

Cooldowns apply only to chronicle presentation, never to event storage.

Baseline cooldowns:

- `migration`: 20 years per polity
- `settlement_consolidated`: 25 years per polity
- `food_stress`: 15 years per polity for repeated reminders

No chronicle cooldown:

- `knowledge_discovered`
- `settlement_founded`
- `stage_changed`
- `fragmentation`
- `polity_collapsed`
- lineage handoff events

Cooldown bypass rules:

- severity increases
- a condition starts, worsens, improves, or ends
- a rare defining milestone occurs
- a major cause-and-effect turning point occurs

In practice this means `shortage -> famine`, `famine -> recovery`, settlement founding, stage transitions, fragmentation, collapse, and major discoveries still appear even if related events happened recently.

## JSONL History Rules

- append-only during the run
- captures lower-severity and chronicle-suppressed events as well as visible turning points
- remains the canonical stored history beneath the chronicle
- keeps `before` / `after` / `metadata` context for later tools and history views

## Example Chronicle Lines

- `Year 18 - River Clan migrated to Red Valley.`
- `Year 41 - River Clan began farming.`
- `Year 57 - River Clan founded a settlement in Red Valley.`
- `Year 84 - River Clan became a Settled Society.`
- `Year 133 - Famine struck River Clan.`
- `Year 149 - River Clan recovered from famine.`
- `Year 136 - Stone Clan split from River Clan in High Ridge.`

## Example JSONL Record

```json
{"eventId":42,"year":118,"month":12,"season":"Winter","type":"stage_changed","severity":"Major","narrative":"Red River Clan became a Settled Society.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"stage":"Tribe"},"after":{"stage":"SettledSociety"},"metadata":{}}
```
