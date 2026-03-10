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
- `Normal`
- `Notable`
- `Critical`

Default chronicle playback only surfaces selected `Notable` and `Critical` events for the focused polity.

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

- follows one focal polity/lineage
- shows notable focal historical beats as short lines
- keeps storage chronological but renders the visible watch buffer newest-first
- suppresses yearly report formatting entirely
- suppresses noisy bookkeeping and most non-focal telemetry

Current chronicle formatting favors:

- polity formation/splits/collapse
- migration
- settlement founding and consolidation
- stage changes
- knowledge discoveries
- severe food stress
- major population shifts and milestone growth
- focus handoffs when the watched lineage changes subject

Current noise-control rules include:

- only selected notable event types reach the player chronicle
- repeated focal migrations are collapsed to one visible chronicle entry per year
- structured history still records the full underlying event stream

## JSONL History Rules

- append-only during the run
- captures important events across the whole world
- remains the canonical stored history beneath the chronicle
- keeps `before` / `after` / `metadata` context for later tools and history views

## Example Chronicle Lines

- `Year 18 - River Clan migrated to Red Valley.`
- `Year 41 - River Clan discovered Agriculture.`
- `Year 57 - River Clan founded a settlement in Red Valley.`
- `Year 84 - River Clan became a Settled Society.`
- `Year 133 - River Clan suffered famine.`
- `Year 136 - Stone Clan split from River Clan in High Ridge.`

## Example JSONL Record

```json
{"eventId":42,"year":118,"month":12,"season":"Winter","type":"stage_changed","severity":"Notable","narrative":"Red River Clan became a Settled Society.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"stage":"Tribe"},"after":{"stage":"SettledSociety"},"metadata":{}}
```
