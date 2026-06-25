# Drop The Man Movement And Collection Rules

## Purpose

This document locks the exact movement, collection, capacity, closing, and win-timing rules for the first playable `DropTheMan` slice.

Use this document before implementing:

* movement
* drag-time enterability
* collection
* capacity filling
* hole closing
* win timing

This document is prototype-owned.

It does not move gameplay meaning into `PuzzleFramework`.

---

## Core Truth

`Drop The Man` uses:

```text
drag-time movement
    ->
drag-time enterability checks
    ->
drag-time collection
    ->
release-time snap for alignment only
```

It is not:

```text
drag visually
    ->
release
    ->
snap
    ->
collect
```

Snap does not own:

* collection timing
* wrong-color rejection
* capacity filling
* win triggering

---

## State Models

### Collectible States

Collectibles use this state model:

```text
Available
Collecting
Collected / Removed
```

Rules:

* `Available` collectibles can block or be collected depending on color.
* Same-color `Available` collectibles can be accepted for collection.
* Wrong-color `Available` collectibles block movement.
* `Collecting` collectibles do not block movement.
* `Collecting` collectibles cannot be collected again.
* `Collected / Removed` collectibles are no longer part of board interaction.

### Hole States

Holes use this state model:

```text
Active
Full
Closing
Completed / Removed
```

Rules:

* `Active` holes can be dragged and can collect while capacity remains.
* `Full` holes cannot collect more.
* `Closing` holes are in their cap-close or completion sequence.
* `Completed / Removed` holes are no longer active board participants.
* `Full` or `Closing` holes are not normal draggable collectors.

Do not mix collectible state with hole state.

---

## Movement Authority

`Drop The Man` holes are freely dragged in continuous 2D space.

They are not restricted to:

* horizontal-only movement
* vertical-only movement
* diagonal-only movement
* cell-by-cell visible movement

The grid is used for validation and collection truth, not for visible step-based movement.

During drag, every requested movement must answer:

```text
Can this hole enter the next board position?
```

That answer is produced by `DropTheMan` game-module rule logic using framework board data.

Framework provides:

* grid structure
* occupancy truth
* shape footprint data
* drag position input
* snap alignment support

`DropTheMan` game logic decides:

* color enterability
* collection acceptance
* capacity filling
* hole completion
* level completion meaning

---

## Cell Enterability

For each candidate position or swept movement segment:

### Empty valid cell

Allowed.

### Same-color available collectible

Allowed if the hole has remaining capacity.

Also triggers collection acceptance.

### Same-color collecting collectible

Allowed.

Does not trigger collection again.

### Same-color collected or removed collectible

Ignored.

### Wrong-color available collectible

Blocked.

The hole may not pass through it.

### Blocked, inactive, or out-of-board cell

Blocked.

---

## Capacity Rules

Capacity is required for the real `DropTheMan` rule model.

Capacity is based on hole shape or size.

Examples:

```text
1-cell hole -> capacity 1
2-cell hole -> capacity 2
3-cell hole -> capacity 3
4-cell square hole -> capacity 4
plus hole -> capacity 5
```

When a same-color collectible is accepted:

```text
hole fill count increases immediately
collectible enters Collecting immediately
```

The fill count does not wait for animation completion.

Animation completion may finalize visual removal, but gameplay capacity changes at acceptance time.

---

## Immediate Non-Blocking Collection Rule

When a collectible is accepted for collection:

```text
Available
    ->
Collecting
```

it immediately stops blocking movement and must not be collectible again.

Correct rule:

```text
collection accepted
    ->
collectible enters Collecting immediately
    ->
cell no longer blocks movement
    ->
animation plays
    ->
animation completion finalizes removal
```

Animation is visual only.

Gameplay truth must not depend on animation timing.

---

## Over-Capacity Handling

If one swept movement segment or footprint overlap finds more same-color available collectibles than the hole has remaining capacity, process candidates in deterministic order.

Rule:

```text
Collect candidates one by one until remaining capacity reaches zero.
As soon as capacity reaches zero:
    - mark the hole Full
    - stop accepting further collectibles
    - stop further movement traversal
    - begin completion or closing flow
```

Any unprocessed collectibles remain `Available`.

They are not collected.

They are not counted.

They are not silently removed.

Candidate ordering must be deterministic:

1. order by distance along the movement segment from the previous accepted position
2. if multiple cells are reached at the same sweep distance, use a stable coordinate tie-breaker
3. if the footprint overlaps multiple same-distance cells, use the same stable tie-breaker

The implementation must not depend on:

* physics callback order
* collection animation timing
* frame rate

---

## Multi-Cell Footprint Rules

Movement validity is footprint-based.

If a hole occupies multiple cells, every footprint cell matters.

For every attempted accepted movement segment:

```text
candidate continuous position
    ->
candidate footprint
    ->
validate every footprint-overlapped cell
    ->
collect same-color available collectibles in footprint
    ->
reject if any footprint-overlapped cell is invalid or blocked
```

Bad:

```text
Only anchor cell decides movement.
```

Correct:

```text
Entire footprint decides movement.
```

### Footprint Collection

When a multi-cell hole moves, same-color collectibles overlapped by any part of the footprint can be accepted for collection.

Rules:

* same-color available collectibles can be collected if capacity remains
* wrong-color available collectibles block the entire movement step
* collecting or removed collectibles do not block
* collection order must be deterministic
* capacity can stop further traversal

---

## Fast Drag And Swept Traversal

Holes are dragged freely in continuous 2D space.

The game must not treat movement as only horizontal, vertical, diagonal, or cell-by-cell.

During drag, the system receives a continuous candidate position.

Do not evaluate only the final desired pointer position.

Required model:

```text
previous accepted position
    ->
continuous candidate position
    ->
deterministic swept footprint or board-cell overlap evaluation
    ->
accepted movement applied or clamped
```

This prevents:

* tunneling through wrong-color blockers
* skipping same-color collectibles
* frame-rate-dependent behavior

The sweep should identify every board cell that the moving hole footprint crosses or overlaps along the movement segment.

Those crossed or overlapped cells are then evaluated deterministically against:

* board bounds
* active or blocked cells
* collectible state
* collectible color
* hole capacity
* hole state

### Sweep Processing

For each accepted drag update:

1. Resolve the previous accepted hole position.
2. Resolve the current continuous candidate hole position.
3. Resolve the swept movement segment between them.
4. Resolve the candidate footprint along that sweep.
5. Check structural validity:
   * inside board
   * active cell
   * not blocked structurally
6. Check gameplay occupancy:
   * wrong-color available collectible blocks
   * same-color available collectible can collect if capacity remains
   * collecting or removed collectible is ignored
7. Apply accepted collections immediately.
8. If capacity becomes full, stop traversal and begin hole completion flow.
9. If the sweep encounters a blocker, stop at the last valid accepted position before the blocker.

---

## Any-Angle Drag Handling

Player drag may move at any angle.

Horizontal, vertical, and diagonal movement are not separate gameplay modes.

They are all continuous movement directions.

The implementation must evaluate the swept footprint between the previous accepted position and the current candidate position.

If crossed cells need ordering, ordering must be deterministic.

Ordering is an internal processing detail, not the visible movement rule.

Rules:

* the hole does not move one grid step at a time as a player-facing rule
* the hole moves continuously
* gameplay validity is still evaluated through board-cell overlap and swept footprint checks
* no physics-dependent any-angle resolution
* no frame-rate-dependent any-angle resolution

---

## Physics Rule

Physics may be used for:

* feel
* visuals
* helper detection

Physics must not be the authoritative gameplay rule source.

Allowed:

```text
colliders for visual feel
colliders for helper detection
rigidbody movement for smoothing if final state is still board-authoritative
```

Not allowed:

```text
OnTriggerEnter alone decides collection
Rigidbody collision alone decides movement validity
physics callback order decides collection order
physics alone decides blocking
```

Authoritative gameplay must come from deterministic swept footprint and board-cell evaluation.

---

## Animation Rules

When a collectible is accepted:

1. gameplay state changes immediately to `Collecting`
2. hole fill count updates immediately
3. animation starts
4. animation completes
5. collectible finalizes as `Collected / Removed`

Animation must not decide:

* whether collection is valid
* whether movement is blocked
* whether capacity is filled

DOTween and Animator are presentation tools, not gameplay authority.

---

## Full Hole Completion Sequence

When a hole reaches capacity:

```text
hole becomes Full
    ->
hole stops collecting
    ->
hole stops normal dragging
    ->
hole aligns or settles if needed
    ->
cap mesh closes using blend shape
    ->
closing sequence completes
    ->
hole becomes Completed / Removed
```

The cap mesh may be implemented with blend shapes on a `SkinnedMeshRenderer`.

That is presentation implementation.

The gameplay rule is:

```text
capacity full
    ->
completion sequence starts
    ->
hole completes after that sequence
```

---

## Win Timing

Win does not happen at raw overlap.

Win does not happen merely because the final collectible starts animating.

Final win gate for MVP:

```text
all required holes completed
```

Recommended flow:

```text
final collectible accepted
    ->
final hole reaches capacity
    ->
final hole closing sequence starts
    ->
final hole becomes Completed / Removed
    ->
game-module rule logic confirms all required holes are completed
    ->
request Won
```

---

## Timer Interaction

Timer lose condition remains separate.

If the timer expires before win is requested:

```text
TimerExpired
    ->
game-rule owner may request Lost
```

If all required hole completion sequences finish first:

```text
all required holes completed
    ->
request Won
```

`TimerSystem` must not decide win or loss directly.

Game-module rule logic owns the final outcome request.

---

## Snap Rules

On release, snap may:

* align the hole to its final accepted grid position
* visually settle the hole
* rollback or clamp to the last accepted position if needed

Snap must not:

* trigger collection
* decide color blocking
* decide capacity
* decide win
* decide hole completion

---

## Framework vs Game Module Boundary

Framework may own:

* Grid
* Cell Occupancy
* Shape footprints
* Drag movement infrastructure
* Snap alignment infrastructure
* Runtime Flow infrastructure
* Event infrastructure if needed

`DropTheMan` owns:

* hole color
* collectible color
* same-color enterability
* wrong-color blocking
* collection acceptance
* capacity filling
* hole closing and completion
* win rule

Framework must not know:

* `Hole`
* `Stickman`
* `Drop The Man`-specific rules

---

## Implementation Warning

Do not implement `DropTheMan` collection inside:

* `GridSystem`
* `CellOccupancySystem`
* `GridSnapSystem`
* generic `DragMovementSystem`
* generic `RuntimeConstructionSystem`

Collection belongs to `DropTheMan` game-module rule logic.

Framework systems provide generic mechanics.

Game-module rules provide gameplay meaning.

---

## Final Rule Summary

```text
Holes move freely during drag.
The grid does not force step-by-step movement.
The board is used to validate what the moving footprint overlaps or crosses.
Same-color collectibles collect during drag.
Wrong-color collectibles block during drag.
Accepted collectibles stop blocking immediately.
Capacity fills immediately.
Full holes close and complete.
Snap aligns on release only.
Win happens after all required holes complete their closing or completion sequence.
```
