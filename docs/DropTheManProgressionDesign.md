# Drop The Man Progression Design

Status: Approved first slice, 2026-09-02 owner discussion.
Replay continuation amendment approved 2026-09-03.

## Scope And Ownership

This milestone adds local progression, completed-level replay and configurable post-campaign
looping. Framework owns progress state/snapshot validation and safe JSON persistence. The game
owns level order, unlock/replay rules, loop range, save timing and startup selection. Authored level
JSON and runtime board state are unchanged. No mid-level resume, rewards, cloud or polished menu.

Framework sources: `PlayerProgressDataSystem.md`, `ProgressSaveLoadSystem.md` and progression
overview; integration follows the existing outcome, runtime and scene-adapter designs. Accepted
`Won` still occurs only after required holes finish completion, never from cat/overlap facts.

## Player Rules

* Start at the first unfinished shipped level. Loss, restart and quitting mid-level do not advance.
* Save completion and the next campaign level at accepted `Won`, before any Next button click.
* Completed levels can be selected for replay. Replay never rewinds/advances the campaign cursor.
  Closing during replay returns to campaign Continue on launch; replay mode is session-only.
* After a replay win, Next Level follows catalog order through completed levels. At the first
  unfinished successor, resume normal campaign play; never skip an earlier unfinished level if
  new content has introduced a gap. At the end of the shipped catalog, return to saved campaign
  Continue (including its configured loop). Completed successors remain replay even when their
  ID matches the saved loop cursor. Only the transition back to campaign can advance that cursor.
* Next Level requires an accepted win and only selects a destination; successful construction
  begins the next session. Failed loads/repeated selection queries must not change progress.
  Keep a separate Resume Campaign action in the level picker, available without completing replay.
* When every shipped level is completed, cycle the configured inclusive level-number range in
  catalog order. Range defaults to first=1, last=0 (last shipped level). Missing numbers are skipped;
  empty/reversed ranges are configuration errors, not silent unlocks. Single-level ranges are valid.
* The first post-campaign selection is the first level in the range. Subsequent loop wins advance
  and wrap. Loop play is campaign Continue, not a manually selected replay.
* Save only stable completed IDs and a resume ID, not numeric indexes or loop policy. New shipped
  unfinished levels take priority over looping; removed IDs remain in history but do not create
  catalog entries. Level IDs must not be repurposed or renumbered when reordering content.
* Replacing the looping rule later changes the game-owned selection method, not the save format.

## Persistence And Failure Behavior

Use `Application.persistentDataPath/DropTheMan/progress.json` in a player. Editor Play Mode defaults
to a separate `progress.editor.json`; a serialized toggle can disable progression for authored
starting-level tests. The level editor scene does not write progress. Inspector-only dev levels
also bypass progression because they are not a shipped catalog.
Set the editor toggle and loop range before entering Play Mode; live settings changes are not
supported by this first slice. Loop bounds are authored sequence numbers, not array indexes.

Load before choosing/constructing the first level. Missing save creates fresh progress. Invalid,
unsupported, or unreadable saves show an error and block progression startup without overwriting
the file; offer Retry Load. Write failures retain in-memory progress and show a visible unsaved
warning with Retry Save. Retry also on pause/quit when dirty, but normal durability comes from the
win callback. Restart/load-next must not reconstruct or reset the profile.

Subscribe directly to the active runtime controller's once-only accepted terminal notification;
unsubscribe before replacing its level so late callbacks cannot affect the new session. Completion
notification is not an event bus and persistence does not decide gameplay outcomes.

## UI And Validation

Extend the existing dev IMGUI controls with Next Level, Resume Campaign and a paged completed-level picker.
Opening the picker cancels drag and disables pointer input; the timer is not paused. Successful
level load closes the picker. Keep save errors visible independently of the win/result window.

Test snapshot copies/duplicates/missing fields, file roundtrip/replacement/errors, cold restart,
win-before-Next persistence, loss/restart, replay isolation, loop wrap/range changes/single level,
new/removed content, duplicate callbacks, editor save isolation and corrupted-save preservation.
Framework tests and push must precede the consuming package-pin change. Do not commit the game
slice until the owner requests it. No unrelated scene/material/content edits belong to this task.

## Implementation Status (2026-09-03)

Framework implementation published as `96e9b7751686f2652c0374a40841e74c96c74c9f`; prototype manifest
and Unity-resolved lockfile now pin that revision. Normal Unity compilation and all 27 prototype
Edit Mode tests passed against it, following the original 38-test isolated validation. Seven added
tests cover the approved replay continuation amendment, including saved-loop identity and retries.
The gameplay scene starts successfully with the Editor sandbox HUD. Stopped without completing a
level or creating a profile, leaving the owner's win/quit/reopen check fresh. No scenes or normal
save files changed. Owner approved the scoped progression commit/push on September 3. See
`DropTheManProgressionImplementationHandoff.md` for contracts and verification details.
