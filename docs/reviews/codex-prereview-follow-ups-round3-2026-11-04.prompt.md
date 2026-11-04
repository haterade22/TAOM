# Codex review: the fixes no Codex pass has read, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls, and run no game.
Read `AGENTS.md` and `.ai/review-reference.md` first. Engine source for the installed build (v1.5.6, changeset
125270) is cached in `C:\Users\mikew\.taom-src\`; `pwsh tools/taom-src.ps1 path <Type>` adds a missing type.
The game install is `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`. Do not write to that folder, and
do not read or write `E:\LOTRAOM_Releases`. Mark anything you cannot prove from code as UNVERIFIED.

The known red test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is not a finding.

**Scope:** branch `chore/follow-ups` in this worktree. Read these commits with `git show`, and read each changed
function whole in the current tree: `4a7b7164d`, `0385cc041`, `931d59084`, `95a348a60` and the commit after it.
Merges from trunk and all other commits are out of scope, except where they change a result in scope.

## Questions to answer with evidence

**Warg rage (`Main/Features/Warg/`, mission tick).** `4a7b7164d` and `931d59084` add `CloseRageOnNewRider`,
`ShouldClose`, `GiveFormationBack`, `ValidStoredRider`, two ifs in `OnWargDied`, a 100 mission second stranded limit
and `FirstAttack.SetValue(false)` at the close. Trace every order of events in `WargBehaviorTree.cs`: a rider who
changes, a second rage, a warg or rider death, a recycled slot, a player, a panicked rider, a co-op puppet. Can a
rider end with no formation, the wrong formation, or scripted movement left on? Can the tree throw? Does the Seat
of Seeing, new on trunk, change any of this?

**Former hero's ladder quest (`95a348a60`).** `IsFormerPlayerHeroQuest` now returns true for a lord's gear quest.
Trace `CareerQuestCampaignBehavior`, `LordsLadderBehavior.OnQuestCompleted`, `LordsLadderService`, `LadderPresenter`
and `CareerQuest`. Does the cancel change any ladder state, lose a hero's materials or block a later start? What
happens in a Player Switcher return, and at session launch on an old save? Does any non-career quest with a
definition now cancel when it should not?

**Mutation gate (`95a348a60`).** Does keeping the value hide any case that worked, and can a NaN still reach a
template by another path?

**Prefabs (`95a348a60`).** `StagedSiegeHud.xml` and three `DiamondTooltip` blocks in `CareerScreen.xml`. From
`Widget`, `BrushWidget` and `DefaultLayout` source: is the size now right, does `RenderLate` still cover the children,
does `TaomCareerDiamondWidget` still find and drive the tooltip, and does the frame still show? Does `Popup.Frame`'s
state style change because the frame is no longer disabled?

**Hard AI income (`95a348a60` and the next commit).** Does anything else read 1.2 or depend on it?

**Tools and hooks (`0385cc041`, `931d59084`).** `.claude/hooks/block-no-verify.sh` with the `\>` terminator, the
`secret-nuget-cleartext` regex in `tools/audit_claude_config.py`, the data gates and `tools/rebuild_translation_files.py`.
Find a command or file the change gets wrong in either direction.

## Report

List findings by severity. For each: file and line, the concrete failing order or input, the evidence, and CONFIRMED
or UNVERIFIED. Say what you read and what you did not. If you find nothing in an area, say so.
