# Drop The Man Release Snap And Occupancy Commit Design

## Purpose

This document defines the smallest safe release-time flow for a non-full dragged hole in `DropTheMan`.

Its job is narrow:

* take the last authoritative freeform drag position
* snap it to a committed board coordinate
* update structural occupancy once
* update `HoleRuntimeState.CurrentCoordinate`

It does not implement:

* collection
* color matching
* capacity fill
* full-hole closing
* win/loss
* timer routing
* presentation animation

Read alongside:

* `docs/DropTheManMovementAndCollectionRules.md`
* `docs/DropTheManMovementCoordinatorDesign.md`
* `docs/DropTheManDragSessionDesign.md`

---

## Ownership

This release path is prototype-owned orchestration built on top of framework systems.

Framework may provide:

* world-to-grid conversion
* grid-to-world conversion
* shape-aware snap validation
* occupancy queries
* occupancy mutation operations

`DropTheMan` owns:

* deciding when release commit should run
* refusing normal release snap for full holes
* deciding what to do when snap or commit fails
* keeping collection and puzzle meaning out of snap

This preserves the approved boundary:

* framework owns alignment and structural usage
* prototype owns gameplay meaning

---

## Responsibilities

The release snap / occupancy-commit layer is responsible for:

* accepting a non-full hole plus the last authoritative drag world position
* evaluating snap using framework `GridSnapSystem`
* resolving the snapped origin coordinate and snapped world position
* validating the multi-cell footprint structurally at the snapped coordinate
* validating occupancy while ignoring the hole's current committed self-footprint
* releasing old committed occupancy if a new committed origin is accepted
* occupying the new committed footprint
* rolling back occupancy if the move commit fails after old occupancy was released
* updating `HoleRuntimeState.CurrentCoordinate` only after successful occupancy commit
* returning an explicit result to the caller

---

## Non-Responsibilities

This layer must not:

* trigger collection
* evaluate wrong-color blocking
* interpret stickman state
* fill capacity
* transition holes to `Full`, `Closing`, or `Completed`
* request `Won` or `Lost`
* react to timer expiry
* update occupancy during drag
* drive animation or presentation sequencing
* use physics as the source of gameplay truth

Those remain outside this slice.

---

## Core Rules Preserved

The release design preserves these required rules:

1. Snap is release-time alignment only.
2. Snap does not trigger collection.
3. Snap does not decide color matching.
4. Snap does not fill capacity.
5. Snap does not request win/loss.
6. Drag-time coordinator already owns collection and wrong-color blocking.
7. `HoleRuntimeState.CurrentCoordinate` remains committed state until successful release commit.
8. `CellOccupancySystem` does not mutate during drag.
9. Occupancy updates once on release for non-full holes.
10. Full holes do not run the normal non-full snap/commit flow.

---

## Existing Framework Surface

This design intentionally targets the existing framework APIs rather than inventing new framework systems.

Relevant current framework behavior:

* `GridSnapSystem.Evaluate(...)` converts world position to nearest grid coordinate using `GridWorldLayout`
* `GridSnapSystem` validates:
  * board bounds
  * active structural cells
  * blocked cells
  * occupied cells
  * reserved cells
* `GridSnapSystem` already ignores the caller's current committed footprint through `CurrentOriginCell`
* `CellOccupancySystem` provides:
  * `Occupy`
  * `Release`
  * `Reserve`
  * `Unreserve`
  * read queries for occupied and reserved cells

This is enough for the first release-commit slice.

---

## Recommended Owner Shape

The recommended next layer is a prototype-owned release coordinator or commit service separate from the drag-time movement coordinator.

Reason:

* drag-time movement is already stateful and mutating
* release-time commit has different invariants
* keeping release commit separate avoids mixing drag-time collection logic with release-time alignment logic

Recommended call direction:

```text
drag-session owner
    ->
release snap / commit owner
    ->
GridSnapSystem
    ->
CellOccupancySystem
```

The drag-session owner should call this only when:

* release succeeds
* the hole is still `Active`
* the previous drag result did not stop because the hole became `Full`

---

## Inputs

The release layer should receive:

* `DropTheManRuntimeModel`
* `HoleRuntimeState`
* last authoritative drag world position
* `GridWorldLayout`
* framework `GridSnapSystem`

It may also receive:

* the old committed coordinate explicitly
* the old committed footprint explicitly

but this is optional because both can be resolved from `HoleRuntimeState.CurrentCoordinate` and `HoleRuntimeState.Footprint`.

---

## Result Contract

The release layer should return a result that makes commit outcome explicit.

Recommended conceptual fields:

* `Success`
* `WasCommitted`
* `CommittedOriginCoordinate`
* `CommittedWorldPosition`
* `UsedExistingCommittedOrigin`
* `FailureReason`

Optional helpful field:

* `RequiresFallbackVisualReturn`

Intended meaning:

* `Success` means the release flow completed without internal failure
* `WasCommitted` means the snapped placement became the new committed board state
* `CommittedOriginCoordinate` is the authoritative committed coordinate after the operation
* `CommittedWorldPosition` is the visual position the hole should align to after release

If commit fails:

* `CurrentCoordinate` must remain unchanged
* old occupancy must remain restored
* returned world position should normally be the old committed world position

---

## Release Flow

### Happy Path

Recommended algorithm:

```text
release non-full active hole
    ->
read hole.CurrentCoordinate as old committed origin
    ->
resolve old committed footprint from hole footprint + old origin
    ->
evaluate framework GridSnapSystem with:
        worldPosition = last authoritative drag world position
        currentOriginCell = old committed origin
        footprintOffsets = hole footprint offsets
        worldLayout
        gridBoard
        cellOccupancySystem
    ->
if snap invalid:
        return old committed origin/world position
        do not mutate occupancy
        do not update CurrentCoordinate
    ->
read snapped origin and snapped world position
    ->
resolve new footprint coordinates from snapped origin
    ->
if snapped origin == old committed origin:
        no occupancy mutation required
        keep CurrentCoordinate as-is
        return snapped world position
    ->
release old footprint occupancy
    ->
occupy new footprint occupancy
    ->
if all occupy calls succeed:
        update HoleRuntimeState.CurrentCoordinate
        return committed snapped world position
```

### Why This Is Acceptable

This is the smallest safe release-time path because:

* framework snap already performs footprint-aware structural validation
* framework snap already ignores the hole's current committed footprint
* occupancy mutation happens only once the snapped placement is accepted
* puzzle rules stay outside snap

---

## Rollback Flow

### Failure Window

The only dangerous window is:

```text
old footprint released
    ->
new footprint occupancy partially or fully fails
```

This can happen if:

* occupancy changed between validation and commit
* the caller misused the API
* later orchestration adds competing structural mutation

### Required Rollback

Recommended rollback algorithm:

```text
track released old coordinates in order
track newly occupied new coordinates in order
if any new occupy call fails:
    release every newly occupied new coordinate
    re-occupy every released old coordinate
    keep HoleRuntimeState.CurrentCoordinate unchanged
    return failure with old committed world position
```

### Why CurrentCoordinate Must Update Last

`HoleRuntimeState.CurrentCoordinate` must update only after the new footprint has been fully occupied successfully.

Reason:

* it is the authoritative committed board state
* if it updates early, rollback becomes ambiguous
* the rest of the runtime would temporarily believe the hole has moved even if occupancy commit later fails

### Restore Failure Policy

In the current MVP architecture, rollback restore failure should be treated as an unexpected integrity failure rather than a normal gameplay branch.

Reason:

* the board is single-threaded
* the release owner is expected to be the only commit writer for this hole at that moment
* if re-occupying the old footprint fails after newly occupied cells were released, a broader orchestration invariant has already been broken

For MVP:

* still attempt restore
* report explicit failure
* prefer surfacing the failure rather than silently continuing with corrupted occupancy state

---

## Snap Validity Policy

### Use Existing GridSnapSystem As First Authority

The first release slice should use the existing framework `GridSnapSystem` as the alignment and validation authority.

Reason:

* it already rounds world position to nearest grid cell
* it already validates shape-aware occupancy
* it already ignores self-footprint through `CurrentOriginCell`
* re-implementing snap rules in the prototype would duplicate framework behavior unnecessarily

### No Extra Prototype Gameplay Validation At Release

The release layer should not perform color or collection checks.

Release-time validation is structural only:

* bounds
* active cells
* blocked cells
* occupied cells
* reserved cells

This is correct because drag-time movement coordinator already owns gameplay blocking and collection.

---

## Fallback Position Policy

If snap or occupancy commit fails, the default fallback should be:

* keep `HoleRuntimeState.CurrentCoordinate` at the old committed origin
* keep old occupancy
* return the old committed world position

This is safer than returning the unsnapped freeform drag position because:

* unsnapped freeform position is not committed board state
* occupancy still belongs to the old footprint
* the framework snap docs already approve gameplay fallback to the last valid placement

Presentation may later animate the visible return, but gameplay truth should be the old committed placement.

---

## Edge Cases

### Release After Blocked Drag

If drag was previously clamped by a blocker, the authoritative release world position is already the last accepted drag position.

Release still runs the normal snap evaluation from that authoritative position.

Outcomes:

* if nearest snapped coordinate is valid, commit there
* if nearest snapped coordinate is invalid, return to old committed coordinate

### Release After Drag-Time Collection But Hole Is Not Full

This is normal.

Accepted stickmen are already in `Collecting` and already removed from the active coordinate index.

Release snap does not inspect stickman state and must not re-run collection logic.

### Release When Final Authoritative Position Is Already Aligned

If the final authoritative world position already maps to the snapped committed coordinate:

* snap returns that same coordinate
* if it matches the old committed origin, no occupancy mutation is needed
* if it is a different valid coordinate, commit proceeds normally

### Release Near A Border Or Corner

Nearest-cell selection is inherited from framework `GridSnapSystem`.

For MVP, the prototype should accept the current framework conversion policy rather than adding puzzle-specific snap math.

If border tie behavior later feels wrong, that is a framework snap concern, not a `DropTheMan` rule concern.

### Snapped Coordinate Would Put Footprint Partly Outside Board

Framework snap returns invalid.

No occupancy mutation occurs.

Old committed placement remains authoritative.

### Snapped Coordinate Would Overlap Another Hole

Framework snap returns invalid because occupancy or reservation blocks the footprint.

No occupancy mutation occurs.

Old committed placement remains authoritative.

### Snapped Coordinate Would Overlap Reserved Or Occupied Non-Self Cells

Same behavior as above.

This is structural rejection, not gameplay rejection.

### Release After Hole Became Full

Normal non-full release snap must not run.

The drag-session owner already knows whether the session ended because the hole became full.

Recommended rule:

* if `EndedBecauseHoleBecameFull` is true, bypass normal release snap/commit entirely

### Duplicate Release Call

The drag-session owner should make duplicate release difficult by clearing its session after the first release.

If a release-commit service is called directly with stale context anyway, it should fail safely rather than reapply.

### Failed Occupancy Commit And Rollback

Rollback must:

* release any partially occupied new footprint cells
* re-occupy the old footprint
* keep `CurrentCoordinate` unchanged
* return failure

### Old Footprint Release Succeeds But New Footprint Occupation Fails

This is the exact rollback case the design exists to protect.

The old committed coordinate must remain the authoritative runtime state until full commit success.

### New Footprint Same As Old Footprint

This should be treated as a valid no-op commit:

* no occupancy mutation
* no `CurrentCoordinate` change
* returned world position aligns to the snapped world position for that same origin

---

## Interaction With Drag Session Owner

The drag-session owner should remain responsible for session lifecycle only.

Recommended handoff:

```text
drag-session owner Release()
    ->
if session ended because full:
        return cleanup-only full result
    ->
if active non-full hole:
        call release snap / occupancy-commit owner
        receive committed result
        clear drag-session state
```

This keeps responsibilities separated:

* drag-session owner handles drag state
* release owner handles board commit

---

## Proposed First Implementation Slice

The next smallest safe implementation step after this design approval is:

* add a prototype-owned release commit service
* feed it:
  * runtime model
  * hole
  * last authoritative world position
  * world layout
* use existing framework `GridSnapSystem`
* use existing framework `CellOccupancySystem`
* support:
  * valid snap commit
  * same-origin no-op commit
  * invalid snap fallback
  * rollback on failed occupancy transfer
* update `DropTheManDragSessionOwner.Release()` to call it only for non-full active holes

Do not add in that same step:

* closing flow
* completed removal
* timer routing
* win/loss routing
* scene/prefab integration
* animation

---

## Still-Open Questions

### 1. Release Owner Placement

The design assumes a separate release service is cleaner than expanding `DropTheManDragSessionOwner`.

This is the recommended approach, but the final component split is still a small implementation choice.

### 2. Framework Snap Border Ties

Current nearest-cell conversion relies on framework rounding.

This is acceptable for MVP, but if release feel around exact half-cell positions becomes questionable, that should be reviewed as a framework snap concern later.

### 3. Should Commit Failure Return `Success = false` Or `Success = true, WasCommitted = false`

The design prefers explicit failure when commit could not be completed, because occupancy rollback is not just a cosmetic branch.

This should be locked when result types are introduced.

---

## Final Summary

```text
Release-time snap for non-full holes should use the framework GridSnapSystem as the
alignment and structural validation authority.
If snap is invalid, keep the old committed origin and old occupancy.
If snap is valid and unchanged, treat it as a no-op committed release.
If snap is valid and moved, transfer occupancy from old footprint to new footprint,
rollback on failure, and update HoleRuntimeState.CurrentCoordinate only after full success.
Full holes bypass this normal release path entirely.
Snap remains alignment and commit only, never collection or gameplay-rule ownership.
```
