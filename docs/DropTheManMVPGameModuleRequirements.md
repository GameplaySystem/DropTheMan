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

## Required Prototype Decisions Still Missing

The following rule details are not yet documented precisely enough to implement safely:

* exact collection trigger timing
* exact wrong-color interaction result
* exact timer usage for the first playable slice
* exact win condition expression
* exact lose condition expression when timer is active

These are prototype-owned gameplay decisions.

They should be documented before substantial `DropAwayPrototype` runtime implementation begins.

---

## Implementation Rule

Until the missing gameplay decisions above are documented, `DropAwayPrototype` implementation should stay limited to compile-safe scaffolding and requirement-definition work.

Do not silently invent puzzle rules during coding.

---

## Next Required Documentation Step

Create a small prototype rule document that answers:

1. When does collection happen?
2. What happens on wrong-color overlap or placement?
3. Is the first playable slice timed?
4. What exactly causes win?
5. What exactly causes lose?

After that document is approved, prototype runtime implementation can proceed in small slices.
