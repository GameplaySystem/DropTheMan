# Drop The Man Drag Session Design

## Purpose

This document defines the prototype-owned drag-session owner that will connect player drag input to the existing `DropTheManMovementCoordinator`.

This is an integration design only.

It does not implement:

* snap behavior
* occupancy recommit
* closing flow
* win/loss routing
* timer routing
* scene wiring

Read alongside:

* `docs/DropTheManMovementAndCollectionRules.md`
* `docs/DropTheManMovementCoordinatorDesign.md`
* `docs/DropTheManMovementEdgeCaseChecklist.md`
* `docs/DropTheManMVPGameModuleRequirements.md`

---

## Ownership

The drag-session owner is prototype-owned.

Its job is to bridge:

* pointer or drag input
* hole presentation objects
* freeform world-space drag positions
* the mutating `DropTheManMovementCoordinator`

It must not move gameplay meaning into `PuzzleFramework`.

Framework still does not know:

* hole gameplay meaning
* stickman gameplay meaning
* collection
* color matching
* capacity

---

## Responsibilities

The drag-session owner is responsible for:

* selecting the active hole for the current drag
* rejecting drag start on `Full`, `Closing`, or `Completed` holes
* capturing the drag-start committed board coordinate
* capturing the drag-start committed self-footprint
* storing the previous accepted world position for the active drag session
* resolving the current candidate world position for each drag update
* making exactly one mutating coordinator call per accepted drag update
* applying the returned authoritative world position to the visual hole object
* updating the stored previous accepted world position only from coordinator output
* stopping drag immediately when `ShouldStopDragging` becomes true
* routing non-full release into a future snap or commit path
* refusing further coordinator calls after the drag session has ended

---

## Non-Responsibilities

The drag-session owner is not responsible for:

* deciding color matching
* deciding collection acceptance
* deciding wrong-color blocking
* deciding capacity fill
* updating `HoleRuntimeState.CurrentCoordinate` during drag
* updating framework structural occupancy during drag
* performing snap logic
* committing release-time occupancy
* driving hole closing animation
* deciding win or loss
* using physics as gameplay authority

Those remain owned elsewhere.

---

## Core Invariants

These invariants are required for correct integration.

### 1. Coordinator calls are mutating apply calls

`DropTheManMovementCoordinator.EvaluateAndApply(...)` is not a harmless preview query.

One call may:

* change stickman state to `Collecting`
* remove stickmen from active coordinate lookup
* increment hole fill
* mark a hole `Full`

The drag-session owner must never call it for speculative previews, hover checks, or duplicate replays of the same drag update.

### 2. One call per accepted drag update

The drag-session owner must issue at most one coordinator call per accepted drag update.

It must not:

* call once for preview and again for apply
* call separately for visual motion and gameplay motion
* retry the same update after partially applying the result

### 3. Previous accepted position updates only from coordinator result

The drag-session owner stores `previousAcceptedWorldPosition` as session state.

That value changes only when the coordinator returns `AuthoritativeWorldPosition`.

It must not be replaced by raw pointer position.

### 4. Visual movement follows authoritative world position

The visible hole object follows `AuthoritativeWorldPosition`, not the raw drag pointer.

This preserves:

* blocker clamping
* swept validation truth
* drag-time collection truth

### 5. Full-hole stop is authoritative

When `ShouldStopDragging` is true, the drag-session owner must stop normal dragging immediately.

That means:

* stop sending coordinator updates
* stop following raw pointer movement
* keep the hole at the authoritative position returned by the last update

### 6. Snap does not trigger collection

Collection truth exists only inside drag-time coordinator updates.

Release-time alignment must not trigger collection, re-collection, or wrong-color rejection logic.

### 7. Committed board state stays committed during drag

`HoleRuntimeState.CurrentCoordinate` remains the committed board coordinate during drag.

`CellOccupancySystem` remains committed to the pre-drag footprint during drag.

The drag-session owner must not reinterpret either of those as live freeform drag state.

### 8. Release-time occupancy commit is a later separate step

The drag-session owner ends at authoritative drag-time motion plus release routing.

Later code will own:

* snap alignment
* new committed board coordinate
* occupancy release and occupy update

---

## Required Session State

The first implementation should keep the session state explicit.

Required fields:

* active hole runtime state
* active visual hole reference
* drag-start committed board coordinate
* drag-start committed self-footprint coordinates
* previous accepted world position
* drag active flag
* stop-requested flag or equivalent end-of-session guard

Reason:

* the current coordinator depends on committed self-occupancy ignore
* committed occupancy is not updated during drag
* visual freeform position is external to `HoleRuntimeState`

---

## Lifecycle Flow

### Drag Start

Steps:

1. Hit-test or otherwise identify the selected hole visual.
2. Resolve its `HoleRuntimeState`.
3. Reject start if the hole is not in `Active`.
4. Reject start if another drag session is already active.
5. Capture the hole's current visual world position as the initial `previousAcceptedWorldPosition`.
6. Capture the hole's committed board coordinate from `HoleRuntimeState.CurrentCoordinate`.
7. Capture the committed self-footprint coordinates from that committed origin.
8. Mark the drag session active.

Important notes:

* no coordinator call is required just to begin drag
* drag start does not collect by itself
* drag start does not alter committed occupancy

### Drag Update

Steps:

1. Resolve the current candidate world position from pointer or drag input.
2. Build exactly one `DropTheManMovementCoordinatorRequest`.
3. Call `EvaluateAndApply(...)` exactly once.
4. Read the returned `AuthoritativeWorldPosition`.
5. Move the visual hole object to that authoritative position.
6. Replace the stored `previousAcceptedWorldPosition` with that authoritative position.
7. Forward `NewlyCollectingStickmen` to later presentation hooks if needed.
8. If `ShouldStopDragging` is true, terminate the drag session immediately.

Important notes:

* raw pointer position is only an input candidate
* authoritative position comes only from the coordinator result
* blocked movement still updates the visual from the authoritative result, which may remain at the previous accepted position

### Drag Release

For a non-full active hole:

1. End the raw drag session.
2. Hand off the final authoritative drag position to a future snap owner.
3. Do not call the movement coordinator again during release.
4. Do not collect on release.

For a hole that became full during drag:

1. The drag session already ended because `ShouldStopDragging` was authoritative.
2. Release must not restart normal drag handling.
3. Release must not run normal non-full snap-owned collection logic.

### Future Occupancy Commit Path

The intended later path is:

```text
drag ends on non-full hole
    ->
snap aligns final accepted position
    ->
occupancy commit updates old footprint to new footprint
    ->
HoleRuntimeState.CurrentCoordinate updates to snapped committed coordinate
```

This path is intentionally not implemented in this design step.

---

## Integration With Current Coordinator

The current coordinator already assumes:

* committed self-occupancy is ignored using the hole's committed footprint
* structural occupancy is not updated during drag
* `HoleRuntimeState.CurrentCoordinate` is not the live freeform drag position
* `StickmanCoordinateIndex` removes accepted stickmen immediately from active blocking lookup

The drag-session owner must preserve those assumptions rather than fight them.

That means the first integration should be conservative:

* one active hole per session
* one mutating coordinator call per update
* one authoritative visual position after each call
* no mid-session occupancy recommit

---

## Failure And Safety Cases

### Duplicate update safety

If the same input update is processed twice, gameplay may double-mutate.

Design rule:

* the drag-session owner should guard against accidental duplicate apply calls for the same accepted update

For MVP, this can be satisfied by centralizing all drag-time coordinator access in one owner.

### Full-hole mid-drag safety

If a hole becomes full during a drag update:

* the coordinator result is authoritative
* the drag-session owner stops immediately
* no further normal drag updates are sent for that hole in that session

### Non-draggable state safety

If a hole is already `Full`, `Closing`, or `Completed`:

* drag start is rejected
* no coordinator request should be issued

### Release-after-stop safety

If input release happens after the session was already force-stopped by a full-hole result:

* release must be treated as cleanup only
* it must not create new gameplay motion or collection

### Visual desync safety

If the raw pointer outruns authoritative motion:

* the visual hole still follows authoritative motion only
* blocked or clamped motion must remain visibly clamped

### Outcome isolation safety

The drag-session owner must not decide win, loss, timer precedence, or closing completion.

Those remain future integrations.

---

## Risks

### Stale committed occupancy model during drag

Current drag-time logic intentionally ignores only the committed pre-drag self-footprint.

This is acceptable for the current narrow slice, but later release-time occupancy commit must preserve that contract carefully.

### Duplicate mutation risk

Because the coordinator mutates gameplay state, any accidental preview-style second call is dangerous.

This is the highest integration risk for the next implementation step.

### External visual position dependency

The gameplay model currently depends on freeform drag position living outside `HoleRuntimeState`.

That is correct for now, but the drag-session owner must make that ownership obvious in code.

### No release commit yet

A non-full hole can be dragged authoritatively, but there is still no committed end-of-drag board update path.

That is intentionally deferred, not forgotten.

---

## Still-Open Design Questions

These do not block the next narrow implementation slice, but they remain real follow-up decisions.

### 1. Candidate world-position source

The owner still needs a concrete adapter for:

* pointer-to-plane conversion
* pointer-to-board drag plane conversion
* camera dependency

That is scene integration, not movement-rule design.

### 2. Release-time snap owner shape

The future snap handoff path is clear, but the concrete component boundary is still open:

* same component as drag-session owner
* separate release/snap coordinator
* separate presentation adapter

### 3. Full-hole post-stop presentation handoff

The gameplay rule is clear:

* stop dragging immediately when full

The later visual handoff for:

* align or settle
* close
* disappear

still needs its own implementation design.

---

## Recommended First Implementation Slice

The next smallest safe implementation step is:

* a prototype-owned drag-session owner that manages one active hole
* captures drag-start state
* sends exactly one mutating coordinator call per drag update
* moves the visual hole to `AuthoritativeWorldPosition`
* stops immediately on `ShouldStopDragging`
* ends release on a deferred non-full snap handoff without implementing snap itself

Do not add in the same step:

* occupancy recommit
* snap behavior
* closing flow
* timer or outcome routing
* scene-wide game-state orchestration

That would mix too many responsibilities into the first integration slice.

---

## Final Summary

```text
The drag-session owner is a prototype-owned integration layer.
It owns active drag session state, candidate position gathering, single-call coordinator application,
and authoritative visual hole motion.
It does not own collection rules, snap rules, occupancy commit, or terminal game outcomes.
During drag, committed board state stays committed.
During release, non-full holes route toward future snap and commit.
Full holes stop immediately from coordinator truth, not from release logic.
```
