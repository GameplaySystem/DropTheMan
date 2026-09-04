# Drop The Man

A Unity puzzle-game prototype built on the reusable
[Puzzle Framework](https://github.com/Find-Games/PuzzleFramework). Drag colored holes across the
board, collect matching cats while moving, fill every required hole, and finish the level before
the timer when one is enabled.

This repository has two purposes: deliver a playable reconstruction of the *Drop Away* core loop,
and act as the first real consumer used to prove or reject abstractions in Puzzle Framework. Generic
grid, interaction, content, runtime-flow, presentation-foundation, and persistence concerns belong
to the framework. Hole rules, cat collection, level policy, visuals, and authoring remain here.

## Current Features

- freeform pointer dragging constrained by grid shape, board bounds, blockers, occupancy, and color
- same-color collection during movement with capacity derived from hole footprint size
- asynchronous cat presentation with six falling variants, live moving-hole socket tracking,
  shrinking, and callback-driven fill
- eight concrete hole shapes with quarter-turn footprint resolution and completion presentation
- modular board visuals, URP stencil apertures, color-specific materials, and dynamic camera framing
- JSON-authored levels discovered through an ordered Resources catalog
- a dedicated in-game level-authoring scene with placement, erasing, blocked cells, and hole rotation
- local campaign persistence, completed-level replay, explicit campaign resume, and configurable
  looping after all shipped levels are complete
- three authored prototype levels

## Technology

- Unity `6000.3.17f1`
- Universal Render Pipeline `17.3.0`
- C# and Unity assembly definitions
- DOTween Core for prototype-owned collection and hole presentation
- Puzzle Framework pinned through Unity Package Manager to an immutable Git commit

## Getting Started

1. Clone this repository.
2. Open the repository root in Unity `6000.3.17f1`.
3. Allow Unity Package Manager to resolve the pinned Puzzle Framework revision.
4. Open `Assets/Scenes/DropTheManDevTest.unity`.
5. Enter Play Mode and drag a hole with the primary mouse button.

The gameplay scene loads the first unfinished level from
`Assets/Resources/DropTheMan/Levels`. Editor Play Mode uses a separate
`progress.editor.json` sandbox under `Application.persistentDataPath`; it does not write the normal
player profile. The current gameplay and progression controls are functional development UI and
will be replaced by authored Canvas/prefab UI before a player-facing build.

## Level Editor

Open `Assets/Scenes/DropTheManLevelEditor.unity` and enter Play Mode. The primary authoring workflow
uses the runtime HUD plus these shortcuts:

| Input | Action |
| --- | --- |
| `O` | Blocked-cell mode |
| `M` | Cat placement mode |
| `H` | Hole placement mode |
| `R` | Rotate an already placed hole on click |
| `0`-`9` | Select a configured color slot |
| Left click | Apply the active placement action |
| Right click | Erase when right-click erase is enabled |

Level JSON can be imported/exported through the editor HUD. Shipped content belongs in
`Assets/Resources/DropTheMan/Levels` and uses stable canonical IDs such as `Level 1`; existing IDs
must not be repurposed merely to reorder content.

## Architecture

```text
Pointer/authoring input
        |
Game-owned scene adapters and Drop The Man rules
        |
Puzzle Framework requests, state, validation, and persistence
        |
Game-owned views, animation, tweening, UI, and authored assets
```

The framework does not interpret holes or cats, determine this game's win condition, or own its
replay/loop policy. Runtime victory is accepted only after all required holes complete; collection
overlap or a cat entering its animation is not sufficient. Progress is saved on the accepted win,
before the player presses **Next Level**.

## Tests

Run **Window > General > Test Runner > EditMode** in Unity. The current prototype suite contains 27
passing tests covering progression policy/session behavior, replay continuation, loop boundaries,
save failure handling, terminal outcome timing, and Editor profile isolation.

Level 3 has also been checked for unique IDs, board bounds, placement overlap, and matching per-color
hole capacity. Manual visual, replay/relaunch, aspect-ratio, and target-device acceptance checks are
still tracked separately.

## Project Status

The core gameplay, content pipeline, authoring foundation, animation integration, and first
progression slice are implemented. The next production-facing milestone is real Unity UI for the
gameplay HUD, results, level selection, replay/resume, and save errors, followed by a development
device build and tuning based on that build.

See [Drop The Man Remaining Work](https://github.com/Find-Games/PuzzleFramework/blob/main/docs/DropTheManRemainingWork.md)
for the prioritized finish checklist.

## Detailed Documentation

- [MVP rules](docs/DropTheManMVPRules.md)
- [Movement and collection rules](docs/DropTheManMovementAndCollectionRules.md)
- [Runtime integration](docs/DropTheManRuntimeIntegrationDesign.md)
- [Level editor design](docs/DropTheManLevelEditorDesign.md)
- [Progression design](docs/DropTheManProgressionDesign.md)
- [Progression implementation handoff](docs/DropTheManProgressionImplementationHandoff.md)
- [Cat animation integration handoff](docs/DropTheManCatAnimationIntegrationHandoff.md)

## Scope Boundaries

Cloud saves, multiple profiles, rewards/stars, mid-level resume, adaptive endgame, monetization,
and reusable level-editor extraction are not part of the current finish. Broader abstractions move
into Puzzle Framework only after another game demonstrates the same need.
