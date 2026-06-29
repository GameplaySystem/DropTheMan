# Drop The Man Playable Scene Adapter Design

## Purpose

This document defines the narrow Unity scene-adapter design for the first playable `DropTheMan` prototype scene.

It sits on top of the already implemented runtime integration foundation:

* `DropTheManRuntimeController`
* `DropTheManRuntimeBootstrapper`
* `DropTheManViewRegistry`
* `IDropTheManHoleView`
* `IDropTheManStickmanView`

This is design only.

It does not implement code.

It does not edit scenes, prefabs, or assets.

It does not add:

* animation
* visual feedback framework behavior
* prefab spawning
* event bus behavior
* level-loading UX
* framework changes
* `TimerMode.CountUp` support
* required-hole schema

---

## Problem

The prototype now has runtime gameplay orchestration, but no Unity scene layer that can:

* expose pre-placed hole and stickman objects as runtime view adapters
* author stable runtime ids on scene objects
* hit-test selectable holes
* convert pointer screen positions into world positions
* call the runtime controller once per accepted pointer sample
* advance the countdown timer from Unity `Update`
* stop input when a terminal outcome is accepted

The next implementation must make the prototype playable without letting scene objects become gameplay authority.

---

## Assumptions

This design assumes:

* runtime model construction already happened before scene adapter play begins
* scene objects are pre-placed for this slice
* runtime ids on scene views match the ids in `HoleRuntimeState.Id` and `StickmanRuntimeState.Id`
* the scene adapter receives or creates a `DropTheManRuntimeController` through the existing prototype bootstrapper path
* pointer screen-to-world conversion can be simple and explicit for the MVP
* timer behavior remains countdown-only
* terminal state acceptance is already represented by `DropTheManRuntimeControllerResult.TerminalOutcomeAccepted`

This design rejects:

* prefab spawning for this slice
* camera conversion hidden inside runtime gameplay services
* view components calling movement, completion, or outcome services directly
* scene objects deciding win/loss
* a temporary event bus

---

## Ownership

### Scene Adapter Owns

The scene adapter layer owns:

* pre-placed MonoBehaviour view adapters
* serialized runtime id fields on those adapters
* pointer hit-testing against selectable hole views
* pointer screen-to-world conversion
* active pointer/sample gating
* calling `DropTheManRuntimeController`
* forwarding Unity `Update` delta time to timer ticking
* disabling input after terminal acceptance
* simple no-op or placeholder view reactions

### Scene Adapter Does Not Own

The scene adapter must not own:

* movement rules
* collection rules
* wrong-color blocking
* capacity fill
* release snap math
* occupancy commit
* full-hole completion
* win predicate logic
* timer countdown internals
* scene object destruction
* animation sequencing
* level loading UX
* framework generalization

Those remain in existing runtime services or future approved slices.

---

## Recommended Scene Types

Exact names are flexible, but the next implementation should keep the following responsibilities separate.

### `DropTheManHoleView`

MonoBehaviour implementing:

```text
IDropTheManHoleView
```

Responsibilities:

* expose serialized `runtimeId`
* expose current `transform.position` as `WorldPosition`
* apply authoritative positions via `ApplyWorldPosition(...)`
* enable or disable selection through `SetSelectable(...)`
* expose or own a collider/hit target for pointer selection

Non-responsibilities:

* do not call `DropTheManMovementCoordinator`
* do not call `DropTheManDragSessionOwner`
* do not call `DropTheManOutcomeRouter`
* do not inspect hole fill/capacity to decide rules
* do not destroy itself when completed

For MVP, `SetSelectable(false)` may simply disable a collider or set an internal selectable flag.

### `DropTheManStickmanView`

MonoBehaviour implementing:

```text
IDropTheManStickmanView
```

Responsibilities:

* expose serialized `runtimeId`
* receive `OnCollectionStarted(StickmanRuntimeState stickman)`
* perform a minimal placeholder reaction

Allowed placeholder reactions:

* no-op
* disable a simple collider if one exists
* hide a basic renderer only if that is already part of the local view component

Non-responsibilities:

* do not decide collection
* do not mutate `StickmanRuntimeState`
* do not remove itself as gameplay truth
* do not trigger win/loss
* do not start a broad visual feedback system

### `DropTheManSceneController`

MonoBehaviour scene composition adapter.

Responsibilities:

* hold serialized references to pre-placed `DropTheManHoleView` objects
* hold serialized references to pre-placed `DropTheManStickmanView` objects
* provide or receive the already-built runtime model path for MVP
* call `DropTheManRuntimeBootstrapper.CreateController(...)`
* call `DropTheManRuntimeController.StartGameplay()`
* own scene-level enabled/disabled input state
* own timer ticking by calling `DropTheManRuntimeController.TickTimer(Time.deltaTime)`

Non-responsibilities:

* do not spawn prefabs
* do not implement level browser UX
* do not run save/load
* do not decide game outcomes
* do not own input hit-test details if a separate input adapter exists

### `DropTheManPointerInputAdapter`

MonoBehaviour or helper owned by the scene adapter layer.

Responsibilities:

* detect pointer down, move, release, and cancel
* hit-test holes on pointer down
* convert pointer screen positions to board/world positions
* call the runtime controller exactly once per accepted input sample
* suppress input after terminal acceptance

Non-responsibilities:

* do not call movement coordinator directly
* do not call full-hole completion service directly
* do not route win/loss directly
* do not own timer countdown

The scene controller may own this directly for MVP if keeping it separate adds unnecessary ceremony.

---

## Runtime Id Authoring

Runtime ids should be authored explicitly on pre-placed scene objects:

```text
DropTheManHoleView.runtimeId
DropTheManStickmanView.runtimeId
```

These values must match:

```text
HoleRuntimeState.Id
StickmanRuntimeState.Id
```

from the runtime model built from level content.

### Validation Policy

At startup, the scene adapter should register all configured views with `DropTheManRuntimeBootstrapper` / `DropTheManViewRegistry`.

The registry already rejects:

* null views
* blank runtime ids
* duplicate hole view ids
* duplicate stickman view ids
* missing views for runtime holes
* missing views for runtime stickmen

Startup should fail visibly for development if ids do not match.

Do not silently auto-generate ids in the scene adapter.

Reason:

* auto-generation would hide mismatches between authored scene views and runtime level content
* the first playable slice needs debuggable explicit mapping more than convenience

### Future Alternatives

Prefab spawning or runtime-generated views may assign ids from runtime model data later.

That is deliberately deferred.

---

## Pointer Hit-Test Ownership

Pointer hit-testing belongs to the scene input adapter.

Recommended MVP approach:

```text
pointer down screen position
    ->
Unity raycast / overlap against hole view hit targets
    ->
resolve DropTheManHoleView
    ->
call DropTheManRuntimeController.BeginDragByHoleView(...)
```

The runtime controller should not know about:

* cameras
* rays
* colliders
* layers
* screen positions
* `Camera.main`

### Selectability

The hit-test should ignore views that are not selectable.

For MVP, this can be implemented by:

* disabling the collider in `SetSelectable(false)`
* keeping a local `IsSelectable` property on the view
* or both

The runtime state remains the real guard because `BeginDragByHoleId(...)` ultimately delegates to runtime rules.

View selectability is a scene usability guard, not gameplay authority.

---

## Pointer Screen-To-World Conversion Ownership

Screen-to-world conversion belongs to the scene input adapter.

The runtime controller already accepts:

```text
UpdateDragWorldPosition(Vector3 candidateWorldPosition)
```

Therefore, the input adapter must convert before calling runtime code.

### MVP Conversion Policy

Use one explicit board interaction plane.

For the first Drop The Man playable scene, the intended board convention is:

```text
Unity board plane: XZ
Unity visual height axis: Y
GridCoordinate.X -> world.x
GridCoordinate.Y -> world.z
world.y -> visual height only
```

Scene input should therefore use a horizontal board plane with normal:

```text
(0, 1, 0)
```

The `GridWorldLayout` provided to runtime code should use:

```text
BoardOrigin = board plane origin
CellSize = configured board cell size
BoardXAxis = (1, 0, 0)
BoardYAxis = (0, 0, 1)
```

The scene adapter should serialize or otherwise explicitly receive:

* camera reference
* board plane origin
* board plane normal
* optional drag depth offset

Recommended flow:

```text
screen position
    ->
camera ray
    ->
intersect explicit board plane
    ->
candidate world position
    ->
DropTheManRuntimeController.UpdateDragWorldPosition(candidateWorldPosition)
```

Do not use `Camera.main` implicitly.

Do not infer gameplay movement from physics after the initial hit-test.

Physics may select a view; the explicit board plane provides drag positions.

Visual object height should be preserved by the view/adapter layer when needed. Runtime board-coordinate conversion must not use world `Y` as a grid coordinate for this XZ scene.

---

## Input Sample Policy

The adapter must call:

```text
DropTheManRuntimeController.UpdateDragWorldPosition(...)
```

exactly once per accepted input sample.

Reason:

* the underlying movement coordinator mutates gameplay state
* duplicate calls may double-collect stickmen or double-advance capacity

### Recommended Guard

The input adapter should track:

* active pointer id
* whether a drag is active
* last processed input sample key

For the MVP sample key, use the simplest reliable source available in the chosen input path:

* pointer event id / event timestamp if using event callbacks
* frame count plus pointer id if polling in `Update`

The guard should prevent:

* processing the same move sample twice
* processing move after terminal acceptance
* processing move when no drag is active
* processing a second pointer while one drag is active

The guard should not become a reusable input framework.

---

## Pointer Flow

### Pointer Down

```text
if input disabled:
    ignore

hit-test hole view:
    if no hit, ignore

call controller.BeginDragByHoleView(hitView):
    if success and drag began, mark local drag active
    if terminal accepted, disable input
```

Pointer down does not collect.

Pointer down does not call the movement coordinator directly.

### Pointer Move / Drag

```text
if input disabled or no local drag active:
    ignore

if sample was already processed:
    ignore

convert screen position to world position
    ->
call controller.UpdateDragWorldPosition(worldPosition) exactly once
    ->
if terminal accepted, disable input and clear local drag state
    ->
if controller reports no active drag / stop, clear local drag state
```

The hole view position is updated inside the runtime controller through the view registry.

The adapter must not apply the raw candidate world position to the hole view.

### Pointer Up

```text
if input disabled:
    clear local drag state only

if local drag active:
    call controller.ReleaseDrag()
    clear local drag state
    if terminal accepted, disable input
```

Normal non-full snap and occupancy commit remain inside the runtime drag/release path.

### Pointer Cancel

```text
call controller.CancelDrag()
clear local drag state
```

Cancel should not run normal non-full release commit.

Reason:

* cancel is not player release
* terminal-state cancellation must not snap/commit after win or loss acceptance

---

## Timer Update Flow

Timer ticking belongs to the scene controller or a narrow gameplay-loop adapter.

Recommended MVP flow:

```text
Update()
    ->
if controller exists and input/gameplay is not terminal:
    controller.TickTimer(Time.deltaTime)
    if result.TerminalOutcomeAccepted:
        disable input
```

`TickTimer(...)` already:

* advances only the configured framework countdown timer
* routes `ExpiredRaised` to `DropTheManOutcomeRouter`
* ignores ticking after terminal acceptance
* does not add `CountUp`

The scene adapter must not:

* interpret timer expiry as loss directly
* call `GameStateSystem.TryTransitionTo(GameState.Lost)` directly
* implement `TimerMode.CountUp`
* implement timer warning UI in this slice

---

## Terminal State And Input Shutdown

Terminal acceptance means:

```text
Won or Lost was accepted by the outcome path.
```

When a controller result reports:

```text
TerminalOutcomeAccepted == true
```

the scene adapter should:

* disable pointer down processing
* ignore pointer move processing
* ignore pointer release as gameplay
* clear local active pointer state
* avoid calling `ReleaseDrag()` after terminal acceptance
* stop or ignore timer ticking through the controller

The runtime controller already cancels active drag context without normal non-full release commit after terminal acceptance.

The scene adapter should not duplicate outcome guarding; it should only stop feeding inputs.

---

## Startup Flow

Recommended MVP scene startup:

```text
scene has pre-placed hole/stickman views with authored runtime ids
    ->
scene controller receives or obtains dev-only level/runtime model input
    ->
runtime model is built before scene view registration failure can mutate scene objects
    ->
scene controller passes view arrays to DropTheManRuntimeBootstrapper.CreateController(...)
    ->
if bootstrap succeeds:
        controller.StartGameplay()
        enable input
    else:
        log/report failure
        keep input disabled
```

This preserves the current warning that runtime model building mutates occupancy and should not be mixed with prefab/view creation rollback.

---

## Deliberately Unpolished For This Slice

The first playable scene adapter may be visually plain.

Allowed minimal behavior:

* holes move by directly setting transform position
* completed holes become unselectable
* completed holes may hide basic child renderers as a placeholder
* collecting stickman hook may be no-op or simple hide
* timer may have no UI
* terminal outcome may only disable input and log/report result
* startup failure may log a direct error

Deliberately not included:

* cap close animation
* stickman collection animation
* DOTween / Animator behavior
* particles, audio, haptics, or visual feedback framework
* win/loss UI
* scene transition
* level progression
* prefab spawning
* scene object destruction as gameplay truth
* generic event bus
* polished loading flow

---

## Failure Handling

Startup failures:

* keep input disabled
* report missing ids, duplicate ids, or missing views
* do not attempt to rebuild runtime model repeatedly

Hit-test failures:

* ignore pointer down

Screen-to-world conversion failures:

* skip that input sample
* do not call `UpdateDragWorldPosition(...)`

Runtime controller failures:

* clear local active drag state when the result indicates failed drag update/release
* do not retry the same input sample
* do not manually repair runtime state from the scene adapter

---

## Recommended First Implementation Slice

After this design is approved, implement only:

* `DropTheManHoleView` MonoBehaviour implementing `IDropTheManHoleView`
* `DropTheManStickmanView` MonoBehaviour implementing `IDropTheManStickmanView`
* a narrow `DropTheManSceneController` or equivalent adapter that registers pre-placed views and starts gameplay
* a narrow pointer input adapter that owns hit-test and screen-to-world conversion
* timer `Update()` forwarding through `DropTheManRuntimeController.TickTimer(...)`
* terminal-result input disable behavior

Do not include:

* scene or prefab polish beyond what is necessary to compile the adapters
* animation
* visual feedback framework
* prefab spawning
* event bus
* level-loading UX
* required-hole schema
* `TimerMode.CountUp`
* framework changes

---

## Final Summary

The playable scene adapter should be a thin Unity-facing layer.

It should convert:

```text
scene objects and pointer samples
```

into:

```text
runtime ids and world positions
```

then call:

```text
DropTheManRuntimeController
```

The controller and existing runtime services remain gameplay authority.

Scene adapters provide selection, transform updates, simple placeholder reactions, and Unity `Update` timing only.
