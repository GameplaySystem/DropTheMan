# Historical Drop The Man MVP Game Module Requirements

## Archived status

This historical first-slice scope record is not an active implementation authority. Its useful MVP
target, ownership boundary, and Framework assumptions now live in
`../DropTheManMVPRules.md`, which is the canonical high-level MVP rules document.

## Historical rationale

The original document separated prototype-owned gameplay meaning from generic Framework services
while the first playable slice was being planned. The active design preserves that boundary:
Framework owns generic grid, occupancy, construction, interaction, timer, state, and shared-color
mechanics; DropAwayPrototype owns Drop The Man nouns and gameplay interpretation.

Detailed movement and collection semantics are now authoritative in
`../DropTheManMovementAndCollectionRules.md`. Git history retains the original first-slice
requirements and implementation sequencing.
