# Drop The Man Outcome Routing Design

## Purpose

This document defines the narrow prototype-owned outcome-routing design for the first playable `DropTheMan` slice.

It reconciles the MVP win predicate and defines how later runtime code should route:

* completed-hole facts
* timer-expired facts
* win or loss requests through the existing framework `GameStateSystem`

This is design only.

It does not implement code.

It does not add:

* timer arbitration code
* scene wiring
* animation
* presentation callbacks
* generic event bus behavior
* framework changes

---

## Reconciled Win Predicate

User-facing goal:

```text
Collect all required stickmen.
```

Runtime victory gate:

```text
All required holes are completed.
```

Explanation:

* stickman collection happens during drag
* collection acceptance can happen before visual cleanup
* hole completion happens after capacity is filled and the hole reaches `Completed`
* victory must not trigger from raw overlap, collection acceptance, or a stickman entering `Collecting`
* victory should trigger only after all required holes have reached `Completed`

This preserves the player-facing description while keeping runtime outcome timing aligned with the hole lifecycle.

---

## Problem

The prototype now has direct facts for:

```text
hole completed
```

from `DropTheManFullHoleCompletionResult`.

The framework timer can later expose:

```text
timer expired
```

from `TimerAdvanceResult.ExpiredRaised`.

Both facts may arrive close together.

Without one prototype-owned outcome router, the game risks:

* requesting both `Won` and `Lost`
* allowing timer code to own puzzle-specific loss meaning
* allowing hole-completion code to own final victory meaning
* scattering terminal-state requests across multiple gameplay owners

---

## Ownership

Outcome routing is prototype-owned.

Recommended owner:

```text
DropTheManOutcomeRouter
```

The outcome router owns:

* consuming completed-hole facts
* consuming timer-expired facts
* checking whether all required holes are completed
* requesting `GameState.Won`
* requesting `GameState.Lost`
* enforcing first-terminal-outcome-wins behavior

The outcome router does not own:

* drag movement
* collection
* capacity filling
* full-hole completion
* release snap or occupancy commit
* animation
* scene object destruction
* level loading
* broad framework event dispatch

---

## Framework Dependencies

The outcome router should use existing framework runtime foundations:

* `GameStateSystem`
* `GameState`
* `GameStateTransitionResult`
* `TimerAdvanceResult` or an equivalent timer-expired fact from the timer owner

The framework owns:

* generic lifecycle states such as `Playing`, `Won`, and `Lost`
* structural transition validity
* timer tracking and expiration facts

`DropTheMan` owns:

* whether completed holes mean victory
* whether timer expiry means loss
* which terminal request should be attempted first when facts arrive in a specific order

---

## Input Facts

### Hole Completed

Source:

```text
DropTheManFullHoleCompletionResult
```

Required interpretation:

* consume only successful results where a hole completed now
* use the completed hole reference or id as an input to victory evaluation
* do not treat the completion result itself as `Won`

### Timer Expired

Source:

```text
TimerAdvanceResult.ExpiredRaised
```

or a later equivalent direct timer-expired handoff.

Required interpretation:

* timer expiry is a fact
* timer expiry does not directly set `Lost`
* the outcome router decides whether to request `Lost`

---

## Victory Check

The outcome router should evaluate victory by checking all required holes in the current `DropTheManRuntimeModel`.

Recommended MVP rule:

```text
all runtimeModel.Holes have LifecycleState == Completed
```

This intentionally checks holes, not stickmen.

Reasons:

* stickmen may be `Collecting` before presentation cleanup is done
* collecting all stickmen is not enough if a full hole has not completed its close or removal sequence
* the current gameplay lifecycle makes `Completed` the stable fact that a hole has left active board participation

If later content introduces optional holes, disabled holes, or non-required collectors, the required-hole set must be documented before changing this predicate.

---

## Terminal Request Flow

### Hole Completion Flow

Recommended future flow:

```text
DropTheManFullHoleCompletionResult says a hole completed now
    ->
DropTheManOutcomeRouter handles hole-completed fact
    ->
router checks all required holes completed
    ->
if true, router requests GameState.Won through GameStateSystem.TryTransitionTo(GameState.Won)
```

### Timer Expiry Flow

Recommended future flow:

```text
TimerAdvanceResult.ExpiredRaised is true
    ->
DropTheManOutcomeRouter handles timer-expired fact
    ->
router requests GameState.Lost through GameStateSystem.TryTransitionTo(GameState.Lost)
```

The router should only request terminal outcomes while the framework game state is still effectively active for play.

For MVP, that means the router should treat `GameState.Playing` as the expected state for terminal requests.

---

## First Terminal Outcome Wins

Rule:

```text
Only one terminal outcome may win.
```

Recommended router policy:

* keep a prototype-owned terminal accepted flag or accepted terminal state
* once a `Won` or `Lost` request is accepted, ignore later competing facts
* do not rely only on duplicate caller discipline
* still use `GameStateSystem.TryTransitionTo(...)` as the final framework lifecycle authority

Examples:

```text
Final hole completes first
    ->
Win request accepted
    ->
Later timer expiry ignored
```

```text
Timer expiry accepted first
    ->
Loss request accepted
    ->
Later hole completion ignored
```

If `GameStateSystem.TryTransitionTo(...)` rejects a terminal request, the router should report failure and should not mark the terminal outcome accepted.

Reason:

* game state transition failure means the framework lifecycle did not accept the terminal state
* hiding that failure would make gameplay outcome and framework state drift

---

## Race Handling

Same-frame races must be deterministic by call order.

The router should not invent a hidden priority such as always-win or always-lose.

Policy:

```text
first accepted terminal request wins
```

This keeps ordering explicit:

* if hole completion is routed before timer expiry and all required holes are completed, `Won` wins
* if timer expiry is routed before final hole completion, `Lost` wins
* later facts are ignored after a terminal request has been accepted

If a future gameplay requirement needs a different priority rule, that must be documented before implementation.

---

## Why This Stays Prototype-Owned

The router consumes puzzle-specific facts:

* holes
* hole lifecycle state
* completed-hole facts
* Drop The Man timer-loss meaning

Those facts are not framework concepts.

The framework should not know:

* what a hole is
* what a stickman is
* whether a completed hole means victory
* whether timer expiry means loss for this puzzle

The framework provides reusable phase and timer mechanics.

The prototype decides puzzle-specific terminal meaning.

---

## Why This Is Not A Framework Event Bus

The current pressure is narrow:

* one outcome owner consumes completed-hole facts
* one outcome owner consumes timer-expired facts
* one outcome owner requests terminal game state

A generic event bus would add indirection before there is multi-listener pressure.

For MVP, use direct calls and explicit results.

Introduce the approved framework `EventSystem` later only if real cross-system notification pressure appears.

---

## Proposed Result Contract

The later implementation should return explicit outcome-routing results.

Recommended conceptual fields:

* `Success`
* `TerminalAccepted`
* `RequestedState`
* `CurrentState`
* `IgnoredBecauseTerminalAlreadyAccepted`
* `FailureReason`

Reason:

* callers need to know whether the router consumed a fact
* terminal acceptance must be visible for testing
* rejected game-state transitions should not be silent

---

## Deferred Work

This design does not implement:

* outcome router runtime code
* timer update loop
* scene wiring
* UI or presentation reactions
* animation completion callbacks
* progression save updates
* event publication
* final content validation for required hole sets

---

## Recommended First Implementation Slice

After this design is approved, the smallest implementation slice should:

* add a prototype-owned `DropTheManOutcomeRouter`
* accept `DropTheManRuntimeModel` and `GameStateSystem`
* consume successful full-hole completion results
* check all required holes are `Completed`
* consume timer-expired facts
* request `GameState.Won` or `GameState.Lost`
* enforce first accepted terminal outcome wins
* return explicit routing results

Do not add in the same slice:

* scene object destruction
* animation
* UI
* progression
* generic event infrastructure

---

## Final Summary

```text
Player-facing objective:
Collect all required stickmen.

Runtime victory gate:
All required holes are Completed.

Outcome routing:
Prototype-owned router consumes hole-completed and timer-expired facts.
It requests Won or Lost through GameStateSystem.
The first accepted terminal request wins.
Completion facts and timer facts are not terminal outcomes by themselves.
```
