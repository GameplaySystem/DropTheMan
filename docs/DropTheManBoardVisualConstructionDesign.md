# Drop The Man Board Visual Construction Design

## Purpose

This document defines how `Drop The Man` should construct board cell, straight-wall, and
corner visuals from authored level data.

It establishes the structural algorithm and approved modular piece profile implemented by the
framework and the Drop The Man scene adapters. The concrete prefab is assembled, wired into the
editor config and gameplay scene, and manually validated in Unity.

This design does not change gameplay coordinates, JSON schema, occupancy, drag rules, or
runtime board authority.

Concrete Drop The Man board materials now use the prototype's URP baseline documented in
`DropTheManRenderingPipelineDesign.md`. Wall topology, modular slot planning, and framework cell
view contracts remain render-pipeline agnostic.

---

## Architecture Review

### Restated Problem

Drop The Man levels use rectangular dimensions plus blocked coordinates, but the visible board
may contain internal holes, edge cutouts, and irregular silhouettes. Board visuals must be
generated consistently for both outer boundaries and those cutouts without hand-authoring every
wall.

The supplied art set is expected to contain:

* one cell visual
* a `0.5`-cell straight half-wall piece
* a convex outer-corner pillar/cap with a `0.145` footprint
* a concave inner-corner L piece with a `0.145` corner core and two `0.355` arms

### Key Assumptions

* the board plane remains Unity XZ
* authored grid coordinates remain cell centers
* Drop The Man blocked coordinates represent visually absent board cells
* no cell base, wall children, or other board geometry is rendered at a blocked coordinate
* walls are rendered on participating cells adjacent to absent space
* the convex pillar intentionally covers the intersection of two `0.5` half-walls
* the concave L replaces the two `0.5` half-walls touching that vertex
* `0.145 + 0.355 = 0.5`, so each concave arm reaches the midpoint of a one-cell edge
* optional wall and corner objects are positioned and rotated in the cell prefab before runtime

### Risks

* framework `Blocked` metadata does not generically mean visually absent, so that mapping cannot
  be embedded in shared wall-generation logic
* the convex cap must hide intersecting wall ends without coplanar z-fighting
* incorrect inner-L ownership or rotation would suppress half-walls on the wrong neighboring cells
* one unordered Inspector list would make directional prefab configuration fragile
* cells touching only diagonally create two boundary turns at the same vertex and may not be
  supported cleanly by the art set
* separate editor and gameplay adapters could drift if they bypass the shared framework systems

### Simpler Solutions Considered

Hand-author every wall in each level.

Rejected because walls would duplicate board shape data and drift whenever a level changes.

Determine corners from whichever wall GameObjects happen to be enabled.

Rejected because scene presentation would become the structural source of truth and corner
classification would become sensitive to prefab setup order.

Treat every framework blocked cell as absent board geometry.

Rejected because blocked is generic structural metadata. Drop The Man owns the visual-hole
interpretation.

### Recommended Approach

Use a framework-owned topology and modular-visual pipeline:

```text
Drop The Man authored/runtime board data
    ->
Drop The Man creates a visual-presence mask
    ->
framework Wall Generation derives edges and geometric corners
    ->
framework Modular Board Visual System derives and applies cell-prefab slot state
    ->
Drop The Man supplies its concrete prefab and art assets
```

Drop The Man should not reimplement boundary or slot-activation logic. It only maps its blocked
coordinates to absent visual space and requests the reusable framework build.

---

## Visual-Presence Mapping

For the current Drop The Man data model:

```text
coordinate is inside declared dimensions
and coordinate is not blocked
    -> visible board cell participates

coordinate is blocked or outside dimensions
    -> absent board space does not participate
```

Blocked coordinates therefore carve holes and notches out of the visible board. A blocked cell
does not instantiate a cell visual. Its participating neighbors receive exposed walls facing the
empty coordinate.

This is a prototype presentation rule. It must not redefine framework-wide blocked-cell meaning.

The current JSON schema does not separately represent a rendered-but-non-enterable obstacle.
That distinction should not be added until Drop The Man requires it.

---

## Boundary Algorithm

### Straight Walls

For each visible cell, inspect North, East, South, and West.

Each cell side has two pre-positioned `0.5` half-wall slots. When the neighboring coordinate is
not in the visual-presence mask, initially enable both halves on that side. This applies equally
to the board exterior, a blocked-cell hole, and an edge notch.

### Corners

Classify each grid vertex once by inspecting the four cell quadrants touching it:

| Visible quadrants | Result |
| --- | --- |
| 0 | No corner |
| 1 | Convex corner, mapped to the outer-corner visual |
| 2 orthogonally adjacent | No corner; boundary is straight |
| 2 diagonally opposite | Diagonal-touch diagnostic; two coincident convex turns |
| 3 | Concave corner toward the missing quadrant, mapped to the inner-corner visual |
| 4 | No corner |

For modular cell-prefab application:

* the sole visible cell owns a convex pillar at its local corner
* the convex pillar caps the two enabled half-walls that meet at the vertex
* the visible cell diagonally opposite the missing quadrant owns a concave L at its local corner
* the concave L replaces the two touching half-walls, so those specific halves are disabled on
  the orthogonally adjacent visible cells
* diagonal-touch topology must be reported and deliberately handled after the assets are tested

These are board-level decisions. Individual cell components must not derive them independently.

---

## Cell Prefab Responsibilities

The reusable framework cell-view component should expose explicit serialized references for the
visual slots the art set supports. The Drop The Man cell prefab assigns those references to its
pre-positioned child objects.

The expected logical slots are:

* cell base
* eight half-walls: two for each North, East, South, and West side
* four convex pillars: NorthEast, SouthEast, SouthWest, and NorthWest
* four concave L pieces: NorthEast, SouthEast, SouthWest, and NorthWest

Inspector directions use board-local coordinates:

* North is increasing `GridCoordinate.Y`, which is world `+Z` in the current scenes
* East is increasing `GridCoordinate.X`, which is world `+X` in the current scenes
* `NorthWestHalfWall` means the western half of the cell's north side
* `EastNorthHalfWall` means the northern half of the cell's east side
* corner names identify the local corner of the owning cell, not the direction the mesh faces

Directional fields or validated enum-indexed arrays are acceptable. A single generic list whose
meaning depends on Inspector order is not acceptable.

All optional pieces should default inactive in the production prefab. The framework component
must reset them before applying a new visual state so editor rebuilds cannot retain stale pieces.

The component should only apply derived presentation state. It should not query occupancy, decide
movement, mutate level data, or infer topology from currently active child objects.

The current Drop The Man asset contract is:

```text
Half wall length:          0.5 cell
Convex pillar footprint:   0.145 cell
Concave corner core:       0.145 cell
Concave arm extension:     0.355 cell per arm
Concave total reach:       0.5 cell per direction
```

Half-wall pivots use the horizontal center and vertical base. Convex-pillar pivots use the
horizontal footprint center and vertical base. Concave-L pivots use the logical bend/grid vertex
and vertical base. All imported transforms should use scale `1,1,1`.

---

## Runtime And Editor Use

Gameplay and level-editor scenes should consume the same framework Wall Generation and Modular
Board Visual systems. They may have separate scene adapters, but they must not maintain separate
corner or slot-activation algorithms.

Gameplay runtime:

* builds the visual mask from the loaded level
* asks the framework visual builder to construct participating cell visuals
* supplies the Drop The Man cell prefab and board-to-world layout

Play-mode level editor:

* rebuilds the same visual result after dimension or blocked-cell edits
* keeps authoring data as truth
* uses the same framework visual builder as a preview, not as authored wall data

Walls and corners remain derived. They are not added to JSON.

---

## Framework Boundary

The approved framework Wall Generation System owns:

* explicit boundary-participation input
* exposed edge derivation
* convex and concave corner classification
* diagonal-touch reporting

The approved framework Modular Board Visual System owns:

* exposed-edge conversion into two half-wall slots
* convex-cap activation while retaining touching half-walls
* concave-elbow activation and replacement of touching half-walls
* generic directional cell-view contracts
* reusable visual planning, application, and rebuild behavior

DropAwayPrototype owns:

* mapping blocked cells to visually absent space
* the concrete cell prefab and assigned child references
* concrete meshes, materials, dimensions, and art orientation
* gameplay/editor scene adapters that request framework board construction
* puzzle-specific gameplay meaning of absent space
* concrete URP materials, shaders, and any future stencil-based board/hole presentation

The reusable algorithm must be implemented in PuzzleFramework rather than copied into
DropAwayPrototype.

---

## Validation Matrix

The first implementation should be checked against these masks before scene integration:

* one visible cell: four walls and four convex corners
* full rectangle: perimeter walls, four convex corners, no concave corners
* 3x3 with blocked center: an empty center, four inward-facing walls, and four concave corners
  around the hole
* edge notch: walls and concave corners follow the inward cutout
* L-shaped silhouette: both convex exterior turns and one concave turn classify correctly
* multiple adjacent blocked cells: one continuous internal or edge boundary without duplicate
  walls
* disconnected regions: each region receives a complete boundary
* diagonal-only contact: diagnostic is raised and presentation behavior is reviewed with the
  final assets
* all cells blocked: no cell, wall, or corner visuals
* repeated editor rebuilds: no stale active slots or duplicate cell instances

Useful deterministic count checks include:

```text
1x1 visible cell
-> 4 exposed edges, 4 convex corners, 0 concave corners

3x3 full rectangle with center blocked
-> 16 exposed edges, 4 convex corners, 4 concave corners
```

---

## Intentionally Deferred

This design does not yet approve:

* prefab creation or scene wiring
* collider generation from wall visuals
* wall-owned gameplay collision
* JSON schema changes
* diagonal-touch presentation policy
* mesh combining or runtime batching

---

## Implementation And Validation Status

Implemented:

1. Framework boundary topology derives exposed edges and vertex classifications from an explicit participation mask.
2. Framework visual planning produces eight half-wall, four convex-corner, and four concave-corner slots per visible cell.
3. `ModularBoardCellView` exposes named references for the cell base and all 16 optional pieces.
4. The framework builder resets and rebuilds presentation-only cells under a dedicated root.
5. Drop The Man editor and gameplay adapters exclude blocked cells and invoke the same framework path when a modular prefab is assigned.
6. Existing editor checkerboard and gameplay visuals remain available while no modular prefab is assigned.

Validated integration:

1. `ModularBoardCellView` is attached to the production cell prefab with 17 unique assigned objects.
2. All eight half-walls, four convex corners, and four concave corners default inactive.
3. The prefab is assigned to `DropTheManEditorConfig.editorCellPrefab` and the gameplay bootstrapper.
4. The owner confirmed the generated board visuals work correctly in Unity.
5. Duplicate imported models and materials were removed; retained models share canonical border, grid, and cell materials.

Remaining limitation:

* diagonal-only participating-cell contact remains an explicit unsupported visual topology
