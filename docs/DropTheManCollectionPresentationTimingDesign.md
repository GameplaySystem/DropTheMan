# Drop The Man Collection Presentation Timing Design

## Purpose

This document reconciles the current immediate MVP collection behavior with the desired final collection feel for `DropTheMan`.

Desired player-facing feel:

```text
The hole is dragged underneath the stickman.
When the hole is visually under the stickman, the stickman starts collection.
The stickman falls into the hole.
The stickman keeps falling until it disappears.
Then hole capacity and full-hole completion continue.
```

This is design only.

It does not implement:

* animation
* DOTween or Animator behavior
* prefab spawning
* event bus behavior
* framework redesign
* `TimerMode.CountUp`
* required-hole schema

---

## Current Documented Rule

The current approved MVP rules say:

```text
same-color target cell entered or overlapped
    ->
collection accepted immediately
    ->
stickman enters Collecting immediately
    ->
hole fill count increases immediately
    ->
if capacity is reached, hole becomes Full immediately
```

That rule is documented in:

* `DropTheManMovementAndCollectionRules.md`
* `DropTheManMVPRules.md`
* `CRITICAL_RULE_CLARIFICATIONS.md`

The current runtime follows that rule.

The first playable scene placeholder then hides the stickman immediately when `OnCollectionStarted(...)` is called.

That proves the gameplay chain, but it does not match the desired final visual target.

---

## Gap

The current model merges four different concepts into one moment:

```text
drag-time enterability
collection acceptance
visual collection start
capacity fill
```

For the intended visual, those should not all happen at first grid-cell overlap.

The important problem is not physics collision.

The problem is timing:

* same-color stickmen must become non-blocking early enough for the hole to pass under them
* visual collection should wait until the hole looks under the stickman
* capacity should not fill before the player sees the stickman begin falling into the hole
* full-hole completion should not bypass the falling/disappear presentation

---

## Definitions

### Runtime Reservation

A same-color stickman is reserved when the movement coordinator determines that the moving hole may collect it during drag.

Reservation means:

* the stickman is no longer available to other collection attempts
* the stickman no longer blocks the moving hole
* the hole reserves one capacity slot
* visual collection has not necessarily started
* hole fill count has not necessarily increased

### Visual Collection Trigger

The visual collection trigger is the moment the hole is close enough to the reserved stickman's anchor for the presentation to look correct.

This should be evaluated during drag from runtime-authoritative positions, not Unity physics callbacks.

### Collection Presentation Completion

Collection presentation completion is the moment the stickman falling/disappear sequence is done.

Only after this moment should the stickman be considered fully collected for visual sequencing.

---

## What Does "Underneath Stickman" Mean?

For the first playable prototype, define "underneath" in board-local runtime terms:

```text
reserved stickman anchor
    is within collection trigger radius
of the closest hole collection point
```

Recommended MVP anchors:

* stickman anchor = `StickmanRuntimeState.Coordinate` converted through `GridWorldLayout`
* hole collection points = live hole origin plus each footprint offset, converted through `GridWorldLayout`
* trigger threshold = serialized prototype value in cell units

For a single-cell hole, this means:

```text
distance(live hole origin, stickman coordinate anchor) <= threshold
```

This matches the current manually wired scene where:

```text
hole_red_01 coordinate (1, 2) -> world (1, 0.1, 2)
stickman_red_01 coordinate (3, 2) -> world (3, 1, 2)
```

Do not use Unity collider overlap as the authoritative trigger.

Colliders may still help presentation later, but runtime timing should remain deterministic.

### Future Refinement

If visual assets later need authored sockets, replace the default footprint-derived collection points with prototype-owned scene-authored collection sockets.

That should remain a `DropAwayPrototype` concern, not a framework feature.

---

## Recommended Timing Model

Use this sequence:

```text
Available
    ->
Reserved
    ->
Collecting
    ->
Collected
```

And this hole capacity sequence:

```text
capacity slot reserved
    ->
visual collection starts
    ->
visual collection completes
    ->
hole fill count increments
    ->
if fill count reaches capacity, hole becomes Full
    ->
full-hole completion begins
```

### 1. Drag-Time Reservation

When swept movement reaches a same-color available stickman:

* reserve the stickman
* reserve one hole capacity slot
* remove or exclude the stickman from active blocking/collection lookup
* return a reservation fact to the runtime integration layer
* do not yet increment `HoleRuntimeState.FillCount`
* do not yet transition the hole to `Full`
* do not yet hide the stickman

This preserves:

* same-color enterability
* wrong-color blocking
* deterministic swept traversal
* no duplicate collection

### 2. Visual Trigger Threshold

While dragging continues, evaluate reserved stickmen against the live authoritative hole position.

When a reserved stickman reaches the trigger threshold:

```text
Reserved -> Collecting
```

Then notify the stickman view to begin collection presentation.

For the first playable placeholder, this may hide the stickman immediately, but only after the threshold is reached.

### 3. Presentation Completion

When presentation finishes:

```text
Collecting -> Collected
```

Then increment hole fill count.

For the first playable placeholder, presentation completion may be synchronous:

```text
visual trigger reached
    ->
hide stickman
    ->
mark collection presentation complete immediately
    ->
increment fill
```

This gives the improved visual trigger without requiring animation infrastructure yet.

### 4. Full-Hole Completion

If the hole reaches capacity after presentation completion:

```text
hole becomes Full
    ->
drag stops
    ->
full-hole completion flow starts
```

This prevents hole completion from running before the collected stickman has visually started or completed its fall.

---

## Capacity Semantics

Separate these values:

```text
FillCount
ReservedCollectionCount
Capacity
```

Recommended remaining capacity check:

```text
remaining collection slots = Capacity - FillCount - ReservedCollectionCount
```

Reason:

* reservation must prevent over-collecting
* fill must wait until presentation completion
* a capacity-1 hole should not reserve two stickmen while the first is waiting for visual trigger

This likely requires `HoleRuntimeState` to expose a prototype-owned pending/reserved count or equivalent collection-session tracking.

Do not put this in the framework.

---

## Lifecycle State Recommendation

Add an intermediate stickman lifecycle state:

```text
Reserved
```

Recommended lifecycle:

```text
Available
    ->
Reserved
    ->
Collecting
    ->
Collected
```

Why `Reserved` is needed:

* `Collecting` currently means visual collection has started
* the new model needs a non-blocking, non-duplicate state before visual collection starts
* using `Collecting` for both reservation and falling presentation would keep the ambiguity that caused the current visual mismatch

No new hole lifecycle state is required for the first slice.

Hole fullness can remain derived from fill count reaching capacity after collection completion.

---

## Wrong-Color Interaction

Wrong-color behavior does not change.

Wrong-color available stickmen:

* are not reservable
* are not enterable
* block movement through the existing swept validation path

Only same-color reserved stickmen become non-blocking.

Do not implement sliding or delayed blocking for wrong-color targets in this slice.

---

## Multi-Cell Footprints

Reservation remains footprint-aware.

If any hole footprint cell reaches a same-color available stickman, that stickman may be reserved if the hole has remaining unreserved capacity.

Visual trigger should use the closest hole collection point:

```text
hole origin + each footprint offset
```

compared against:

```text
reserved stickman coordinate anchor
```

This keeps multi-cell holes deterministic without requiring authored sockets for the first playable slice.

---

## Swept Movement

The existing swept traversal should still own detection of:

* wrong-color blockers
* same-color reservation candidates
* deterministic candidate ordering
* capacity-slot reservation limits

The visual trigger threshold should not replace swept traversal.

Instead:

```text
swept traversal reserves valid same-color targets
    ->
threshold evaluation starts presentation when the hole is visually close enough
```

This avoids tunneling while preventing early visual disappearance.

---

## Full-Hole Completion And Outcome Routing

Full-hole completion should not consume raw reservation facts.

It should wait until:

```text
reserved collection presentation completes
    ->
hole fill count reaches capacity
    ->
hole becomes Full
```

Only then should the existing full-hole completion flow produce:

```text
hole completed
```

Outcome routing remains unchanged:

```text
all required holes Completed -> request Won
```

Collected or collecting stickmen alone still must not request victory.

---

## First Playable Placeholder Behavior

Before real animation exists, use this placeholder:

```text
reservation happens on swept same-color entry/overlap
    ->
stickman remains visible
    ->
when threshold is reached, hide stickman immediately
    ->
mark presentation complete synchronously
    ->
increment fill
    ->
if full, run existing full-hole completion flow
```

This is intentionally still ugly, but it fixes the current bad feel:

```text
stickman disappears only when the hole is visually under it
```

without adding animation, DOTween, Animator behavior, or a visual feedback framework.

---

## Ownership

### Prototype Runtime Owns

* reservation state
* reserved capacity slot accounting
* threshold evaluation using runtime positions
* capacity fill timing
* full-hole completion trigger timing

### Prototype Scene/View Layer Owns

* temporary hide placeholder
* later falling animation
* later disappear presentation
* reporting presentation completion back to prototype runtime when asynchronous animation exists

### Framework Does Not Own

* stickman reservation meaning
* hole capacity reservation
* collection trigger thresholds
* falling animation timing
* Drop The Man-specific lifecycle states

This can be solved prototype-side.

No framework change is required.

---

## Alternatives Considered

### Alternative A: Keep Immediate Fill, Delay Only Visual Hide

Rejected.

Why:

* hole can become `Full` and complete before the visual collection looks correct
* full-hole completion can hide or disable the hole while the stickman should still be falling
* this preserves the core mismatch

### Alternative B: Trigger Collection Only At Threshold, No Reservation

Rejected.

Why:

* same-color stickmen may remain active blockers too long
* fast movement could tunnel unless the threshold check duplicates swept traversal
* duplicate collection prevention becomes unclear

### Alternative C: Reserve Immediately, Fill On Visual Trigger Start

Acceptable as a temporary compromise, but weaker than the recommendation.

Why:

* better than immediate fill
* still allows hole full/completion to start before the falling/disappear presentation finishes

Recommended only if the next implementation deliberately keeps presentation completion synchronous.

### Recommended Option

Reserve immediately, trigger visual collection at threshold, and fill capacity on presentation completion.

This is the smallest model that matches the final visual target without moving gameplay authority to physics or views.

---

## Required Follow-Up Documentation Changes

If this design is approved, update the older rule docs that currently say:

```text
hole fill count increases immediately
collectible enters Collecting immediately
capacity fills immediately
```

Those should be replaced with:

```text
reservation happens immediately
visual collection starts at threshold
capacity fills on collection presentation completion
```

Do not silently leave both models active in source-of-truth docs.

---

## Smallest Implementation Slice After Approval

Implement only:

* add `Reserved` to `StickmanLifecycleState`
* add reservation methods/results to stickman runtime/index logic
* add reserved capacity slot accounting for holes or collection sessions
* change swept same-color handling from immediate fill to reservation
* add threshold evaluation using `GridWorldLayout` and live authoritative hole position
* make current placeholder hide fire only when threshold is reached
* complete placeholder presentation synchronously
* fill capacity and trigger full-hole completion after placeholder completion

Do not add:

* real falling animation
* DOTween or Animator behavior
* scene object destruction polish
* event bus behavior
* framework APIs
* schema changes

---

## Final Summary

The current implementation is valid for the old MVP proof, but it compresses reservation, visual trigger, and capacity fill into one instant.

The recommended model is:

```text
swept drag detects same-color candidate
    ->
reserve stickman and capacity slot
    ->
stickman becomes non-blocking
    ->
hole moves underneath stickman
    ->
threshold starts visual collection
    ->
presentation completes
    ->
capacity fills
    ->
if full, full-hole completion begins
```

This preserves drag-time collection ownership while matching the intended visual sequence.

The concrete hole visual contract and direct presentation callback are defined in
`DropTheManHolePresentationDesign.md`. Stickman collection presentation remains synchronous for
now, but a full hole now enters `Closing` before its configured hole presentation and finalizes
`Completed` only from the presentation callback. Missing or invalid hole presentation uses the
same callback immediately.
