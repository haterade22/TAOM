# Codex review: army panel merge with trunk's co-op map bar rule (#1532)

You are a read-only adversarial reviewer. Do not edit, build, deploy or run the game. Report defects with file:line
evidence and a severity (P1 breaks behaviour or crashes, P2 wrong in a real case, P3 minor). Mark anything you
could not prove from source as UNVERIFIED. Read AGENTS.md and .ai/roles/reviewer.md first.

## The change

Branch `feat/army-overlay-theme` (commit 415277404, reviewed earlier: see
`docs/reviews/rca-army-overlay-2026-11-22.md`) is being merged with trunk `origin/bannerlord-1.5.x` (8f41b7593). The
merge is uncommitted in the working tree: `git diff --cached HEAD` and `git diff HEAD` show it against the branch.

Trunk commit 40cc17b54 replaced the co-op presence skip in `GauntletLayerLoadMoviePatch` with
`ThemedMapBarCoopPolicy` (`Main/Features/FactionUI/Menus/ThemedMapBarCoopPolicy.cs`): a BannerlordCoop session and solo
play now get the themed map bar; only a co-op mod TAOM cannot probe keeps vanilla's. The branch had added
`"ArmyOverlay"` to the old presence skip. The resolution takes trunk's patch and adds `"ArmyOverlay"` to
`ThemedMapBarCoopPolicy.IsMapBarMovie`, so the themed army panel (`TAOMArmyOverlay.xml`, swapped in by
`FrontEndMovieService` for `ArmyOverlay` with `ArmyMenuOverlayVM`) follows the bar's rule.

## Check

1. Under a BannerlordCoop client session, the themed army panel now loads. Does BannerlordCoop (its source or DLLs
   under `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Coop`, and `E:/repos/BannerlordCoop` if
   present) patch, extend or replace the `ArmyOverlay` prefab, `ArmyMenuOverlayVM`, `GauntletMapOverlayView` or
   `ArmyOverlayWidget` in a way the themed prefab breaks? Its `OnArmyOverlaySetDirty` handler was seen in
   `GameInterface.dll`.
2. Is the policy read at every load of the army panel (an army forms and breaks up many times in a session, and a
   session can start and end in one process)?
3. Does any path still disagree: themed bar with vanilla panel, or vanilla bar with themed panel?
4. Anything else the merge broke in the branch's files (`git diff 415277404 -- <file>` for each branch file).

Engine sources: `pwsh -NoProfile tools/taom-src.ps1 path <Type>` (output under `C:/Users/mikew/.taom-src/`).
