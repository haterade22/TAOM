# Adversarial review: three ported features on branch feat/yotthani-camps

You are an independent adversarial reviewer on TAOM, a Bannerlord **1.4.8** total conversion.
Work in **E:\repos\taom-camps** (a git worktree on branch `feat/yotthani-camps`). The changeset
under review is `git diff origin/bannerlord-1.4.5...HEAD` (130 files, +18.5k lines): three new
features under `Main/Features/SupplyLines`, `Main/Features/FieldCamp`, `Main/Features/Refuge`,
plus integration edits (IoC, SubModule, TaomSettings, two combat models, one visibility model) and
docs. Assume there are shipping bugs and find them.

## Ground rules

- Engine truth is the INSTALLED 1.4.8: `pwsh E:\repos\TAOM\tools\taom-src.ps1 path <FullTypeName>`;
  module-bin assemblies (SandBox.View, MountAndBlade.View, GauntletUI.Widgets) via
  `E:\Decompiled_Bannerlord\_modules_build_v1.4.8\`.
- Current state: `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId=`
  reports 7323 passed / 0 failed. Main builds clean. Narrow --filter runs are fine; skip full runs.
- READ-ONLY. Report findings; do not edit.
- Treat every claim in commit messages, CHANGELOG and docs/features/*.md as a HYPOTHESIS to check
  against the code. A claim the code does not satisfy is a finding.
- These are ports of three standalone modules; the decompiled sources are at
  C:\Users\mikew\AppData\Local\Temp\claude\e--repos-TAOM\413f1596-cb71-4a76-adbb-49363efff81e\scratchpad\yotthani\
  ({SupplyLines,FieldCamp,Refuge}.cs). A mechanic the source shipped that the port silently lost,
  or a transcription error against the source, is a finding.

## Questions worth your time (probe, do not assume the answers)

1. The caravan/refuge/camp parties are stationary or teleported clan-owned MobileParties with AI
   pinned. What does vanilla's hourly/daily machinery do to such parties over days of game time:
   wages, food/starvation, morale, disband checks, army gathering, prisoner escorts targeting
   them? Can the engine destroy, move or mutate them behind the features' backs?
2. What happens when the PLAYER or an AI party clicks/encounters a supply caravan without the
   refuge prefix in play: conversation with whom? Does `SupplyCaravanComponent` provide everything
   the encounter/conversation path dereferences (Leader is null)?
3. Save/load in every mid-state: mid-build camp, mid-transit order (settlement + lord source),
   refuge mid-raise, militia mid-battle, order screen open. Walk the SyncData and definer shapes
   and OnGameLoaded paths for each; hostile/null inputs included.
4. The three features run SIMULTANEOUSLY and interact through seams (break camp cancels orders;
   refuge blocks camping; contributor overlays). Trace the interaction matrix for ordering bugs.
5. The two combat-model consults and the MapVisibilityModel contributor: hot-path cost and
   correctness when no campaign is loaded, between campaigns, and when settings hold degenerate
   values.
6. Menus: option indexes, ids, conditions across the two FieldCamp menus and the Refuge insertions;
   GameMenu context rules (SwitchToMenu vs ActivateGameMenu) per the repo's IGameMenuAdapter notes.
7. Localization: every player-visible string keyed and registered? Text-variable substitution
   ({NAME}, {PROGRESS}...) actually wired where used?
8. The four shipped .tpac AssetPackages under Main/_Module/AssetPackages: referenced mesh names vs
   code constants; deployment path; fallback behaviour when missing.
9. Economy: follow every gold/item/troop flow (order pricing/charging/refunds, forage grain,
   fortify/found/upgrade costs, militia troops, dismantle merges). Anything created from nothing,
   destroyed wrongly, or double-charged?
10. Concurrency of ticks: HourlyTick vs per-frame FrameTick vs MapEvent handlers touching the same
    books; re-entrancy via inquiries/menus opened from ticks.

## Output

Per finding: severity P1 (crash/corruption) / P2 (wrong behaviour) / P3 (quality), file:line, what
is wrong, the exact code or decompiled evidence, a concrete failure scenario, minimal fix. State
explicitly which probe areas came up CLEAN; a cleared area is useful. End with:
`P1: n | P2: n | P3: n` and `VERDICT: ISSUES FOUND` or `VERDICT: CLEAN`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
