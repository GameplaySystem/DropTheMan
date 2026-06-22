# Drop The Man MVP Rules

## Purpose

This document defines the smallest approved gameplay rule set for the first playable `DropTheMan` slice.

The goal is to unblock prototype runtime implementation without expanding into broader or speculative rules.

This document is prototype-owned.

It does not move puzzle meaning into `PuzzleFramework`.

---

## Problem

The framework MVP is ready for prototype-side gameplay work, but the first playable `DropTheMan` slice still needs exact rule decisions for:

* collection timing
* wrong-color interaction
* timer usage
* win condition
* lose condition

Without those decisions, runtime implementation would be forced to invent puzzle rules during coding.

---

## Assumptions

The first playable slice uses these simplifying assumptions:

* holes are single-cell objects
* stickmen are single-cell objects
* hole capacity is not used in the first slice
* queue, buffer, pathfinding, and progression remain out of scope
* collection is evaluated after snap, not during drag preview

These assumptions match the approved MVP narrowing decisions.

---

## Important Architecture Constraint

`CellOccupancySystem` must not be used as the authoritative blocker for collectible stickmen in the first prototype slice.

Reason:

* the hole must be able to snap onto a collectible target cell
* if stickmen occupy structural blocking space in the same way as holes, collection becomes impossible

Approved prototype interpretation:

* structural occupancy blocks hole placement only where placement should truly fail
* stickmen are tracked separately as collectible targets addressable by coordinate
* collection checks happen after a successful snap resolves the hole position

This keeps framework occupancy generic while allowing prototype collection behavior.

---

## Approved MVP Rules

### 1. Collection Timing

Collection is evaluated after a successful snap.

It is not evaluated continuously during drag preview.

Flow:

```text
Drag preview
    ->
Snap resolves final valid board position
    ->
Prototype rule checks target cell
    ->
If matching stickman exists, collect it
```

Reason:

* this matches the existing framework boundary where drag previews and snap finalizes placement
* this avoids smuggling collection meaning into drag preview logic
* this keeps the first prototype slice simpler to reason about

### 2. Wrong-Color Interaction

If a hole snaps onto a cell containing a stickman of a different color:

* no collection happens
* the placement is rejected
* the hole returns to its last valid snapped position

Reason:

* this preserves clear puzzle feedback
* this avoids adding punishment systems or extra failure rules too early
* this keeps wrong-color behavior consistent with snap-owned placement resolution

### 3. Timer Usage

The first playable slice is timed.

The timer starts when gameplay enters the active `Playing` state after runtime construction completes.

Reason:

* the approved MVP target includes proving win or lose flow
* timer expiry is the narrowest reusable lose condition already supported by the framework
* this proves the `TimerSystem` and `GameStateSystem` with a real prototype use case

### 4. Win Condition

The level is won when all authored stickmen in the level have been collected.

Reason:

* this is the smallest clear completion rule
* it aligns with the core game description already captured in framework docs

### 5. Lose Condition

The level is lost when the timer expires before all stickmen are collected.

Reason:

* this gives the first slice a clean lose flow without inventing additional puzzle-specific failure mechanics
* it stays independent from wrong-color interaction, which remains a placement rejection rather than an end-state trigger

---

## Explicitly Deferred Rules

The following are not part of the first playable slice:

* hole capacity limits
* wrong-color loss
* collection during drag
* combo scoring
* chain reactions
* multi-cell holes
* multi-cell stickman groups
* queue or buffer-style staging

If one of these becomes necessary later, it should be introduced as a separate documented step.

---

## Prototype Ownership Boundaries Preserved

Prototype-owned:

* hole color meaning
* stickman color meaning
* collection evaluation
* wrong-color rejection
* win decision
* lose decision
* timer consequence interpretation

Framework-owned:

* grid structure
* occupancy mechanics
* drag preview
* snap validation
* game state transitions
* timer tracking
* shared color identity

---

## Recommended Next Implementation Step

After this rules document, the next smallest safe implementation slice is:

* prototype-owned runtime data and contracts for holes and stickmen
* coordinate-based stickman lookup that is separate from structural occupancy blocking
* no scene/prefab spawning complexity beyond what is needed to compile and connect to the existing framework seams
