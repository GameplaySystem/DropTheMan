# Progression First Slice: Implementation Handoff

Date: 2026-09-02; package adoption and replay continuation verified 2026-09-03.

Status: framework committed/pushed as `96e9b7751686f2652c0374a40841e74c96c74c9f` with owner approval.
Prototype manifest and Unity-resolved lockfile pin that published revision. Normal-project
compilation, all 27 prototype Edit Mode tests and gameplay-scene startup passed. Owner approved
committing/pushing the game-side progression slice. Manual win/stop/reopen acceptance and target-device checks
remain; the automated coverage includes cold reload and accepted-win persistence.

## 0. Preflight

Framework documents read:

* `docs/PROJECT_STATE.md`, `docs/IMPLEMENTATION_WATCHLIST.md`, `docs/CRITICAL_RULE_CLARIFICATIONS.md`.
* `docs/FrameworkArchitecture.md`, `docs/ImplementationRoadmap/FrameworkMVPPlan.md`.
* `docs/FrameworkSystems/ProgressionSystems/Overview.md`, `PlayerProgressDataSystem.md`,
  `ProgressSaveLoadSystem.md` in that directory.
* `docs/Workflow/FrameworkPackageDependencyWorkflow.md`, `DailyMilestoneWorkflow.md`.

Prototype design baseline consulted:

* `docs/DropTheManOutcomeRoutingDesign.md`, `DropTheManRuntimeIntegrationDesign.md`.
* `docs/DropTheManPlayableSceneAdapterDesign.md`, `DropTheManMVPRules.md`.
* `docs/DropTheManMovementAndCollectionRules.md`, `DropTheManCollectionPresentationTimingDesign.md`.
* New approved first-slice decisions recorded in `docs/DropTheManProgressionDesign.md` before code.

Rules: framework owns state/schema/persistence, game owns unlocks, sequence and save timing.
Accepted win remains after required hole completion, not collection trigger or cat arrival.
The older roadmap deferred progression; owner approval explicitly activates this slice. Docs
were updated accordingly. Package adoption must follow framework publication, not a local path.
The September 3 replay amendment was documented before implementation: Next Level chains completed
replays, while Resume Campaign explicitly returns to saved progression. No framework/schema change.

## 1. What Changed

Framework paths relative to `Packages/com.gaming.puzzleframework`:

* `Runtime/Progression/PlayerProgressData.cs`: completion history, resume ID, copied versioned snapshot,
  schema-only all-or-nothing restoration; no catalog dependency.
* `Runtime/Progression/IProgressSaveLoadService.cs`: storage boundary and explicit load/save results.
* `Runtime/Progression/JsonProgressSaveLoadService.cs`: JSON load, sibling temporary file, flush,
  replacement/move and cleanup. Failed replacement never uses delete-then-write.
* `Tests/EditMode/ProgressionSystemTests.cs`: 18 state/schema/file tests. Added source/folder metas.

Prototype paths relative to `Assets/GameModules`:

* `Runtime/DropTheManLevelProgression.cs`: ordered campaign, replay eligibility/successor, loop range/cursor.
* `Runtime/DropTheManProgressionSession.cs`: load lifetime, active selection, once-only win, dirty retry,
  read-only post-win destination selection.
* `Runtime/DropTheManRuntimeController.cs`: once-only accepted terminal event after stopping input.
* `Runtime/DropTheManDevSceneBootstrapper.cs`: profile composition, startup selection, subscription
  lifetime, Continue/Replay APIs, temporary paged picker, errors/retry, Editor sandbox path.
* `Tests/EditMode/DropTheManProgressionTests.cs`: 27 policy/session/runtime callback/Editor path tests.
* `Tests/EditMode/DropAwayPrototype.Tests.EditMode.asmdef`: Editor test assembly using existing test
  framework dependency. Added source, assembly and folder metas.

Documentation: framework progression specs, MVP roadmap, project state/watchlist; prototype
progression design and this handoff. Prototype `Packages/manifest.json` and Unity-generated
`Packages/packages-lock.json` now pin the published framework. No scene, prefab, animation or
material edits from this task.

## 2. Public Contracts

* `PlayerProgressSnapshot`: version-1 transport fields `FormatVersion`, `CompletedLevelIds`,
  `ResumeLevelId`; storage and migrations may use it, gameplay should use validated state.
* `PlayerProgressData`: `IsCompleted`, `MarkCompleted`, `SetResumeLevel`, `CaptureSnapshot`,
  `TryRestore`, version/count/resume accessors. Game policy records already-decided facts;
  this class must not determine wins, unlock rules or level availability.
* `IProgressSaveLoadService.Load/Save`: caller supplies path/state; returns `ProgressLoadResult`,
  `ProgressSaveResult`. `ProgressLoadStatus` distinguishes missing, invalid, unsupported and I/O
  failures. Used by a game session, not authored-level loading or runtime construction.
* `JsonProgressSaveLoadService`: local single-writer implementation, not cloud/multi-profile logic.
* `DropTheManLevelProgression`: `GetContinueLevelId`, `CanReplay`, `GetLevelAfterReplay`, `RecordWin`,
  `Progress`. The replay successor API returns both ID and mode without changing progress.
  Used by this game's session/UI. No framework caller or persistence code should reference it.
* `DropTheManProgressionSession`: `TryLoad`, `BeginLevel`, `RecordAcceptedWin`, `TrySave` and
  `TryGetNextLevelAfterWin`, plus state/error properties. Post-win selection requires an accepted
  win, is repeatable without side effects, and does not start a level. Scene adapter supplies only successfully built selections and accepted
  wins. Cat views, hole views and persistence must not call it to determine outcomes.
* `TerminalOutcomeAcceptedNow`: game runtime event; bootstrapper filters Won, ignoring Lost.
  Subscribe to the active controller only. Not a shared event bus or a new win evaluator.
* Bootstrapper `TryContinueCampaign`, `TryReplayLevel`, `ProgressSavePath`, `ProgressError`,
  `ProgressionEnabled`: game UI/debug integration points, not framework contracts.

## 3. Ownership Boundaries

No game-specific nouns, progression policy or presentation enter the framework. Framework does
not parse game payloads or build levels. The game still uses the existing content/catalog/build
pipeline; progress JSON is separate. No singleton, service locator or event-bus shortcut added.
Persistence failures do not reverse an accepted gameplay outcome.

## 4. Key Decisions And Tuning

* Save stable IDs, not array indexes, loop bounds, replay state or board snapshots.
* First unfinished shipped content takes priority. Removed IDs remain history, unavailable for
  replay. Do not repurpose existing level IDs; canonical `Level N` IDs currently couple identity
  with ordering, so renumbering content needs an explicit identity/migration decision.
* Once all content is complete, enter `loopFirstLevel`..`loopLastLevel` inclusively in sequence order.
  Last=0 means last shipped level; missing numbers are skipped. Invalid/empty ranges fail clearly.
  Defaults 1/0 loop the entire catalog. Configure on the gameplay bootstrapper before Play Mode.
* Changing future looping policy primarily changes `DropTheManLevelProgression`, not saved data.
  A future rule needing genuinely new history (e.g. loop counts) could still require new fields.
* Replay is session-only and never moves the campaign cursor. Restart after a win is a replay.
* Replay Next follows the next completed catalog entry. An unfinished successor returns to the
  earliest unfinished campaign entry (including inserted content); the final shipped replay returns
  to saved campaign/loop selection. Completed successors matching the saved loop ID remain replay.
  Resume Campaign remains a separate picker action. Relaunch discards the replay selection.
* Next selection and successful construction are separate. A failed load/repeated Next query cannot
  advance progress or start a session; no second saved replay cursor or new framework abstraction.
* Player save: `Application.persistentDataPath/DropTheMan/progress.json`. Editor uses
  `progress.editor.json`; `useProgressionInEditor=false` restores authored starting-index tests.
  Inspector dev data and the level editor never save progression. No profile writes each frame.
* Invalid load blocks startup and offers Retry Load, never silently overwrites. Save failure keeps
  dirty state and visible Retry Save; pause/quit also retry. Unsaved data can be lost if the app exits
  while storage remains unavailable. A successful Next click is not required to save a win.
* Picker disables pointer input, not timer progression. Settings/catalog are not hot-reloaded.

## 5. Dependency Impact

Other puzzle games can reuse the progress state/storage without adopting this game's replay/loop
rules. Removing those framework types breaks the new game session/policy compilation. Removing
the terminal callback wiring stops automatic win saving; result HUD polling is not a substitute.

The prototype now pins framework `96e9b7751686f2652c0374a40841e74c96c74c9f`, replacing
`879de67ecb1b8f788d768acfd8cb4ac5b3329d89`. The full SHA was verified on the remote before changing
the manifest; Unity generated the matching lock entry without unrelated dependency updates.
No temporary local override or new third-party package was used.

## 6. Intentionally Deferred

No cloud sync, multiple profiles, rewards, currency, stars, stats, encrypted saves, board snapshots,
mid-level resume, randomized/adaptive looping, migrations beyond explicit version rejection,
reset-progress UI, or polished level-select screens. No multi-process writer coordination.
Do not automatically erase unreadable progress to recover; repair/reset needs an explicit decision.

## 7. Verification

* Unity 6000.3.17f1, Windows isolated validation host: all 38 new tests passed (18 framework, 20 game).
  Report: `C:/Users/Gaming/.codex/diagnostics/progression-final-tests.xml`.
* Full framework and game runtime source compiled in that host with existing DOTween DLL.
* Runtime test exercises drag/collection, deferred cat completion, deferred hole completion,
  synchronous win save before Next, duplicate callbacks, and loss before hole completion.
* Tests cover cold reload, replay isolation, looping, range changes, new/removed content, failed
  load/save, locked-file replacement preserving the original, and Editor save-path separation.
* Broader suite before the last four new tests: 53/54 passed. Existing
  `LevelCatalogBuilderTests.Build_ReportsSequenceGapsWithoutRejectingCatalog` fails because NUnit
  `Has.Count` cannot find Count on the returned array. Left untouched and recorded in watchlist.
* Host input configuration differs from the prototype. A validation-only copy of its authoring
  controller was compiled with legacy input defines; production input code/settings unchanged.
  The temporary host copied asmdef also referenced the host's existing Input System package.
* September 3: actual prototype compiled successfully after package resolution; all 20 prototype
  Edit Mode tests passed against the published dependency. Result archived at
  `C:/Users/Gaming/.codex/diagnostics/progression-prototype-tests-2026-09-03.xml`.
* After the replay amendment: actual prototype recompiled; all 27 Edit Mode tests passed, including
  seven new tests for replay chains, saved-loop identity, pre-win/restart guards, read-only retry,
  catalog-number gaps, inserted unfinished content and single-level replay. Report:
  `C:/Users/Gaming/.codex/diagnostics/progression-replay-tests-2026-09-03.xml`.
* Gameplay scene Play Mode smoke test passed: Level 1 board/cats/holes and Editor sandbox HUD loaded.
  Stopped without completing a level; no sandbox save was created. Win/stop/reopen acceptance,
  replay/retry UI, player build and target-device filesystem verification remain manual followups.
* Temporary validation source/assets archived outside the repository under
  `C:/Users/Gaming/.codex/diagnostics/progression-validation-2026-09-02`; validation-triggered
  pipeline asset upgrade reverted.
  No normal profile touched. Unrelated dirty scenes, hole material and `level_3.json` preserved.
* Gameplay startup smoke repeated successfully after the replay change, without winning or saving.
* Framework implementation is already published. Prototype commit scope includes its package
  pin/lock and these docs; unrelated scenes, material and new level are explicitly excluded.

## 8. Next Smallest Step

In the Editor sandbox verify win then stop before Next Level, reopen and confirm the next unfinished
level. Check replay Next through completed content into campaign, final replay returning to the
saved loop, separate Resume Campaign, loss/restart and Retry Save/Load. Follow with player/device
checks; automated coverage does not replace the UI/filesystem acceptance pass.
