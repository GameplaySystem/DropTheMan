# Drop The Man Publication Readiness (2026-09-19)

Status: **NOT READY** for public visibility. The owner wants external viewers to inspect code
without being able to download the artist's raw assets. GitHub cannot make tracked files public
for viewing while withholding those files, and old commits also remain downloadable. The current
playable repository should stay private unless the owner explicitly changes that requirement.
No visibility, access, history, Unity assets, gameplay code, manifest, or lock file changed here.

## Audit Basis

- Read: root README, `AGENTS.md`, `.gitignore`, `Packages/manifest.json`, lock file, MVP rules,
  progression handoff, and the framework package dependency workflow and status/watchlist.
- Read-only current-file and all-locally-reachable-blob pattern scans checked common credential
  formats, private-key headers, embedded credential URLs, webhooks, service-account markers,
  credential assignments, email addresses, and local user paths. No likely secret was found.
  Empty Unity keystore/certificate fields were checked. This is not a guarantee about opaque
  files, remote-only refs, or information outside the repository.

## Findings And Cleanup

| Classification | Finding / action |
| --- | --- |
| SAFE | 361 tracked files at audit start, about 6.1 MB current working content. No tracked Unity generated folders, archives, builds, audio, fonts, or signing files found. The largest current files are `Cat.fbx` (about 2.0 MB) and the generated editor scene (about 1.2 MB); neither warrants a history rewrite for size. |
| SAFE | `.gitignore` now covers additional local captures, caches, IDE output, logs, env, and signing files. It cannot hide an already tracked file or protect secrets once committed. Unity `Assets`, `Packages`, `ProjectSettings`, and associated `.meta` remain tracked. |
| NEEDS CLEANUP | The README now identifies this as prototype #1 and a completed systems showcase, names runtime construction/spawning and the full gameplay loop, and distinguishes deferred UI/art/device acceptance. Machine-specific diagnostic paths were removed from the progression handoff and `AGENTS.md`. |
| ATTRIBUTION REQUIRED / LICENSE REVIEW | Bundled DOTween Core 1.3.030 files match the [official release](https://dotween.demigiant.com/download.php); author headers and original `readme.txt` remain. Its [custom license](https://dotween.demigiant.com/license.php) allows verbatim Standard Version redistribution under notice/readme conditions but forbids distributing modified versions. The tracked subset omits vendor upgrade-manager binaries, so confirm the subset and notice/disclaimer handling before making the prototype public. `THIRD_PARTY_NOTICES.md` records source and terms; this is not DOTween Pro. |
| LICENSE UNKNOWN / PRIVATE-SENSITIVE | The owner says the cat, animations, models, and textures were artist-created but prefers raw files not to be publicly downloadable. Current `Assets/RuntimeAssets/RuntimeModels/*.fbx`, textures, and prefab/material assets would be downloadable in a public clone. Some FBXs embed artist workstation paths. The artist's grant for public raw-source distribution has not been documented. Do not infer it from permission to use art in the game. |
| SAFE | Unity/URP/Recorder dependencies are resolved by Package Manager, not vendored as source. `Packages/manifest.json` and lock agree on framework SHA `96e9b7751686f2652c0374a40841e74c96c74c9f`. The historical `Find-Games` URL still resolves to that same remote commit after the move to `GameplaySystem`. Change the URL later only with Unity re-resolution and compile verification. |
| HISTORY RISK | Locally reachable main history has 58 commits and about 580 unique blobs. Older versions of `Cat.fbx` and other art are present; deleting current art would not hide them. Deleted authoring exports and an old editor config remain in history but no obvious private files or credential patterns were found. Rewriting history could break exact framework commit references; no rewrite was performed. |
| NEEDS CLEANUP | Two tracked `.mdb.meta` files have ignored `.mdb` companions. They are likely stale DOTween debug-symbol metadata; left untouched because removing plugin metadata without a Unity import check has little publication value. Two `Inner_Texture.png` files are byte-identical but may be intentionally referenced by distinct Unity GUIDs, so both remain. |

No repository-wide `LICENSE` exists. Project-owned code has no automatic open-source grant.
Third-party DOTween and Unity terms remain separate. No contribution policy is needed for a
portfolio repository. The owner can publish the framework first and keep this playable repository
private, or later authorize a **new code-only public showcase made from a fresh history** with
placeholders and clear private-asset setup instructions. A GitHub branch, `.gitignore` entry,
Git LFS pointer, or current-HEAD asset deletion does not conceal assets already in this history.

Files changed for cleanup: `.gitignore`, `AGENTS.md`, `README.md`,
`docs/DropTheManProgressionImplementationHandoff.md`, `THIRD_PARTY_NOTICES.md`, and this report.
No files were removed. No gameplay, editor, scene, prefab, level, or package files were edited.

## Before Any Publication

1. Decide whether a code-only showcase should be created, or explicitly permit raw asset and
   historical art distribution. Do not switch this repository to public while the no-download
   preference remains.
2. If a code-only showcase is approved, create it from a fresh history outside this repository,
   omit private art and any derived raw files, and verify it clones and compiles with placeholders.
   Keep the playable source private. Do not use a fork that preserves the original art history.
3. Review DOTween redistribution conditions and artist usage/publication permission. Recheck any
   FBX embedded metadata or exported textures before building a public snapshot.
4. For any eventual public repository, review collaborators/teams, organization base permissions,
   installed apps, webhooks, and deploy keys. Protect `main` against force pushes and deletion;
   consider required PR review. Public read access does not itself grant push access.
