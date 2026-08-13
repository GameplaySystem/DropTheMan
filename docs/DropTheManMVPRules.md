# Drop The Man MVP Rules

## Purpose

This document defines the smallest approved gameplay rule set for the first playable `DropTheMan` slice.

The goal is to unblock prototype runtime implementation without expanding into broader or speculative rules.

This document is prototype-owned.

It does not move puzzle meaning into `PuzzleFramework`.

Detailed movement, collection, capacity, and win-sequencing rules are further locked in:

* `docs/DropTheManMovementAndCollectionRules.md`

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

* holes include the intended multi-cell MVP shapes
* stickmen are single-cell objects
* hole capacity is part of the first slice
* queue, buffer, pathfinding, and progression remain out of scope
* collection eligibility and reservation are evaluated during drag when a matching target cell is entered

These assumptions match the approved MVP narrowing decisions.

---

## Important Architecture Constraint

`CellOccupancySystem` must not be used as the authoritative blocker for collectible stickmen in the first prototype slice.

Reason:

* same-color target cells must stay enterable so drag-time collection can happen
* if stickmen occupy structural blocking space in the same way as holes, matching overlap and collection become impossible

Approved prototype interpretation:

* structural occupancy blocks hole placement only where placement should truly fail
* stickmen are tracked separately as collectible targets addressable by coordinate
* prototype-owned rule queries may still treat some stickman coordinates as non-enterable for a specific hole
* collection reservation happens during drag when a color-compatible target cell is entered or overlapped
* visual collection starts only when the assigned hole reaches the collection trigger threshold
* capacity fills after collection presentation completes

This keeps framework occupancy generic while allowing prototype collection behavior.

---

## Approved MVP Rules

### 1. Collection Timing

Collection is evaluated during drag.

It is not owned by release-time snap alignment.

Flow:

```text
Drag preview
    ->
If target cell is wrong-color for the moving hole:
    movement stays at last valid position
    ->
If target cell is same-color for the moving hole:
    overlap is allowed
    collectible and one capacity slot are reserved immediately
    ->
When the hole reaches the collection trigger threshold:
    placeholder collection presentation starts and completes
    ->
Capacity fill is applied
    ->
Player releases drag
    ->
Snap aligns the hole to the final valid grid position
```

Reason:

* this matches the intended gameplay behavior
* this keeps snap limited to release-time alignment instead of making it the owner of collection timing
* this keeps color-based collection meaning inside prototype-owned rules

### 2. Wrong-Color Interaction

If a hole tries to move into a cell containing a stickman of a different color:

* no collection happens
* that target cell is treated as non-enterable for that hole
* drag preview should stop at the last valid position
* snap does not own this rejection because the hole should never be allowed into that cell during drag

Reason:

* this preserves clear puzzle feedback earlier than post-snap rejection
* this matches the intended gameplay behavior more closely
* this keeps gameplay truth inside prototype-owned rule logic rather than physics-authoritative collision behavior
* this still leaves room for later interpolation, smoothing, or collider-assisted feel as presentation only

### 3. Timer Usage

The first playable slice is timed.

The timer starts when gameplay enters the active `Playing` state after runtime construction completes.

Reason:

* the approved MVP target includes proving win or lose flow
* timer expiry is the narrowest reusable lose condition already supported by the framework
* this proves the `TimerSystem` and `GameStateSystem` with a real prototype use case

### 4. Hole Capacity And Completion

For the MVP:

* hole capacity is determined by the current hole shape
* a hole can collect only until it reaches that capacity
* reservation alone does not increase fill count or make a hole full
* presentation completion converts a reserved slot into one filled slot
* when fill count reaches capacity during a drag update, the hole becomes full and stops movement immediately
* a full hole becomes non-draggable immediately
* the collected targets finish their collection animation first
* after target visuals finish, the hole aligns, closes, and disappears

Reason:

* this is core `Drop The Man` behavior, not optional polish
* it keeps capacity meaning prototype-owned instead of forcing early framework generalization

### 5. Win Condition

User-facing goal:

```text
Collect all required stickmen.
```

Runtime victory gate:

```text
All required holes are completed.
```

Stickman collection happens during drag.

Victory must not trigger from raw collection overlap, collection acceptance, or a stickman entering `Collecting`.

The final win request may only happen after the game-module outcome owner confirms all required holes have reached `Completed`.

Reason:

* this preserves the player-facing collection objective
* this prevents victory from bypassing full-hole completion
* the final win request should respect the full-hole completion sequence rather than bypassing it

### 6. Lose Condition

The level is lost when timer expiry is accepted before the runtime victory gate is accepted.

Timer expiry must eventually compete with hole-completion victory through a single terminal-state guard.

Reason:

* this gives the first slice a clean lose flow without inventing additional puzzle-specific failure mechanics
* it stays independent from wrong-color interaction, which remains a movement-blocking rule rather than an end-state trigger
* it prevents simultaneous or duplicate `Won` and `Lost` outcomes

---

## Explicitly Deferred Rules

The following are not part of the first playable slice:

* wrong-color loss
* physics-authoritative movement resolution
* combo scoring
* chain reactions
* multi-cell stickman groups
* queue or buffer-style staging

If one of these becomes necessary later, it should be introduced as a separate documented step.

---

## Prototype Ownership Boundaries Preserved

Prototype-owned:

* hole color meaning
* stickman color meaning
* color-based target enterability
* drag-time collection evaluation
* shape-based capacity meaning
* full-hole close and disappear sequencing
* wrong-color entry blocking
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

* a prototype-owned rule coordinator that evaluates target-cell enterability during interaction
* matching collection during drag
* shape-based capacity completion
* timer-driven lose requests and win evaluation on top of the existing runtime model

Implementation should follow the detailed rules document above rather than re-inferring edge-case behavior from this summary.
