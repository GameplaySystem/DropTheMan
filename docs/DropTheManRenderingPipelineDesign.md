# Drop The Man Rendering Pipeline Design

## Purpose

This document records the rendering-pipeline baseline for `DropAwayPrototype` and the ownership
boundary for future pipeline-specific presentation work.

It does not change gameplay rules, authored level data, board topology, runtime construction, or
framework contracts.

---

## Approved Baseline

`DropAwayPrototype` uses Universal Render Pipeline `17.3.0` with Unity `6000.3.17f1`.

The project-owned rendering assets live under:

```text
Assets/Settings/Rendering/
```

The baseline configuration uses:

* one Forward universal renderer
* one URP asset assigned in Graphics Settings and every Quality tier
* URP Lit for the existing project materials
* HDR enabled
* SRP Batcher enabled
* main-light shadows enabled
* additional-light shadows disabled
* depth and opaque textures disabled until a concrete effect requires them
* no custom renderer features
* no project-wide post-processing profile

These settings are a conservative functional baseline, not final mobile performance tuning.

---

## Ownership Boundary

DropAwayPrototype owns:

* URP package and project settings
* pipeline, renderer, global-settings, and volume-profile assets
* concrete materials and Shader Graph or shader assets
* renderer features required by prototype presentation
* profiling and visual tuning for the shipped prototype

PuzzleFramework remains render-pipeline agnostic. Framework board systems continue to derive
topology and generic visual slot state without depending on URP types, shaders, materials, or
renderer features.

Concrete prototype presentation may consume framework output, but framework code must not depend
on this rendering configuration.

---

## Stencil Hole Presentation

The planned hole-depth experiment is a separate prototype-owned presentation slice. Its likely
shape is:

```text
hole mask/writer
    -> writes a stencil value
board and wall receiver materials
    -> use that value to suppress pixels inside the opening
inner hole mesh
    -> renders the visible cavity/interior
```

The exact stencil compare operations, render queue, depth behavior, and mesh contract must be
validated with the real hole assets before becoming an approved production setup.

Concrete hole hierarchy, stencil-aperture, cap, root, and collection-socket requirements are
defined in `DropTheManHolePresentationDesign.md`.

Do not add a URP renderer feature merely because URP supports one. Prefer material/shader stencil
state first; add a renderer feature only if ordering or pass control cannot be solved cleanly by
the participating materials.

### Material-Only Proof Of Concept

The first approved experiment uses two prototype-owned test materials without layers or renderer
features:

* an invisible writer renders at queue `Geometry-1`, writes stencil reference `1`, writes no color
  or depth, and uses `ZTest LEqual`
* a Lit receiver renders at queue `Geometry`, uses URP's Lit input/forward implementation with the
  stencil state declared directly on its ForwardLit pass, and exposes both stencil reference and
  comparison as material properties
* board receivers use `NotEqual` so pixels under reference `1` are suppressed
* the inner-cavity test material uses `Equal` so cavity pixels render only through the aperture
  that wrote reference `1`
* the writer and receivers expose matching stencil-reference properties so another value can be
  tested without editing shader source

The experiment assets live under:

```text
Assets/RuntimeAssets/Rendering/StencilTest/
```

The proof includes separate stencil-aware grid and cell materials that preserve the current
artist-authored base colors and surface values. They replace the existing two material slots during
testing; they are never appended as an extra pass. The originals remain unchanged.

`Hole_Inner_Cavity_Stencil.mat` is the validated real-hole cavity material baseline. It preserves
the current dark-blue inner material values, uses backface culling, and selects stencil comparison
`Equal` with reference `1`. It replaces `Holes_Inner_Material` on the inner-wall submesh; it is not
appended as an additional material slot. Game-view validation confirmed that the cavity remains
visible through the aperture without rendering below or outside its silhouette.

This is still not the final cell shader. The receiver currently defines only its ForwardLit pass
and does not define dedicated shadow-caster, depth-only, depth-normals, meta, or motion-vector
passes. Its purpose is to validate stencil availability, normal Lit appearance, ordering, camera
behavior, and the future hole opening mesh contract before production shader-pass requirements are
approved. An earlier `UsePass` experiment was rejected because its surrounding stencil state did
not affect the imported URP pass reliably.

---

## Validation

After pipeline or material changes, manually check both dedicated scenes:

* `DropTheManLevelEditor` renders the board and authoring previews without pink materials
* `DropTheManDevTest` loads and plays a JSON level without rendering regressions
* board cells, half-walls, convex corners, and concave corners retain their intended colors
* inner cavity walls appear through the stencil aperture but not below or outside its silhouette
* lighting and shadows remain acceptable in the Game view
* the Console has no shader, renderer, or missing-reference errors

Final mobile quality settings, post-processing, the stencil hole effect, and final cat/hole/UI
materials remain deferred.
