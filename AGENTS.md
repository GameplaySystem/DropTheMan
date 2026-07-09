# AGENTS.md

## Project Overview

This repository is the `DropAwayPrototype` Unity project for the Drop The Man / Drop Away playable prototype.

It consumes the shared `PuzzleFramework` package locally and owns Drop The Man-specific runtime rules, scene adapters, prototype content, and presentation placeholders.

Framework code must remain in `PuzzleFramework`. Game-specific nouns and behavior such as holes, stickmen, collection, capacity, and Drop The Man win timing belong in this repository.

## Documentation Preflight Before Implementation

Before implementing any non-trivial task, Codex must identify and read the documentation files relevant to that task.

The preflight should include:

1. List the docs that were read.
2. Summarize the rules, constraints, and decisions from those docs that affect the task.
3. Identify any conflicts, stale assumptions, or gaps between the prompt and the docs.
4. Stop for clarification if the requested implementation would contradict approved documentation.
5. Proceed only after the task scope is aligned with the docs.

Do not read every document blindly. Read the smallest sufficient set of task-relevant docs.

Examples:

- For Drop The Man movement, collection, snap, capacity, or completion tasks, read:
  - `docs/DropTheManMovementAndCollectionRules.md`
  - `docs/DropTheManMVPRules.md`
  - `docs/DropTheManRuntimeIntegrationDesign.md`
  - `docs/DropTheManPlayableSceneAdapterDesign.md`
  - `C:/Users/Gaming/Projects/PuzzleFramework/docs/CRITICAL_RULE_CLARIFICATIONS.md`
  - `C:/Users/Gaming/Projects/PuzzleFramework/docs/IMPLEMENTATION_WATCHLIST.md`
- For collection presentation timing tasks, also read:
  - `docs/DropTheManCollectionPresentationTimingDesign.md`
- For full-hole completion or outcome routing tasks, also read:
  - `docs/DropTheManFullHoleCompletionFlowDesign.md`
  - `docs/DropTheManOutcomeRoutingDesign.md`
- For framework-level system changes, work in `PuzzleFramework` and read:
  - the relevant `PuzzleFramework/docs/FrameworkSystems/**` document
  - `PuzzleFramework/docs/FrameworkArchitecture.md`
  - `PuzzleFramework/docs/ImplementationRoadmap/FrameworkMVPPlan.md`
  - `PuzzleFramework/docs/IMPLEMENTATION_WATCHLIST.md`
- For content, JSON, save/load, or editor tasks, read:
  - relevant Content Systems docs
  - relevant Runtime Construction docs
  - Drop The Man level/content docs
  - `C:/Users/Gaming/Projects/PuzzleFramework/docs/IMPLEMENTATION_WATCHLIST.md`

Documentation is the source of truth. If code and docs disagree, report the drift before changing behavior.

## Prototype Ownership Rules

- Keep Drop The Man gameplay meaning inside `DropAwayPrototype`.
- Do not add Drop The Man-specific nouns or behavior to shared framework code.
- Runtime services own gameplay truth; scene objects, colliders, and views are adapters unless a document explicitly changes that rule.
- Physics may support selection or visual feel, but deterministic prototype runtime rules own movement, collection, capacity, completion, and outcome meaning.
- Do not implement animation, prefab spawning, event bus behavior, level loading UX, `TimerMode.CountUp`, schema changes, or framework API changes unless the task and approved docs explicitly authorize them.

## Unity Project Rules

- Do not modify Unity scenes, prefabs, package manifests, project settings, or non-script assets unless the task explicitly asks for that scope.
- Do not modify Unity-generated folders such as `Library`, `Temp`, `Obj`, `Logs`, or `UserSettings`.
- Do not add third-party packages without approval.
- Keep changes inside intentional source locations.

## Handoff Requirement

After implementation, Codex must report:

- documents read
- files changed
- root cause or design reason
- verification performed
- any docs updated
- any unresolved risks
- whether unrelated dirty files were left untouched

Documentation-only tasks may use a shorter changed-files summary, but implementation tasks must include enough context for a maintainer to defend the change later.

## Git Expectations

- Do not commit automatically unless the user explicitly asks for commits.
- Keep commits scoped to one coherent change.
- Do not revert, delete, or overwrite unrelated local changes.
- When committing, stage explicit paths and exclude unrelated Unity-generated or user-owned changes.
