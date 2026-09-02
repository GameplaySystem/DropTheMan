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

The reservation and threshold timing described here are implemented. The current implementation
slice advances the collection presentation from the synchronous hide placeholder to a concrete
prototype-owned Animator and DOTween sequence.

This design does not add:

* event bus behavior
* framework redesign
* `TimerMode.CountUp`
* required-hole schema

---

## Historical MVP Rule

The original synchronous MVP proof used:

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

The reservation/threshold implementation supersedes that immediate-fill rule. Current gameplay
reserves capacity first and fills it only on collection presentation completion.

The first playable scene originally hid the stickman immediately when collection started. The
concrete cat presentation now delays capacity fill until its movement and disappearance sequence
reports completion.

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

## Historical Foundation Slice

The first approved implementation established:

* add `Reserved` to `StickmanLifecycleState`
* add reservation methods/results to stickman runtime/index logic
* add reserved capacity slot accounting for holes or collection sessions
* change swept same-color handling from immediate fill to reservation
* add threshold evaluation using `GridWorldLayout` and live authoritative hole position
* placeholder hide firing only when threshold is reached
* synchronous placeholder completion
* capacity fill and full-hole completion after placeholder completion

That foundation intentionally deferred:

* real falling animation
* DOTween or Animator behavior
* event bus behavior
* framework APIs
* schema changes

The concrete cat presentation below is the approved follow-up that replaces the synchronous
placeholder when valid presentation assets are configured.

---

## Concrete Cat Presentation Contract

When a reserved cat reaches the visual collection threshold:

1. Runtime changes the cat to `Collecting` and requests presentation for its already-assigned hole.
2. The hole presentation assigns the closest unclaimed authored collection socket. Socket choice
   is deterministic by prefab array order when distances are equal.
3. When its view is spawned, each cat receives one persistent falling-animation assignment from a
   presentation-owned shared shuffle bag. This randomizes assignment while distributing every
   valid configured variant before any variant repeats across cats in the level. On collection,
   the cat disables collection colliders and starts its preassigned non-looping variant.
   The September 2 artist delivery in `Cat.fbx` is the canonical rendered model, owns the Generic
   Avatar, and supplies `Idle_1` and `Jump_1` through `Jump_6`. Axis conversion remains enabled.
   Its bone rest transforms and mesh binding differ from the obsolete `Cat_3D.fbx`; matching bone
   names alone does not make those two rigs interchangeable. Both imported Idle takes loop; Jump takes do
   not. `Idle_1` is the controller default. The cat-model root owns the Animator. Idle
   and collection remain controller-driven; collection immediately plays the preassigned `Fall_1`
   through `Fall_6` state from normalized time zero, whose motion references the corresponding
   artist-authored Jump take. Variant count follows the configured clip array rather than a
   hard-coded three-clip limit. The setup pipeline must not generate rebased animation copies or
   move the Animator above the model root.
4. The cat rises while moving from its current world position toward a point above the socket.
5. The cat then descends from above the socket to a configured depth below it while shrinking.
6. Both motion phases evaluate the socket transform every tween update. A hole that continues
   moving therefore carries the target path with it rather than leaving the cat aimed at a stale
   world position.
7. The cat hides or destroys its spawned view and invokes one completion callback.
8. Runtime converts the reserved capacity slot into fill. If the hole is now full, hole completion
   presentation begins.

Socket claims last for the lifetime of the hole presentation. They are presentation bookkeeping,
not capacity authority. The runtime reservation remains the source of truth.

If the assigned hole view, socket, Animator, falling clip, or tween configuration is missing or
invalid, the view logs the presentation problem, applies the immediate hide fallback, and invokes
the same completion callback. Presentation failure must not strand a cat in `Collecting`.

Collection completion resolves the assigned hole from `ReservedHoleId`; it must not require the
hole to remain in the active drag session. This allows the player to release the hole while a cat
is still moving and allows the live hole transform to continue being the presentation target.

---

## Animation Asset Maintenance

The September 2 cleanup removes the unreferenced generated `CatCollection_Fall_*.anim`
experiments. The controller continues referencing clips imported directly from the artist's FBXs.
The setup command preserves valid explicit clip ranges and names when setting looping; obsolete
take mappings are removed and missing delivered takes use their default ranges. It updates existing
controller states and reuses the canonical prefab model child rather than discarding state-speed
or transform tuning on every setup run. Runtime collection timing is unchanged by this cleanup.

The August 30 matched-pose contact sheet showed similar opening motion across the old jump
exports, with the strongest differences late in the clips. Distinct clip references or mesh hashes
alone are not acceptance evidence for visible variety during the actual collection window.
Validate revised clips at matched early times and during moving-hole collection before release.

The artist supplied the revised combined `Cat.fbx` locally on September 2, not through a commit.
Its take names changed completely: stale `Armature|Armature|...` importer entries produced no
usable clips. The integration replaces obsolete take mappings with the actual delivered takes,
while preserving valid authored clip names/ranges. The old separate animation FBXs are no longer
the runtime sources. The canonical mesh also comes from the new delivery because its rest/bind
pose differs from the old mesh. No bone-rotation workaround or clip curve rewriting is needed.
The five obsolete separate model/animation FBXs and temporary diagnostic scripts were removed.

September 2 verification passed for all six active controller motions and early mesh samples,
rendered pose comparisons, 36 shuffle-bag assignments and moving-target tweens, and 20 actual
level collections through hole completion and win. Restart mid-fall and next-level reload passed.
The cat tint now targets only its configured body material slot, leaving textured white/details
materials untouched. See `DropTheManCatAnimationIntegrationHandoff.md` for the maintained setup
commands, verification scope, and remaining visual checks.

---

## Final Summary

The original MVP proof compressed reservation, visual trigger, and capacity fill into one instant.

The current model separates them:

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
`DropTheManHolePresentationDesign.md`. Cat collection presentation is asynchronous when its
configured Animator, falling clips, and tween presentation are valid. A full hole enters `Closing` only after the
last required cat presentation callback converts its reservation into fill, and finalizes
`Completed` only from the hole presentation callback. Missing or invalid cat or hole presentation
uses the corresponding callback immediately.
