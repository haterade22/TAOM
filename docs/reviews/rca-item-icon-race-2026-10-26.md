# RCA: Patch132 item-icon race framing (deep review, 2026-10-26)

## Top-line

The `/deep-review` of Patch132 (dwarven armour inventory icons, third surface `ItemIconPatch.json` of
`RacePositionStore`) ran at tier 1 with Lens 5 Data flow and Lens 2 Engine compatibility. Codex did not run: the
session had no standing word for paid dispatch. Neither lens found a CRITICAL or HIGH defect, so no second wave
ran. The final full suite added one more (finding 8). The lenses found one design flaw in the patch target, a false-positive rule in the matcher, a cache clear that
released textures a screen may still show, a console path that saves rows nothing reads, a flipped axis and a
NaN gate written the wrong way round. Every finding below was re-read against the v1.5.5 decompile
(`ItemThumbnailCache.cs`) or the installed item data before it was fixed. One convergence pass ran on the fixes.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The postfix moved the entity returned by `CreateItemBaseEntity`, but for gloves vanilla had already ticked the human skeleton with the original `itemFrame` (`ItemThumbnailCache.cs` :603-611), so bones and frame could part. Also the target is 45 bytes of IL, an inlining candidate, and whether `AddItemEntity` puts the pose in the entity frame or the mesh frame is native (Lens 5 #2, Lens 2 #3, #4) | Patched the output after the engine consumed the input | The author copied Patch72's shape (adjust the finished thing) without reading what `AddItem` does with the frame before it returns | Retargeted to a postfix on `GetItemPoseAndCamera` that moves `ref itemFrame` before `AddItem` reads it; binding test pins `itemFrame` as by-ref `MatrixFrame`. Lesson "Patch the pose the engine consumes, not the object it built from it" (`lessons/harmony-il.md`) |
| 2 | LOW | Matching on the item id made `hon_ring_dwarf_1..7` (HandArmor, human `leather_gloves` mesh) dwarven; `moria` was a standing risk for future orc content (Lens 5 #8, found by a scan of every installed module) | Identity inferred from the weakest evidence | The user's rule ("erebor in the name") was taken as id OR mesh OR culture with equal weight; no scan of installed items ran before the rule shipped | Mesh name decides when present; id and culture only without a mesh; `moria` dropped; both cases are test rows |
| 3 | MED | `ForceClearAllCache(true)` releases every thumbnail texture immediately (items, portraits, banners) while the inventory or the map bar may still show them; vanilla does that only from `MapScreen.OnInitialize` and `ClearGPUMemory` (Lens 5 #3, Lens 2 #1) | Engine call used outside the context vanilla uses it in | The author checked the API exists, not where vanilla calls it | `ForceClearAllCache(false)` (deferred release); the console reply now says when the clear did not happen |
| 4 | LOW | The console accepted `icon <anyKnownRace>_<slot>`, saved it and reported success, though only `dwarf` is ever produced by the matcher (Lens 5 #6) | A writer accepts keys no reader reads | Validation reused the avatar surface's "known race" rule | `ItemIconRaceMatcher.Races`; parser refuses others; the shipped-config test asserts every row's race is in it |
| 5 | LOW | Horizontal pointed screen-left: `Cross(toCamera, up)` where the camera looks along `-toCamera` (Lens 5 #9, Lens 2 #2) | Sign of a derived axis not worked through | No shipped row uses Horizontal, so nothing showed it | `Cross(up, toCamera)` with the derivation in a comment; still to be checked in game |
| 6 | LOW | `if (LengthSquared < 1e-6f) return` lets a NaN through to `NormalizedCopy` and the frame (Lens 5 #10) | NaN gate written as `<` | A REPEAT of the inverted-gate pattern the engine-float rule exists for | `!(x > eps)`; covered by the existing rule, no new lesson |
| 8 | MED | `RacePositionTuningCheats.ClearIconCache` named `ThumbnailCacheManager` (View, client-only) in a core method, so it fails to JIT on a dedicated server. Caught by `ServerJitGateTests` in the final full suite, not by a lens | Client type in core code | Neither lens was asked about ADR-013; the gate runs only in the full suite, and the per-feature test filter left it out | Seam `IThumbnailCacheAdapter` with `NullThumbnailCacheAdapter` in core and `ThumbnailCacheAdapter` in TAOM.Client, as the other front-end adapters do. Covered by ADR-013 and the gate; no new lesson, but the per-feature filter for any change that touches a View type now includes `ServerJitGate` |
| 7 | LOW | Doc drift: "two configs" / "both" in the store, validator and comments after a third surface; registry token list missed `ironhills` (Lens 5 #5, #14) | Wording not swept after adding a member to a set | The diff touched the switch statements, not the prose around them | Fixed in place |

NOT APPLIED: no load-time warning for hand-edited icon rows that name an unknown slot or race (Lens 5 #7, LOW).
The validator is shared by all three surfaces; the shipped file is pinned by a test, and the console no longer
writes such rows. FOLLOW-UP: no issue: a hand edit of `ItemIconPatch.json` is a developer action and the tuning
console is the supported path.

Out of scope: the 3D item inspector (`ItemCollectionElementViewExtensions`) frames items itself and is not covered
by Patch132. FOLLOW-UP: no issue: not reported by players; to be filed if dwarf armour looks wrong there in game.

## Why each lens missed nothing more

Lens 2 and Lens 5 agreed on findings 1, 3 and 5 independently. Finding 2 needed the installed item data, which only
Lens 5 scanned. No lens could verify native behaviour (`AddItemEntity` frame placement, texture lifetime after
`ReleaseImmediately`); the retarget in finding 1 and the deferred release in finding 3 remove the dependence on it.

## In-game checks still owed

Checked 2026-10-29: armour (V 0.2) and capes (V 0.25) framed. Helmets were not: the starting value moved them
along world up, and the helmet camera looks down on the item, so world up read as zoom and no row value could
centre them. Fixed by adding the rows in the camera's own axes and centring helmets from their mesh bounds; the
helmet row is now 0/0/0 and helmets are centred in game. Still owed: that gloves move with their skeleton once a
`dwarf_glove` row exists.
