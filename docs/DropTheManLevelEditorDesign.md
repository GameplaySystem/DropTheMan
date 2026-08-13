# Drop The Man Level Editor Design

## Purpose

This document defines the first concrete `Drop The Man` level editor direction.

It exists to translate the approved framework `LevelEditorFoundation` design into a
prototype-owned authoring plan for `DropAwayPrototype`.

This is editor design only.

It does not implement:

* full visual editor interaction
* runtime spawning
* play button behavior
* collection presentation timing
* animation
* undo or redo
* a broad plugin-style editor framework

---

## Problem

The framework already documents a generic level editor foundation, but `Drop The Man`
does not yet have a concrete editor architecture that answers:

* what the board view should look like
* how board resizing should behave
* which placement modes exist
* which hotkeys control those modes
* how hole palette selection should work
* how authored blocked cells should be saved
* where editor config data should live
* what is intentionally deferred from the first editor slice

Without this document, the first editor implementation would be forced to invent
prototype-specific rules during coding.

---

## Architecture Review

### Restated Problem

Build the smallest clean authoring foundation for `Drop The Man` without pushing
puzzle-specific tools into `PuzzleFramework` or accidentally turning editor code into a
runtime spawning path.

### Key Assumptions

* the first editor slice should author data, not playable runtime scene objects
* the framework already owns generic authored level structure and JSON persistence
* `DropAwayPrototype` owns hole, stickman, and blocked-cell authoring meaning
* play button behavior is not required for this phase
* obstacle mode currently means blocked-cell painting, not separate obstacle entities

### Risks

* editor scope can balloon into a full scene tool before data foundations are stable
* a prototype editor can accidentally hard-code puzzle-specific assumptions into framework code
* a play button can drag runtime spawning into a content-authoring slice too early
* board resize can silently destroy authored content unless the rule is explicit
* color hotkeys can outgrow the current four-color framework enum unless the identity model is widened

### Simpler Solutions Considered

Put everything in one scene MonoBehaviour with ad hoc serialized lists.

Rejected because it would hide the editor data model, duplicate JSON logic, and mix
framework-safe editing concerns with puzzle-specific authoring.

Keep using hand-authored JSON and skip editor design until later.

Rejected because the project now needs a documented editor direction before puzzle-specific
authoring code expands further.

Add play button and runtime spawning in the same slice.

Rejected because current runtime scene wiring still assumes pre-placed runtime views, and
spawning is a separate prototype runtime concern.

### Recommended Approach

Use a two-layer editor direction:

```text
PuzzleFramework
    ->
generic level editor foundation concepts
    ->
DropAwayPrototype
    ->
Drop The Man-specific authoring config and tools
```

For Phase 1, implement only the data and configuration foundations needed before the
visual placement modes.

---

## Ownership Boundaries

### PuzzleFramework Owns

* generic authored level structure
* generic board cell states
* JSON save and load persistence
* generic editor-foundation direction
* shared color identities

### DropAwayPrototype Owns

* hole authoring
* stickman authoring
* blocked-cell painting meaning for `Drop The Man`
* hole palette data
* editor hotkeys and authoring modes
* editor-only hole rotation behavior
* prototype editor config assets

### Explicitly Not Owned By This Slice

* play button behavior
* runtime spawning from authored data
* collection timing
* animation
* runtime scene composition changes
* generic editor plugin architecture

---

## Board View

The intended editor board is a visible checkered grid where each visible square represents
one authored board cell.

Phase 1 does not implement the full interaction layer, but it does establish the board
authoring assumptions:

* board cells are shown through an editor cell prefab or equivalent visual settings
* the default authored board size is `5 x 5`
* board coordinates remain framework-standard `x, y` cell coordinates
* board visualization is editor-facing only and must not become runtime gameplay truth

---

## Board Dimension Regeneration Rule

The editor must regenerate its visible board when authored width or height changes.

Approved regeneration rule:

```text
author changes width or height
    ->
editor regenerates board visuals from authored data
    ->
cells inside new bounds are preserved
    ->
cells and placed items outside new bounds are dropped
    ->
editor reports that out-of-bounds authored content was removed
```

Reason:

* full wipe-on-resize is too destructive
* silent out-of-bounds survival would create invalid authored data

Phase 1 only establishes this rule. It does not yet implement the full interactive board.

---

## Supported Cell States

The framework already supports:

* `Active`
* `Blocked`
* `Inactive`

For the first `Drop The Man` editor phase:

* default cells are `Active`
* obstacle mode authors `Blocked` cells
* `Inactive` cell authoring is deferred

Reason:

* blocked-cell painting is the requested current obstacle interpretation
* inactive cells are framework-supported, but they do not yet have a concrete `Drop The Man`
  authoring need in this slice

Deferred rule:

* if inactive-cell authoring becomes necessary later, add it as a separate documented mode
  rather than silently overloading obstacle mode

---

## Placement Modes

The intended authoring modes are:

* stickman placement mode
* hole placement mode
* obstacle mode

Current interpretation:

* stickman placement mode authors single-cell stickmen
* hole placement mode authors hole entries using the selected palette footprint and rotation
* obstacle mode paints blocked cells

Obstacle mode does not create separate obstacle entities in this slice.

---

## Hotkeys

The planned hotkeys are:

* `M` = stickman placement mode
* `H` = hole placement mode
* `O` = obstacle mode
* `0` through `9` = select shared color slot
* mouse scroll = cycle configured hole palette entries
* `R` = rotate the currently selected hole footprint

Hotkey meaning remains prototype-owned editor behavior, not framework runtime input behavior.

---

## Color Slot Model

The editor uses ten stable shared color slots:

* `Slot0`
* `Slot1`
* `Slot2`
* `Slot3`
* `Slot4`
* `Slot5`
* `Slot6`
* `Slot7`
* `Slot8`
* `Slot9`

For compatibility with the current prototype content:

* legacy names such as `Red`, `Blue`, `Green`, and `Yellow` remain valid aliases for the
  first four slots

Approved hotkey mapping:

* `0` maps to `Slot0`
* `1` maps to `Slot1`
* `2` maps to `Slot2`
* `3` maps to `Slot3`
* `4` maps to `Slot4`
* `5` maps to `Slot5`
* `6` maps to `Slot6`
* `7` maps to `Slot7`
* `8` maps to `Slot8`
* `9` maps to `Slot9`

This keeps the framework color model game-agnostic while preserving current prototype data.

---

## Hole Palette Selection

Hole shape selection is prototype-owned and cycles through a configured list of hole palette
entries using mouse scroll.

Each palette entry should be able to describe:

* display name
* footprint offsets
* default or current color identity
* optional preview prefab

The palette is data-driven through a prototype-owned config asset.

The framework editor foundation must not know what a `hole` is.

---

## Rotation Rule

Hole rotation is editor-only.

Runtime gameplay continues to use authored orientation exactly as documented by the shared
`ShapeSystem`.

This means:

* editor tools may rotate a selected hole footprint while authoring
* runtime gameplay does not rotate holes dynamically

---

## DropTheManEditorConfig

The prototype should own a `DropTheManEditorConfig` `ScriptableObject`.

Its job is authoring configuration, not runtime gameplay state.

It should be ready to hold:

* editor cell prefab or visual settings
* stickman preview prefab
* blocked-cell visual or material
* hole palette entries
* color materials for ten color slots
* default board size

This asset belongs in `DropAwayPrototype`, not in `PuzzleFramework`.

Reason:

* the asset contains prototype-specific authoring tools and visuals
* the framework editor foundation must remain puzzle-agnostic

---

## JSON Save/Load Ownership

The editor authors data.

The framework save/load service persists that data.

`Drop The Man` editor-specific authoring data should flow through the existing content path:

```text
Drop The Man editor tools
    ->
Drop The Man authored data
    ->
framework LevelDefinition
    ->
framework JSON save/load
```

Prototype-owned JSON additions for this phase:

* board width
* board height
* blocked cell coordinate list

Deferred from this phase:

* inactive cell lists
* runtime scene object references
* drag state
* collection state
* runtime spawning metadata

Default rule:

* cells not listed as blocked are authored as active

---

## Play Button Decision

Play button behavior is explicitly out of scope for Phase 1.

Reason:

* the current runtime playable-scene path still assumes pre-placed runtime view objects
* a real play button would drag runtime spawning or a temporary preview-runtime path into this slice

If a play button is added later, it should be documented as a separate prototype runtime
integration step.

---

## Phase 1 Scope

Phase 1 should implement only:

* this concrete editor design baseline
* ten shared color identities with legacy compatibility
* blocked-cell authored data support
* prototype editor config asset foundation

Phase 1 should not implement:

* click placement behavior
* drag placement behavior
* board hover previews
* play button behavior
* runtime spawning
* collection timing
* animation
* undo or redo
* polished UX

---

## Deferred Work

The following are intentionally deferred:

* visual board interaction
* actual placement mode input handling
* hole palette preview UX
* inactive-cell authoring mode
* play button behavior
* runtime view spawning
* runtime preview scene generation
* undo or redo
* generic plugin-style editor extensibility

---

## Final Summary

`Drop The Man` editor work should start with data foundations, not with a full scene tool.

Phase 1 establishes:

* a checkered board-based editor direction
* a default `5 x 5` board
* board regeneration on size change
* stickman, hole, and blocked-cell authoring modes
* documented hotkeys
* ten shared color slots
* editor-only hole rotation
* prototype-owned editor config
* blocked-cell JSON authoring support

Play button behavior, runtime spawning, and full visual placement interaction remain separate
future slices.
