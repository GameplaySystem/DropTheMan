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

Do not add a URP renderer feature merely because URP supports one. Prefer material/shader stencil
state first; add a renderer feature only if ordering or pass control cannot be solved cleanly by
the participating materials.

---

## Validation

After pipeline or material changes, manually check both dedicated scenes:

* `DropTheManLevelEditor` renders the board and authoring previews without pink materials
* `DropTheManDevTest` loads and plays a JSON level without rendering regressions
* board cells, half-walls, convex corners, and concave corners retain their intended colors
* lighting and shadows remain acceptable in the Game view
* the Console has no shader, renderer, or missing-reference errors

Final mobile quality settings, post-processing, the stencil hole effect, and final cat/hole/UI
materials remain deferred.
