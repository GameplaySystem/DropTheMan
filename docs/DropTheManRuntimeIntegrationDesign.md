# Drop The Man Runtime Integration Design

## Purpose

This document defines the narrow runtime integration and playable-scene wiring plan for the first playable `DropTheMan` prototype slice.

It connects the already implemented prototype runtime foundations:

* runtime model building
* drag-session ownership
* drag-time movement and collection
* non-full release snap and occupancy commit
* full-hole completion
* outcome routing
* framework game state and timer facts

This is design only.

It does not implement code.

It does not modify:

* Unity scenes
* prefabs
* assets
* MonoBehaviours
* runtime scripts
* framework package code

---

## Problem

The prototype now has the core gameplay services needed for a playable MVP, but they are not yet composed into a scene flow.

Without a narrow integration design, the next implementation risks:

* letting scene objects call gameplay services directly in scattered places
* calling the mutating drag coordinator more than once for one input sample
* letting visual state become gameplay authority
* routing timer expiry or hole completion directly to terminal states from the wrong owner
* turning one prototype scene into a service locator or generic event-bus experiment

The integration layer should make one thing clear:

```text
runtime state and prototype services are gameplay authority;
scene objects are input and presentation adapters.
```

---

## Assumptions

This design assumes the current approved runtime behavior:

* collection happens during drag updates
* snap does not collect
* non-full release owns snap and occupancy commit
* full-hole completion starts from the drag update that makes the hole `Full`
* release after a full-ended session is cleanup-only
* completed holes are no longer normal drag/release participants
* runtime victory checks holes, not raw stickman collection state
* for MVP, all runtime holes are required holes
* timer expiry is a fact that competes with hole-completion victory through outcome routing
* the first accepted terminal outcome wins

This design also assumes no current approved need for:

* optional-hole content schema
* scene-object destruction as gameplay truth
* animation-completion-gated gameplay
* a framework event bus for this prototype slice

Phase 4A addendum:

* JSON-driven gameplay runtime spawning is now an approved prototype-owned follow-up
* the dev gameplay bootstrapper may clone one hole-view template and one stickman-view
  template into runtime-spawned scene objects after the runtime model is built
* spawned views still remain presentation/input adapters only; runtime state stays gameplay
  authority

Phase 4B addendum:

* the dev gameplay bootstrapper may also own a prototype-only serialized `TextAsset[]`
  level sequence plus current-level index for the gameplay test scene
* same-scene restart and next-level loading should reuse the existing JSON -> framework build ->
  runtime model -> runtime view spawn -> scene-controller initialization path
* a minimal prototype-owned `OnGUI` result window is acceptable for this slice
* this sequence owner is scene-local gameplay flow, not framework progression

---

## Architecture Review

### Restated Problem

Build the smallest clean bridge between Unity scene input/views and the existing `DropTheMan` runtime services so the prototype can become playable without moving puzzle-specific meaning into the framework.

### Key Assumptions

* One active drag session is enough for the MVP.
* A single prototype composition root is acceptable for the first playable slice.
* Direct calls are simpler than event publication because the current fact flow has one known owner at each step.
* Runtime model state remains authoritative even when views animate or visually lag.

### Risks

* Input adapters can accidentally double-apply a drag update because `DropTheManMovementCoordinator.EvaluateAndApply(...)` mutates state.
* Scene view components can start owning lifecycle rules if they directly inspect too much gameplay state.
* Timer and outcome handling can split terminal authority if timer code directly requests `Lost` outside the router.
* A broad service locator would hide dependencies and make future puzzle extraction harder.

### Simpler Solutions Considered

Use one scene MonoBehaviour to do everything.

Rejected because it would mix level construction, input, drag, view mapping, timer advancement, and outcome routing into one hard-to-test owner.

Use framework event publication for every fact.

Rejected because the current flow has direct owner-to-owner handoffs and no proven multi-listener pressure.

Let each view own its matching runtime object and call services directly.

Rejected because it scatters gameplay calls across scene objects and makes duplicate drag updates harder to prevent.

### Recommended Approach

Use a small prototype-owned integration stack:

```text
DropTheManRuntimeBootstrapper
    creates runtime model and services
    owns explicit references
    starts framework game state and timer
    hands dependencies to scene adapters

DropTheManRuntimeViewRegistry
    maps runtime ids to view objects
    maps selected views back to runtime state
    applies authoritative visual positions

DropTheManInputAdapter
    converts pointer input into one drag-session call per accepted sample
    never owns collection, capacity, snap, completion, or outcome rules

DropTheManGameplayLoop
    advances timer while playing
    routes timer-expired facts to DropTheManOutcomeRouter
```

For the smallest implementation slice, these names are conceptual. Exact type names may vary, but ownership should remain prototype-owned under `DropAwayPrototype`.

---

## Ownership Boundaries

### Prototype Integration Owns

* composing framework and prototype runtime services
* connecting runtime model state to scene views
* converting selected view objects into `HoleRuntimeState`
* converting pointer positions into candidate world positions
* calling `DropTheManDragSessionOwner` exactly once per accepted drag update
* forwarding successful hole-completed facts to `DropTheManOutcomeRouter`
* forwarding timer-expired facts to `DropTheManOutcomeRouter`
* enabling or disabling scene input based on framework game state

### Prototype Integration Does Not Own

* drag movement rules
* color matching
* stickman collection acceptance
* capacity filling
* full-hole completion lifecycle
* release snap and occupancy commit
* terminal outcome meaning
* timer countdown internals
* framework state-transition validity
* animation
* scene object destruction
* level loading policy beyond receiving the selected level for MVP
* persistence or save/load changes

### Framework Still Owns

* generic runtime context construction
* board, occupancy, shape, snap, timer, and game-state mechanics
* structural lifecycle transition validity

Framework must not learn `Hole`, `Stickman`, collection, or Drop The Man victory meaning.

---

## 1. Composition Root / Bootstrapper

Recommended future owner:

```text
DropTheManRuntimeBootstrapper
```

The bootstrapper should be prototype-owned and live in `DropAwayPrototype`.

Its job is composition, not gameplay rule execution.

### Bootstrapper Inputs

For MVP, the bootstrapper may receive explicit serialized references or constructor-provided test data for:

* the selected `LevelDefinition`
* the framework runtime construction entry point used by the prototype
* `GridWorldLayout`
* board root or coordinate-to-world reference data
* scene hole views
* scene stickman views
* optional timer configuration already present in level data

For the first Drop The Man playable scene, the runtime `GridWorldLayout` should be configured for an XZ board:

```text
GridCoordinate.X -> world.x
GridCoordinate.Y -> world.z
world.y -> visual height only
```

The framework layout remains generic; the prototype composition root owns choosing XZ axes for this scene.

This design does not require a new level-selection system or content schema.

### Bootstrapper Creates Or Owns

The bootstrapper should create and hold explicit references to:

* framework runtime context
* `DropTheManRuntimeModel`
* `GameStateSystem`
* optional `TimerSystem`
* `DropTheManDragSessionOwner`
* `DropTheManOutcomeRouter`
* view registry
* input adapter
* timer/gameplay loop owner
* scene-level movement-feel tuning such as collection trigger radius and drag clearance inset

### Startup Flow

Recommended startup sequence:

```text
load or receive LevelDefinition
    ->
build framework RuntimeLevelContext
    ->
build DropTheManRuntimeModel
    ->
register runtime holes and stickmen with scene views
    ->
create GameStateSystem and transition NotStarted -> Playing
    ->
create and start TimerSystem when timer content is present
    ->
enable input adapter
```

If any setup step fails, the bootstrapper should report a setup failure and avoid starting play.

It should not partially start input or timer advancement after a failed runtime model build.

### Why A Composition Root Is Enough

The next slice does not need:

* global service locators
* scene-wide object searches during gameplay
* a framework dependency injection system
* generic event publishing

Explicit references are easier to audit and safer for the current prototype.

---

## 2. Runtime Model To Scene View Mapping

Recommended future owner:

```text
DropTheManRuntimeViewRegistry
```

The registry maps runtime state to scene views. It does not decide gameplay rules.

### Mapping Keys

Use runtime ids as the primary mapping key:

```text
HoleRuntimeState.Id -> DropTheManHoleView
StickmanRuntimeState.Id -> DropTheManStickmanView
```

Runtime object references may be cached after registration, but ids should remain the stable authoring-to-runtime bridge.

### Hole View Responsibilities

A hole view should be a thin scene adapter for:

* selection hit target
* current visual transform
* applying authoritative world position
* enabling or disabling selectable presentation
* starting the optional full-hole completion presentation and reporting one callback

A hole view should not:

* call the movement coordinator
* call the completion service
* request `Won` or `Lost`
* mutate `HoleRuntimeState`
* decide whether a hole is draggable

### Stickman View Responsibilities

A stickman view should be a thin scene adapter for:

* current visual transform
* authored/runtime id
* optional collection-start presentation
* optional collection-complete presentation later

A stickman view should not:

* decide color matching
* decide collection acceptance
* remove itself as gameplay truth
* mutate `StickmanCoordinateIndex`

### Applying Runtime Results To Views

Drag update:

```text
DropTheManDragSessionUpdateResult.AuthoritativeWorldPosition
    ->
registry applies position to active hole view
```

Newly collecting stickmen:

```text
DropTheManDragSessionUpdateResult.NewlyCollectingStickmen
    ->
registry finds each stickman view
    ->
view starts collection presentation
    ->
current placeholder returns synchronously
    ->
drag-session owner converts the reserved slot into fill
```

Full-hole completion:

```text
DropTheManFullHoleCompletionResult.PresentationPending
    ->
registry disables hole interaction but keeps its renderers visible
    ->
integration optionally snaps the live view to the nearest valid footprint-origin cell without committing it
    ->
view plays cap-close and shrink, or invokes an immediate fallback callback
    ->
runtime finalizes Completed
    ->
registry destroys or hides the completed view
    ->
outcome router consumes the completed-hole fact
```

The view registry should treat missing views as setup errors for the first playable scene, not gameplay fallback behavior.

### Completed Holes

Completed holes stay out of normal drag and release participation through runtime lifecycle state:

```text
HoleRuntimeState.IsDraggable == false
```

The view layer should mirror that by disabling selection for the completed hole view.

The view layer must not be the only guard.

---

## 3. Input Adapter

Recommended future owner:

```text
DropTheManInputAdapter
```

The adapter converts Unity pointer input into explicit runtime calls.

It does not own gameplay rules.

### Pointer Down

Recommended flow:

```text
reject if GameStateSystem.CurrentState is not Playing
    ->
hit-test a DropTheManHoleView
    ->
resolve HoleRuntimeState through view registry
    ->
reject if the runtime hole is not draggable
    ->
read the view's current world position
    ->
DropTheManDragSessionOwner.TryBeginDrag(runtimeModel, hole, worldPosition, worldLayout)
```

Pointer down should not call the movement coordinator.

Pointer down should not collect stickmen.

### Pointer Move

Recommended flow:

```text
reject if no active drag session
    ->
reject if GameStateSystem.CurrentState is not Playing
    ->
convert pointer to candidate world position
    ->
optionally clamp candidate travel against the active hole-view position using a configured
max speed so blocker release cannot produce a large one-frame jump
    ->
call DropTheManDragSessionOwner.UpdateDrag(candidateWorldPosition) once
    ->
apply returned authoritative world position to the active hole view
    ->
start collection presentation for newly collecting stickmen
    ->
report synchronous placeholder completion to the drag-session owner
    ->
if the hole becomes Full, cancel drag and begin the registered completion presentation
    ->
on callback, finalize Completed, clean up the view, and route the completed-hole fact
```

The adapter must not call `DropTheManMovementCoordinator` directly.

The adapter must not call `UpdateDrag(...)` twice for one pointer sample.

The runtime controller may also pass a scene-configured shape-aware drag clearance inset into the
movement coordinator so only the actively dragged hole receives narrow-corridor tolerance. Exact
release, snap, occupancy, and content footprint truth remain unchanged.

### Pointer Up / Cancel

Recommended flow:

```text
if drag-session owner has session context
    ->
call DropTheManDragSessionOwner.Release()
    ->
apply returned authoritative world position to active hole view when available
```

If the session ended because the hole became full, `Release()` remains cleanup-only through the existing drag-session owner behavior.

Pointer cancel should follow the same release/cleanup path as pointer up unless a later approved design defines a different cancellation rule.

### World Position Conversion

The input adapter should convert pointer position to world position using one explicit board interaction plane.

The first playable slice should document or configure:

* which camera is used
* which board plane is used
* how the visual z/depth offset is preserved
* how the converted point maps to the `GridWorldLayout`

Do not infer gameplay position from physics collision after the input hit-test.

Physics may identify selected views, but runtime services remain gameplay authority.

### Duplicate Sample Guard

Because drag updates mutate gameplay state, the adapter should keep a simple guard such as:

* active pointer id
* last processed frame or input sequence id
* one active drag session flag
* optional per-frame candidate-distance clamp for drag feel only

The guard exists only to prevent duplicate calls into `DropTheManDragSessionOwner.UpdateDrag(...)`.

It should not become a general input framework.

---

## 4. Drag Flow Integration

The drag flow should preserve the existing service boundaries.

### Drag-Time Collection

Collection reservation and trigger evaluation remain inside:

```text
DropTheManMovementCoordinator
```

The integration layer observes:

```text
NewlyCollectingStickmen
```

and forwards those runtime states to views for presentation. When the current synchronous
placeholder hook returns, integration explicitly reports presentation completion to the
drag-session owner so the reserved slot can become filled.

The integration layer must not re-check color matching or collection acceptance.

### Full-Hole Completion

Full-hole completion starts inside:

```text
DropTheManDragSessionOwner.CompleteCollectionPresentation(...)
```

after the triggered placeholder presentation returns and capacity fill makes the hole `Full`.
This still happens in the controller call handling the accepted drag input sample; release remains
outside the collection and full-hole path.

The begin call returns:

```text
DropTheManFullHoleCompletionResult with PresentationPending
```

The direct view callback invokes the completion service's finalize operation. Only the finalized
result may be passed to the outcome router. Missing or invalid view presentation invokes the same
callback immediately so placeholder scenes retain deterministic completion.

Before requesting presentation, a scene-level option may make integration evaluate the live view
through the framework grid snap query and apply only its world position. When disabled, the view
keeps its final freeform drag position. Neither policy may reuse the non-full release commit, change
`CurrentCoordinate`, or occupy the footprint again.

The integration layer routes successful completed-hole facts to:

```text
DropTheManOutcomeRouter.HandleHoleCompleted(...)
```

The integration layer should not call `DropTheManFullHoleCompletionService` directly.

### Non-Full Release

Non-full release remains inside:

```text
DropTheManDragSessionOwner.Release()
```

The integration layer applies the returned authoritative world position to the hole view.

The integration layer must not run collection on release.

### Failure Handling

Failed drag begin:

* keep the view at its current position
* keep input idle
* report the failure for debugging

Failed drag update:

* apply the returned authoritative position if meaningful
* stop the active scene drag
* report the failure

Failed full-hole completion:

* do not route a hole-completed fact
* keep terminal outcome unchanged
* report the completion failure

Failed release commit:

* apply the authoritative fallback position returned by release result
* keep runtime occupancy and coordinate behavior owned by the release service
* report the failure

---

## 5. Timer / Outcome Integration

The outcome router is already the prototype owner of terminal meaning.

The integration layer should route facts to it, not bypass it.

### Timer Startup

Recommended flow:

```text
GameStateSystem.TryTransitionTo(GameState.Playing) accepted
    ->
TimerSystem.Start()
```

If there is no timer for a specific MVP scene, the timer owner may be absent. That should mean no timer-expired facts are produced.

### Timer Advancement

Recommended future owner:

```text
DropTheManGameplayLoop
```

or a similarly narrow prototype-owned update adapter.

Per update while playing:

```text
if GameStateSystem.CurrentState == Playing and timer is running
    ->
TimerSystem.Advance(deltaTime)
    ->
if TimerAdvanceResult.ExpiredRaised
        DropTheManOutcomeRouter.HandleTimerExpired(gameStateSystem)
```

Timer warning facts may be forwarded to later presentation, but they must not decide terminal outcomes.

### Hole Completion Outcome Routing

After a drag update produces a successful completed-hole result:

```text
DropTheManOutcomeRouter.HandleHoleCompleted(
    runtimeModel,
    fullHoleCompletionResult,
    gameStateSystem)
```

If the router accepts `Won`, integration should:

* disable further input
* stop or ignore future timer advancement
* hand off to later presentation when such design exists

### Timer Expiry Outcome Routing

After timer expiry:

```text
DropTheManOutcomeRouter.HandleTimerExpired(gameStateSystem)
```

If the router accepts `Lost`, integration should:

* disable further input
* stop processing active drag updates
* hand off to later presentation when such design exists

### First Terminal Outcome Wins

The integration layer should preserve call-order determinism:

```text
final hole completion routed first -> Won may be accepted
timer expiry routed first -> Lost may be accepted
later competing facts are ignored
```

The integration layer should not add a hidden priority rule.

The `DropTheManOutcomeRouter` and framework `GameStateSystem` remain the terminal guards.

---

## Minimal First Implementation Slice After Design

The smallest implementation slice should add prototype-owned integration contracts and a thin orchestration foundation without editing scenes or adding polished presentation.

Recommended slice:

* add a `DropTheManRuntimeBootstrapper` or equivalent composition root shell
* add a `DropTheManRuntimeViewRegistry` or equivalent id-to-view mapping owner
* add minimal hole and stickman view adapter contracts or components needed by the registry
* add a narrow input adapter/controller that calls `DropTheManDragSessionOwner`
* add a narrow timer/gameplay loop handoff that routes `TimerAdvanceResult.ExpiredRaised`
* keep all code under `DropAwayPrototype`
* avoid framework API changes
* avoid scene, prefab, animation, and level-loading polish

The first implementation should be accepted even if it uses simple placeholder views, provided gameplay authority remains runtime-owned and explicit.

Do not include in that slice:

* new required-hole content schema
* optional-hole logic
* animation completion gates
* scene object destruction
* level progression
* generic event bus behavior
* reusable framework service locator

---

## Open Questions Before Playable Scene Wiring

These questions do not block the design, but they should be answered before editing scenes or prefabs:

* Which MVP level source should the bootstrapper use first: serialized scene reference, test asset, loaded JSON, or an existing prototype content loader?
* Runtime spawning now uses scene-local template views in the gameplay test scene; revisit
  whether that should become dedicated prefab assets before broader level-selection or
  gameplay-bridge work.
* Which board plane and camera should convert pointer screen positions into drag world positions?
* Should timer start immediately on `Playing`, or after the first successful input?
* What should the scene do visually when a stickman is `Collecting` but no animation system exists yet?
* Should terminal outcome acceptance immediately disable current active drag input, or should release cleanup also be invoked first for consistency?

---

## Deferred Work

This design does not include:

* code implementation
* scene editing
* prefab creation
* animation
* polished UI
* audio
* production level loading UX
* progression save updates
* optional required-hole schema
* framework event-system use
* framework runtime or package changes

Concrete hole completion presentation is specified separately in
`DropTheManHolePresentationDesign.md`. The corrected single-hole prefab passed isolated validation,
so the direct callback handshake is approved without changing framework ownership.

---

## Final Summary

The first playable integration should be a prototype-owned composition layer.

For the current gameplay test scene, a higher-level prototype-owned loop may sit above that
composition layer to:

```text
choose a level asset from a serialized sequence
    ->
run the existing bootstrap/build/spawn path
    ->
observe accepted Won/Lost state
    ->
show a temporary OnGUI Restart/Next result window
```

That loop still must not become framework progression, persistence, or a general content catalog.

It should directly connect:

```text
scene input -> drag-session owner -> runtime results -> views and outcome router
timer advancement -> timer-expired fact -> outcome router
```

Runtime state remains authoritative.

Views adapt selection and presentation only.

The framework remains game-agnostic.

The outcome router remains the only prototype owner that converts completed-hole and timer-expired facts into terminal `Won` or `Lost` requests.
