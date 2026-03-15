# AGENTS.md

## Purpose

This file defines how AI coding agents should work in the LivingWorld repository.

LivingWorld is a chronicle-first command-line world simulation. The player follows one focal line of history while the full world simulation continues underneath. Agents must preserve simulation truth, chronicle readability, and documentation accuracy.

---

## Read First

Before making changes, read these files in this order:

1. `IMPLEMENTED_SYSTEMS_LIST.md`
2. `SIMULATION_ROADMAP.md`
3. `README.md`
4. `ARCHITECTURE.md`
5. `SIMULATION_LOOP.md`

Then read only the subsystem docs relevant to the task, such as:

- `WORLD_GENERATION.md`
- `SIMULATION_FLOW.md`
- `CAUSE_AND_EFFECT.md`
- `EVENT_TYPES.md`
- `SYSTEM_INTERACTIONS.md`
- `DATA_MODEL.md`
- `EVENTS.md`
- `ADVANCEMENT_SYSTEM.md`

---

## Canonical Sources of Truth

### Implementation status
`IMPLEMENTED_SYSTEMS_LIST.md` is the canonical source of truth for what is implemented.

### Priority and sequencing
`SIMULATION_ROADMAP.md` is the canonical source of truth for planned work and phase ordering.

### Runtime and architecture intent
`README.md`, `ARCHITECTURE.md`, and `SIMULATION_LOOP.md` define the baseline system intent unless a newer project doc explicitly supersedes them.

### Current highest-priority work
The **Prehistory Rework** is the current top priority and should be treated as the main development track.

---

## Core Project Rules

### 1. Chronicle-first experience
The chronicle is the primary player-facing output.
Do not turn the game into debug spam, raw state dumps, or repetitive logs.

### 2. Preserve simulation truth
Do not fake outcomes to make the game look better.
Fix underlying model truth rather than papering over problems in output.

### 3. Initialization is not history
If something exists only because the world was initialized, do not chronicle it as if it happened.
If it happened because the simulation changed state after initialization, it may be chronicle-worthy.

### 4. No duplicate chronicle lines
Any work affecting events, messaging, chronology, or rendering must be checked for duplicate or near-duplicate output.

### 5. Preserve terminology
Use project language consistently:

- **Discovery / Discoveries** = revealing or learning about the world
- **Learned** = capability or advancement gained

Do not blur these concepts in code, UI, or chronicle text.

### 6. Preserve observer vs evaluator boundaries
Observer snapshots contain facts.
Evaluator artifacts contain judgments, readiness, viability, scoring, or recommendations.
Do not mix them casually.

### 7. Keep docs in sync
If behavior, architecture, or implementation status changes, update the relevant `.md` files in the same work.

---

## Prehistory Rework Guardrails

When touching prehistory systems, preserve these rules:

- prehistory runs real simulation truth, not a fake side system
- readiness is observer-driven
- viable focal candidates must be truthfully viable
- max-age handling must stay honest
- weak worlds must be surfaced honestly
- candidate scoring applies only to viable candidates
- focal selection must present real starts clearly
- active-play handoff must preserve the real current state
- active play begins paused
- selected peoples convert into the correct active control wrapper descriptively, not through a fake upgrade

Do not weaken hard viability truth just to force a better-looking result.

---

## How Agents Should Work

### Before coding
- identify the subsystem being changed
- read the canonical docs for that subsystem
- identify player-facing impact
- identify testing impact
- identify documentation impact

### During coding
- prefer minimal, coherent changes over scattered hacks
- preserve existing architecture unless the task explicitly changes it
- centralize tunable thresholds in settings/config where possible
- avoid magic-number sprawl
- preserve causal clarity
- keep player-facing text concise and readable

### After coding
- run relevant tests
- review chronicle output if messaging changed
- update relevant docs
- update `IMPLEMENTED_SYSTEMS_LIST.md` if implementation status changed
- update `SIMULATION_ROADMAP.md` if planning or phase order changed
- summarize changes, tests, docs, and follow-up risks

---

## Testing Rules

- Run the narrowest relevant tests first.
- Do not default to a full-suite run for a narrow local change.
- Expand test coverage when changing shared simulation flow, prehistory runtime, focal selection, handoff logic, or chronicle behavior.
- Add or update regression tests for bugs that previously broke world generation, readiness, candidate selection, handoff, or visible output.

If chronicle behavior changed, validate:

- no duplicate lines
- no initialization spam
- no obvious chronology contradictions
- concise phrasing
- acceptable output noise

---

## Documentation Rules

Documentation is part of the feature.

### Update `IMPLEMENTED_SYSTEMS_LIST.md` when:
- a feature is completed
- a phase status changes
- implementation truth changes

### Update `SIMULATION_ROADMAP.md` when:
- priority changes
- phases are reorganized
- major new work is added
- a feature area is split into clearer phases

### Update subsystem docs when:
- simulation behavior changes
- runtime phase behavior changes
- event/cause-effect behavior changes
- data model expectations change
- terminology changes
- prehistory or handoff contracts change

A feature is not complete until code, tests, and relevant docs are updated together.

---

## UI and Output Guidance

Prefer:

- stable layouts
- concise labels
- readable summaries
- player-meaningful information
- consistency across screens

Avoid:

- raw dump screens
- flicker-heavy redraw behavior
- giant repetitive text blocks
- debug wording in player-facing output

LivingWorld should feel like unfolding history, not a diagnostic console.

---

## Definition of Done

A task is done only when:

- the code compiles
- relevant tests pass
- behavior matches project intent
- no obvious chronicle duplication/regression was introduced
- relevant docs were updated
- `IMPLEMENTED_SYSTEMS_LIST.md` was updated if implementation status changed
- `SIMULATION_ROADMAP.md` was updated if roadmap ordering changed
- the final summary explains what changed, what was tested, and any follow-up risks

---

## Preferred Agent Response Format

When reporting completed work, use:

1. What changed
2. Why it changed
3. Files touched
4. Tests run
5. Docs updated
6. Known risks or follow-up work

Keep the summary concrete and concise.
