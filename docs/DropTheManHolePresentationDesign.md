# Drop The Man Hole Presentation Design

## Purpose

This document defines the prototype-owned presentation contract for concrete Drop The Man hole
assets.

It covers:

* logical-root and visual-root placement
* concrete FBX and prefab responsibilities
* stencil aperture, cap blend shape, and collection-socket references
* the isolated cap-close and shrink sequence
* the direct callback boundary between presentation and runtime completion

It does not move hole gameplay rules into scene objects. Capacity, collection eligibility,
occupancy, lifecycle state, and outcome routing remain runtime-owned.

Read alongside:

* `DropTheManCollectionPresentationTimingDesign.md`
* `DropTheManFullHoleCompletionFlowDesign.md`
* `DropTheManRenderingPipelineDesign.md`
* `DropTheManRuntimeIntegrationDesign.md`

---

## Current Implementation Stage

The configurable `DropTheManHolePresentation` component and corrected single-cell prefab have
passed isolated Unity validation. The cap blend shape resolves by its authored name, the cap-close
and shrink sequence plays, and reset restores the authored state.

All eight canonical hole wrapper prefabs are now authored and referenced by the shared visual
config. Runtime spawning resolves them from exact footprint sets across quarter-turn rotations.
Owner validation confirmed that the eight unrotated canonical footprints spawn their distinct
configured visuals rather than falling back to the single-hole view. Rotated variants, individual
root alignment, every selection collider, sockets, cap blend shapes, stencil apertures, and
completion sequences still require focused Unity validation.

The direct callback is connected to `Full -> Closing -> Completed` gameplay orchestration. The
configured path now waits for cap-close and shrink before finalization; placeholder or invalid
presentation configurations use an immediate callback fallback so animation availability never
becomes gameplay authority. The runtime handshake passed integrated Unity validation for drag
termination, cap closing, shrink, destruction, win routing, restart, and next-level loading. The
new visual-only nearest-cell alignment before closing still requires manual validation.

---

## Ownership

### Runtime owns

* hole identity, footprint, color, capacity, and lifecycle
* drag termination and structural occupancy release
* deciding when a full-hole presentation may begin
* finalizing `Closing -> Completed` after the presentation callback
* outcome routing after completion finalizes

### Hole presentation owns

* concrete mesh references
* resolving the authored cap-close blend shape
* cap-close and shrink tween configuration
* cancelling owned tweens when disabled or destroyed
* reporting one completion callback after the visual sequence finishes
* exposing authored collection sockets for later cat presentation

### PuzzleFramework does not own

* hole meshes or prefab hierarchy
* DOTween
* stencil shaders or materials
* cap animation
* Drop The Man collection sockets or disappearance timing

---

## Root And Pivot Contract

Each runtime hole uses two distinct transforms.

```text
HoleRoot
    PresentationRoot
        artist-authored hole objects
    CollectionSockets
        Slot_0
        Slot_1 ...
```

`HoleRoot` is the logical root used by `DropTheManHoleView` and board movement. Its local origin is
the footprint-origin cell center represented by the authored level coordinate. Runtime movement,
snap, and footprint calculations must continue to move this root.

`PresentationRoot` is the visual root. It may be offset from `HoleRoot` so an asymmetric or
multi-cell hole is visually centered correctly. Completion shrink animation targets only this
transform. Scaling `HoleRoot` is rejected because it couples gameplay coordinates, colliders, and
presentation animation.

Artist FBX pivots do not define gameplay coordinates. The prefab wrapper establishes the logical
root and visual offset explicitly.

---

## Artist FBX Contract

Every concrete hole export should provide independently referenceable objects for:

* outer shell
* visible inner cavity/walls
* cap as a `SkinnedMeshRenderer`
* invisible stencil aperture matching the open area

The cap mesh must contain a consistently named closing blend shape. `Close` is the current default
contract name; the prefab may override it if the final artist naming differs.

The stencil aperture must:

* match the visible opening rather than the complete outer footprint
* use the prototype stencil-writer material
* remain visually invisible
* avoid owning gameplay colliders

Each prefab may use multiple selection colliders to approximate non-rectangular visual surfaces
such as L, T, and plus shapes. These colliders are pointer hit targets only. Runtime footprint data
remains authoritative for movement, overlap, occupancy, capacity, and board-boundary rules.

The inner-cavity material should read the aperture's stencil reference with comparison `Equal`.
Board receiver materials continue to use `NotEqual`. This keeps the interior visible through the
opening without making the cavity or stencil mesh authoritative for gameplay.

The single-hole prefab now uses this material on its inner-wall submesh, and the configuration
passed Game-view validation.

All visual parts should share a consistent authored origin where practical. The prefab wrapper
still remains authoritative for the logical footprint origin and final visual offset.

---

## Collection Socket Contract

Each hole prefab exposes one collection socket per footprint cell.

For the planned shapes this means:

* single square: 1 socket
* double rectangle: 2 sockets
* triple rectangle: 3 sockets
* two-by-two square: 4 sockets
* short L: 3 sockets
* long L: 4 sockets
* T: 4 sockets
* plus: 5 sockets

Sockets are presentation targets, not capacity truth. Runtime capacity remains derived from the
shape footprint.

Later cat presentation should choose the closest available socket inside the cat's already-assigned
hole. It must not choose a different hole based on visual distance.

---

## Isolated Completion Sequence

The presentation component owns this sequence:

```text
validate references
    ->
tween cap blend-shape weight from open to closed
    ->
tween PresentationRoot scale to zero
    ->
invoke completion callback exactly once
```

The component must:

* reject duplicate play requests while a sequence is active
* resolve the blend shape by name instead of relying on an unstable imported index
* capture the authored presentation scale and open cap weight for reset
* kill owned tweens without invoking completion when disabled or destroyed
* avoid changing runtime lifecycle state directly

The callback is intentionally a direct callback, not a global event or service locator.

---

## Runtime Handshake

The corrected FBX and isolated tween passed manual validation. Full-hole completion is therefore
split into:

```text
hole becomes Full
    ->
runtime releases stale occupancy and enters Closing
    ->
integration optionally aligns the view to the nearest valid footprint-origin cell
    ->
view plays completion presentation
    ->
view callback
    ->
runtime finalizes Completed
    ->
outcome routing may request Won
```

The runtime disables dragging before presentation starts. A scene-level toggle chooses whether the
view first aligns to the nearest valid footprint-origin cell or closes from its final freeform drag
position. Alignment is visual-only: it uses framework snap validation but does not mutate committed
coordinates or occupancy. The view keeps its renderers alive while the hole is `Closing`, then the
callback allows runtime finalization, visual destruction, and outcome routing in that order.

Missing or invalid presentation retains an immediate callback fallback so placeholder scenes and
tests do not deadlock. Animation never becomes gameplay authority.

---

## Validation

The isolated prefab and integrated runtime validation confirmed:

* a full hole stops responding to pointer drag before the animation starts
* the hole remains visible while `Closing`
* `PresentationRoot` shrinks while the logical root and collider hierarchy remain stable
* all configured selection colliders disable together when the hole stops being selectable
* the completion callback finalizes the hole exactly once
* the completed view is destroyed only after the callback
* win routing occurs only after `Completed`
* restarting or loading another level leaves no active tween or destroyed-reference errors
* the eight canonical unrotated footprints resolve to eight distinct configured hole prefabs

The remaining manual A/B check is presentation-only:

* with the toggle enabled, the full hole aligns to the nearest valid cell before cap closing starts
* with the toggle disabled, the full hole closes from its final freeform drag position
* visual alignment does not change `CurrentCoordinate` or structural occupancy

Automated or focused fallback-path coverage should still verify:

* a placeholder hole without presentation completes immediately
* disabling or destroying the view cancels the tween without a late callback

---

## Deferred Work

* production receiver materials and complete URP auxiliary passes
* cat falling animation and socket reservation
* rotated-shape, root-alignment, collider, and completion validation for the seven non-single
  prefabs
* final cavity lighting policy; current sharp inner-wall self-shadows remain an art review item
* audio, particles, haptics, and UI feedback
