# Cat Animation Integration Handoff

Date: 2026-09-02

## Preflight And Cause

Read the collection presentation timing, movement/collection rules, MVP rules, runtime integration,
playable scene adapter and hole presentation designs; framework project state, watchlist, critical
clarifications and daily milestone workflow. Runtime owns reservation, capacity and outcomes;
views own live-socket motion and report completion. No framework changes are required.

The artist replaced `Assets/RuntimeAssets/RuntimeModels/Cat.fbx` locally. The old importer named
removed takes and imported no clips; the controller still referenced older separate FBXs. The
delivery contains two Idle takes and six Jump takes. Its bone names match the old `Cat_3D`, but its
rest transforms and mesh binding do not: mixing them reversed facing and deformed the feet/tail.
The replacement's matching mesh, Avatar and clips must be used together. Early rendered poses
now visibly differ; clip/state names alone were insufficient evidence with the previous exports.

## Changes And Ownership

* `RuntimeModels/Cat.fbx` and `.meta`: canonical mesh, Generic Avatar (Armature root), baked axis
  conversion, delivered take mappings, looping Idle takes and non-looping Jump takes.
* `Animations/CatCollection.controller`: default `Idle_1`; `Fall_1` through `Fall_6` directly reference
  their corresponding FBX Jump clips. No generated/rebased `.anim` copies or runtime overrides.
* `RuntimePrefabs/CatCollectible.prefab`: Animator on the model child, six clip references, authored
  motion settings, and body material index 0. White/detail material slot 1 is not tinted.
* `DropTheManCatCollectionPresentation.cs`: persistent per-cat shuffle-bag selection, Animator entry,
  rise/approach, fall, concurrent shrink, live socket updates and once-only completion. Count follows
  the clip array, with no per-assignment temporary candidate lists or hard-coded three-state limit.
* `DropTheManStickmanView.cs`: disable colliders on start; hide/destroy on completion; immediate
  fallback on missing presentation. Renamed hide/destroy fields retain serialized migration aliases.
* `DropTheManHolePresentation.cs` and `DropTheManHoleView.cs`: closest unclaimed socket assignment;
  reset clears claims. The shared color helper supports body-slot tint without changing hole tint.
* `DropTheManViewRegistry.cs`, `DropTheManRuntimeController.cs`, `DropTheManDragSessionOwner.cs`:
  pass the socket and direct callback; complete capacity using `ReservedHoleId`, independently of
  the active drag, then run the existing hole-completion/outcome path.
* `DropTheManCatCollectionPresentationSetup.cs`: repeatable import/controller/prefab setup and
  read-only validation. Preserves valid trims, controller state tuning and the matching model child.

Runtime scripts are under `Assets/GameModules/Runtime`; setup is under `Assets/GameModules/Editor`.
Asset paths above are relative to `Assets/RuntimeAssets`. Removed obsolete separate Cat model/Idle/
Jump FBXs, generated clip experiments, duplicated sampling paths and temporary diagnostics.

## Contracts And Maintenance

`TryPlay(socket, callback, out failure)` and `ResetPresentation()` are presentation/view contracts,
not collection eligibility or capacity APIs. `IDropTheManStickmanView.TryPlayCollectionPresentation`
reports completion explicitly. `IDropTheManHoleView.TryClaimCollectionSocket` allocates only a
visual target. `CompleteCollectionPresentation(runtimeModel, stickman)` resolves runtime ownership
after release. The former unused stickman argument was removed from the view-only playback call.

No singleton/service-locator, event bus, persistence/schema, runtime construction, package, input,
or project-wide setting changes. Framework still has no dependency on game-specific presentation.
Removing this slice restores neither asynchronous completion nor socket tracking automatically;
views can use the immediate fallback, but runtime completion must still receive its callback.

Use **Tools > Drop The Man > Configure Cat Collection Presentation** after a compatible replacement,
then **Validate Cat Falling Clip Sampling**. The latter checks exact active clips, bone bindings,
non-looping jumps and distinct early baked-mesh sequences. Inspect early poses visually as well.
If an artist renames the required takes, update the setup's explicit take list deliberately.
Changing the rig requires its matching model/Avatar, not just a copied Avatar with similar names.

Motion remains 0.35s approach + 0.7s fall, rise 0.8, depth 3.5, final scale 0.05. Clips run at authored
speed from zero; no guarantee is made that the complete 0.9-2.583s Jump takes play before hiding.
`Idle_2` is imported and looped but is not the default or randomly selected. Additional idle behavior,
endgame features and a new animation framework are intentionally out of scope.

## Verification And Follow-Up

* Final runtime/editor build: `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v quiet`
  succeeded with zero warnings and zero errors after cleanup. Unity also recompiled successfully.
* Unity editor validation: six exact controller motions at full weight, valid binding paths, early
  mesh samples; repeated setup left the prefab unchanged. Rendered comparisons checked early poses,
  facing, deformation and white details on the final model.
* Temporary in-Editor Play Mode harness: 36 cats across six full shuffle cycles with no boundary
  repeats; exact active clips, moving socket during both phases, shrink, once-only callbacks and
  reset to Idle. Harness removed after validation; no auto-running test script remains.
* Actual level integration: 20 cats across five square holes; moved and released holes during
  animation, delayed capacity fill, full-hole completion and final win all passed. Restart during
  collection discarded old callbacks; restarting and loading the next level restored correct views.
  Body-only tint assertions passed. These are in-Editor checks, not a standalone NUnit test suite.
* No scene or material edits were needed for this integration. Existing changes to both scenes,
  `Holes_Material.mat` and `level_3.json` remain separate and are excluded from its commit.

Updated the collection timing/rules, hole presentation and runtime integration docs plus the
framework project state/watchlist. Next smallest step: owner playtest of pacing on other hole
shapes and target camera angles, then resume the approved endgame roadmap.
