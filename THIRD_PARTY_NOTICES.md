# Third-Party Notices

These notices do not grant an open-source license for project-owned code or artwork. This is a
portfolio inspection project; dependencies retain their own rights and restrictions.

## DOTween Core

- Location: `Assets/Plugins/Demigiant/DOTween/`.
- Copyright (c) 2014-2026 Daniele Giardini - Demigiant.
- Source: [official DOTween downloads](https://dotween.demigiant.com/download.php), version 1.3.030.
- Terms: [DOTween license](https://dotween.demigiant.com/license.php), a custom Artistic license,
  not MIT and not the DOTween Pro license.
- Original [readme.txt](Assets/Plugins/Demigiant/DOTween/readme.txt) and author headers are retained.

The license permits verbatim Standard Version distribution subject to its notice, disclaimer,
and original-readme requirements; modified versions must not be redistributed. All 19 tracked
non-meta plugin files match the official 1.3.030 distribution (text comparison ignores CRLF/LF).
No DOTween Pro package or EPOOutline implementation is bundled; an optional integration module
does not supply that separate dependency. Preserve the original notices when redistributing.

## Unity And Framework Dependencies

Unity packages, including URP, Recorder, IDE tooling, and their transitive dependencies, are
resolved from the manifests by Unity Package Manager. Their sources are not vendored here.
Package-specific licenses and notices accompany those downloads; do not commit the package cache.

The project consumes [PuzzleFramework](https://github.com/GameplaySystem/PuzzleFramework) at the
immutable revision in `Packages/manifest.json`. It does not grant independent reuse rights to
that package. Unity-derived shaders/materials remain subject to any applicable Unity terms.

## Artwork Publication Pending

The owner identifies the cat, animations, hole/board models, and textures as artist-created.
The owner also prefers that external visitors cannot download those raw assets. Public GitHub
repositories cannot enforce that restriction. Do not interpret this notice as permission to
publish the artwork or its historical revisions. Keep this playable repository private until
the publication approach in the [audit report](docs/PublicationReadinessReport.md) is resolved.
