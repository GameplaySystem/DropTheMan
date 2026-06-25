# Drop The Man Movement Edge Case Checklist

## Purpose

This document turns movement and collection edge-case analysis into a structured implementation support checklist for the first playable `DropTheMan` slice.

It is not a replacement for:

* `docs/DropTheManMVPRules.md`
* `docs/DropTheManMovementAndCollectionRules.md`

Its job is narrower:

* isolate decisions that still block coordinator implementation
* convert the remaining edge cases into testable review scenarios
* keep architecture-boundary risks visible while implementation proceeds

Do not use this document to silently redefine gameplay rules.

---

## Implementation-Blocking Decisions

These items should be resolved before implementing the movement and collection coordinator because they directly affect deterministic gameplay truth.

### 1. Exact swept cell enumeration strategy for continuous freeform movement

Problem:

* holes move freely in continuous 2D space
* gameplay still needs deterministic board-cell validation
* fast drag must not tunnel through blockers or skip collectibles

Why it matters:

* different sweep-enumeration strategies may produce different crossed-cell sets
* if the crossed-cell set is unstable, movement, blocking, and collection will be inconsistent

Proposed rule:

* use continuous freeform drag as the player-facing movement model
* for gameplay truth, evaluate the swept hole footprint between the previous accepted position and the current candidate position
* include every board cell that the moving footprint crosses or overlaps along that movement segment

Implementation consequence:

* the coordinator needs a deterministic sweep helper rather than final-position-only sampling
* runtime tests must verify that fast drag cannot skip blockers or collectibles

Status:

* Resolved

### 2. Deterministic tie-breaker for equal-distance crossed cells

Problem:

* multiple cells may be reached at the same sweep distance
* wide footprints may overlap several new cells together

Why it matters:

* collection order matters when capacity fills mid-sweep
* if equal-distance handling is unstable, gameplay may vary by list order or floating-point noise

Proposed rule:

* order crossed cells first by distance along the movement segment from the previous accepted position
* if multiple cells are reached at the same sweep distance, use a stable coordinate tie-breaker
* if footprint overlap still produces equal-distance candidates, apply the same stable tie-breaker

Implementation consequence:

* the coordinator or sweep helper needs one documented coordinate ordering rule and must reuse it everywhere

Status:

* Resolved

### 3. First-contact rule when a wide footprint reaches multiple cells together

Problem:

* a wide footprint can first touch multiple cells in the same accepted sweep portion
* some of those cells may be same-color collectibles, while others may be wrong-color blockers

Why it matters:

* collection and blocking must not depend on arbitrary iteration order
* the entire footprint must behave consistently when contact happens across multiple cells at once

Proposed rule:

* validate the whole candidate footprint for blockers before treating that candidate movement as accepted
* if any overlapped cell is a wrong-color available collectible or a structural blocker, reject that candidate progression
* if the candidate progression is valid, accept same-color available collectibles in deterministic order

Implementation consequence:

* the coordinator should separate `blocking validation` from `accepted collection processing`
* collection must not be accepted from a footprint configuration that was never valid to occupy

Status:

* Resolved

### 4. Exact stop or clamp point when wrong-color blocking is encountered mid-sweep

Problem:

* a sweep may encounter a blocker between the previous accepted position and the candidate position
* the hole must not pass through the blocker

Why it matters:

* the stop point affects visual feel, accepted position, and later snap alignment
* if the stop point is vague, the hole may jitter or visually overlap blocked content

Proposed rule:

* movement stops at the last valid accepted position before the blocker
* the blocked candidate progression is rejected
* the final candidate position alone must not override that stop point

Implementation consequence:

* the coordinator must preserve the last accepted valid position and clamp to it on rejection
* visual smoothing must not move the authoritative position past the blocker

Status:

* Resolved

### 5. Exact behavior when capacity fills partway through a sweep

Problem:

* one accepted sweep may encounter more same-color collectibles than remaining capacity
* capacity can fill before all collectible candidates in that sweep are processed

Why it matters:

* gameplay must deterministically decide what is collected, what remains available, and where movement stops

Proposed rule:

* accept collectible candidates in deterministic order
* increase fill count immediately on each accepted collectible
* as soon as remaining capacity reaches zero:
  * mark the hole `Full`
  * stop accepting further collectibles
  * stop further traversal
  * begin full-hole completion flow
* any unprocessed collectibles remain `Available`

Implementation consequence:

* the coordinator must support mid-sweep interruption rather than only whole-sweep acceptance
* tests must verify that leftover collectibles remain interactable for other valid holes

Status:

* Resolved

### 6. Timer-vs-final-completion precedence on same-frame or near-same-frame races

Problem:

* timer expiry and final hole completion may both occur very close together
* without a precedence rule, the game could request both `Won` and `Lost`

Why it matters:

* final outcome must be deterministic
* race conditions here are likely in real gameplay near the timer end

Proposed rule:

* `TimerSystem` does not decide outcomes directly
* game-module rule logic owns final outcome requests
* if all required holes complete before a timer-driven loss is applied, request `Won`
* if timer expiry is applied before the final required completion sequence finishes, request `Lost`
* once a terminal state is requested, later competing callbacks must be ignored

Implementation consequence:

* the coordinator or surrounding prototype rule owner needs terminal-state guards
* tests must cover same-frame and near-same-frame ordering

Status:

* Resolved

---

## Movement And Collection Test Checklist

### Movement Geometry

* Test that a zero-distance drag update does not change movement, collection, or hole state.
* Test that tiny pointer jitter near a stable position does not trigger duplicate collection or invalid blocking.
* Test that drag beginning while the hole already overlaps collectible cells does not incorrectly auto-collect unless the rules explicitly allow that start state.
* Test that very fast drag across many cells does not skip wrong-color blockers.
* Test that very fast drag across many cells does not skip same-color collectibles that should be accepted.
* Test that shallow-angle freeform drag behaves consistently with the swept footprint rules.
* Test that a candidate position exactly on a cell border does not flicker between valid and invalid states.
* Test that a candidate position exactly on a cell corner does not produce inconsistent crossed-cell results.
* Test that floating-point precision near borders does not make movement or collection non-deterministic.
* Test that a sweep crossing the same cell more than once in one update does not double-collect or double-block.
* Test that rapid reversal of drag direction does not leave the hole beyond the last accepted valid position.
* Test that moving briefly out of board bounds and back in does not create invalid accepted positions.

### Sweep Ordering

* Test that equal-distance crossed cells are processed in a stable deterministic order.
* Test that multiple footprint cells entering new cells at once do not depend on arbitrary list order.
* Test that two same-color collectibles reached at the same sweep distance are collected in the documented tie-break order.
* Test that same-color and wrong-color cells reached at similar sweep distance do not produce inconsistent results across runs.
* Test that mirrored movement directions still produce deterministic ordering.

### Footprint Validation

* Test that a candidate anchor that looks valid is still rejected when another footprint cell overlaps a wrong-color blocker.
* Test that a multi-cell hole overlapping one same-color and one wrong-color available collectible rejects the movement step.
* Test that a multi-cell hole overlapping multiple same-color collectibles accepts them only if the footprint is otherwise valid.
* Test that `Collecting` collectibles inside the footprint do not block further movement.
* Test that `Collected/Removed` cells do not block further movement.
* Test that a footprint spanning active and inactive cells is rejected.
* Test that a footprint spanning valid and structurally blocked cells is rejected.
* Test that a footprint partially outside the board is rejected even if part of it remains inside.
* Test that movement into space freed by a collectible accepted earlier in the same sweep behaves consistently.

### Collection Acceptance

* Test that the same collectible cannot be accepted twice in one sweep.
* Test that a `Collecting` collectible cannot be accepted again on later frames.
* Test that a collectible stops blocking immediately when it enters `Collecting`.
* Test that multiple same-color collectibles in one valid sweep update can all be accepted if capacity remains.
* Test that same-color collectibles behind a wrong-color blocker are not collected if the blocker rejects the progression first.
* Test that collection acceptance does not depend on animation completion timing.
* Test that collection acceptance does not depend on physics callback order.

### Capacity

* Test that a hole with one remaining capacity accepts only one collectible when more same-color candidates are encountered in the same sweep.
* Test that leftover same-color collectibles remain `Available` when capacity fills mid-sweep.
* Test that a hole marked `Full` does not accept additional collectibles later in the same update.
* Test that a hole marked `Full` does not continue traversal after capacity is reached.
* Test that shape-based capacity matches the authored hole footprint size assumptions for the MVP shapes.
* Test that multiple holes of the same color can split total collectible demand without per-hole ownership issues.

### Hole Lifecycle

* Test that a hole becoming `Full` mid-drag immediately stops normal dragging.
* Test that a hole becoming `Full` on the same frame as release still follows the full-hole completion flow.
* Test that a `Closing` hole cannot be dragged again.
* Test that a `Completed/Removed` hole is no longer treated as an active board participant.
* Test that selecting another hole while one is `Closing` does not corrupt the first hole’s completion state.
* Test that re-clicking a `Full` or `Closing` hole does not restart collection or drag.
* Test that visual hole completion does not finish before logic has correctly marked the hole non-draggable.

### Snap

* Test that releasing a non-full hole only aligns it and does not trigger collection by itself.
* Test that releasing after a blocked sweep keeps the hole at the last accepted valid position.
* Test that releasing after drag-time collection does not re-collect already accepted collectibles.
* Test that releasing a hole already marked `Full` does not run normal snap-owned gameplay logic.
* Test that snap never moves the authoritative hole position through a blocker that drag-time rules rejected.
* Test that snap on a hole already aligned does not change gameplay state.

### Timer / Outcome

* Test that timer expiry on the same frame as final collectible acceptance does not request both `Won` and `Lost`.
* Test that timer expiry after final collectible acceptance but before final hole completion requests the documented outcome only once.
* Test that timer expiry on the same frame the final hole becomes `Completed` does not produce double terminal requests.
* Test that all collectibles being accepted without all required holes being completed does not request `Won` early.
* Test that once `Won` or `Lost` is requested, later callbacks do not request the opposite terminal state.
* Test that levels without a timer do not route through timer-loss logic accidentally.

### State Synchronization

* Test that a collectible entering `Collecting` is immediately non-blocking even while still visually present.
* Test that a hole marked `Full` is no longer considered `Active` by drag-time rule logic.
* Test that completion animation callbacks arriving late do not re-open completed gameplay state.
* Test that restart or reset while collectibles are `Collecting` restores deterministic board interaction state.
* Test that restart or reset while holes are `Closing` restores deterministic hole lifecycle state.

### Authoring / Validation

* Test that duplicate collectible coordinates are rejected or detected by the appropriate validation layer.
* Test that collectibles authored on blocked or inactive cells are handled according to the approved validation boundary.
* Test that holes authored partly outside the board are rejected by the appropriate validation layer.
* Test that structurally overlapping authored holes are rejected by the appropriate validation layer.
* Test that collectible colors with no matching total hole capacity are treated according to the current MVP validation decision.
* Test that levels with matching total capacity but impossible arrangement remain outside current build-safety validation unless explicitly approved later.

### Framework Boundary Risks

* Test that generic `DragMovementSystem` does not start making color-based gameplay decisions.
* Test that generic `GridSnapSystem` does not start accepting or rejecting collectibles as gameplay truth.
* Test that `CellOccupancySystem` does not start treating collectibles as permanent structural blockers.
* Test that physics callbacks are not required for correct collection or blocking truth.
* Test that animation completion is not required for correct collection truth.
* Test that runtime construction validation does not drift into gameplay solvability checks.

---

## Architecture Boundary Risks

These are review warnings, not implementation tasks.

* Drag system must not own color logic.
* Snap system must not own collection logic.
* Occupancy must not treat collectibles as permanent structural blockers.
* Physics callbacks must not become gameplay truth.
* Animation completion must not become collection truth.
* Runtime construction must not validate gameplay solvability.
