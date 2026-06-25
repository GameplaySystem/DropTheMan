# Drop The Man Movement Coordinator Design

## Purpose

This document resolves the remaining deterministic implementation-strategy decisions for the first playable `DropTheMan` movement and collection coordinator.

This is a technical design document.

It does not change the approved gameplay rules.

Read alongside:

* `docs/DropTheManMovementAndCollectionRules.md`
* `docs/DropTheManMovementEdgeCaseChecklist.md`
* `docs/DropTheManMVPGameModuleRequirements.md`

Do not implement code from this document without review.

---

## Scope

This document chooses the MVP technical policy for:

* swept cell enumeration for freeform drag
* deterministic candidate ordering
* wide-footprint first contact handling
* blocker stop and clamp behavior
* timer versus hole-completion terminal ordering

It intentionally does not redesign framework ownership.

`PuzzleFramework` still owns:

* board layout data
* structural occupancy data
* shape footprint data
* drag infrastructure
* snap infrastructure
* runtime flow infrastructure

`DropTheMan` still owns:

* collectible color meaning
* same-color enterability
* wrong-color blocking
* collection acceptance
* capacity filling
* hole completion
* win and loss meaning

---

## Coordinator Inputs

The coordinator should receive or have access to:

* previous accepted hole world position
* current candidate hole world position from drag
* board world-layout data
* board dimensions and active or blocked cell queries
* hole runtime state
* hole footprint definition
* collectible coordinate index and collectible runtime states
* hole capacity or fill state
* timer and outcome-owner access for terminal routing

The coordinator should work in both:

* world space for visual positions
* board-local continuous space for deterministic validation

### Board-Space Conversion Policy

The sweep helper should operate in board-local continuous space expressed in cell units.

Policy:

* convert previous accepted world position to board-local continuous coordinates
* convert candidate world position to board-local continuous coordinates
* represent each footprint cell as a unit axis-aligned rectangle in board-local cell space

Reason:

* board-local cell space avoids repeated world-space geometry ambiguity
* validation remains aligned with grid truth without making movement visibly step-based

---

## 1. Swept Cell Enumeration Strategy

### Chosen Policy

Use an event-based swept-footprint enumeration in board-local continuous space.

For each drag update:

1. Convert previous accepted world position into previous accepted board-local continuous position.
2. Convert candidate world position into candidate board-local continuous position.
3. For each local footprint rectangle:
   * compute the parametric times `t` in `[0, 1]` where the moving rectangle's min or max edge crosses an integer grid line on the X or Y axis
4. Add `t = 1`
5. Deduplicate all `t` values with a small numeric epsilon
6. Sort `t` ascending
7. For each sorted `t` group:
   * sample a position just after that contact time, or exactly at `1` for the final group
   * resolve the full footprint overlap set at that sampled position
   * compare it against the previously accepted overlap set to determine newly entered cells
   * validate and process that contact group deterministically

### How Overlap Is Resolved

Each footprint rectangle should use half-open board-local intervals:

```text
[minX, maxX)
[minY, maxY)
```

Policy:

* a cell counts as overlapped only if the footprint rectangle has positive area inside that cell
* touching a border without entering the cell does not count as overlap

Reason:

* this avoids double-counting border-touch cases
* this makes border behavior deterministic

### Why This Is Acceptable For MVP

* it is deterministic
* it respects freeform continuous drag
* it avoids final-position-only sampling
* it does not require physics authority
* it is safer than trying to perfect geometric clamping in the first implementation

### Intentionally Deferred

* aggressive performance optimization of sweep event generation
* generalized reusable framework sweep infrastructure
* perfect visual edge-clamping against blockers

### Coordinator Consequence

The coordinator needs a dedicated sweep helper that:

* works in board-local continuous space
* returns contact groups in parametric order
* exposes overlap sets and newly entered cells

---

## 2. Deterministic Tie-Breaker

### Chosen Policy

Sort collection candidates by:

1. distance along the sweep from the previous accepted position
2. grid `Y` ascending
3. grid `X` ascending

Assumption:

* `Y` increases upward in board coordinates
* `X` increases to the right in board coordinates

### Why This Is Acceptable For MVP

* it is simple
* it is stable
* it is easy to test
* it avoids dependence on arbitrary container order

### Intentionally Deferred

* a more perceptual ordering rule based on exact first-contact geometry across the whole footprint

### Coordinator Consequence

Every collection-acceptance path must use this same ordering rule.

Do not allow:

* physics callback order
* animation order
* dictionary iteration order

to decide collection order.

---

## 3. Wide-Footprint First Contact

### Chosen Policy

Blocking validation happens before collection acceptance for each contact group.

For a contact group reached at the same sweep distance:

1. Resolve the full footprint overlap set at that sampled position.
2. Validate structural blockers across the full overlap set.
3. Validate wrong-color available collectibles across the full overlap set.
4. If any blocker exists, reject the whole contact group.
5. Only if the whole contact group is valid:
   * identify same-color available collectibles in newly entered cells
   * process them in the deterministic ordering rule

### Why This Is Acceptable For MVP

* it preserves the rule that invalid mixed groups do not partially collect
* it avoids accepting same-color targets from a footprint position that was never valid to occupy
* it keeps blocking and collection responsibilities clearly ordered

### Intentionally Deferred

* more exact sub-cell first-contact prioritization inside the same contact group

### Coordinator Consequence

The coordinator must clearly separate:

* `validate contact group`
* `accept collections in valid group`

It must not mix them in one pass.

---

## 4. Blocker Stop / Clamp Method

### Chosen Policy

Use previous-accepted-position clamping for MVP.

Rule:

```text
When a blocker is encountered,
keep the authoritative hole position at the previous accepted continuous position before that blocked contact group.
```

Do not attempt perfect geometric edge-clamping in MVP.

### Why This Is Acceptable For MVP

* it is simple
* it is deterministic
* it avoids visual overlap with blockers
* it avoids geometry-heavy edge solving too early

### Intentionally Deferred

* exact edge-clamp placement against the blocker boundary
* smoother visual correction when the pointer overshoots a blocker

### Coordinator Consequence

The coordinator must store:

* previous accepted continuous position
* previous accepted overlap set

When a blocked group is found:

* reject that group
* preserve the previous accepted position as authoritative
* stop traversal immediately

---

## 5. Capacity Fill Mid-Sweep

### Chosen Policy

Capacity can interrupt a valid sweep mid-processing.

Rule:

1. Process collectible candidates in deterministic order inside valid contact groups.
2. On each accepted collectible:
   * increment fill immediately
   * mark collectible `Collecting` immediately
3. As soon as remaining capacity reaches zero:
   * mark hole `Full`
   * stop accepting further collectibles
   * stop further traversal
   * begin full-hole completion flow
4. Any unprocessed collectibles remain `Available`

### Why This Is Acceptable For MVP

* it matches the approved gameplay rule
* it avoids overfill ambiguity
* it keeps leftover collectibles available for future valid collectors

### Intentionally Deferred

* more advanced content validation that predicts impossible over-capacity arrangements

### Coordinator Consequence

The coordinator must support:

* mid-group interruption
* immediate hole state change to `Full`
* immediate stop of further movement evaluation

---

## 6. Timer Versus Completion Ordering

### Chosen Policy

Use one prototype-owned terminal-state guard.

Rule:

* timer expiry does not directly set `Lost`
* final hole completion does not directly bypass the terminal guard
* both timer-driven loss requests and hole-completion win requests go through the same terminal request gate
* first accepted terminal request wins
* after `Won` or `Lost` is accepted, later competing callbacks are ignored

### Why This Is Acceptable For MVP

* it makes race outcomes deterministic
* it avoids duplicate terminal requests
* it keeps final game outcome ownership in the game module rather than inside framework timing

### Intentionally Deferred

* more elaborate transition arbitration beyond first-terminal-wins

### Coordinator Consequence

The prototype needs one outcome owner or arbiter that:

* receives timer expiry notifications
* receives hole completion notifications
* decides whether to accept `Won` or `Lost`
* refuses later competing terminal requests

---

## Highest-Priority Tests From The Edge Case Checklist

These should be treated as the first verification targets once implementation begins:

* Test that very fast drag across many cells does not skip wrong-color blockers.
* Test that very fast drag across many cells does not skip same-color collectibles that should be accepted.
* Test that equal-distance crossed cells are processed in a stable deterministic order.
* Test that a multi-cell hole overlapping one same-color and one wrong-color available collectible rejects the movement step.
* Test that releasing after a blocked sweep keeps the hole at the last accepted valid position.
* Test that a hole with one remaining capacity accepts only one collectible when more same-color candidates are encountered in the same sweep.
* Test that leftover same-color collectibles remain `Available` when capacity fills mid-sweep.
* Test that timer expiry on the same frame as final collectible acceptance does not request both `Won` and `Lost`.
* Test that timer expiry on the same frame the final hole becomes `Completed` does not produce double terminal requests.
* Test that a collectible entering `Collecting` is immediately non-blocking even while still visually present.

---

## Remaining Deferred Work

These are intentionally not solved in MVP:

* perfect geometric edge-clamping
* optimized broad-phase sweep acceleration
* reusable framework generalization of the sweep helper
* physics-assisted feel improvements beyond non-authoritative support
* advanced solvability validation for authored content

---

## Final MVP Technical Summary

```text
Use freeform continuous drag as the visible movement model.
Convert drag positions into board-local continuous coordinates.
Enumerate swept footprint contact groups by parametric grid-line crossing events.
Use half-open overlap intervals for deterministic cell coverage.
Validate blockers across the whole contact group before accepting collections.
Accept collectibles in deterministic order:
    1. sweep distance
    2. Y ascending
    3. X ascending
Clamp blocked movement to the previous accepted continuous position.
Let capacity interrupt traversal immediately.
Route both win and loss through one terminal-state guard.
```
