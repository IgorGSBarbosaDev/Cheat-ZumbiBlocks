# AGENTS.md

## Project
ZB2SecurityLab is an authorized security-research lab for **Zumbi Blocks 2 Open Alpha**.
Its purpose is to test client/server trust boundaries with small, reversible experiments and produce evidence for the game developer.
It is not a cheat distribution project.

## Stack
- C# / .NET
- Unity
- BepInEx 5
- Unity IMGUI diagnostics
- JSONL logs
- `dotnet`
- Git

Inspect the repo before assuming exact framework/package versions.

## Repository map
Use the repository as the source of truth; do not duplicate detailed knowledge here.
- `README.md` — setup, architecture, usage
- `docs/player-state.md` — mapped runtime/player state
- `docs/mutation-tests.md` — mutation lifecycle and restore rules
- `docs/` — findings and experiment notes
- tests — expected behavior and invariants

If code and docs disagree, verify the implementation and update stale docs in scope.

## Engineering rules
Prioritize:
1. minimal scope;
2. reproducibility;
3. observability;
4. reversibility;
5. fail-closed behavior;
6. deterministic cleanup;
7. evidence-based conclusions.

Prefer the smallest experiment that answers the current hypothesis.
Do not perform unrelated refactors.

## Read/write separation
- Keep diagnostics and mutations separate.
- Keep snapshots/read models pure where practical.
- UI must not write directly to gameplay state.
- Reuse existing context, experiment, logging and restore infrastructure.
- Do not add a gameplay write target unless explicitly required by the task.

## Mutation safety
Mutation mode must start disabled on every new game/session.
Before any write:
- validate build/fingerprint;
- validate player/session context;
- resolve the exact target member;
- capture the original value;
- ensure restoration is possible.

If any prerequisite is uncertain, abort and log the reason.
Prefer one-shot writes over per-frame writes unless continuous mutation is the explicit experiment.
Keep at most one mutation experiment active unless the task explicitly changes this invariant.

## Restore
Every mutable experiment must be reversible.
Restore must be safe and idempotent and run when relevant on:
- manual stop;
- timeout;
- player/context loss;
- scene/session change;
- plugin disable/shutdown;
- experiment failure.

Never swallow restore failures; log enough context to diagnose them.

## Security boundaries
Testing is limited to:
- single-player;
- local lab builds;
- private sessions controlled by the developer/testers.

Do not implement:
- anti-cheat bypass/evasion;
- stealth, DLL/process hiding or kernel components;
- persistence or credential access;
- public-server exploitation;
- packet flooding/replay/arbitrary injection/forgery;
- automated abuse against unrelated players;
- distribution-ready cheat functionality.

Network work should begin with observation and authority validation, not arbitrary packet generation.

## Reverse engineering
Use real classes/members discovered in the build; do not guess names or semantics.
Distinguish:
- configuration;
- local runtime state;
- UI-only state;
- network-synchronized state;
- unknown state.

Do not call state server-authoritative without code or runtime evidence.
Use `INCONCLUSIVE` when evidence is insufficient.

## Logging
Use structured JSONL and preserve compatibility where practical.
Log transitions/events, not every frame.
Capture enough to reconstruct: test, original value, requested value, observed value, role/context, outcome and restore result.

## Validation
Preserve existing tests and add coverage for pure/core logic when feasible.
Before finishing, run applicable checks:

```bash
dotnet build -c Release
dotnet test
dotnet format --verify-no-changes
git diff --check
```

Also verify:
- supported fingerprint still passes;
- no unintended write paths were added;
- mutation mode starts disabled;
- restore still works;
- original game files were not modified.

Report checks that could not be executed.

## Agent workflow
For non-trivial changes:
1. inspect relevant code, docs and tests;
2. state the hypothesis;
3. identify the smallest change;
4. implement only that scope;
5. run validation;
6. provide exact manual in-game validation when needed;
7. analyze evidence before proposing the next experiment.

If asked for `/plan`, do not edit or deploy. Return the plan, affected files, tests, risks and manual validation steps first.
Do not automatically continue to the next PoC or expand scope after completing the requested task.
