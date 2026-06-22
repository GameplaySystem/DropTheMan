# Drop The Man MVP Game Module Requirements

## Purpose

This document defines the smallest prototype-owned game-module requirements for the first playable `DropTheMan` slice.

This document does not redesign the approved framework architecture.

It exists to prevent prototype implementation drift while the framework MVP transitions into prototype-side gameplay work.

---

## Ownership Boundary

`PuzzleFramework` already owns:

* level definition loading
* grid structure
* occupancy truth
* runtime construction contracts
* drag preview
* snap validation
* game state flow
* timer flow
* shared color identity

`DropAwayPrototype` owns:

* `Drop The Man` gameplay nouns
* hole behavior
* stickman behavior
* color meaning
* collection rules
* wrong interaction behavior
* win conditions
* lose conditions
* scene setup
* prototype visuals and prefabs

Forbidden dependency:

```text
PuzzleFramework
    ->
DropAwayPrototype
```

Allowed dependency:

```text
DropAwayPrototype
    ->
PuzzleFramework
```

---

## Confirmed MVP Target

The approved first playable milestone remains:

```text
Load level
    ->
Drag hole
    ->
Snap hole
    ->
Collect matching stickmen
    ->
Win or Lose
```

This prototype document only narrows what `DropAwayPrototype` must own to realize that flow.

---

## Prototype-Owned MVP Responsibilities

The first `DropTheMan` game-module slice must provide:

* a prototype-owned runtime representation for holes
* a prototype-owned runtime representation for stickmen
* a prototype-owned mapping from framework `ColorIdentity` to hole and stickman meaning
* a prototype-owned rule query that answers whether a target cell is enterable for a specific hole
* a prototype-owned rule entry point that evaluates collection
* a prototype-owned rule entry point that evaluates win
* a prototype-owned rule entry point that evaluates lose when timer-driven failure is enabled
* prototype-owned adapters that connect framework construction and interaction outputs to puzzle-specific rules

The prototype must not move these responsibilities back into `PuzzleFramework`.

---

## MVP Simplifications Already Supported By Framework Planning

The first prototype slice should stay narrow:

* single-cell holes are acceptable for the first playable slice
* single-cell stickman occupancy is acceptable for the first playable slice
* hole capacity remains deferred unless the first actual ruleset proves it is required
* queue, buffer, pathfinding, and progression remain out of scope
* broad visual-feedback generalization remains out of scope

These simplifications keep the prototype aligned with the approved framework MVP decisions.

---

## Rule Definition Status

The minimum gameplay-rule decisions that originally blocked prototype runtime implementation are now documented in:

* `docs/DropTheManMVPRules.md`

That rules document currently defines:

* collection timing
* wrong-color interaction
* timer usage
* win condition
* lose condition

For current MVP planning, wrong-color behavior means entry blocking before overlap, not post-snap placement rejection.

---

## Implementation Rule

Prototype implementation may proceed only within the approved rule boundaries documented in:

* this requirements document
* `docs/DropTheManMVPRules.md`

Do not silently invent puzzle rules during coding.

---

## Next Required Implementation Step

The next smallest safe runtime step is:

* prototype rule coordination on top of the built runtime model

That should connect:

* target-cell enterability checks during interaction validation
* snap outcomes
* collection checks
* wrong-color entry blocking
* timer-driven lose requests
* win evaluation
