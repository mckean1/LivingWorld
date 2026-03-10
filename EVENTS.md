# EVENTS.md

# LivingWorld Event System

LivingWorld now uses a canonical structured event model for simulation events, then routes those events to two separate outputs:

- focused yearly chronicle (console, player-facing)
- append-only JSONL history (developer-facing)

---

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

---

## Severity

- `Debug`
- `Normal`
- `Notable`
- `Critical`

Chronicle view prioritizes focal `Notable` and `Critical` events.

---

## Important Event Types

- `migration`
- `knowledge_discovered`
- `settlement_founded`
- `settlement_consolidated`
- `harvest`
- `food_stress`
- `population_changed`
- `fragmentation`
- `stage_changed`
- `polity_collapsed`
- `world_event`

---

## Chronicle Filtering Rules (Default)

- show focal-polity events only
- show 1-5 short lines in `This Year`
- show optional `Notable Changes` (before -> after)
- show optional `World Notes` (0-2 rare outside events)

---

## JSONL History Rules

- append-only during run
- captures important events for all polities/world entities
- designed for grep/filter/post-run analysis
- intentionally excludes low-value telemetry spam

---

## Example Chronicle Lines

- `Red River Clan migrated to Lower Valley.`
- `Red River Clan discovered Agriculture.`
- `Red River Clan suffered famine.`
- `Red River Clan declined from 84 to 71.`
- `Red River Clan became a Settled Society.`

---

## Example JSONL Record

```json
{"eventId":42,"year":118,"month":12,"season":"Winter","type":"stage_changed","severity":"Notable","narrative":"Red River Clan became a Settled Society.","polityId":3,"polityName":"Red River Clan","regionId":7,"regionName":"Lower Valley","before":{"stage":"Tribe"},"after":{"stage":"SettledSociety"},"metadata":{}}
```
