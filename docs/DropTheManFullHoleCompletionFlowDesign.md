# Drop The Man Full-Hole Completion Flow Design

## Purpose

This document defines the smallest safe prototype-owned completion flow for a hole that becomes `Full` during drag in `DropTheMan`.

Its job is narrow:

* accept the fact that a hole became `Full`
* prevent normal drag and non-full release flow from continuing
* clean up stale committed occupancy
* transition the hole through `Full -> Closing -> Completed`
* expose a direct completion result for later outcome routing

It does not implement:

* win or loss routing
* timer arbitration
* presentation animation systems
* broad event-system generalization
* framework redesign

Read alongside:

* `docs/DropTheManMovementAndCollectionRules.md`
* `docs/DropTheManMovementCoordinatorDesign.md`
* `docs/DropTheManDragSessionDesign.md`
* `docs/DropTheManReleaseSnapAndOccupancyCommitDesign.md`

---

## Problem

The current runtime flow already supports:

* drag-time collection reservation and visual trigger evaluation
* transition to `Full` after triggered collection presentation completes and capacity fills
* immediate stop of normal dragging
* non-full release snap and occupancy commit

The missing slice is what happens next for a hole that becomes `Full`.

That gap is not just presentation.

Without a defined completion owner:

* the hole can remain stuck in `Full`
* full holes may be cleaned up too late if completion waits for pointer release
* old committed occupancy can remain behind even though the hole is no longer a normal board participant
* later win routing will not have a clean `hole completed` fact to consume

---

## Assumptions

This design assumes the currently approved runtime model:

* `DropTheManMovementCoordinator` already transitions a hole to `Full`
* `DropTheManDragSessionOwner.UpdateDrag(...)` already receives `HoleBecameFull`
* structural occupancy remains at the old committed footprint during drag
* `DropTheManReleaseCommitService` is only the normal non-full release path
* no approved presentation callback owner exists yet
* no approved reusable event architecture is required for this slice

This design also assumes that full-hole completion must stay separate from the final win predicate.

The completion flow should expose:

```text
this hole completed
```

but should not decide:

```text
the level is won
```

`DropTheManFullHoleCompletionResult` is an input fact for later outcome routing.

It is not a terminal outcome by itself.

---

## Risks

### 1. Waiting until `Release()` to start completion

Risk:

* gameplay truth becomes dependent on whether the player has physically released input yet
* a hole that already became `Full` can remain in limbo longer than the rules intend

Conclusion:

* rejected

### 2. Leaving old committed occupancy in place until presentation finishes

Risk:

* the board keeps stale structural blockers from the hole's pre-drag committed footprint
* the runtime no longer matches the rule that full or completed holes are not normal board participants

Conclusion:

* rejected

### 3. Putting completion inside the movement coordinator

Risk:

* drag-time movement would start owning occupancy cleanup and future outcome handoff
* the mutating drag coordinator would become a broader orchestration owner

Conclusion:

* rejected

### 4. Introducing a prototype event bus or broad callback framework now

Risk:

* only one immediate consumer is known
* the added indirection would be premature abstraction
* it would create architecture that exists only to be replaced later

Conclusion:

* rejected

---

## Simpler Alternatives Considered

### Alternative A

```text
Mark the hole Completed immediately inside the movement coordinator.
```

Why it looks simple:

* one place already sees the hole become `Full`

Why it is weaker:

* movement coordination should not own stale-occupancy cleanup
* movement coordination should not own future completion notification shape
* it makes the drag coordinator harder to maintain and test

### Alternative B

```text
Do nothing until pointer release, then let Release() handle full holes.
```

Why it looks simple:

* reuse the existing release path entry point

Why it is weaker:

* full-hole completion becomes input-timing dependent
* a player holding input would block completion start
* the rules already state that full-hole stop is authoritative during drag

### Recommended Simpler-Enough Option

```text
Use one prototype-owned full-hole completion service.
Call it directly when the drag-session owner receives a full-ending drag result.
Return an explicit completion result instead of introducing events.
```

This is the smallest option that still keeps ownership boundaries clear.

---

## Recommended Approach

Add one prototype-owned service:

```text
DropTheManFullHoleCompletionService
```

Recommended call direction:

```text
DropTheManMovementCoordinator
    ->
hole becomes Full
    ->
DropTheManDragSessionOwner.UpdateDrag(...)
    ->
DropTheManFullHoleCompletionService
    ->
explicit completion result for later outcome routing
```

The important timing rule is:

```text
full-hole completion begins on the drag update that made the hole Full
```

not:

```text
full-hole completion begins later on input release
```

Reason:

* the drag update is where gameplay truth already becomes authoritative
* `Release()` should be cleanup-only after a full-ended session

---

## Ownership

### `DropTheManFullHoleCompletionService` owns

* accepting a `Full` hole into completion flow
* releasing the hole's old committed structural occupancy
* transitioning `Full -> Closing`
* transitioning `Closing -> Completed` for MVP
* preventing duplicate completion notification
* returning a direct completion result for later outcome routing

### `DropTheManDragSessionOwner` owns

* active drag-session lifecycle
* calling the movement coordinator once per drag update
* stopping drag immediately when a hole becomes `Full`
* handing the full-ended result to the completion service
* treating later `Release()` as cleanup-only for that session

### This flow does not own

* collection rules
* non-full release snap and commit
* win or loss requests
* timer expiry interpretation
* animation playback
* visual destroy or hide timing

---

## Immediate State Changes vs Deferred Presentation

### Immediate gameplay changes

When the hole becomes `Full`, these gameplay changes should happen immediately:

* the hole stops normal dragging
* the hole stops normal non-full release participation
* stale committed occupancy is released
* the hole enters completion flow
* the runtime can expose a `hole completed` fact once MVP completion finalizes

### Deferred presentation concerns

These should remain deferred until later presentation work exists:

* settling the visual hole to a preferred close position
* cap-close animation timing
* visual disappearance timing
* visual cleanup callbacks

Important policy:

* gameplay completion must not wait on presentation in the MVP foundation

Reason:

* there is no approved presentation completion owner yet
* gameplay should not remain blocked by absent animation infrastructure

---

## State Sequencing

The approved lifecycle remains:

```text
Active
    ->
Full
    ->
Closing
    ->
Completed
```

Recommended MVP sequencing:

1. Movement coordinator reserves a valid target and later triggers it at the configured threshold.
2. The collection presentation completes and its reserved capacity slot becomes filled.
3. If fill count reaches capacity, the drag-session owner makes the hole `Full` and stops normal dragging immediately.
4. Drag-session owner calls the full-hole completion service immediately.
5. Completion service releases the old committed occupancy footprint.
6. Completion service calls `BeginClosing()`.
7. MVP completion service immediately calls `MarkCompleted()`.
8. Completion result reports that the hole completed now.

This keeps the explicit `Closing` state in the sequence without requiring an asynchronous callback path yet.

### Why `CurrentCoordinate` should not move here

Full holes are leaving active board participation.

They are not establishing a new committed snapped placement.

Therefore:

* do not route full holes through the normal non-full release commit path
* do not invent a new committed board origin for a hole that is about to complete and leave play
* keep `CurrentCoordinate` as the last committed origin history value

---

## Occupancy Cleanup Policy

### Problem

During drag:

* the hole's structural occupancy remains on its old committed footprint

If the hole becomes full and bypasses normal non-full release commit:

* some owner must release that old committed occupancy

### Chosen policy

The full-hole completion service owns old-footprint occupancy release.

Recommended sequence:

1. resolve the hole's current committed footprint from `HoleRuntimeState.CurrentCoordinate`
2. release that footprint from framework `CellOccupancySystem`
3. only after successful occupancy cleanup, continue into `Closing` and `Completed`

### Why release before advancing the lifecycle

If occupancy release fails, the safest MVP behavior is:

* leave the hole in `Full`
* report explicit failure
* do not claim the hole is closing or completed

This avoids a state where:

* the hole says `Closing` or `Completed`
* but stale occupancy still blocks the board

### Failure policy

Old committed occupancy release failure is not a normal gameplay branch.

It indicates runtime integrity drift.

For MVP:

* attempt footprint release deterministically
* if any release fails, restore any partially released coordinates
* return failure
* do not move the hole past `Full`

---

## Duplicate-Completion Prevention

Repeated completion calls must not produce repeated notifications or repeated occupancy mutation.

Recommended policy by lifecycle state:

### `Active`

Reject.

Reason:

* the hole is not ready for completion flow

### `Full`

Accept exactly once.

Reason:

* this is the legitimate entry state

### `Closing`

Do not restart.

Recommended result meaning:

* already in progress
* no new completion notification

### `Completed`

Do not restart.

Recommended result meaning:

* already completed
* no new completion notification

### API-shape recommendation

Prefer an explicit completion status in the result rather than multiple loosely related booleans.

Example conceptual statuses:

* `CompletedNow`
* `AlreadyClosing`
* `AlreadyCompleted`
* `Rejected`

This is clearer than scattering duplicate-prevention meaning across several flags.

---

## Drag-Session Handoff

### Chosen handoff point

The handoff point is:

```text
DropTheManDragSessionOwner.UpdateDrag(...)
```

when the coordinator result says:

* `ShouldStopDragging == true`
* `HoleBecameFull == true`

### Why this is the correct handoff

At that point:

* gameplay already knows the hole became `Full`
* the authoritative world position is already known
* waiting until `Release()` would add input timing to a solved gameplay fact

### Release behavior after that handoff

After full-hole completion was already accepted:

* `Release()` should perform cleanup only
* `Release()` must not start normal non-full snap/commit
* `Release()` must not trigger completion a second time

### Visual position handoff

The completion flow should preserve the last authoritative world position from the full-ending drag update in its result.

Reason:

* future presentation may want to settle or close from that location
* gameplay still should not convert that world position into a new committed board coordinate

---

## Synchronous vs Callback-Based Sequencing

### Chosen MVP policy

Use synchronous direct-call completion for the MVP foundation.

Policy:

* the completion service performs `Full -> Closing -> Completed` in one gameplay call
* it returns an explicit result
* future outcome routing consumes that result directly

### Why this is the right MVP choice

* no approved presentation callback owner exists yet
* only one immediate consumer is known
* a direct result is simpler than event fan-out

### Deferred future extension

When presentation later exists, the same owner can be split into:

```text
BeginCompletion
    ->
presentation callback
    ->
FinalizeCompletion
```

That later split does not require a new framework system.

It is only a refinement of the same prototype-owned ownership.

---

## Proposed Result Contract

The completion flow should expose an explicit result for the caller and for later outcome routing.

Recommended conceptual fields:

* `Success`
* `Status`
* `Hole`
* `AuthoritativeWorldPosition`
* `ShouldNotifyHoleCompleted`
* `FailureReason`

Meaning:

* `Status` communicates whether the hole completed now, was already closing, was already completed, or was rejected
* `AuthoritativeWorldPosition` preserves the final drag-time world position for later presentation use
* `ShouldNotifyHoleCompleted` is the narrow hook future outcome routing can consume directly

This should stay a direct result object.

Do not introduce:

* global event buses
* scene searches
* service locators

---

## Recommended First Implementation Slice

After this design is approved, the next smallest implementation slice should:

* add `DropTheManFullHoleCompletionService`
* release stale committed occupancy for a full hole
* transition `Full -> Closing -> Completed`
* return an explicit completion result
* call the service from the full-ending path of `DropTheManDragSessionOwner.UpdateDrag(...)`
* keep `Release()` cleanup-only after a full-ended session

Do not add in that same step:

* win or loss requests
* timer arbitration
* presentation animation orchestration
* framework event-system generalization
* scene wiring

---

## Open Risks After This Step

These remain intentionally deferred even with the design in place:

* exact presentation callback timing for cap-close visuals
* whether completed-hole visuals linger briefly while gameplay already treats them as gone
* final win predicate integration and terminal guard wiring
* reset or restart cleanup while a future asynchronous closing path is active

Those are real follow-up decisions, but they should not block the narrow completion-flow foundation.

---

## Final Summary

```text
Use a prototype-owned full-hole completion service.
Start completion immediately on the drag update that makes the hole Full.
Do not wait for pointer release.
Release stale committed occupancy as part of full-hole completion ownership.
Transition Full -> Closing -> Completed synchronously for MVP.
Expose a direct completion result for later outcome routing.
Keep win/loss, timer arbitration, and presentation sequencing out of this slice.
```
