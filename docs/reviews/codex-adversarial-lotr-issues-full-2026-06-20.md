OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019ee636-775f-79f1-bbb4-8b1ffc0c0fd5
--------
user
# Codex Adversarial Review — LotrIssues (full feature, Waves 1-7)

You are an independent adversarial reviewer for TAOM, a Bannerlord v1.4.6 total-conversion mod. Find real bugs. Be specific, cite file:line, and CONFIRM or DISPUTE each Known Suspect. Do not invent issues; verify against the source and the decompiled engine.

## What this feature does

LotrIssues replaces all 43 vanilla procedural "issues" (the problems-at-a-notable the player solves for reward) with 43 TAOM-authored Middle-earth issues, and suppresses the vanilla ones. Generic-template + XML-config architecture: one config row per issue, 3 mechanic templates. Wave 0 (framework + DeliverGoods template + suppression + dispatch + adapters + SaveableTypeDefiner) was already reviewed by you (review 60) and by a 5-agent deep-review; its 8 deep-review findings + 2 Codex MEDIUMs were all fixed. THIS pass focuses on what shipped AFTER Wave 0: the Combat template (+ its 3 variants), the DeliverPersonnel template, the 43 configs, and the localization wiring.

Engine is v1.4.6 (NOT 1.4.5 despite the branch name). Verify signatures against the INSTALLED DLLs at:
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/
using ilspycmd. Decompiled browse-only dump is at E:/Decompiled_Bannerlord/ (do NOT trust it for signatures).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
Culture IDs (vanilla-engine): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings, sturgia=Dale, battania=Khand. NOTE "rohan"/"dunland"/"dol_guldur" are NOT valid ids.

## READ FIRST

- docs/features/lotr-issues.md (the "Implementation (as built)" section + the known per-type-saturation limitation)
- docs/reviews/rca-lotr-issues-wave0-2026-06-17.md (Wave 0 findings already fixed; the M1 parsed-but-unresolvable + M2 trimmed-behavioral-port lessons)
- Main/Features/LotrIssues/LotrIssueConfigProvider.cs (the validation rules every config must pass)

## FILES IN SCOPE (new since Wave 0)

Templates:
- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs  (CombatLotrIssue : IssueBase + CombatLotrIssueQuest : QuestBase; variant DefeatRaids/CaptureLords/WinTournaments)
- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs  (bandit-prisoner delivery)
Dispatch + save + config:
- Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs  (TemplateType + CreateIssue switches; RelatedObject dispatch)
- Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs  (base 726900801, localIds 101-106)
- Main/Features/LotrIssues/Domain/LotrIssueDefinition.cs  (Variant field)
- Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml  (43 configs)
Localization:
- Main/_Module/ModuleData/taom_lotr_issue_strings.xml  (308 keys)
- Main/_Module/SubModule.xml  (the new taom_lotr_issue_strings GameText node)
Context (Wave 0, already reviewed — read only if a finding requires it):
- Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs, LotrIssueService.cs, LotrIssueSuppression.cs, Main/Adapters/LotrIssue*Adapter.cs
Tests:
- TAOM.Tests/Features/LotrIssues/*

## KNOWN SUSPECTS — CONFIRM or DISPUTE each

1. CombatLotrIssueQuest auto-completes by calling Success() -> CompleteQuestWithSuccess() directly inside the CampaignEvent handlers (OnPlayerBattleEnd / OnHeroPrisonerTaken / OnTournamentFinished). Is this re-entrancy-safe in v1.4.6? Can completing a quest from inside an MbEvent dispatch corrupt the listener list or double-fire? Is the `if (!IsOngoing) return;` guard in Bump() sufficient?

2. The 3-way event routing in RegisterEvents: CaptureLords -> HeroPrisonerTaken, WinTournaments -> TournamentFinished, else (DefeatRaids) -> OnPlayerBattleEnd. Confirm exactly ONE count-source is subscribed per variant (no double-count), and that WarDeclared + OnClanChangedKingdom cancellation hooks fire for all variants without interfering.

3. OnPlayerBattleEnd counts a won battle via mapEvent.WinningSide == mapEvent.PlayerSide. Does this over-count (e.g., fire for sieges, hideouts, or simulated battles the player didn't actually fight) or under-count? Compare to how a vanilla "win N battles" quest filters MapEvents in 1.4.6.

4. OnHeroPrisonerTaken (CaptureLords) requires capturer == PartyBase.MainParty && prisoner.IsLord && prisoner.MapFaction.IsAtWarWith(playerFaction). Could a lord captured by an allied/army party, or a non-combatant lord, mis-count or NRE? Is prisoner.MapFaction ever null mid-capture?

5. OnTournamentFinished (WinTournaments) bumps when winner == CharacterObject.PlayerCharacter. Verify the TournamentFinished delegate signature (CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject) and that the player winning is correctly detected.

6. DeliverPersonnelLotrIssue counts/removes bandit prisoners from PartyBase.MainParty.PrisonRoster keyed on CharacterObject.Occupation == Occupation.Bandit. Is that the correct way to identify bandit prisoners in 1.4.6? Does removal use the modifier-preserving roster API? Can the turn-in remove prisoners that were freed/escaped/recruited between accept and turn-in (stale-count bug like Wave-0 M2)?

7. SaveableTypeDefiner: base 726900801 + localIds 101-106 -> derived ids. Confirm no collision with CareerQuest (726900701/726900802) or any other TAOM definer, and that all 6 localIds are unique.

8. CombatLotrIssueQuest [SaveableField] set (_defId, _neededCount, _rewardGold, _difficulty, _variant, _progress, _log) — is any mutable runtime field used after load but NOT saved (would reset to default / NRE on load)? Is _def correctly NON-saveable + re-resolved via EnsureDef? Same audit for DeliverPersonnelLotrIssueQuest.

9. PER-TYPE SATURATION (we believe this is real + accepted): all 27 Combat configs share typeof(CombatLotrIssue) and all 14 Deliver share typeof(DeliverGoodsLotrIssue). Does the v1.4.6 IssueManager / IssuesCampaignBehavior over-representation + cooldown logic key on the issue TYPE such that the 27 Combat variants compete for a single per-type slot? CONFIRM the mechanism and state the practical spawn-rate consequence. (We have documented this as an accepted v1 limitation; confirm we have it right, and flag if it is WORSE than "fewer simultaneous issues + rare variants surface infrequently" — e.g. if it can fully starve a variant or soft-lock the panel.)

10. Config integrity (taom_lotr_issues.xml, 43 rows): every DeliverGoods item_source is item:<id> with <id> a real engine item; every Combat variant is one of the 3 implemented; every giver_occupation/frequency/template parses; no duplicate id; no reward is zero-everything or sign-flipped. Flag any row that LotrIssueConfigProvider would silently skip at load.

11. Localization: every {=KEY} referenced in taom_lotr_issues.xml AND in the 3 template .cs files has a matching <string id> in taom_lotr_issue_strings.xml. Any missing key renders the inline default (acceptable) — but flag any key referenced with NO inline default anywhere (would render the raw {=KEY}).

## REQUIRED OUTPUT SECTIONS

- VANILLA CODE: paste the v1.4.6 decompiled signatures/bodies you relied on (IssueManager over-representation + cooldown, the MapEvent win-detection a vanilla quest uses, TournamentFinished, the QuestBase completion path) as evidence for suspects 1/3/5/9.
- KNOWN SUSPECTS VERDICTS: CONFIRMED / DISPUTED + evidence for each of the 11.
- FINDINGS: any NEW bugs not in the suspect list, with severity (HIGH/MED/LOW), file:line, and the fix.
- CONFIG CROSS-REFERENCE: result of suspect 10.

## QUALITY GATES

- Decompile the actual v1.4.6 installed DLLs for every engine claim; do not assert from memory.
- A finding is a hypothesis — show the code that proves it.
- TAOM custom cultures use LOTR-name StringIds; vanilla-engine cultures use Calradic ids (see cheatsheet). Do not flag culture="" (means "all cultures") as a bug.
- These quests are issue-attached (created via IssueBase.GenerateIssueQuest), so leaving SpecialQuestType empty is CORRECT (not a bug) — they survive QuestManager.OnGameLoaded via the issue-link branch.
- The IssueBase/QuestBase templates are TaleWorlds-constructed entry points; IoC.Resolve + direct TaleWorlds types in them are allowed (ADR boundary). Only flag sealed types leaking into the pure LotrIssueService/ConfigProvider.

Output your full review below.

ERROR: The process "52200" not found.
codex
I’ll review this as a code review only: first I’m reading the LotrIssues implementation/config and validation path, then I’ll verify the engine-facing claims with `ilspycmd` against the installed v1.4.6 DLLs.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class LotrIssueConfigProvider|Validate|TemplateType|item_source|variant|giver_occupation|frequency|reward" Main/Features/LotrIssues/LotrIssueConfigProvider.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"Implementation \\(as built\\)|per-type|saturation|Combat|DeliverPersonnel|known\" docs/features/lotr-issues.md docs/reviews/rca-lotr-issues-wave0-2026-06-17.md" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class CombatLotrIssue|class CombatLotrIssueQuest|RegisterEvents|OnPlayerBattleEnd|OnHeroPrisonerTaken|OnTournamentFinished|Bump|Success|SaveableField|EnsureDef" Main/Features/LotrIssues/Templates/CombatLotrIssue.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class DeliverPersonnelLotrIssue|class DeliverPersonnelLotrIssueQuest|Bandit|PrisonRoster|RemoveTroop|Success|SaveableField|EnsureDef|stale|Available|Turn" Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:07:03.453696Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.7 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CombatLotrIssue|class CombatLotrIssueQuest|RegisterEvent …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-20T18:07:03.453699Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.7 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "Implementation \(as built\)|per-type|saturation|Combat|Deliver …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2674ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "Implementation \(as built\)|per-type|saturation|Combat|Deliver …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2674ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CombatLotrIssue|class CombatLotrIssueQuest|RegisterEvent …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2674ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class LotrIssueConfigProvider|Validate|TemplateType|item_sourc …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-20T18:07:03.461023Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.7 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class LotrIssueConfigProvider|Validate|TemplateType|item_sourc …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-20T18:07:03.465395Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.7 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class DeliverPersonnelLotrIssue|class DeliverPersonnelLotrIssu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2671ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class DeliverPersonnelLotrIssue|class DeliverPersonnelLotrIssu …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this shell, so I’m switching to `git grep`/PowerShell search and keeping the same scope.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "git grep -n -E \"Implementation \\(as built\\)|per-type|saturation|Combat|DeliverPersonnel|known\" -- docs/features/lotr-issues.md docs/reviews/rca-lotr-issues-wave0-2026-06-17.md" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git grep -n -E "class LotrIssueConfigProvider|Validate|TemplateType|item_source|variant|giver_occupation|frequency|reward" -- Main/Features/LotrIssues/LotrIssueConfigProvider.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git grep -n -E "class DeliverPersonnelLotrIssue|class DeliverPersonnelLotrIssueQuest|Bandit|PrisonRoster|RemoveTroop|Success|SaveableField|EnsureDef|Available|Turn" -- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git grep -n -E "class CombatLotrIssue|class CombatLotrIssueQuest|RegisterEvents|OnPlayerBattleEnd|OnHeroPrisonerTaken|OnTournamentFinished|Bump|Success|SaveableField|EnsureDef" -- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:07:13.881493Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:

 exited 1 in 383ms:
 exited 1 in 409ms:
 succeeded in 409ms:
docs/features/lotr-issues.md:5:> See **Implementation (as built)** below for the shipped design; the disposition matrix and risk analysis that
docs/features/lotr-issues.md:129:  ModuleData (`resolvesInTaom: "unknown"`).
docs/features/lotr-issues.md:271:## Implementation (as built)
docs/features/lotr-issues.md:279:| **DeliverPersonnel** | hand over N bandit prisoners from the player's `PrisonRoster` | 2 (gang recruits, mine laborers) |
docs/features/lotr-issues.md:280:| **Combat** (`variant=`) | event-driven count, auto-completes on N (no turn-in) | 27 — `DefeatRaids` (24, won battles), `CaptureLords` (1, at-war lord taken prisoner), `WinTournaments` (2, tournament won) |
docs/features/lotr-issues.md:283:mechanics from the matrix below were each reframed onto the proven Deliver/Combat mechanics rather than authored as
docs/features/lotr-issues.md:305:**Known limitation — per-type spawn saturation (accepted v1 trade-off; this doc's Risk #5):** all 27 Combat configs
docs/features/lotr-issues.md:306:share `typeof(CombatLotrIssue)` (and all 14 Deliver share `typeof(DeliverGoodsLotrIssue)`), so Bannerlord's
docs/features/lotr-issues.md:309:issues than vanilla's 43 distinct types would, and rare Combat variants surface infrequently. A true per-config type
docs/features/lotr-issues.md:335:   in-progress quest's objective list in config between versions soft-locks saved progress (CareerQuest known
docs/features/lotr-issues.md:344:| `Main/Features/LotrIssues/Templates/{DeliverGoods,DeliverPersonnel,Combat}LotrIssue.cs` | the 3 generic mechanic `IssueBase` + paired `QuestBase` classes |

 exited 1 in 387ms:
2026-06-20T18:07:13.882854Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:

2026-06-20T18:07:13.884163Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:

codex
The docs confirm the intended architecture and explicitly document the per-type saturation tradeoff. The targeted grep didn’t hit the C# paths, so I’m checking whether these files are untracked or named differently before reading them directly.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\LotrIssues -Recurse | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\LotrIssueConfigProvider.cs -TotalCount 260" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs -TotalCount 360" in C:\Users\mikew\source\repos\TAOM
 succeeded in 409ms:
 M AGENTS.md
 M CHANGELOG.md
 M Main/IoC.cs
 M Main/SubModule.cs
 M Main/_Module/ModuleData/Languages/BR/language_data.xml
 M Main/_Module/ModuleData/Languages/CNs/language_data.xml
 M Main/_Module/ModuleData/Languages/CNt/language_data.xml
 M Main/_Module/ModuleData/Languages/DE/language_data.xml
 M Main/_Module/ModuleData/Languages/FR/language_data.xml
 M Main/_Module/ModuleData/Languages/IT/language_data.xml
 M Main/_Module/ModuleData/Languages/JP/language_data.xml
 M Main/_Module/ModuleData/Languages/KO/language_data.xml
 M Main/_Module/ModuleData/Languages/PL/language_data.xml
 M Main/_Module/ModuleData/Languages/RU/language_data.xml
 M Main/_Module/ModuleData/Languages/SP/language_data.xml
 M Main/_Module/ModuleData/Languages/TR/language_data.xml
 M Main/_Module/ModuleData/troops/troops_dale.xml
 M Main/_Module/SubModule.xml
 M TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs
 M docs/features/lotr-issues.md
 M docs/reviews/REVIEW-LOG.md
 M tools/generate_dale_troops.py
?? Main/Adapters/ILotrIssueGiverAdapter.cs
?? Main/Adapters/ILotrIssueRewardAdapter.cs
?? Main/Adapters/LotrIssueGiverAdapter.cs
?? Main/Adapters/LotrIssueRewardAdapter.cs
?? Main/Features/LotrIssues/
?? Main/_Module/ModuleData/Languages/BR/std_taom_lotr_issue_strings_por-BR.xml
?? Main/_Module/ModuleData/Languages/CNs/std_taom_lotr_issue_strings_zho-CN.xml
?? Main/_Module/ModuleData/Languages/CNt/std_taom_lotr_issue_strings_zho-HK.xml
?? Main/_Module/ModuleData/Languages/DE/std_taom_lotr_issue_strings_deu-DE.xml
?? Main/_Module/ModuleData/Languages/FR/std_taom_lotr_issue_strings_fre-FR.xml
?? Main/_Module/ModuleData/Languages/IT/std_taom_lotr_issue_strings_ita-IT.xml
?? Main/_Module/ModuleData/Languages/JP/std_taom_lotr_issue_strings_jpn-JP.xml
?? Main/_Module/ModuleData/Languages/KO/std_taom_lotr_issue_strings_kor-KO.xml
?? Main/_Module/ModuleData/Languages/PL/std_taom_lotr_issue_strings_pol-PL.xml
?? Main/_Module/ModuleData/Languages/RU/std_taom_lotr_issue_strings_rus-RU.xml
?? Main/_Module/ModuleData/Languages/SP/std_taom_lotr_issue_strings_spa-LA.xml
?? Main/_Module/ModuleData/Languages/TR/std_taom_lotr_issue_strings_tur-TR.xml
?? Main/_Module/ModuleData/lotr_issues/
?? Main/_Module/ModuleData/taom_lotr_issue_strings.xml
?? TAOM.Tests/Features/LotrIssues/
?? docs/reviews/codex-adversarial-lotr-issues-2026-06-17.md
?? docs/reviews/codex-adversarial-lotr-issues-2026-06-17.prompt.md
?? docs/reviews/codex-adversarial-lotr-issues-full-2026-06-20.md
?? docs/reviews/codex-adversarial-lotr-issues-full-2026-06-20.prompt.md
?? docs/reviews/rca-lotr-issues-wave0-2026-06-17.md
?? tools/_harvest_lotr_issue_strings.py
?? tools/_scaffold_lotr_issue_lang.py
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 523ms:
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Loads LOTR issue definitions from <c>lotr_issues/taom_lotr_issues.xml</c> and validates them per
/// .claude/rules/csharp-architecture.md "Config Providers MUST Validate": an invalid issue is SKIPPED
/// with a warning; a recoverable bad field (non-finite/negative scalar, bad source scheme) is COERCED to
/// the compiled default with a warning. A summary warning fires when any entry was coerced. Singleton —
/// cached for the whole process, so retuning needs an application restart.
/// </summary>
public class LotrIssueConfigProvider : ILotrIssueConfigProvider
{
    private static readonly string[] ValidTroopSources = { "basic", "elite", "bandit", "mount", "prisoners" };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private List<LotrIssueDefinition> _issues;

    public LotrIssueConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<LotrIssueDefinition> LoadIssues()
    {
        if (_issues != null) return _issues;

        _issues = new List<LotrIssueDefinition>();
        var path = Path.Combine(_pathService.ModuleDataPath, "lotr_issues", "taom_lotr_issues.xml");
        if (!File.Exists(path))
        {
            _logger.LogInfo($"LotrIssues: no issue file at '{path}' — no custom issues will spawn (vanilla issues are still suppressed)");
            return _issues;
        }

        try
        {
            _issues = ParseIssues(XDocument.Load(path));
            _logger.LogInfo($"LotrIssues: loaded {_issues.Count} valid issue definition(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"LotrIssues: failed to load issue XML: {ex.Message}");
            _issues = new List<LotrIssueDefinition>();
        }

        return _issues;
    }

    /// <summary>Parse + validate issues from an in-memory document. Internal for unit testing.</summary>
    internal List<LotrIssueDefinition> ParseIssues(XDocument doc)
    {
        var result = new List<LotrIssueDefinition>();
        var root = doc?.Root;
        if (root == null) return result;

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coerced = 0;

        foreach (var el in root.Elements("LotrIssue"))
        {
            var issue = ParseIssue(el, seenIds, ref coerced);
            if (issue != null) result.Add(issue);
        }

        if (coerced > 0)
            _logger.LogWarning($"LotrIssues: {coerced} issue field(s) were invalid and reverted to defaults — see prior warnings.");

        return result;
    }

    private LotrIssueDefinition ParseIssue(XElement el, HashSet<string> seenIds, ref int coerced)
    {
        var id = el.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("LotrIssues: skipping a <LotrIssue> with no id");
            return null;
        }
        if (!seenIds.Add(id))
        {
            _logger.LogWarning($"LotrIssues: skipping duplicate issue id '{id}'");
            return null;
        }

        if (!Enum.TryParse<LotrIssueTemplate>(el.Attribute("template")?.Value, true, out var template))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — unknown template '{el.Attribute("template")?.Value}'");
            return null;
        }
        if (!Enum.TryParse<IssueGiverOccupation>(el.Attribute("giver_occupation")?.Value, true, out var giver))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — unknown giver_occupation '{el.Attribute("giver_occupation")?.Value}'");
            return null;
        }
        if (!Enum.TryParse<IssueFrequencyTier>(el.Attribute("frequency")?.Value, true, out var frequency))
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — unknown/absent frequency '{el.Attribute("frequency")?.Value}', defaulting to Common");
            frequency = IssueFrequencyTier.Common;
            coerced++;
        }

        var count = ParseInt(el, "count", 1);
        if (count <= 0)
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — count must be > 0 (was {count}); an uncompletable issue would soft-lock");
            return null;
        }

        var titleKey = el.Attribute("title_key")?.Value ?? "";
        var descKey = el.Attribute("description_key")?.Value ?? "";
        if (string.IsNullOrEmpty(titleKey) || string.IsNullOrEmpty(descKey))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — title_key and description_key are required");
            return null;
        }

        var countPerDiff = CoerceNonNegFloat(el, "count_per_difficulty", id, ref coerced);
        var rewardGoldPerDiff = CoerceNonNegFloat(el, "reward_gold_per_difficulty", id, ref coerced);
        var rewardGoldBase = CoerceNonNegInt(el, "reward_gold_base", id, ref coerced);
        var rewardRenown = CoerceNonNegInt(el, "reward_renown", id, ref coerced);

        var relationMin = ParseInt(el, "relation_min", -10);
        if (relationMin < -100 || relationMin > 100)
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — relation_min {relationMin} out of [-100,100], reverting to -10");
            relationMin = -10;
            coerced++;
        }

        var itemSource = ValidateItemSource(el.Attribute("item_source")?.Value ?? "", id, ref coerced);
        var troopSource = ValidateTroopSource(el.Attribute("troop_source")?.Value ?? "", id, ref coerced);

        // Wave 0: the DeliverGoods template only resolves "item:<id>" sources. A "category:" (or empty)
        // source would spawn an uncompletable issue that silently never stays alive, so skip it at load
        // with a clear warning until a later wave implements category sourcing in the template.
        if (template == LotrIssueTemplate.DeliverGoods && !itemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — DeliverGoods requires an 'item:<id>' source (got '{el.Attribute("item_source")?.Value}'); 'category:' is not implemented yet.");
            return null;
        }

        var cultures = ParseCultures(el.Attribute("cultures")?.Value);

        var text = new LotrIssueText(
            titleKey,
            descKey,
            el.Attribute("brief_key")?.Value ?? "",
            el.Attribute("accept_key")?.Value ?? "",
            el.Attribute("explanation_key")?.Value ?? "",
            el.Attribute("solution_accept_key")?.Value ?? "",
            el.Attribute("task_key")?.Value ?? "",
            el.Attribute("success_key")?.Value ?? "",
            el.Attribute("fail_key")?.Value ?? "");

        return new LotrIssueDefinition(
            id, template, giver, frequency, cultures, count, countPerDiff,
            itemSource, troopSource, rewardGoldBase, rewardGoldPerDiff, rewardRenown,
            el.Attribute("reward_item")?.Value ?? "", el.Attribute("variant")?.Value ?? "", relationMin, text);
    }

    private string ValidateItemSource(string raw, string id, ref int coerced)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (raw.StartsWith("category:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            return raw;
        _logger.LogWarning($"LotrIssues: issue '{id}' — item_source '{raw}' must be 'category:<X>' or 'item:<X>', clearing");
        coerced++;
        return "";
    }

    private string ValidateTroopSource(string raw, string id, ref int coerced)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        foreach (var v in ValidTroopSources)
            if (string.Equals(v, raw, StringComparison.OrdinalIgnoreCase)) return v.ToLowerInvariant();
        _logger.LogWarning($"LotrIssues: issue '{id}' — troop_source '{raw}' must be one of basic|elite|bandit|mount|prisoners, clearing");
        coerced++;
        return "";
    }

    private float CoerceNonNegFloat(XElement el, string attr, string id, ref int coerced)
    {
        var raw = el.Attribute(attr)?.Value;
        if (raw == null) return 0f;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !FiniteFloatValidator.IsFiniteAtLeast(v, 0f))
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — {attr} '{raw}' is not a finite value >= 0, reverting to 0");
            coerced++;
            return 0f;
        }
        return v;
    }

    private int CoerceNonNegInt(XElement el, string attr, string id, ref int coerced)
    {
        var v = ParseInt(el, attr, 0);
        if (v < 0)
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — {attr} {v} < 0, reverting to 0");
            coerced++;
            return 0;
        }
        return v;
    }

    private static List<string> ParseCultures(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        foreach (var token in raw.Split(','))
        {
            var t = token.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list;
    }

    private static int ParseInt(XElement el, string attr, int defaultValue)
    {
        var raw = el.Attribute(attr)?.Value;
        if (raw == null) return defaultValue;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }
}

 succeeded in 540ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Domain
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\ILotrIssueConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\ILotrIssueService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSuppression.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Domain\IssueFrequencyTier.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Domain\IssueGiverOccupation.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Domain\LotrIssueDefinition.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Domain\LotrIssueTemplate.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\CombatLotrIssue.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverGoodsLotrIssue.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverPersonnelLotrIssue.cs


 succeeded in 501ms:
using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// Combat template: an event-driven count objective that auto-completes on reaching the target. The
/// definition's <c>Variant</c> selects the objective -- "CaptureLords" counts at-war enemy lords the
/// player's party takes prisoner; anything else ("DefeatRaids", default) counts battles the player wins.
/// Covers the vanilla clear-hideout + defeat/capture-target archetypes. No turn-in dialog -- it resolves
/// itself when the count is met. Entry-point layer (ADR-002); decisions delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class CombatLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    public CombatLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int NeededCount
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
    }

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededCount);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}A foe must be dealt with.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}We are beset by enemies, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}What needs doing?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_combat_expl}Deal with our enemies and you will be rewarded.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_combat_soln}I will deal with them.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    protected override int RewardGold
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
    }

    public override IssueFrequency GetFrequency()
    {
        EnsureDef();
        if (_def == null) return IssueFrequency.Common;
        switch (_def.Frequency)
        {
            case IssueFrequencyTier.VeryCommon: return IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueFrequency.Rare;
            default: return IssueFrequency.Common;
        }
    }

    public override bool IssueStayAliveConditions() => true;

    protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
    {
        if (issueEffect == DefaultIssueEffects.SettlementSecurity) return -0.3f;
        if (issueEffect == DefaultIssueEffects.SettlementProsperity) return -0.2f;
        return 0f;
    }

    protected override void OnGameLoad() => EnsureDef();

    protected override void HourlyTick() { }

    protected override void CompleteIssueWithTimedOutConsequences() { }

    protected override QuestBase GenerateIssueQuest(string questId)
    {
        EnsureDef();
        return new CombatLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(25f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold, _def?.Variant ?? "");
    }

    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest for <see cref="CombatLotrIssue"/>: count battles won (or at-war lords captured) until the
/// target is met, then auto-grant the reward and complete. No turn-in dialog. Count logic mirrors the
/// validated CareerQuest WinBattles / HeroPrisonerTaken handlers.
/// </summary>
public class CombatLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private int _neededCount;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private float _difficulty;
    [SaveableField(5)] private string _variant;
    [SaveableField(6)] private int _progress;
    [SaveableField(7)] private JournalLog _log;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    private bool IsCaptureLords => string.Equals(_variant, "CaptureLords", StringComparison.OrdinalIgnoreCase);
    private bool IsWinTournaments => string.Equals(_variant, "WinTournaments", StringComparison.OrdinalIgnoreCase);

    public CombatLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold, string variant)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
        _variant = variant ?? "";
        SetDialogs();
        InitializeQuestOnCreation();
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    public override TextObject Title
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TitleKey)
                ? "{=taom_lotr_issue_fallback_title}A Request for Aid" : _def.Text.TitleKey);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            t.SetTextVariable("COUNT", _neededCount);
            return t;
        }
    }

    public override bool IsRemainingTimeHidden => false;

    private TextObject TaskLogText
    {
        get
        {
            var t = new TextObject(IsCaptureLords
                ? "{=taom_lotr_issue_combat_task_capture}Capture enemy lords ({COUNT})"
                : IsWinTournaments
                    ? "{=taom_lotr_issue_combat_task_tourney}Win tournaments ({COUNT})"
                    : "{=taom_lotr_issue_combat_task_defeat}Defeat the raiders ({COUNT})");
            t.SetTextVariable("COUNT", _neededCount);
            return t;
        }
    }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
    }

    protected override void HourlyTick() { }

    protected override void RegisterEvents()
    {
        if (IsCaptureLords)
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        else if (IsWinTournaments)
            CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
        else
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None || mapEvent.WinningSide != mapEvent.PlayerSide) return;
        Bump();
    }

    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (capturer != PartyBase.MainParty || prisoner == null || !prisoner.IsLord) return;
        var playerFaction = Hero.MainHero.MapFaction;
        if (playerFaction == null || prisoner.MapFaction == null || !prisoner.MapFaction.IsAtWarWith(playerFaction)) return;
        Bump();
    }

    private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
    {
        if (winner == CharacterObject.PlayerCharacter) Bump();
    }

    private void Bump()
    {
        if (!IsOngoing) return;
        _progress++;
        if (_progress > _neededCount) _progress = _neededCount;
        _log?.UpdateCurrentProgress(_progress);
        if (_progress >= _neededCount) Success();
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    protected override void OnTimedOut()
        => AddLog(new TextObject("{=taom_lotr_issue_combat_timeout}You failed to deal with the threat in time."));

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
        OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
            .NpcLine(npcAccept)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .Consequence(QuestAcceptedConsequences)
            .CloseDialog();
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var body = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.DescriptionKey)
            ? "{=taom_lotr_issue_fallback_desc}A foe must be dealt with." : _def.Text.DescriptionKey);
        _log = AddDiscreteLog(body, TaskLogText, _progress, _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }
}

codex
The LotrIssues tree is untracked in this worktree, so `git grep` misses it; I’m reading it directly. The Combat template saves the expected quest state and routes variants with a single event branch, but I still need to verify the engine event/completion contracts before calling those suspects clean.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs -TotalCount 420" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\LotrIssuesCampaignBehavior.cs -TotalCount 320" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Domain\\LotrIssueDefinition.cs -TotalCount 220" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\LotrIssueSaveableTypeDefiner.cs -TotalCount 240" in C:\Users\mikew\source\repos\TAOM
 succeeded in 453ms:
using TaleWorlds.SaveSystem;
using TAOM.Features.LotrIssues.Templates;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Registers the LOTR issue/quest template classes with the TaleWorlds save system. Auto-discovered by
/// the engine (no manual registration). BaseId 726900801 — TAOM-unique, next in the 7269008xx series
/// after CareerQuest (726900701), FormationPreset (726900601), EquipPresets (726900501).
/// </summary>
/// <remarks>
/// The engine global type id is <c>_saveBaseId + localId</c>. TAOM's definer bases step by 100, so the
/// per-class localId MUST start at 101 — that lands the id in the base+100 century block (726900902+),
/// clear of CareerQuest's 726900802. localId 1 would yield 726900802 and collide → "An item with the
/// same key has already been added" at Module.Initialize.
///
/// One (Issue, Quest) pair per template. Wave 0 ships T1 (DeliverGoods) at 101/102. Each later wave
/// appends its pair at the next free localId (103/104, …) — additive, never renumbered, so older saves
/// keep loading.
/// </remarks>
public sealed class LotrIssueSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 726900801;

    public LotrIssueSaveableTypeDefiner() : base(SaveBaseId) { }

    protected override void DefineClassTypes()
    {
        // T1 — DeliverGoods (Wave 0)
        AddClassDefinition(typeof(DeliverGoodsLotrIssue), 101);
        AddClassDefinition(typeof(DeliverGoodsLotrIssueQuest), 102);
        // DeliverPersonnel — bandit-prisoner delivery (Wave 1)
        AddClassDefinition(typeof(DeliverPersonnelLotrIssue), 103);
        AddClassDefinition(typeof(DeliverPersonnelLotrIssueQuest), 104);
        // Combat — defeat-raids / capture-lords (Wave 2)
        AddClassDefinition(typeof(CombatLotrIssue), 105);
        AddClassDefinition(typeof(CombatLotrIssueQuest), 106);
    }
}

 succeeded in 495ms:
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;
using TAOM.Features.LotrIssues.Templates;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// The single host behavior for all LOTR custom issues. One <c>OnCheckForIssueEvent</c> listener asks
/// the pure <see cref="ILotrIssueService"/> which configured issues a polled hero is eligible for, then
/// registers each via <c>IssueManager.AddPotentialIssueData</c>. The definition is carried into the
/// constructed issue through <c>PotentialIssueData.RelatedObject</c> (no closure), so one template type
/// serves many configured issues. Thin entry point (ADR-002) — all decisions live in the service.
/// </summary>
public class LotrIssuesCampaignBehavior : CampaignBehaviorBase
{
    private readonly ILotrIssueService _service;
    private readonly IModLogger _logger;

    public LotrIssuesCampaignBehavior(ILotrIssueService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnCheckForIssueEvent.AddNonSerializedListener(this, OnCheckForIssue);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnCheckForIssue(Hero hero)
    {
        if (hero == null) return;
        var eligible = _service.GetEligibleIssues(new LotrIssueGiverAdapter(hero));
        if (eligible == null || eligible.Count == 0) return;

        foreach (var def in eligible)
        {
            var type = TemplateType(def.Template);
            if (type == null) continue; // template not yet implemented (later waves)
            Campaign.Current.IssueManager.AddPotentialIssueData(hero,
                new PotentialIssueData(OnSelected, type, MapFrequency(def.Frequency), def));
        }
    }

    private static IssueBase OnSelected(in PotentialIssueData pid, Hero issueOwner)
    {
        var def = pid.RelatedObject as LotrIssueDefinition;
        return def == null ? null : CreateIssue(def, issueOwner);
    }

    private static IssueBase CreateIssue(LotrIssueDefinition def, Hero owner)
    {
        switch (def.Template)
        {
            case LotrIssueTemplate.DeliverGoods: return new DeliverGoodsLotrIssue(owner, def);
            case LotrIssueTemplate.DeliverPersonnel: return new DeliverPersonnelLotrIssue(owner, def);
            case LotrIssueTemplate.Combat: return new CombatLotrIssue(owner, def);
            default: return null; // implemented in later waves
        }
    }

    private static Type TemplateType(LotrIssueTemplate t)
    {
        switch (t)
        {
            case LotrIssueTemplate.DeliverGoods: return typeof(DeliverGoodsLotrIssue);
            case LotrIssueTemplate.DeliverPersonnel: return typeof(DeliverPersonnelLotrIssue);
            case LotrIssueTemplate.Combat: return typeof(CombatLotrIssue);
            default: return null;
        }
    }

    private static IssueBase.IssueFrequency MapFrequency(IssueFrequencyTier tier)
    {
        switch (tier)
        {
            case IssueFrequencyTier.VeryCommon: return IssueBase.IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueBase.IssueFrequency.Rare;
            default: return IssueBase.IssueFrequency.Common;
        }
    }
}

 succeeded in 519ms:
using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// "Deliver personnel" template: the player hands over N bandit prisoners to the giver (a gang pressing
/// recruits, or a landlord wanting forced mine labor). Same offer/turn-in shape as DeliverGoods, but the
/// objective tracks bandit prisoners in the player's <c>PrisonRoster</c> rather than an item. Entry-point
/// layer (ADR-002) — count/reward/eligibility decisions delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class DeliverPersonnelLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private IModLogger _logger;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();

    public DeliverPersonnelLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int NeededCount
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
    }

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededCount);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}Someone needs captives delivered.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}I have need of strong backs, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}How can I help?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_pers_expl}Bring me {COUNT} captives and I will pay you well.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_pers_soln}I will bring you captives.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    protected override int RewardGold
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
    }

    public override IssueFrequency GetFrequency()
    {
        EnsureDef();
        if (_def == null) return IssueFrequency.Common;
        switch (_def.Frequency)
        {
            case IssueFrequencyTier.VeryCommon: return IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueFrequency.Rare;
            default: return IssueFrequency.Common;
        }
    }

    public override bool IssueStayAliveConditions() => true;

    protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
    {
        if (issueEffect == DefaultIssueEffects.SettlementProsperity) return -0.2f;
        if (issueEffect == DefaultIssueEffects.SettlementLoyalty) return -0.5f;
        return 0f;
    }

    protected override void OnGameLoad() => EnsureDef();

    protected override void HourlyTick() { }

    protected override void CompleteIssueWithTimedOutConsequences() { }

    protected override QuestBase GenerateIssueQuest(string questId)
    {
        EnsureDef();
        return new DeliverPersonnelLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(20f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold);
    }

    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest for <see cref="DeliverPersonnelLotrIssue"/>: track bandit prisoners in the player's prison
/// roster, turn them in at the giver for the reward. The turn-in gate reads live prison-roster count, not
/// a cached log, so battle/ransom changes can't stale it.
/// </summary>
public class DeliverPersonnelLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private int _neededCount;
    [SaveableField(3)] private int _rewardGold;
    [SaveableField(4)] private float _difficulty;
    [SaveableField(5)] private JournalLog _acceptedLog;
    [SaveableField(6)] private JournalLog _readyLog;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    public DeliverPersonnelLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
        SetDialogs();
        InitializeQuestOnCreation();
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    public override TextObject Title
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TitleKey)
                ? "{=taom_lotr_issue_fallback_title}A Request for Aid" : _def.Text.TitleKey);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            t.SetTextVariable("COUNT", _neededCount);
            return t;
        }
    }

    public override bool IsRemainingTimeHidden => false;

    private TextObject AcceptedLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_pers_accepted}Deliver {COUNT} bandit captives to {QUEST_SETTLEMENT}.");
            t.SetTextVariable("COUNT", _neededCount);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    private TextObject ReadyLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_pers_ready}You have enough captives. Return to {QUEST_SETTLEMENT} to hand them over.");
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
    }

    protected override void HourlyTick() { }

    protected override void RegisterEvents()
    {
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
    }

    protected override void HourlyTickParty(MobileParty mobileParty)
    {
        if (mobileParty == MobileParty.MainParty) Refresh();
    }

    private int CountBanditPrisoners()
    {
        var roster = PartyBase.MainParty.PrisonRoster;
        if (roster == null) return 0;
        int sum = 0;
        for (int i = 0; i < roster.Count; i++)
        {
            var el = roster.GetElementCopyAtIndex(i);
            if (el.Character != null && el.Character.Occupation == Occupation.Bandit) sum += el.Number;
        }
        return sum <= _neededCount ? sum : _neededCount;
    }

    private void Refresh()
    {
        if (_acceptedLog != null) _acceptedLog.UpdateCurrentProgress(CountBanditPrisoners());
        CheckReady();
    }

    private void CheckReady()
    {
        if (_readyLog == null && (_acceptedLog?.CurrentProgress ?? 0) >= _neededCount)
            _readyLog = AddLog(ReadyLogText);
        else if (_readyLog != null && (_acceptedLog?.CurrentProgress ?? 0) < _neededCount)
        {
            RemoveLog(_readyLog);
            _readyLog = null;
        }
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent) => Refresh();

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party == MobileParty.MainParty) Refresh();
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
        => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    protected override void OnTimedOut()
        => AddLog(new TextObject("{=taom_lotr_issue_pers_timeout}You failed to deliver the captives in time."));

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
        var npcHave = new TextObject("{=taom_lotr_issue_pers_have}Have you brought the captives?");
        var npcThanks = new TextObject("{=taom_lotr_issue_pers_thanks}They'll do nicely. You have my thanks.");
        var npcAwait = new TextObject("{=taom_lotr_issue_common_await}We await your return.");

        OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
            .NpcLine(npcAccept)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .Consequence(QuestAcceptedConsequences)
            .CloseDialog();

        DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")
            .NpcLine(npcHave)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .BeginPlayerOptions()
            .PlayerOption(new TextObject("{=taom_lotr_issue_pers_turnin}Yes. Here they are."))
            .ClickableCondition(TurnInClickableConditions)
            .NpcLine(npcThanks)
            .Consequence(() => Campaign.Current.ConversationManager.ConversationEndOneShot += Success)
            .CloseDialog()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_working}Not yet -- I'm working on it."))
            .NpcLine(npcAwait)
            .CloseDialog()
            .EndPlayerOptions()
            .CloseDialog();
    }

    private bool TurnInClickableConditions(out TextObject explanation)
    {
        if (CountBanditPrisoners() >= _neededCount)
        {
            explanation = null;
            return true;
        }
        explanation = new TextObject("{=taom_lotr_issue_pers_not_enough}You don't have enough captives yet.");
        return false;
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var task = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
            ? "{=taom_lotr_issue_pers_task}Take bandit captives" : _def.Text.TaskKey);
        task.SetTextVariable("COUNT", _neededCount);
        _acceptedLog = AddDiscreteLog(AcceptedLogText, task, CountBanditPrisoners(), _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        RemoveBanditPrisoners(_neededCount);
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }

    private void RemoveBanditPrisoners(int count)
    {
        var roster = PartyBase.MainParty.PrisonRoster;
        if (roster == null) return;
        for (int i = roster.Count - 1; i >= 0 && count > 0; i--)
        {
            var el = roster.GetElementCopyAtIndex(i);
            if (el.Character == null || el.Character.Occupation != Occupation.Bandit) continue;
            int take = Math.Min(count, el.Number);
            roster.AddToCounts(el.Character, -take);
            count -= take;
        }
    }
}

 succeeded in 494ms:
using System.Collections.Generic;

namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// The localized text keys a LOTR issue + its quest render. All keys are <c>{=key}default</c> form;
/// the template fills runtime variables (<c>{ISSUE_SETTLEMENT}</c>, <c>{COUNT}</c>, <c>{TARGET}</c>, …)
/// via <c>SetTextVariable</c>. Immutable; never null (empty string when absent).
/// </summary>
public sealed class LotrIssueText
{
    public string TitleKey { get; }
    public string DescriptionKey { get; }
    public string BriefKey { get; }
    public string AcceptKey { get; }
    public string ExplanationKey { get; }
    public string SolutionAcceptKey { get; }
    public string TaskKey { get; }
    public string SuccessKey { get; }
    public string FailKey { get; }

    public LotrIssueText(
        string titleKey, string descriptionKey, string briefKey, string acceptKey,
        string explanationKey, string solutionAcceptKey, string taskKey, string successKey, string failKey)
    {
        TitleKey = titleKey ?? "";
        DescriptionKey = descriptionKey ?? "";
        BriefKey = briefKey ?? "";
        AcceptKey = acceptKey ?? "";
        ExplanationKey = explanationKey ?? "";
        SolutionAcceptKey = solutionAcceptKey ?? "";
        TaskKey = taskKey ?? "";
        SuccessKey = successKey ?? "";
        FailKey = failKey ?? "";
    }
}

/// <summary>
/// One config-driven LOTR issue: a generic <see cref="Template"/> mechanic parameterized with the
/// content (giver gate, frequency, culture filter, target counts, reward, item/troop sourcing, text).
/// Immutable; authored in <c>lotr_issues/taom_lotr_issues.xml</c> and validated by
/// <c>LotrIssueConfigProvider</c>. TaleWorlds-free so the domain + service are unit-testable.
/// </summary>
public sealed class LotrIssueDefinition
{
    public string Id { get; }
    public LotrIssueTemplate Template { get; }
    public IssueGiverOccupation GiverOccupation { get; }
    public IssueFrequencyTier Frequency { get; }

    /// <summary>Runtime culture StringIds this issue may spawn for; empty = all cultures.</summary>
    public IReadOnlyList<string> Cultures { get; }

    /// <summary>Base objective count (items to deliver, bands to clear, etc.); always &gt; 0 for count templates.</summary>
    public int Count { get; }

    /// <summary>Additional count scaled by issue difficulty (0..1); added to <see cref="Count"/>.</summary>
    public float CountPerDifficulty { get; }

    /// <summary>Item sourcing scheme: <c>category:&lt;ItemCategory&gt;</c> or <c>item:&lt;DefaultItemsMember&gt;</c>; empty if none.</summary>
    public string ItemSource { get; }

    /// <summary>Troop sourcing scheme: <c>basic|elite|bandit|mount|prisoners</c>; empty if none.</summary>
    public string TroopSource { get; }

    public int RewardGoldBase { get; }
    public float RewardGoldPerDifficulty { get; }
    public int RewardRenown { get; }

    /// <summary>Optional bonus item id granted on completion; empty if none.</summary>
    public string RewardItem { get; }

    /// <summary>Template-specific mode string (e.g. Combat "DefeatRaids"/"CaptureLords"); "" if none.</summary>
    public string Variant { get; }

    /// <summary>Minimum player↔giver relation for the issue to offer (engine default band is -10).</summary>
    public int RelationMin { get; }

    public LotrIssueText Text { get; }

    public LotrIssueDefinition(
        string id,
        LotrIssueTemplate template,
        IssueGiverOccupation giverOccupation,
        IssueFrequencyTier frequency,
        IReadOnlyList<string> cultures,
        int count,
        float countPerDifficulty,
        string itemSource,
        string troopSource,
        int rewardGoldBase,
        float rewardGoldPerDifficulty,
        int rewardRenown,
        string rewardItem,
        string variant,
        int relationMin,
        LotrIssueText text)
    {
        Id = id;
        Template = template;
        GiverOccupation = giverOccupation;
        Frequency = frequency;
        Cultures = cultures ?? new List<string>();
        Count = count;
        CountPerDifficulty = countPerDifficulty;
        ItemSource = itemSource ?? "";
        TroopSource = troopSource ?? "";
        RewardGoldBase = rewardGoldBase;
        RewardGoldPerDifficulty = rewardGoldPerDifficulty;
        RewardRenown = rewardRenown;
        RewardItem = rewardItem ?? "";
        Variant = variant ?? "";
        RelationMin = relationMin;
        Text = text ?? new LotrIssueText("", "", "", "", "", "", "", "", "");
    }

    /// <summary>True if this issue may spawn for the given runtime culture StringId.</summary>
    public bool AppliesToCulture(string cultureStringId)
    {
        if (Cultures.Count == 0) return true;
        if (string.IsNullOrEmpty(cultureStringId)) return false;
        foreach (var c in Cultures)
            if (string.Equals(c, cultureStringId, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

codex
Next I’m validating the authored data, not just the code: duplicate ids, template/variant parsing, culture ids, text keys, and item ids against actual XML content.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -TotalCount 260" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests\\Features\\LotrIssues -Recurse | Select-Object FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -TotalCount 120" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\SubModule.xml -Pattern \"taom_lotr_issue_strings|GameText|lotr\" -CaseSensitive:"'$false | ForEach-Object { "$($_.LineNumber):$($_.Line)" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 513ms:
<?xml version="1.0" encoding="utf-8"?>
<strings>
  <!-- TAOM LOTR custom-issue player-facing text. English source-of-truth for the 12-language
       pipeline. Defaults also embed inline in taom_lotr_issues.xml so text renders pre-translation.
       Harvested by tools/_harvest_lotr_issue_strings.py. -->
  <string id="taom_lotr_issue_grain_title" text="{=taom_lotr_issue_grain_title}{ISSUE_SETTLEMENT} Needs Seed Grain" />
  <string id="taom_lotr_issue_grain_desc" text="{=taom_lotr_issue_grain_desc}The folk of {ISSUE_SETTLEMENT} have lost their seed grain and will go hungry unless the fields are sown." />
  <string id="taom_lotr_issue_grain_brief" text="{=taom_lotr_issue_grain_brief}The shadow has been hard on us, traveller. Rats and a bitter winter took our seed-corn. Without grain to sow, we will not last the year." />
  <string id="taom_lotr_issue_grain_accept" text="{=taom_lotr_issue_grain_accept}Is there nothing to be done?" />
  <string id="taom_lotr_issue_grain_expl" text="{=taom_lotr_issue_grain_expl}If you could bring us {COUNT} measures of grain, we could sow the fields before the season turns. We have no coin to buy so much ourselves." />
  <string id="taom_lotr_issue_grain_soln" text="{=taom_lotr_issue_grain_soln}I will find that grain for you." />
  <string id="taom_lotr_issue_grain_task" text="{=taom_lotr_issue_grain_task}Collect grain ({COUNT})" />
  <string id="taom_lotr_issue_supplies_title" text="{=taom_lotr_issue_supplies_title}A Host in Need of Provender" />
  <string id="taom_lotr_issue_supplies_desc" text="{=taom_lotr_issue_supplies_desc}A lord mustering for war needs grain to feed the gathered host before it marches." />
  <string id="taom_lotr_issue_supplies_brief" text="{=taom_lotr_issue_supplies_brief}My soldiers gather, but a sword does not fill a belly. Without grain to feed them, this host will scatter before it ever takes the field." />
  <string id="taom_lotr_issue_supplies_accept" text="{=taom_lotr_issue_supplies_accept}How much grain do you need?" />
  <string id="taom_lotr_issue_supplies_expl" text="{=taom_lotr_issue_supplies_expl}Bring me {COUNT} measures of grain and my men will march on full stomachs. Do this and the realm will remember it." />
  <string id="taom_lotr_issue_supplies_soln" text="{=taom_lotr_issue_supplies_soln}I will bring grain for your host." />
  <string id="taom_lotr_issue_supplies_task" text="{=taom_lotr_issue_supplies_task}Bring grain to the host ({COUNT})" />
  <string id="taom_lotr_issue_draught_title" text="{=taom_lotr_issue_draught_title}{ISSUE_SETTLEMENT} Needs Beasts of Burden" />
  <string id="taom_lotr_issue_draught_desc" text="{=taom_lotr_issue_draught_desc}The village of {ISSUE_SETTLEMENT} has lost its draught oxen and cannot work the fields." />
  <string id="taom_lotr_issue_draught_brief" text="{=taom_lotr_issue_draught_brief}Our oxen were taken -- some by raiders, some by hunger. Without beasts to pull the plough, the soil lies hard and the fields go unsown." />
  <string id="taom_lotr_issue_draught_accept" text="{=taom_lotr_issue_draught_accept}What would help?" />
  <string id="taom_lotr_issue_draught_expl" text="{=taom_lotr_issue_draught_expl}If you could drive {COUNT} head of cattle to us, we could break the ground again before the season turns." />
  <string id="taom_lotr_issue_draught_soln" text="{=taom_lotr_issue_draught_soln}I will bring you cattle." />
  <string id="taom_lotr_issue_draught_task" text="{=taom_lotr_issue_draught_task}Deliver cattle ({COUNT})" />
  <string id="taom_lotr_issue_crafting_title" text="{=taom_lotr_issue_crafting_title}{ISSUE_SETTLEMENT} Needs Iron" />
  <string id="taom_lotr_issue_crafting_desc" text="{=taom_lotr_issue_crafting_desc}The smith of {ISSUE_SETTLEMENT} has run dry of iron and cannot mend tool nor blade." />
  <string id="taom_lotr_issue_crafting_brief" text="{=taom_lotr_issue_crafting_brief}The forge stands cold, traveller. Without iron the smith cannot shoe a horse nor mend a ploughshare, and the village suffers for it." />
  <string id="taom_lotr_issue_crafting_accept" text="{=taom_lotr_issue_crafting_accept}Where would you get iron?" />
  <string id="taom_lotr_issue_crafting_expl" text="{=taom_lotr_issue_crafting_expl}Bring us {COUNT} bars of iron and the forge will ring again. We have not the coin to buy so much ourselves." />
  <string id="taom_lotr_issue_crafting_soln" text="{=taom_lotr_issue_crafting_soln}I will find you iron." />
  <string id="taom_lotr_issue_crafting_task" text="{=taom_lotr_issue_crafting_task}Deliver iron ({COUNT})" />
  <string id="taom_lotr_issue_tools_title" text="{=taom_lotr_issue_tools_title}{ISSUE_SETTLEMENT} Needs Tools" />
  <string id="taom_lotr_issue_tools_desc" text="{=taom_lotr_issue_tools_desc}The folk of {ISSUE_SETTLEMENT} lack the tools to work their land and craft their goods." />
  <string id="taom_lotr_issue_tools_brief" text="{=taom_lotr_issue_tools_brief}Our scythes are rusted through and our hammers broken. Honest work is slow work without honest tools." />
  <string id="taom_lotr_issue_tools_accept" text="{=taom_lotr_issue_tools_accept}What do you need?" />
  <string id="taom_lotr_issue_tools_expl" text="{=taom_lotr_issue_tools_expl}If you brought us {COUNT} sets of tools, the village could set itself right and prosper once more." />
  <string id="taom_lotr_issue_tools_soln" text="{=taom_lotr_issue_tools_soln}I will bring you tools." />
  <string id="taom_lotr_issue_tools_task" text="{=taom_lotr_issue_tools_task}Deliver tools ({COUNT})" />
  <string id="taom_lotr_issue_horses_title" text="{=taom_lotr_issue_horses_title}A Lord in Want of Horses" />
  <string id="taom_lotr_issue_horses_desc" text="{=taom_lotr_issue_horses_desc}A lord has lost mounts to war and attrition and needs fresh horses for his riders." />
  <string id="taom_lotr_issue_horses_brief" text="{=taom_lotr_issue_horses_brief}War is cruel to horses, traveller. Mine have foundered on hard roads, and a rider without a mount is but a slow spearman." />
  <string id="taom_lotr_issue_horses_accept" text="{=taom_lotr_issue_horses_accept}How many horses do you need?" />
  <string id="taom_lotr_issue_horses_expl" text="{=taom_lotr_issue_horses_expl}Bring me {COUNT} sound horses and my riders will ride again. You will be paid well for them." />
  <string id="taom_lotr_issue_horses_soln" text="{=taom_lotr_issue_horses_soln}I will bring you horses." />
  <string id="taom_lotr_issue_horses_task" text="{=taom_lotr_issue_horses_task}Deliver horses ({COUNT})" />
  <string id="taom_lotr_issue_recruits_title" text="{=taom_lotr_issue_recruits_title}Fresh Blood for the Gang" />
  <string id="taom_lotr_issue_recruits_desc" text="{=taom_lotr_issue_recruits_desc}A gang leader wants captured outlaws pressed into his service." />
  <string id="taom_lotr_issue_recruits_brief" text="{=taom_lotr_issue_recruits_brief}A gang is only as strong as its numbers, and mine are thin. Bring me captives with no love for the law and I'll make use of them." />
  <string id="taom_lotr_issue_recruits_accept" text="{=taom_lotr_issue_recruits_accept}What kind of captives?" />
  <string id="taom_lotr_issue_recruits_expl" text="{=taom_lotr_issue_recruits_expl}Bring me {COUNT} bandit captives and I'll see them put to work. There's coin in it for you, no questions asked." />
  <string id="taom_lotr_issue_recruits_soln" text="{=taom_lotr_issue_recruits_soln}I'll bring you captives." />
  <string id="taom_lotr_issue_recruits_task" text="{=taom_lotr_issue_recruits_task}Deliver bandit captives ({COUNT})" />
  <string id="taom_lotr_issue_laborers_title" text="{=taom_lotr_issue_laborers_title}Hands for the Mines" />
  <string id="taom_lotr_issue_laborers_desc" text="{=taom_lotr_issue_laborers_desc}A landowner needs strong backs for hard labor and will take captured bandits." />
  <string id="taom_lotr_issue_laborers_brief" text="{=taom_lotr_issue_laborers_brief}The diggings need workers and free men won't go down there. Bring me bandits in chains and they'll earn their keep with a pick." />
  <string id="taom_lotr_issue_laborers_accept" text="{=taom_lotr_issue_laborers_accept}You want prisoners?" />
  <string id="taom_lotr_issue_laborers_expl" text="{=taom_lotr_issue_laborers_expl}Bring me {COUNT} bandit captives for the labor gangs and I'll pay you for the trouble of taking them." />
  <string id="taom_lotr_issue_laborers_soln" text="{=taom_lotr_issue_laborers_soln}I'll bring you captives." />
  <string id="taom_lotr_issue_laborers_task" text="{=taom_lotr_issue_laborers_task}Deliver bandit captives ({COUNT})" />
  <string id="taom_lotr_issue_banditbase_title" text="{=taom_lotr_issue_banditbase_title}A Lair Near {ISSUE_SETTLEMENT}" />
  <string id="taom_lotr_issue_banditbase_desc" text="{=taom_lotr_issue_banditbase_desc}A nest of raiders preys on the roads near {ISSUE_SETTLEMENT}." />
  <string id="taom_lotr_issue_banditbase_brief" text="{=taom_lotr_issue_banditbase_brief}There is a foul nest of them in the hills, traveller. They take our goods and our people, and the watch is too few to root them out." />
  <string id="taom_lotr_issue_banditbase_accept" text="{=taom_lotr_issue_banditbase_accept}Where is this lair?" />
  <string id="taom_lotr_issue_banditbase_expl" text="{=taom_lotr_issue_banditbase_expl}Break the raiders that haunt our roads and we will reward you. The land will breathe easier for it." />
  <string id="taom_lotr_issue_banditbase_soln" text="{=taom_lotr_issue_banditbase_soln}I will scour them out." />
  <string id="taom_lotr_issue_outlaws_title" text="{=taom_lotr_issue_outlaws_title}Outlaws on the Trade Roads" />
  <string id="taom_lotr_issue_outlaws_desc" text="{=taom_lotr_issue_outlaws_desc}Raiding bands are strangling a merchant's trade and must be broken." />
  <string id="taom_lotr_issue_outlaws_brief" text="{=taom_lotr_issue_outlaws_brief}My caravans cannot pass for the raiders on the roads. Break enough of their bands and trade may flow again." />
  <string id="taom_lotr_issue_outlaws_accept" text="{=taom_lotr_issue_outlaws_accept}How many bands?" />
  <string id="taom_lotr_issue_outlaws_expl" text="{=taom_lotr_issue_outlaws_expl}Defeat the bands plaguing the roads -- a few good victories will scatter the rest -- and I will pay you handsomely." />
  <string id="taom_lotr_issue_outlaws_soln" text="{=taom_lotr_issue_outlaws_soln}I will clear the roads." />
  <string id="taom_lotr_issue_poachers_title" text="{=taom_lotr_issue_poachers_title}A Gang of Poachers" />
  <string id="taom_lotr_issue_poachers_desc" text="{=taom_lotr_issue_poachers_desc}A large band of poachers is ruining a merchant's hunting trade." />
  <string id="taom_lotr_issue_poachers_brief" text="{=taom_lotr_issue_poachers_brief}Poachers have grown bold and many -- they strip the woods bare and drive off honest hunters. Someone must put them down." />
  <string id="taom_lotr_issue_poachers_accept" text="{=taom_lotr_issue_poachers_accept}Where do they range?" />
  <string id="taom_lotr_issue_poachers_expl" text="{=taom_lotr_issue_poachers_expl}Defeat the poacher band and the woods will be ours again. There is coin in it for you." />
  <string id="taom_lotr_issue_poachers_soln" text="{=taom_lotr_issue_poachers_soln}I will deal with them." />
  <string id="taom_lotr_issue_captured_title" text="{=taom_lotr_issue_captured_title}My Men in Chains" />
  <string id="taom_lotr_issue_captured_desc" text="{=taom_lotr_issue_captured_desc}A gang leader's people were taken by bounty hunters and he wants them freed." />
  <string id="taom_lotr_issue_captured_brief" text="{=taom_lotr_issue_captured_brief}Bounty hunters took some of my lads and mean to sell them. Break the curs holding them and there's coin in it -- quiet coin." />
  <string id="taom_lotr_issue_captured_accept" text="{=taom_lotr_issue_captured_accept}Who holds them?" />
  <string id="taom_lotr_issue_captured_expl" text="{=taom_lotr_issue_captured_expl}Find the band that took my men and break it. Do that and you'll be paid, no questions asked." />
  <string id="taom_lotr_issue_captured_soln" text="{=taom_lotr_issue_captured_soln}I'll free your men." />
  <string id="taom_lotr_issue_deserters_title" text="{=taom_lotr_issue_deserters_title}Deserters Prey on {ISSUE_SETTLEMENT}" />
  <string id="taom_lotr_issue_deserters_desc" text="{=taom_lotr_issue_deserters_desc}A band of deserters extorts {ISSUE_SETTLEMENT} and has killed those who refused." />
  <string id="taom_lotr_issue_deserters_brief" text="{=taom_lotr_issue_deserters_brief}Broken men with swords come for our grain and our coin, and they have spilled blood. We cannot fight them alone." />
  <string id="taom_lotr_issue_deserters_accept" text="{=taom_lotr_issue_deserters_accept}I will help. Where are they?" />
  <string id="taom_lotr_issue_deserters_expl" text="{=taom_lotr_issue_deserters_expl}Lie in wait and break the deserters when they come. Rid us of them and you will have our thanks and our coin." />
  <string id="taom_lotr_issue_deserters_soln" text="{=taom_lotr_issue_deserters_soln}I will end their raiding." />
  <string id="taom_lotr_issue_commons_title" text="{=taom_lotr_issue_commons_title}Trouble at the Commons" />
  <string id="taom_lotr_issue_commons_desc" text="{=taom_lotr_issue_commons_desc}Armed rivals are seizing a village's grazing commons by force." />
  <string id="taom_lotr_issue_commons_brief" text="{=taom_lotr_issue_commons_brief}Hard men have taken our grazing land and drive off any who come near. Our herds starve while they hold it." />
  <string id="taom_lotr_issue_commons_accept" text="{=taom_lotr_issue_commons_accept}Who are they?" />
  <string id="taom_lotr_issue_commons_expl" text="{=taom_lotr_issue_commons_expl}Drive the rivals off our commons by force and the pasture is ours again. We will pay what we can." />
  <string id="taom_lotr_issue_commons_soln" text="{=taom_lotr_issue_commons_soln}I will drive them off." />
  <string id="taom_lotr_issue_rivalgang_title" text="{=taom_lotr_issue_rivalgang_title}A Rival Moves In" />
  <string id="taom_lotr_issue_rivalgang_desc" text="{=taom_lotr_issue_rivalgang_desc}A rival gang is muscling in on a crime boss's turf and must be put down." />
  <string id="taom_lotr_issue_rivalgang_brief" text="{=taom_lotr_issue_rivalgang_brief}Another outfit thinks it can take what's mine. Break them in the alleys and the streets are quiet again -- and you're paid." />
  <string id="taom_lotr_issue_rivalgang_accept" text="{=taom_lotr_issue_rivalgang_accept}Where do they gather?" />
  <string id="taom_lotr_issue_rivalgang_expl" text="{=taom_lotr_issue_rivalgang_expl}Defeat the rival gang and make an example of them. Do it and there's good coin waiting." />
  <string id="taom_lotr_issue_rivalgang_soln" text="{=taom_lotr_issue_rivalgang_soln}Consider it done." />
  <string id="taom_lotr_issue_spy_title" text="{=taom_lotr_issue_spy_title}An Enemy Spy" />
  <string id="taom_lotr_issue_spy_desc" text="{=taom_lotr_issue_spy_desc}A lord suspects an enemy agent moves against him and wants the spy's party hunted down." />
  <string id="taom_lotr_issue_spy_brief" text="{=taom_lotr_issue_spy_brief}There is a viper in the grass -- an agent of my enemies, moving with a band of cutthroats. Find them and end them before they strike." />
  <string id="taom_lotr_issue_spy_accept" text="{=taom_lotr_issue_spy_accept}How will I know them?" />
  <string id="taom_lotr_issue_spy_expl" text="{=taom_lotr_issue_spy_expl}Hunt down the spy's party and destroy it. Rid me of this threat and you will be well rewarded." />
  <string id="taom_lotr_issue_spy_soln" text="{=taom_lotr_issue_spy_soln}I will hunt them down." />
  <string id="taom_lotr_issue_revolt_title" text="{=taom_lotr_issue_revolt_title}A Renegade Noble" />
  <string id="taom_lotr_issue_revolt_desc" text="{=taom_lotr_issue_revolt_desc}A lesser noble has raised the peasants in revolt and must be put down." />
  <string id="taom_lotr_issue_revolt_brief" text="{=taom_lotr_issue_revolt_brief}A petty lordling stirs the peasants against their betters and gathers armed men. Break his strength before the revolt spreads." />
  <string id="taom_lotr_issue_revolt_accept" text="{=taom_lotr_issue_revolt_accept}Where does he muster?" />
  <string id="taom_lotr_issue_revolt_expl" text="{=taom_lotr_issue_revolt_expl}Defeat the renegade and his rabble and order will be restored. You will be rewarded for your service." />
  <string id="taom_lotr_issue_revolt_soln" text="{=taom_lotr_issue_revolt_soln}I will put down this revolt." />
  <string id="taom_lotr_issue_company_title" text="{=taom_lotr_issue_company_title}A Company of Trouble" />
  <string id="taom_lotr_issue_company_desc" text="{=taom_lotr_issue_company_desc}A band of sellswords hired to harass a lord must be driven off." />
  <string id="taom_lotr_issue_company_brief" text="{=taom_lotr_issue_company_brief}My enemies have set a company of sellswords on my lands. They burn and pillage under another's coin. Break them for me." />
  <string id="taom_lotr_issue_company_accept" text="{=taom_lotr_issue_company_accept}Where do they raid?" />
  <string id="taom_lotr_issue_company_expl" text="{=taom_lotr_issue_company_expl}Defeat the mercenary company and my lands will know peace again. I will pay you well." />

 succeeded in 525ms:
<?xml version="1.0" encoding="utf-8"?>
<!--
  TAOM LOTR custom issues (Wave 0 — T1 DeliverGoods proof-of-life).
  Loaded + validated by LotrIssueConfigProvider (NOT a GameText/SubModule.xml node).
  Each <LotrIssue> instantiates a generic template (template=) parameterized by these attributes.
  Text keys embed {=KEY}default inline so they render before the Wave-7 localization harvest;
  {ISSUE_SETTLEMENT}/{COUNT}/{ITEM} are filled at runtime by the template.
  item_source: "item:<id>" (Wave 0) or "category:<cat>" (Wave 1+). troop_source: basic|elite|bandit|mount|prisoners.
-->
<LotrIssues>
  <LotrIssue id="lotr_headman_needs_grain"
             template="DeliverGoods"
             giver_occupation="Headman"
             frequency="Common"
             cultures=""
             count="12"
             count_per_difficulty="120"
             item_source="item:grain"
             troop_source=""
             reward_gold_base="0"
             reward_gold_per_difficulty="1500"
             reward_renown="1"
             reward_item=""
             relation_min="-10"
             title_key="{=taom_lotr_issue_grain_title}{ISSUE_SETTLEMENT} Needs Seed Grain"
             description_key="{=taom_lotr_issue_grain_desc}The folk of {ISSUE_SETTLEMENT} have lost their seed grain and will go hungry unless the fields are sown."
             brief_key="{=taom_lotr_issue_grain_brief}The shadow has been hard on us, traveller. Rats and a bitter winter took our seed-corn. Without grain to sow, we will not last the year."
             accept_key="{=taom_lotr_issue_grain_accept}Is there nothing to be done?"
             explanation_key="{=taom_lotr_issue_grain_expl}If you could bring us {COUNT} measures of grain, we could sow the fields before the season turns. We have no coin to buy so much ourselves."
             solution_accept_key="{=taom_lotr_issue_grain_soln}I will find that grain for you."
             task_key="{=taom_lotr_issue_grain_task}Collect grain ({COUNT})" />

  <!-- Wave 1 — remaining T1 DeliverGoods (single-item deliveries; reuse the proof-of-life template). -->

  <LotrIssue id="lotr_army_needs_supplies"
             template="DeliverGoods" giver_occupation="Lord" frequency="VeryCommon" cultures=""
             count="20" count_per_difficulty="160" item_source="item:grain"
             reward_gold_base="0" reward_gold_per_difficulty="2200" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_supplies_title}A Host in Need of Provender"
             description_key="{=taom_lotr_issue_supplies_desc}A lord mustering for war needs grain to feed the gathered host before it marches."
             brief_key="{=taom_lotr_issue_supplies_brief}My soldiers gather, but a sword does not fill a belly. Without grain to feed them, this host will scatter before it ever takes the field."
             accept_key="{=taom_lotr_issue_supplies_accept}How much grain do you need?"
             explanation_key="{=taom_lotr_issue_supplies_expl}Bring me {COUNT} measures of grain and my men will march on full stomachs. Do this and the realm will remember it."
             solution_accept_key="{=taom_lotr_issue_supplies_soln}I will bring grain for your host."
             task_key="{=taom_lotr_issue_supplies_task}Bring grain to the host ({COUNT})" />

  <LotrIssue id="lotr_village_needs_draught"
             template="DeliverGoods" giver_occupation="Headman" frequency="Common" cultures=""
             count="6" count_per_difficulty="20" item_source="item:cow"
             reward_gold_base="0" reward_gold_per_difficulty="1100" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_draught_title}{ISSUE_SETTLEMENT} Needs Beasts of Burden"
             description_key="{=taom_lotr_issue_draught_desc}The village of {ISSUE_SETTLEMENT} has lost its draught oxen and cannot work the fields."
             brief_key="{=taom_lotr_issue_draught_brief}Our oxen were taken -- some by raiders, some by hunger. Without beasts to pull the plough, the soil lies hard and the fields go unsown."
             accept_key="{=taom_lotr_issue_draught_accept}What would help?"
             explanation_key="{=taom_lotr_issue_draught_expl}If you could drive {COUNT} head of cattle to us, we could break the ground again before the season turns."
             solution_accept_key="{=taom_lotr_issue_draught_soln}I will bring you cattle."
             task_key="{=taom_lotr_issue_draught_task}Deliver cattle ({COUNT})" />

  <LotrIssue id="lotr_village_needs_crafting_materials"
             template="DeliverGoods" giver_occupation="RuralNotable" frequency="Rare" cultures=""
             count="8" count_per_difficulty="24" item_source="item:iron"
             reward_gold_base="0" reward_gold_per_difficulty="1400" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_crafting_title}{ISSUE_SETTLEMENT} Needs Iron"
             description_key="{=taom_lotr_issue_crafting_desc}The smith of {ISSUE_SETTLEMENT} has run dry of iron and cannot mend tool nor blade."
             brief_key="{=taom_lotr_issue_crafting_brief}The forge stands cold, traveller. Without iron the smith cannot shoe a horse nor mend a ploughshare, and the village suffers for it."
             accept_key="{=taom_lotr_issue_crafting_accept}Where would you get iron?"
             explanation_key="{=taom_lotr_issue_crafting_expl}Bring us {COUNT} bars of iron and the forge will ring again. We have not the coin to buy so much ourselves."
             solution_accept_key="{=taom_lotr_issue_crafting_soln}I will find you iron."
             task_key="{=taom_lotr_issue_crafting_task}Deliver iron ({COUNT})" />

  <LotrIssue id="lotr_village_needs_tools"
             template="DeliverGoods" giver_occupation="Headman" frequency="Common" cultures=""
             count="10" count_per_difficulty="30" item_source="item:tools"
             reward_gold_base="0" reward_gold_per_difficulty="1200" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_tools_title}{ISSUE_SETTLEMENT} Needs Tools"
             description_key="{=taom_lotr_issue_tools_desc}The folk of {ISSUE_SETTLEMENT} lack the tools to work their land and craft their goods."
             brief_key="{=taom_lotr_issue_tools_brief}Our scythes are rusted through and our hammers broken. Honest work is slow work without honest tools."
             accept_key="{=taom_lotr_issue_tools_accept}What do you need?"
             explanation_key="{=taom_lotr_issue_tools_expl}If you brought us {COUNT} sets of tools, the village could set itself right and prosper once more."
             solution_accept_key="{=taom_lotr_issue_tools_soln}I will bring you tools."
             task_key="{=taom_lotr_issue_tools_task}Deliver tools ({COUNT})" />

  <LotrIssue id="lotr_lord_needs_horses"
             template="DeliverGoods" giver_occupation="Lord" frequency="VeryCommon" cultures=""
             count="6" count_per_difficulty="18" item_source="item:sumpter_horse"
             reward_gold_base="0" reward_gold_per_difficulty="2000" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_horses_title}A Lord in Want of Horses"
             description_key="{=taom_lotr_issue_horses_desc}A lord has lost mounts to war and attrition and needs fresh horses for his riders."
             brief_key="{=taom_lotr_issue_horses_brief}War is cruel to horses, traveller. Mine have foundered on hard roads, and a rider without a mount is but a slow spearman."
             accept_key="{=taom_lotr_issue_horses_accept}How many horses do you need?"
             explanation_key="{=taom_lotr_issue_horses_expl}Bring me {COUNT} sound horses and my riders will ride again. You will be paid well for them."
             solution_accept_key="{=taom_lotr_issue_horses_soln}I will bring you horses."
             task_key="{=taom_lotr_issue_horses_task}Deliver horses ({COUNT})" />

  <!-- Wave 1 — DeliverPersonnel (hand over N bandit prisoners; no item/troop source needed). -->

  <LotrIssue id="lotr_gang_needs_recruits"
             template="DeliverPersonnel" giver_occupation="GangLeader" frequency="VeryCommon" cultures=""
             count="5" count_per_difficulty="10"
             reward_gold_base="0" reward_gold_per_difficulty="1600" reward_renown="0" relation_min="-10"
             title_key="{=taom_lotr_issue_recruits_title}Fresh Blood for the Gang"
             description_key="{=taom_lotr_issue_recruits_desc}A gang leader wants captured outlaws pressed into his service."
             brief_key="{=taom_lotr_issue_recruits_brief}A gang is only as strong as its numbers, and mine are thin. Bring me captives with no love for the law and I'll make use of them."
             accept_key="{=taom_lotr_issue_recruits_accept}What kind of captives?"
             explanation_key="{=taom_lotr_issue_recruits_expl}Bring me {COUNT} bandit captives and I'll see them put to work. There's coin in it for you, no questions asked."
             solution_accept_key="{=taom_lotr_issue_recruits_soln}I'll bring you captives."
             task_key="{=taom_lotr_issue_recruits_task}Deliver bandit captives ({COUNT})" />

  <LotrIssue id="lotr_landlord_needs_laborers"
             template="DeliverPersonnel" giver_occupation="RuralNotable" frequency="Common" cultures=""
             count="6" count_per_difficulty="12"
             reward_gold_base="0" reward_gold_per_difficulty="1500" reward_renown="0" relation_min="-10"
             title_key="{=taom_lotr_issue_laborers_title}Hands for the Mines"
             description_key="{=taom_lotr_issue_laborers_desc}A landowner needs strong backs for hard labor and will take captured bandits."
             brief_key="{=taom_lotr_issue_laborers_brief}The diggings need workers and free men won't go down there. Bring me bandits in chains and they'll earn their keep with a pick."
             accept_key="{=taom_lotr_issue_laborers_accept}You want prisoners?"
             explanation_key="{=taom_lotr_issue_laborers_expl}Bring me {COUNT} bandit captives for the labor gangs and I'll pay you for the trouble of taking them."
             solution_accept_key="{=taom_lotr_issue_laborers_soln}I'll bring you captives."
             task_key="{=taom_lotr_issue_laborers_task}Deliver bandit captives ({COUNT})" />

  <!-- Wave 2 — Combat (event-driven count; variant=DefeatRaids counts won battles, CaptureLords counts at-war lord captures). -->

  <LotrIssue id="lotr_nearby_bandit_base" template="Combat" variant="DefeatRaids"
             giver_occupation="Headman" frequency="VeryCommon" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1800" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_banditbase_title}A Lair Near {ISSUE_SETTLEMENT}"
             description_key="{=taom_lotr_issue_banditbase_desc}A nest of raiders preys on the roads near {ISSUE_SETTLEMENT}."
             brief_key="{=taom_lotr_issue_banditbase_brief}There is a foul nest of them in the hills, traveller. They take our goods and our people, and the watch is too few to root them out."
             accept_key="{=taom_lotr_issue_banditbase_accept}Where is this lair?"
             explanation_key="{=taom_lotr_issue_banditbase_expl}Break the raiders that haunt our roads and we will reward you. The land will breathe easier for it."
             solution_accept_key="{=taom_lotr_issue_banditbase_soln}I will scour them out." />

  <LotrIssue id="lotr_merchant_outlaws" template="Combat" variant="DefeatRaids"
             giver_occupation="Merchant" frequency="VeryCommon" cultures="" count="3" count_per_difficulty="2"
             reward_gold_base="0" reward_gold_per_difficulty="2000" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_outlaws_title}Outlaws on the Trade Roads"
             description_key="{=taom_lotr_issue_outlaws_desc}Raiding bands are strangling a merchant's trade and must be broken."
             brief_key="{=taom_lotr_issue_outlaws_brief}My caravans cannot pass for the raiders on the roads. Break enough of their bands and trade may flow again."
             accept_key="{=taom_lotr_issue_outlaws_accept}How many bands?"
             explanation_key="{=taom_lotr_issue_outlaws_expl}Defeat the bands plaguing the roads -- a few good victories will scatter the rest -- and I will pay you handsomely."
             solution_accept_key="{=taom_lotr_issue_outlaws_soln}I will clear the roads." />

  <LotrIssue id="lotr_merchant_poachers" template="Combat" variant="DefeatRaids"
             giver_occupation="Merchant" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1600" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_poachers_title}A Gang of Poachers"
             description_key="{=taom_lotr_issue_poachers_desc}A large band of poachers is ruining a merchant's hunting trade."
             brief_key="{=taom_lotr_issue_poachers_brief}Poachers have grown bold and many -- they strip the woods bare and drive off honest hunters. Someone must put them down."
             accept_key="{=taom_lotr_issue_poachers_accept}Where do they range?"
             explanation_key="{=taom_lotr_issue_poachers_expl}Defeat the poacher band and the woods will be ours again. There is coin in it for you."
             solution_accept_key="{=taom_lotr_issue_poachers_soln}I will deal with them." />

  <LotrIssue id="lotr_captured_gang" template="Combat" variant="DefeatRaids"
             giver_occupation="GangLeader" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1500" reward_renown="0" relation_min="-10"
             title_key="{=taom_lotr_issue_captured_title}My Men in Chains"
             description_key="{=taom_lotr_issue_captured_desc}A gang leader's people were taken by bounty hunters and he wants them freed."
             brief_key="{=taom_lotr_issue_captured_brief}Bounty hunters took some of my lads and mean to sell them. Break the curs holding them and there's coin in it -- quiet coin."
             accept_key="{=taom_lotr_issue_captured_accept}Who holds them?"
             explanation_key="{=taom_lotr_issue_captured_expl}Find the band that took my men and break it. Do that and you'll be paid, no questions asked."
             solution_accept_key="{=taom_lotr_issue_captured_soln}I'll free your men." />

  <LotrIssue id="lotr_extortion_deserters" template="Combat" variant="DefeatRaids"
             giver_occupation="Headman" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1500" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_deserters_title}Deserters Prey on {ISSUE_SETTLEMENT}"
             description_key="{=taom_lotr_issue_deserters_desc}A band of deserters extorts {ISSUE_SETTLEMENT} and has killed those who refused."
             brief_key="{=taom_lotr_issue_deserters_brief}Broken men with swords come for our grain and our coin, and they have spilled blood. We cannot fight them alone."
             accept_key="{=taom_lotr_issue_deserters_accept}I will help. Where are they?"
             explanation_key="{=taom_lotr_issue_deserters_expl}Lie in wait and break the deserters when they come. Rid us of them and you will have our thanks and our coin."
             solution_accept_key="{=taom_lotr_issue_deserters_soln}I will end their raiding." />

  <LotrIssue id="lotr_landlord_commons" template="Combat" variant="DefeatRaids"
             giver_occupation="RuralNotable" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1500" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_commons_title}Trouble at the Commons"
             description_key="{=taom_lotr_issue_commons_desc}Armed rivals are seizing a village's grazing commons by force."
             brief_key="{=taom_lotr_issue_commons_brief}Hard men have taken our grazing land and drive off any who come near. Our herds starve while they hold it."
             accept_key="{=taom_lotr_issue_commons_accept}Who are they?"
             explanation_key="{=taom_lotr_issue_commons_expl}Drive the rivals off our commons by force and the pasture is ours again. We will pay what we can."
             solution_accept_key="{=taom_lotr_issue_commons_soln}I will drive them off." />

  <LotrIssue id="lotr_rival_gang" template="Combat" variant="DefeatRaids"
             giver_occupation="GangLeader" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="1700" reward_renown="0" relation_min="-10"
             title_key="{=taom_lotr_issue_rivalgang_title}A Rival Moves In"
             description_key="{=taom_lotr_issue_rivalgang_desc}A rival gang is muscling in on a crime boss's turf and must be put down."
             brief_key="{=taom_lotr_issue_rivalgang_brief}Another outfit thinks it can take what's mine. Break them in the alleys and the streets are quiet again -- and you're paid."
             accept_key="{=taom_lotr_issue_rivalgang_accept}Where do they gather?"
             explanation_key="{=taom_lotr_issue_rivalgang_expl}Defeat the rival gang and make an example of them. Do it and there's good coin waiting."
             solution_accept_key="{=taom_lotr_issue_rivalgang_soln}Consider it done." />

  <LotrIssue id="lotr_spy_party" template="Combat" variant="DefeatRaids"
             giver_occupation="Lord" frequency="Rare" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2200" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_spy_title}An Enemy Spy"
             description_key="{=taom_lotr_issue_spy_desc}A lord suspects an enemy agent moves against him and wants the spy's party hunted down."
             brief_key="{=taom_lotr_issue_spy_brief}There is a viper in the grass -- an agent of my enemies, moving with a band of cutthroats. Find them and end them before they strike."
             accept_key="{=taom_lotr_issue_spy_accept}How will I know them?"
             explanation_key="{=taom_lotr_issue_spy_expl}Hunt down the spy's party and destroy it. Rid me of this threat and you will be well rewarded."
             solution_accept_key="{=taom_lotr_issue_spy_soln}I will hunt them down." />

  <LotrIssue id="lotr_lesser_noble_revolt" template="Combat" variant="DefeatRaids"
             giver_occupation="Lord" frequency="Rare" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2400" reward_renown="3" relation_min="-10"
             title_key="{=taom_lotr_issue_revolt_title}A Renegade Noble"
             description_key="{=taom_lotr_issue_revolt_desc}A lesser noble has raised the peasants in revolt and must be put down."
             brief_key="{=taom_lotr_issue_revolt_brief}A petty lordling stirs the peasants against their betters and gathers armed men. Break his strength before the revolt spreads."
             accept_key="{=taom_lotr_issue_revolt_accept}Where does he muster?"
             explanation_key="{=taom_lotr_issue_revolt_expl}Defeat the renegade and his rabble and order will be restored. You will be rewarded for your service."
             solution_accept_key="{=taom_lotr_issue_revolt_soln}I will put down this revolt." />

  <LotrIssue id="lotr_company_trouble" template="Combat" variant="DefeatRaids"
             giver_occupation="Lord" frequency="Rare" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2200" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_company_title}A Company of Trouble"
             description_key="{=taom_lotr_issue_company_desc}A band of sellswords hired to harass a lord must be driven off."
             brief_key="{=taom_lotr_issue_company_brief}My enemies have set a company of sellswords on my lands. They burn and pillage under another's coin. Break them for me."
             accept_key="{=taom_lotr_issue_company_accept}Where do they raid?"
             explanation_key="{=taom_lotr_issue_company_expl}Defeat the mercenary company and my lands will know peace again. I will pay you well."
             solution_accept_key="{=taom_lotr_issue_company_soln}I will break them." />

  <LotrIssue id="lotr_prodigal_son" template="Combat" variant="DefeatRaids"
             giver_occupation="Lord" frequency="Rare" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2000" reward_renown="1" relation_min="-10"
             title_key="{=taom_lotr_issue_prodigal_title}A Kinsman in Debt"
             description_key="{=taom_lotr_issue_prodigal_desc}A lord's young kinsman has fallen in with a gang that holds him for his debts."
             brief_key="{=taom_lotr_issue_prodigal_brief}A foolish young cousin of mine fell among thieves and now they hold him against his debts. Break the gang and free him."
             accept_key="{=taom_lotr_issue_prodigal_accept}Where is he held?"
             explanation_key="{=taom_lotr_issue_prodigal_expl}Defeat the gang that holds my kinsman and bring him out. I will pay his debt many times over for your trouble."
             solution_accept_key="{=taom_lotr_issue_prodigal_soln}I will free your kinsman." />

  <LotrIssue id="lotr_rival_captured" template="Combat" variant="CaptureLords"
             giver_occupation="Lord" frequency="Rare" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2600" reward_renown="3" relation_min="-10"
             title_key="{=taom_lotr_issue_rival_title}A Hated Rival"
             description_key="{=taom_lotr_issue_rival_desc}A lord wants a rival lord taken alive in battle."
             brief_key="{=taom_lotr_issue_rival_brief}There is a lord I would see humbled -- taken alive and brought low. Capture an enemy lord in battle and I will count it a great service."
             accept_key="{=taom_lotr_issue_rival_accept}You want him alive?"
             explanation_key="{=taom_lotr_issue_rival_expl}Take an enemy lord prisoner in battle and the deed is done. You will be richly rewarded."
             solution_accept_key="{=taom_lotr_issue_rival_soln}I will bring you a captive lord." />

  <!-- Wave 3 — Escort/ambush cluster (reframed onto Combat/DeliverGoods; a bespoke escort-a-moving-party
       mechanic is deferred to a future iteration, per the lotr-issues.md simplification clause). -->

  <LotrIssue id="lotr_caravan_ambush" template="Combat" variant="DefeatRaids"
             giver_occupation="Merchant" frequency="Common" cultures="" count="1" count_per_difficulty="1"
             reward_gold_base="0" reward_gold_per_difficulty="2000" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_caravanambush_title}An Ambush on the Road"
             description_key="{=taom_lotr_issue_caravanambush_desc}Raiders have set an ambush for a merchant's caravan and must be broken first."
             brief_key="{=taom_lotr_issue_caravanambush_brief}I have word that brigands lie in wait for my caravan on the road ahead. Spring their trap and break them before they bleed me dry."
             accept_key="{=taom_lotr_issue_caravanambush_accept}Where do they lie in wait?"
             explanation_key="{=taom_lotr_issue_caravanambush_expl}Find the raiders waiting in ambush and destroy them. Clear the road and my goods -- and your reward -- will get through."
             solution_accept_key="{=taom_lotr_issue_caravanambush_soln}I will break their ambush." />

  <LotrIssue id="lotr_escort_caravan" template="Combat" variant="DefeatRaids"
             giver_occupation="Merchant" frequency="Common" cultures="" count="2" count_per_difficulty="2"
             reward_gold_base="0" reward_gold_per_difficulty="2200" reward_renown="2" relation_min="-10"
             title_key="{=taom_lotr_issue_escortcaravan_title}Clear the Trade Road"
             description_key="{=taom_lotr_issue_escortcaravan_desc}A merchant's caravan cannot pass until the raiding bands on the road are broken."

 succeeded in 538ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\LotrIssues\LotrIssueConfigProviderTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\LotrIssues\LotrIssueServiceTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\LotrIssues\LotrIssueSuppressionTests.cs


 succeeded in 545ms:
754:    <!-- XSLT to remove vanilla faction strings and replace with LOTR-themed -->
756:      <XmlName id="GameText" path="module_strings"/>
764:      <XmlName id="GameText" path="action_strings"/>
771:      <XmlName id="GameText" path="comment_strings"/>
779:      <XmlName id="GameText" path="taom_module_strings"/>
786:      <XmlName id="GameText" path="taom_wanderer_strings"/>
795:      <XmlName id="GameText" path="named_companions/named_companion_strings"/>
802:      <XmlName id="GameText" path="taom_cc_strings"/>
811:      <XmlName id="GameText" path="taom_career_strings"/>
820:      <XmlName id="GameText" path="taom_messenger_strings"/>
828:    <!-- LOTR custom-issue player-facing text (LotrIssues feature) -->
830:      <XmlName id="GameText" path="taom_lotr_issue_strings"/>
838:      <XmlName id="GameText" path="taom_xslt_strings"/>

codex
The XML output is too large to inspect manually in one pass, so I’m going to parse it with PowerShell/XML and report objective checks: counts by template/variant, invalid enum strings, duplicate ids, reward shapes, unresolved text keys, and item ids.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\LotrIssueService.cs -TotalCount 360" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path TAOM.Tests\\Features\\LotrIssues\\LotrIssueConfigProviderTests.cs -TotalCount 360" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs -TotalCount 420" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path TAOM.Tests\\Features\\LotrIssues\\LotrIssueServiceTests.cs -TotalCount 360" in C:\Users\mikew\source\repos\TAOM
 succeeded in 496ms:
using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Pure LOTR-issue decision logic. Loads definitions once from the config provider, then answers
/// eligibility / scaling / completion / reward queries with no TaleWorlds dependencies. Singleton.
/// </summary>
public class LotrIssueService : ILotrIssueService
{
    private readonly ILotrIssueConfigProvider _configProvider;
    private readonly IModLogger _logger;

    private List<LotrIssueDefinition> _all;
    private Dictionary<string, LotrIssueDefinition> _byId;

    public LotrIssueService(ILotrIssueConfigProvider configProvider, IModLogger logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public IReadOnlyList<LotrIssueDefinition> GetEligibleIssues(ILotrIssueGiverAdapter giver)
    {
        var result = new List<LotrIssueDefinition>();
        if (giver == null || !giver.IsValid || giver.Occupation == null) return result;

        EnsureLoaded();
        foreach (var def in _all)
        {
            if (def.GiverOccupation != giver.Occupation.Value) continue;
            if (!def.AppliesToCulture(giver.CultureStringId)) continue;
            if (giver.RelationWithPlayer < def.RelationMin) continue;
            result.Add(def);
        }
        return result;
    }

    public LotrIssueDefinition GetIssueById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public int ComputeTargetCount(LotrIssueDefinition def, float difficulty)
    {
        if (def == null) return 0;
        return def.Count + (int)Math.Round(def.CountPerDifficulty * Clamp01(difficulty));
    }

    public int ComputeRewardGold(LotrIssueDefinition def, float difficulty)
    {
        if (def == null) return 0;
        return def.RewardGoldBase + (int)Math.Round(def.RewardGoldPerDifficulty * Clamp01(difficulty));
    }

    public bool IsObjectiveSatisfied(int progress, int target) => target > 0 && progress >= target;

    public void ApplyRewards(LotrIssueDefinition def, float difficulty, ILotrIssueRewardAdapter hero)
    {
        if (def == null) return;
        if (hero == null || !hero.IsValid)
        {
            _logger.LogWarning($"LotrIssues '{def.Id}': cannot apply rewards — hero adapter invalid");
            return;
        }

        var gold = ComputeRewardGold(def, difficulty);
        if (gold > 0) hero.AddGold(gold);
        if (def.RewardRenown > 0) hero.AddRenown(def.RewardRenown);
        if (!string.IsNullOrEmpty(def.RewardItem)) hero.AddItemToInventory(def.RewardItem, 1);
        _logger.LogInfo($"LotrIssues '{def.Id}': applied reward (gold {gold}, renown {def.RewardRenown}, item '{def.RewardItem}')");
    }

    private void EnsureLoaded()
    {
        if (_all != null) return;
        _all = new List<LotrIssueDefinition>(_configProvider.LoadIssues());
        _byId = new Dictionary<string, LotrIssueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in _all)
        {
            if (_byId.ContainsKey(def.Id))
            {
                _logger.LogWarning($"LotrIssues: duplicate issue id '{def.Id}' from provider — keeping first");
                continue;
            }
            _byId[def.Id] = def;
        }
    }

    // Non-finite difficulty (NaN/±Inf) collapses to 0 before the clamp (FiniteFloat rule).
    private static float Clamp01(float v)
    {
        if (!FiniteFloatValidator.IsFinite(v)) return 0f;
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}

 succeeded in 500ms:
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrIssueConfigProviderTests
{
    private LotrIssueConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new LotrIssueConfigProvider(Substitute.For<IPathService>(), Substitute.For<IModLogger>());
    }

    private List<LotrIssueDefinition> Parse(string xml) => _sut.ParseIssues(XDocument.Parse(xml));

    // One valid issue with every field populated; per-rule tests override one attribute.
    private static string Doc(string attrs) => $@"
<LotrIssues>
  <LotrIssue id='lotr_grain' template='DeliverGoods' giver_occupation='Headman' frequency='Common'
             cultures='gondor,vlandia' count='12' count_per_difficulty='180' item_source='item:grain'
             troop_source='' reward_gold_base='0' reward_gold_per_difficulty='800' reward_renown='1'
             reward_item='' relation_min='-10'
             title_key='{{=k}}t' description_key='{{=k}}d' brief_key='{{=k}}b' accept_key='{{=k}}a'
             explanation_key='{{=k}}e' solution_accept_key='{{=k}}s' task_key='{{=k}}task' {attrs} />
</LotrIssues>";

    [TestMethod]
    public void ParseIssues_ValidIssue_ParsesAllFields()
    {
        var list = Parse(Doc(""));
        Assert.AreEqual(1, list.Count);
        var d = list[0];
        Assert.AreEqual("lotr_grain", d.Id);
        Assert.AreEqual(LotrIssueTemplate.DeliverGoods, d.Template);
        Assert.AreEqual(IssueGiverOccupation.Headman, d.GiverOccupation);
        Assert.AreEqual(IssueFrequencyTier.Common, d.Frequency);
        CollectionAssert.AreEqual(new[] { "gondor", "vlandia" }, new List<string>(d.Cultures));
        Assert.AreEqual(12, d.Count);
        Assert.AreEqual(180f, d.CountPerDifficulty);
        Assert.AreEqual("item:grain", d.ItemSource);
        Assert.AreEqual(800f, d.RewardGoldPerDifficulty);
        Assert.AreEqual(1, d.RewardRenown);
        Assert.AreEqual(-10, d.RelationMin);
        Assert.AreEqual("{=k}t", d.Text.TitleKey);
    }

    [TestMethod]
    public void ParseIssues_MissingId_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue template='DeliverGoods' giver_occupation='Headman' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_DuplicateId_SecondSkipped()
    {
        var xml = @"<LotrIssues>
          <LotrIssue id='dup' template='DeliverGoods' giver_occupation='Headman' count='5' item_source='item:grain' title_key='t' description_key='d' />
          <LotrIssue id='dup' template='ClearHideout' giver_occupation='Lord' count='3' title_key='t2' description_key='d2' />
        </LotrIssues>";
        var list = Parse(xml);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(LotrIssueTemplate.DeliverGoods, list[0].Template);
    }

    [TestMethod]
    public void ParseIssues_UnknownTemplate_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='Nope' giver_occupation='Headman' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_UnknownGiverOccupation_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Wizard' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_UnknownFrequency_DefaultsToCommon()
    {
        var list = Parse(Doc("frequency='Sometimes'").Replace("frequency='Common'", ""));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(IssueFrequencyTier.Common, list[0].Frequency);
    }

    [TestMethod]
    public void ParseIssues_CountZero_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='0' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_MissingTitle_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='5' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_MissingDescription_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='5' title_key='t' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_NegativeRewardGold_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("reward_gold_base='0'", "reward_gold_base='-50'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0, list[0].RewardGoldBase);
    }

    [TestMethod]
    public void ParseIssues_NaNCountPerDifficulty_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("count_per_difficulty='180'", "count_per_difficulty='NaN'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0f, list[0].CountPerDifficulty);
    }

    [TestMethod]
    public void ParseIssues_InfiniteRewardGoldPerDifficulty_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("reward_gold_per_difficulty='800'", "reward_gold_per_difficulty='Infinity'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0f, list[0].RewardGoldPerDifficulty);
    }

    [TestMethod]
    public void ParseIssues_RelationMinOutOfRange_CoercedToDefault()
    {
        var list = Parse(Doc("").Replace("relation_min='-10'", "relation_min='-500'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(-10, list[0].RelationMin);
    }

    [TestMethod]
    public void ParseIssues_DeliverGoodsBareItemSource_Skipped()
    {
        // A non-prefixed source ('grain') can't be resolved by DeliverGoods (Wave 0 = item: only) → skipped.
        var list = Parse(Doc("").Replace("item_source='item:grain'", "item_source='grain'"));
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void ParseIssues_DeliverGoodsCategorySource_Skipped()
    {
        // category: sourcing is not implemented until a later wave → DeliverGoods issue skipped at load.
        var list = Parse(Doc("").Replace("item_source='item:grain'", "item_source='category:Grain'"));
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void ParseIssues_InvalidTroopSource_Cleared()
    {
        var list = Parse(Doc("").Replace("troop_source=''", "troop_source='legendary'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("", list[0].TroopSource);
    }

    [TestMethod]
    public void ParseIssues_ValidTroopSource_Kept()
    {
        var list = Parse(Doc("").Replace("troop_source=''", "troop_source='Mount'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("mount", list[0].TroopSource);
    }

    [TestMethod]
    public void ParseIssues_EmptyCultures_AppliesToAll()
    {
        var list = Parse(Doc("").Replace("cultures='gondor,vlandia'", "cultures=''"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0, list[0].Cultures.Count);
        Assert.IsTrue(list[0].AppliesToCulture("anything"));
    }

    [TestMethod]
    public void ParseIssues_CultureFilter_OnlyMatchesListed()
    {
        var d = Parse(Doc(""))[0];
        Assert.IsTrue(d.AppliesToCulture("gondor"));
        Assert.IsTrue(d.AppliesToCulture("VLANDIA")); // case-insensitive
        Assert.IsFalse(d.AppliesToCulture("mordor"));
    }

    [TestMethod]
    public void ParseIssues_NullRoot_ReturnsEmpty()
        => Assert.AreEqual(0, _sut.ParseIssues(new XDocument()).Count);

    // End-to-end smoke: drive the ACTUAL shipped config through the provider's parse + validation, so a
    // malformed/invalid issue (bad item-source scheme, missing required key, wrong template) is caught
    // at test time rather than silently dropped in-game. Bump the expected count as waves add configs.
    [TestMethod]
    public void ShippedConfig_AllIssuesParse_DeliverGoodsItemSourced()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml");
        Assert.IsTrue(File.Exists(path), $"shipped config not found at {path}");

        var list = _sut.ParseIssues(XDocument.Load(path));
        Assert.AreEqual(43, list.Count, "every shipped issue must pass validation (none silently dropped)");
        foreach (var d in list)
        {
            Assert.IsTrue(
                d.Template == LotrIssueTemplate.DeliverGoods
                || d.Template == LotrIssueTemplate.DeliverPersonnel
                || d.Template == LotrIssueTemplate.Combat,
                $"{d.Id}: only DeliverGoods/DeliverPersonnel/Combat are implemented so far");
            if (d.Template == LotrIssueTemplate.DeliverGoods)
                Assert.IsTrue(d.ItemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase), $"{d.Id}: DeliverGoods must be item:-sourced");
            Assert.IsFalse(string.IsNullOrEmpty(d.Text.TitleKey), $"{d.Id}: missing title key");
            Assert.IsFalse(string.IsNullOrEmpty(d.Text.DescriptionKey), $"{d.Id}: missing description key");
            Assert.IsTrue(d.Count > 0, $"{d.Id}: count must be > 0");
        }
    }
}

 succeeded in 499ms:
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrIssueServiceTests
{
    private ILotrIssueConfigProvider _config;
    private LotrIssueService _sut;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ILotrIssueConfigProvider>();
        _config.LoadIssues().Returns(new List<LotrIssueDefinition>());
        _sut = new LotrIssueService(_config, Substitute.For<IModLogger>());
    }

    private static LotrIssueDefinition Def(
        string id = "i1",
        IssueGiverOccupation giver = IssueGiverOccupation.Headman,
        IReadOnlyList<string> cultures = null,
        int count = 10,
        float countPerDiff = 0f,
        int rewardGoldBase = 0,
        float rewardGoldPerDiff = 0f,
        int rewardRenown = 0,
        string rewardItem = "",
        int relationMin = -10)
        => new LotrIssueDefinition(id, LotrIssueTemplate.DeliverGoods, giver, IssueFrequencyTier.Common,
            cultures, count, countPerDiff, "", "", rewardGoldBase, rewardGoldPerDiff, rewardRenown,
            rewardItem, "", relationMin, new LotrIssueText("t", "d", "", "", "", "", "", "", ""));

    private static ILotrIssueGiverAdapter Giver(
        IssueGiverOccupation? occ = IssueGiverOccupation.Headman, string culture = "gondor",
        int relation = 0, bool valid = true)
    {
        var g = Substitute.For<ILotrIssueGiverAdapter>();
        g.IsValid.Returns(valid);
        g.Occupation.Returns(occ);
        g.CultureStringId.Returns(culture);
        g.RelationWithPlayer.Returns(relation);
        return g;
    }

    private void Load(params LotrIssueDefinition[] defs)
        => _config.LoadIssues().Returns(new List<LotrIssueDefinition>(defs));

    // ── GetEligibleIssues ────────────────────────────────────────────────────

    [TestMethod]
    public void GetEligibleIssues_MatchingOccupationCultureRelation_Included()
    {
        Load(Def(giver: IssueGiverOccupation.Headman, cultures: new[] { "gondor" }, relationMin: 0));
        var result = _sut.GetEligibleIssues(Giver(IssueGiverOccupation.Headman, "gondor", relation: 5));
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetEligibleIssues_WrongOccupation_Excluded()
    {
        Load(Def(giver: IssueGiverOccupation.Headman));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(IssueGiverOccupation.Lord)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_WrongCulture_Excluded()
    {
        Load(Def(cultures: new[] { "gondor" }));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(culture: "mordor")).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_EmptyCultureList_MatchesAnyCulture()
    {
        Load(Def(cultures: null));
        Assert.AreEqual(1, _sut.GetEligibleIssues(Giver(culture: "mordor")).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_RelationBelowMin_Excluded()
    {
        Load(Def(relationMin: 20));
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(relation: 5)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_NullGiver_ReturnsEmpty()
        => Assert.AreEqual(0, _sut.GetEligibleIssues(null).Count);

    [TestMethod]
    public void GetEligibleIssues_InvalidGiver_ReturnsEmpty()
    {
        Load(Def());
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(valid: false)).Count);
    }

    [TestMethod]
    public void GetEligibleIssues_NullOccupation_ReturnsEmpty()
    {
        Load(Def());
        Assert.AreEqual(0, _sut.GetEligibleIssues(Giver(occ: null)).Count);
    }

    // ── GetIssueById ──────────────────────────────────────────────────────────

    [TestMethod]
    public void GetIssueById_Existing_ReturnsDef()
    {
        Load(Def(id: "grain"));
        Assert.AreEqual("grain", _sut.GetIssueById("grain").Id);
    }

    [TestMethod]
    public void GetIssueById_Unknown_ReturnsNull()
    {
        Load(Def(id: "grain"));
        Assert.IsNull(_sut.GetIssueById("nope"));
    }

    [TestMethod]
    public void GetIssueById_Null_ReturnsNull()
        => Assert.IsNull(_sut.GetIssueById(null));

    // ── ComputeTargetCount / ComputeRewardGold ───────────────────────────────

    [TestMethod]
    public void ComputeTargetCount_ScalesWithDifficulty()
        => Assert.AreEqual(10 + 90, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), 0.5f));

    [TestMethod]
    public void ComputeTargetCount_DifficultyAboveOne_ClampedToOne()
        => Assert.AreEqual(10 + 180, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), 5f));

    [TestMethod]
    public void ComputeTargetCount_NaNDifficulty_BaseOnly()
        => Assert.AreEqual(10, _sut.ComputeTargetCount(Def(count: 10, countPerDiff: 180f), float.NaN));

    [TestMethod]
    public void ComputeRewardGold_ScalesWithDifficulty()
        => Assert.AreEqual(100 + 400, _sut.ComputeRewardGold(Def(rewardGoldBase: 100, rewardGoldPerDiff: 800f), 0.5f));

    // ── IsObjectiveSatisfied ──────────────────────────────────────────────────

    [TestMethod]
    public void IsObjectiveSatisfied_ProgressMeetsTarget_True()
        => Assert.IsTrue(_sut.IsObjectiveSatisfied(10, 10));

    [TestMethod]
    public void IsObjectiveSatisfied_ProgressBelowTarget_False()
        => Assert.IsFalse(_sut.IsObjectiveSatisfied(9, 10));

    [TestMethod]
    public void IsObjectiveSatisfied_TargetZero_False()
        => Assert.IsFalse(_sut.IsObjectiveSatisfied(5, 0));

    // ── ApplyRewards ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyRewards_ValidHero_GrantsGoldRenownItem()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(true);
        _sut.ApplyRewards(Def(rewardGoldBase: 200, rewardRenown: 3, rewardItem: "sword"), 0f, hero);
        hero.Received(1).AddGold(200);
        hero.Received(1).AddRenown(3);
        hero.Received(1).AddItemToInventory("sword", 1);
    }

    [TestMethod]
    public void ApplyRewards_InvalidHero_NoOps()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(false);
        _sut.ApplyRewards(Def(rewardGoldBase: 200, rewardRenown: 3), 0f, hero);
        hero.DidNotReceive().AddGold(Arg.Any<int>());
        hero.DidNotReceive().AddRenown(Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyRewards_ZeroGold_DoesNotCallAddGold()
    {
        var hero = Substitute.For<ILotrIssueRewardAdapter>();
        hero.IsValid.Returns(true);
        _sut.ApplyRewards(Def(rewardGoldBase: 0), 0f, hero);
        hero.DidNotReceive().AddGold(Arg.Any<int>());
    }
}

 succeeded in 515ms:
using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues.Templates;

/// <summary>
/// T1 "deliver goods" template: the player accumulates N of a config-sourced item and turns it in to
/// the issue giver. A generic, config-driven re-implementation of the vanilla delivery archetype
/// (e.g. HeadmanNeedsGrain), driven by a <see cref="LotrIssueDefinition"/> carried in by the behavior's
/// OnSelected closure. Entry-point layer (ADR-002): engine plumbing lives here; the pure decisions
/// (count/reward scaling, eligibility, reward grant) delegate to <see cref="ILotrIssueService"/>.
/// </summary>
public class DeliverGoodsLotrIssue : IssueBase
{
    [SaveableField(1)] private string _defId;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private IModLogger _logger;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();
    private IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();

    public DeliverGoodsLotrIssue(Hero issueOwner, LotrIssueDefinition def)
        : base(issueOwner, CampaignTime.DaysFromNow(30f))
    {
        _def = def;
        _defId = def?.Id;
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    private int NeededCount
    {
        get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
    }

    private string DeliverItemId { get { EnsureDef(); return ResolveItemId(_def?.ItemSource); } }

    // Wave 0 supports "item:<id>"; "category:<id>" sourcing is added in Wave 1.
    internal static string ResolveItemId(string itemSource)
    {
        if (string.IsNullOrEmpty(itemSource)) return null;
        if (itemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase)) return itemSource.Substring(5);
        return null;
    }

    internal static ItemObject ResolveItem(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

    private TextObject Tx(string key, string fallback)
    {
        EnsureDef();
        var t = new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
        if (base.IssueSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
        t.SetTextVariable("COUNT", NeededCount);
        var item = ResolveItem(DeliverItemId);
        if (item != null) t.SetTextVariable("ITEM", item.Name);
        return t;
    }

    public override TextObject Title => Tx(_def?.Text.TitleKey, "{=taom_lotr_issue_fallback_title}A Request for Aid");
    public override TextObject Description => Tx(_def?.Text.DescriptionKey, "{=taom_lotr_issue_fallback_desc}A local needs goods delivered.");
    public override TextObject IssueBriefByIssueGiver => Tx(_def?.Text.BriefKey, "{=taom_lotr_issue_fallback_brief}We are in need, traveller.");
    public override TextObject IssueAcceptByPlayer => Tx(_def?.Text.AcceptKey, "{=taom_lotr_issue_fallback_accept}How can I help?");
    public override TextObject IssueQuestSolutionExplanationByIssueGiver => Tx(_def?.Text.ExplanationKey, "{=taom_lotr_issue_fallback_expl}Bring us {COUNT} {ITEM} and we will be saved.");
    public override TextObject IssueQuestSolutionAcceptByPlayer => Tx(_def?.Text.SolutionAcceptKey, "{=taom_lotr_issue_fallback_soln}I will bring what you need.");

    public override bool IsThereAlternativeSolution => false;
    public override bool IsThereLordSolution => false;

    protected override int RewardGold
    {
        get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
    }

    public override IssueFrequency GetFrequency()
    {
        EnsureDef();
        if (_def == null) return IssueFrequency.Common;
        switch (_def.Frequency)
        {
            case IssueFrequencyTier.VeryCommon: return IssueFrequency.VeryCommon;
            case IssueFrequencyTier.Rare: return IssueFrequency.Rare;
            default: return IssueFrequency.Common;
        }
    }

    // Keep the issue alive only while its sourced item actually resolves — an unresolvable item_source
    // would otherwise spawn a permanently uncompletable quest. A null here removes the offer cleanly.
    public override bool IssueStayAliveConditions() => ResolveItem(DeliverItemId) != null;

    protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
    {
        if (issueEffect == DefaultIssueEffects.SettlementProsperity) return -0.2f;
        if (issueEffect == DefaultIssueEffects.SettlementLoyalty) return -0.5f;
        return 0f;
    }

    protected override void OnGameLoad() => EnsureDef();

    protected override void HourlyTick() { }

    protected override void CompleteIssueWithTimedOutConsequences() { }

    protected override QuestBase GenerateIssueQuest(string questId)
    {
        EnsureDef();
        if (ResolveItem(DeliverItemId) == null)
            Logger?.LogWarning($"LotrIssues '{_defId}': item_source '{_def?.ItemSource}' did not resolve to an ItemObject — the quest would be uncompletable.");
        return new DeliverGoodsLotrIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(18f),
            _defId, base.IssueDifficultyMultiplier, NeededCount, RewardGold, DeliverItemId);
    }

    protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
    {
        skill = null;
        relationHero = null;
        requiredGold = 0;
        flag = PreconditionFlags.None;
        EnsureDef();
        if (issueGiver.GetRelationWithPlayer() < (_def?.RelationMin ?? -10))
        {
            flag |= PreconditionFlags.Relation;
            relationHero = issueGiver;
        }
        if (issueGiver.CurrentSettlement != null
            && FactionManager.IsAtWarAgainstFaction(issueGiver.CurrentSettlement.MapFaction, Hero.MainHero.MapFaction))
        {
            flag |= PreconditionFlags.AtWar;
        }
        return flag == PreconditionFlags.None;
    }
}

/// <summary>
/// The quest spawned when the player takes the <see cref="DeliverGoodsLotrIssue"/> quest path: track the
/// count of the sourced item on the main party, turn it in at the giver's settlement for the reward.
/// Mirrors the vanilla delivery-quest shape (AddDiscreteLog progress + inventory/settlement event tracking
/// + a turn-in dialog), generalized over the config def. Reward grant routes through the pure service.
/// </summary>
public class DeliverGoodsLotrIssueQuest : QuestBase
{
    [SaveableField(1)] private string _defId;
    [SaveableField(2)] private string _itemId;
    [SaveableField(3)] private int _neededCount;
    [SaveableField(4)] private int _rewardGold;
    [SaveableField(5)] private float _difficulty;
    [SaveableField(6)] private JournalLog _acceptedLog;
    [SaveableField(7)] private JournalLog _readyLog;

    private LotrIssueDefinition _def;
    private ILotrIssueService _service;
    private ItemObject _cachedItem;
    private bool _itemResolved;

    private ILotrIssueService Service => _service ??= IoC.Resolve<ILotrIssueService>();

    // Resolve the tracked item once. Without this, CountOnPlayer + the logs + Success would each call
    // MBObjectManager.GetObject on every inventory/settlement event + hourly tick. Not saveable — it
    // re-resolves from the saved _itemId after load.
    private ItemObject Item()
    {
        if (_itemResolved) return _cachedItem;
        _cachedItem = DeliverGoodsLotrIssue.ResolveItem(_itemId);
        _itemResolved = true;
        return _cachedItem;
    }

    public DeliverGoodsLotrIssueQuest(string questId, Hero giverHero, CampaignTime duration,
        string defId, float difficulty, int neededCount, int rewardGold, string itemId)
        : base(questId, giverHero, duration, rewardGold)
    {
        _defId = defId;
        _difficulty = difficulty;
        _neededCount = neededCount;
        _rewardGold = rewardGold;
        _itemId = itemId;
        SetDialogs();
        InitializeQuestOnCreation();
    }

    private void EnsureDef()
    {
        if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
    }

    public override TextObject Title
    {
        get
        {
            EnsureDef();
            var t = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TitleKey)
                ? "{=taom_lotr_issue_fallback_title}A Request for Aid" : _def.Text.TitleKey);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("ISSUE_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            t.SetTextVariable("COUNT", _neededCount);
            return t;
        }
    }

    public override bool IsRemainingTimeHidden => false;

    private TextObject AcceptedLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_common_accepted}Bring {COUNT} {ITEM} to {QUEST_SETTLEMENT}.");
            t.SetTextVariable("COUNT", _neededCount);
            var item = Item();
            if (item != null) t.SetTextVariable("ITEM", item.Name);
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    private TextObject ReadyLogText
    {
        get
        {
            var t = new TextObject("{=taom_lotr_issue_common_ready}You have enough to complete the task. Return to {QUEST_SETTLEMENT} to hand it over.");
            if (base.QuestGiver?.CurrentSettlement != null) t.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
            return t;
        }
    }

    protected override void InitializeQuestOnGameLoad()
    {
        EnsureDef();
        SetDialogs();
    }

    protected override void HourlyTick() { }

    protected override void RegisterEvents()
    {
        CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, OnPlayerInventoryExchange);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        // Food-class deliverables (grain, meat, …) leave the roster via consumption/sharing, not just
        // trade — mirror vanilla HeadmanNeedsGrain's refresh hooks so tracked progress never goes stale.
        CampaignEvents.OnPartyConsumedFoodEvent.AddNonSerializedListener(this, OnPartyConsumedFood);
        CampaignEvents.OnHeroSharedFoodWithAnotherHeroEvent.AddNonSerializedListener(this, OnHeroSharedFoodWithAnotherHero);
        CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
        CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
    }

    protected override void HourlyTickParty(MobileParty mobileParty)
    {
        if (mobileParty == MobileParty.MainParty) Refresh();
    }

    private int CountOnPlayer()
    {
        var item = Item();
        if (item == null) return 0;
        int n = PartyBase.MainParty.ItemRoster.GetItemNumber(item);
        return n <= _neededCount ? n : _neededCount;
    }

    private void Refresh()
    {
        if (_acceptedLog != null) _acceptedLog.UpdateCurrentProgress(CountOnPlayer());
        CheckReady();
    }

    private void CheckReady()
    {
        if (_readyLog == null && (_acceptedLog?.CurrentProgress ?? 0) >= _neededCount)
        {
            _readyLog = AddLog(ReadyLogText);
        }
        else if (_readyLog != null && (_acceptedLog?.CurrentProgress ?? 0) < _neededCount)
        {
            RemoveLog(_readyLog);
            _readyLog = null;
        }
    }

    private void OnPlayerInventoryExchange(List<(ItemRosterElement, int)> purchased, List<(ItemRosterElement, int)> sold, bool isTrading)
        => Refresh();

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (party == MobileParty.MainParty) Refresh();
    }

    private void OnPartyConsumedFood(MobileParty party)
    {
        if (party != null && party.IsMainParty) Refresh();
    }

    private void OnHeroSharedFoodWithAnotherHero(Hero supporter, Hero supported, float influence)
    {
        if (supporter == Hero.MainHero || supported == Hero.MainHero) Refresh();
    }

    private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
    {
        if (prisoner == Hero.MainHero) Refresh();
    }

    private void OnVillageBeingRaided(Village village)
    {
        if (base.QuestGiver?.CurrentSettlement?.Village == village)
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
    {
        if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
            && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
            CompleteQuestWithCancel(CancelLogText);
    }

    private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);
    }

    private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");

    protected override void OnTimedOut()
    {
        AddLog(new TextObject("{=taom_lotr_issue_common_timeout}You failed to deliver the goods in time."));
    }

    protected override void SetDialogs()
    {
        var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
        var npcHave = new TextObject("{=taom_lotr_issue_common_have_you}Have you brought what we need?");
        var npcThanks = new TextObject("{=taom_lotr_issue_common_thanks}Bless you, traveller. You have saved us.");
        var npcAwait = new TextObject("{=taom_lotr_issue_common_await}We await your return.");

        OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
            .NpcLine(npcAccept)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .Consequence(QuestAcceptedConsequences)
            .CloseDialog();

        DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")
            .NpcLine(npcHave)
            .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
            .BeginPlayerOptions()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_turnin}Yes. Here it is."))
            .ClickableCondition(TurnInClickableConditions)
            .NpcLine(npcThanks)
            .Consequence(() => Campaign.Current.ConversationManager.ConversationEndOneShot += Success)
            .CloseDialog()
            .PlayerOption(new TextObject("{=taom_lotr_issue_common_working}Not yet — I'm working on it."))
            .NpcLine(npcAwait)
            .CloseDialog()
            .EndPlayerOptions()
            .CloseDialog();
    }

    private bool TurnInClickableConditions(out TextObject explanation)
    {
        // Check live inventory, not the cached log progress — the player may have consumed/sold the
        // deliverable (grain is food) since the log last refreshed.
        if (CountOnPlayer() >= _neededCount)
        {
            explanation = null;
            return true;
        }
        explanation = new TextObject("{=taom_lotr_issue_common_not_enough}You don't have enough yet.");
        return false;
    }

    private void QuestAcceptedConsequences()
    {
        StartQuest();
        EnsureDef();
        var task = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
            ? "{=taom_lotr_issue_common_task}Collect Goods" : _def.Text.TaskKey);
        task.SetTextVariable("COUNT", _neededCount);
        var item = Item();
        if (item != null) task.SetTextVariable("ITEM", item.Name);
        _acceptedLog = AddDiscreteLog(AcceptedLogText, task, CountOnPlayer(), _neededCount);
    }

    private void Success()
    {
        EnsureDef();
        var item = Item();
        if (item != null && base.QuestGiver?.CurrentSettlement?.Party != null)
        {
            var element = new ItemRosterElement(item, _neededCount);
            GiveItemAction.ApplyForParties(PartyBase.MainParty, base.QuestGiver.CurrentSettlement.Party, in element);
        }
        if (_def != null)
            Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
        RelationshipChangeWithQuestGiver = 5;
        CompleteQuestWithSuccess();
    }
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\taom_career_strings.xml -Pattern '<string id=' | Select-Object -First 10 | ForEach-Object { \""'$($_.LineNumber):$($_.Line)" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Pattern '<string id=' | Measure-Object" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Pattern 'text=\"\\{=' | Measure-Object" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Filter '*strings*.xml' | Select-Object -First 12 FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 483ms:

[32;1mCount             : [0m308
[32;1mAverage           : [0m
[32;1mSum               : [0m
[32;1mMaximum           : [0m
[32;1mMinimum           : [0m
[32;1mStandardDeviation : [0m
[32;1mProperty          : [0m


 succeeded in 493ms:
5:	<string id="taom_career_ranger_of_ithilien" text="{=taom_career_ranger_of_ithilien}Ranger of Ithilien" />
6:	<string id="taom_career_ranger_of_ithilien_desc" text="{=taom_career_ranger_of_ithilien_desc}An elite scout who strikes from the shadows of Ithilien, ambushing enemies of Gondor with deadly precision." />
7:	<string id="taom_career_captain_of_osgiliath" text="{=taom_career_captain_of_osgiliath}Captain of Osgiliath" />
8:	<string id="taom_career_captain_of_osgiliath_desc" text="{=taom_career_captain_of_osgiliath_desc}A battle-hardened infantry officer who commands Gondor's frontline defense at the ruins of Osgiliath, forging order from chaos in brutal urban combat. The Captain holds the line where others would break, rallying soldiers through sheer force of will." />
9:	<string id="taom_career_knight_of_belfalas" text="{=taom_career_knight_of_belfalas}Knight of Belfalas" />
10:	<string id="taom_career_knight_of_belfalas_desc" text="{=taom_career_knight_of_belfalas_desc}An armored champion of the coastal fiefdom of Belfalas, whose thundering charge has broken countless enemy formations. The Knight of Belfalas combines Gondorian discipline with Dol Amroth's proud cavalry tradition, becoming an immovable wall of steel." />
11:	<string id="taom_career_black_uruk_captain" text="{=taom_career_black_uruk_captain}Black Uruk Captain" />
12:	<string id="taom_career_black_uruk_captain_desc" text="{=taom_career_black_uruk_captain_desc}An iron-muscled champion of Mordor who leads Black Uruk warbands into the breach, absorbing punishment that would fell lesser warriors and turning the tide through sheer brutish dominance." />
13:	<string id="taom_career_mulkerhili_cultist" text="{=taom_career_mulkerhili_cultist}Mulkerhílí Cultist" />
14:	<string id="taom_career_mulkerhili_cultist_desc" text="{=taom_career_mulkerhili_cultist_desc}A fanatical devotee of the One Ring's whispered promises who channels the terror of Sauron's voice to shatter enemy resolve, blending cursed archery with a maddening presence that erodes the will to fight." />

 succeeded in 456ms:

[32;1mCount             : [0m308
[32;1mAverage           : [0m
[32;1mSum               : [0m
[32;1mMaximum           : [0m
[32;1mMinimum           : [0m
[32;1mStandardDeviation : [0m
[32;1mProperty          : [0m


 succeeded in 468ms:

[32;1mFullName[0m
[32;1m--------[0m
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_messenger_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_wanderer_strings.xml
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_xslt_strings.xml


codex
TAOM’s other GameText files use the same `{=key}default` text attribute convention, so I’m not treating that as a localization-format issue. I’m moving to engine verification now: event delegate signatures, QuestBase completion, issue type throttling, and a vanilla comparable quest.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd -ErrorAction SilentlyContinue | Format-List *' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.CampaignSystem*.dll' | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*Quest*.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.Core*.dll' | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 478ms:

[32;1mName                                   [0m[32;1m FullName[0m
[32;1m----                                   [0m [32;1m--------[0m
TaleWorlds.Core.dll                     E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Clien…
TaleWorlds.Core.ViewModelCollection.dll E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Clien…


 succeeded in 524ms:

[32;1mHelpUri            : [0m
[32;1mFileVersionInfo    : [0mFile:             C:\Users\mikew\.dotnet\tools\ilspycmd.exe
                     InternalName:     ilspycmd.dll
                     OriginalFilename: ilspycmd.dll
                     FileVersion:      10.0.1.8346
                     FileDescription:  ilspycmd
                     Product:          ilspycmd
                     ProductVersion:   10.0.1.8346+aad16c66e96eb887eb05887d6b5a9e0522637906
                     Debug:            False
                     Patched:          False
                     PreRelease:       False
                     PrivateBuild:     False
                     SpecialBuild:     False
                     Language:         Language Neutral
                     
[32;1mPath               : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mExtension          : [0m.exe
[32;1mDefinition         : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mSource             : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mVersion            : [0m10.0.1.8346
[32;1mVisibility         : [0mPublic
[32;1mOutputType         : [0m{System.String}
[32;1mName               : [0milspycmd.exe
[32;1mCommandType        : [0mApplication
[32;1mModuleName         : [0m
[32;1mModule             : [0m
[32;1mRemotingCapability : [0mPowerShell
[32;1mParameters         : [0m
[32;1mParameterSets      : [0m


 succeeded in 516ms:

[32;1mName                                             [0m[32;1m FullName[0m
[32;1m----                                             [0m [32;1m--------[0m
TaleWorlds.CampaignSystem.dll                     E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Ship…
TaleWorlds.CampaignSystem.ViewModelCollection.dll E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Ship…


 succeeded in 522ms:

[32;1mFullName[0m
[32;1m--------[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\QuestHelper.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\QuestBase.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\QuestManager.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\QuestTaskBase.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\IQuestsStateHandler.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\QuestsState.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsWeaponsIss…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordNeedsGarrisonTroopsIs…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantNeedsHelpWithOutl…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\TheConquestOfSettlementIs…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues.IssueQuestTasks\ChangeCom…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues.IssueQuestTasks\DefeatPar…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues.IssueQuestTasks\RaidVilla…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues.IssueQuestTasks\TalkToNpc…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\IssueQuestLogEntry.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\IssueQuestStartLogEnt…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCol…


codex
`ilspycmd` is available and the installed DLL path is present. I’m using the decompiled tree only to locate relevant type names, then I’ll decompile those types from the installed assemblies for evidence.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'OnPlayerBattleEndEvent|WinningSide ==.*PlayerSide|PlayerSide.*WinningSide' -SimpleMatch:"'$false | Select-Object -First 80 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'TournamentFinished' -SimpleMatch | Select-Object -First 60 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'HeroPrisonerTaken' -SimpleMatch | Select-Object -First 80 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'AddPotentialIssueData|OverIssue|cooldown|Cooldown|IssueFrequency' -SimpleMatch:"'$false | Select-Object -First 120 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 4212ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:328:obj26.GetFieldDefinitionWithId(new MemberTypeId(3, 2)).InitializeForAutoGeneration(IssueManager.AutoGeneratedGetMemberValue_issuesCoolDownData);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1433:((TypeDefinition)definitionContext.TryGetTypeDefinition(new TypeSaveId(330268))).GetFieldDefinitionWithId(new MemberTypeId(2, 0)).InitializeForAutoGeneration(IssueCoolDownData.AutoGeneratedGetMemberValueExpireTime);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1435:CollectObjectsDelegate collectObjectsDelegate166 = HeroRelatedIssueCoolDownData.AutoGeneratedStaticCollectObjectsHeroRelatedIssueCoolDownData;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1437:obj173.GetFieldDefinitionWithId(new MemberTypeId(3, 1)).InitializeForAutoGeneration(HeroRelatedIssueCoolDownData.AutoGeneratedGetMemberValueRelatedHero);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:5265:IssueCoolDownData item = (IssueCoolDownData)list[i];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:5275:IssueCoolDownData item = (IssueCoolDownData)list[i];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:5285:IssueCoolDownData item = (IssueCoolDownData)list[i];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SaveableCampaignTypeDefiner.cs:224:AddClassDefinition(typeof(IssueCoolDownData), 268);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SaveableCampaignTypeDefiner.cs:225:AddClassDefinition(typeof(HeroRelatedIssueCoolDownData), 269);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SaveableCampaignTypeDefiner.cs:482:ConstructContainerDefinition(typeof(List<IssueCoolDownData>));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\SaveableCampaignTypeDefiner.cs:582:ConstructContainerDefinition(typeof(Dictionary<string, List<IssueCoolDownData>>));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:76:Debug.FailedAssert("Barter with the hero is on cooldown.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\BarterSystem\\BarterManager.cs", "StartBarterOffer", 83);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:79:ClearHeroCooldowns();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:189:HandleHeroCooldown(otherHero);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:249:private void HandleHeroCooldown(Hero hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:251:CampaignTime value = CampaignTime.Now + CampaignTime.Days(Campaign.Current.Models.BarterModel.BarterCooldownWithHeroInDays);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterManager.cs:262:private void ClearHeroCooldowns()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BanditSpawnCampaignBehavior.cs:21:private const float HideoutInfestCooldownAfterFightInDays = 1.5f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:14:private const int BannerLevel1CooldownDays = 4;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:16:private const int BannerLevel2CooldownDays = 8;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:18:private const int BannerLevel3CooldownDays = 12;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:199:private int GetCooldownDays(int bannerLevel)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\BannerCampaignBehavior.cs:214:CampaignTime value = CampaignTime.DaysFromNow(GetCooldownDays(bannerLevel));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionGrievanceBehavior.cs:134:private const int GrievanceCooldownForSameTypeInDays = 1;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:65:private CampaignTime _lastGlobalIncidentCooldown = CampaignTime.Zero;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:67:private Dictionary<Incident, CampaignTime> _incidentsOnCooldown = new Dictionary<Incident, CampaignTime>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:89:_incidentsOnCooldown.Add(incident, CampaignTime.Now);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:90:_lastGlobalIncidentCooldown = CampaignTime.Now + GetCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:111:_lastGlobalIncidentCooldown = CampaignTime.Now + GetCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:178:dataStore.SyncData("_incidentsOnCooldown", ref _incidentsOnCooldown);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:179:dataStore.SyncData("_lastGlobalIncidentCooldown", ref _lastGlobalIncidentCooldown);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:185:_lastGlobalIncidentCooldown = CampaignTime.Now + CampaignTime.Hours(1f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:186:_incidentsOnCooldown.Clear();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:204:if ((((trigger & IncidentTrigger.EnteringCastle) == 0 && (trigger & IncidentTrigger.EnteringTown) == 0 && (trigger & IncidentTrigger.EnteringVillage) == 0) || (MobileParty.MainParty.CurrentSettlement != null && !MobileParty.MainParty.CurrentSettlement.IsSettlementBusy(this))) && (((trigger & IncidentTrigger.LeavingTown) == 0 && (trigger & IncidentTrigger.LeavingVillage) == 0 && (trigger & IncidentTrigger.LeavingSettlement) == 0) || !MobileParty.MainParty.LastVisitedSettlement.IsSettlementBusy(this)) && !Hero.MainHero.IsPrisoner && !Campaign.Current.ConversationManager.IsConversationFlowActive && _lastGlobalIncidentCooldown.IsPast)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:206:CheckIncidentsOnCooldown();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:217:private CampaignTime GetCooldownTime()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:219:CampaignTime minGlobalCooldownTime = Campaign.Current.Models.IncidentModel.GetMinGlobalCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:220:CampaignTime maxGlobalCooldownTime = Campaign.Current.Models.IncidentModel.GetMaxGlobalCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:221:return CampaignTime.Hours(MBRandom.RandomFloatRanged((float)minGlobalCooldownTime.ToHours, (float)maxGlobalCooldownTime.ToHours));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:224:private void CheckIncidentsOnCooldown()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:227:foreach (KeyValuePair<Incident, CampaignTime> item in _incidentsOnCooldown)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:229:if (item.Value + item.Key.Cooldown <= CampaignTime.Now)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:236:_incidentsOnCooldown.Remove(item2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:245:if (!_incidentsOnCooldown.ContainsKey(objectType) && (objectType.Trigger & trigger) != 0 && objectType.CanIncidentBeInvoked())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:262:private Incident RegisterIncident(string id, string title, string description, IncidentTrigger trigger, IncidentType type, CampaignTime cooldown, Func<TextObject, bool> condition)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IncidentsCampaignBehaviour.cs:265:incident.Initialize(title, description, trigger, type, cooldown, condition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:239:if (num > 0f && !issueManager.HasIssueCoolDown(pid.IssueType, notable))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:281:if (num2 > num && !issueManager.HasIssueCoolDown(pid.IssueType, aliveLord))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:359:private int GetFrequencyScore(IssueBase.IssueFrequency frequency)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:364:case IssueBase.IssueFrequency.VeryCommon:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:367:case IssueBase.IssueFrequency.Common:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:370:case IssueBase.IssueFrequency.Rare:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:425:Campaign.Current.IssueManager.AddIssueCoolDownData(issue.GetType(), new HeroRelatedIssueCoolDownData(issue.IssueOwner, CampaignTime.DaysFromNow(Campaign.Current.Models.IssueModel.IssueOwnerCoolDownInDays)));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\MarriageOfferCampaignBehavior.cs:17:private const int MarriageOfferCooldownDurationAsDays = 7;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RomanceCampaignBehavior.cs:90:private CampaignTime RomanceCourtshipAttemptCooldown => CampaignTime.DaysFromNow(-1f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RomanceCampaignBehavior.cs:1150:if (list.Count == 0 || list[0].GameTime < RomanceCourtshipAttemptCooldown)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RomanceCampaignBehavior.cs:1171:if (list.Count == 0 || list[0].GameTime < RomanceCourtshipAttemptCooldown)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\SiegeAmbushCampaignBehavior.cs:15:private const int SiegeAmbushCooldownPeriodAsHours = 24;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\BarterModel.cs:9:public abstract int BarterCooldownWithHeroInDays { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\IncidentModel.cs:7:public abstract CampaignTime GetMinGlobalCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\IncidentModel.cs:9:public abstract CampaignTime GetMaxGlobalCooldownTime();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\IssueModel.cs:9:public abstract int IssueOwnerCoolDownInDays { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultBarterModel.cs:12:public override int BarterCooldownWithHeroInDays => 3;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultIncidentModel.cs:7:public override CampaignTime GetMinGlobalCooldownTime()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultIncidentModel.cs:12:public override CampaignTime GetMaxGlobalCooldownTime()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultIssueModel.cs:21:public override int IssueOwnerCoolDownInDays => 30;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\Incident.cs:49:public CampaignTime Cooldown { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\Incident.cs:63:public void Initialize(string title, string description, IncidentsCampaignBehaviour.IncidentTrigger trigger, IncidentsCampaignBehaviour.IncidentType type, CampaignTime cooldown, Func<TextObject, bool> condition)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Incidents\Incident.cs:69:Cooldown = cooldown;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArmyNeedsSuppliesIssueBehavior.cs:111:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArmyNeedsSuppliesIssueBehavior.cs:113:return IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArmyNeedsSuppliesIssueBehavior.cs:595:private const IssueBase.IssueFrequency ArmyNeedsSuppliesIssueFrequency = IssueBase.IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArmyNeedsSuppliesIssueBehavior.cs:617:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnStartIssue, typeof(ArmyNeedsSuppliesIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArmyNeedsSuppliesIssueBehavior.cs:621:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(ArmyNeedsSuppliesIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:389:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:391:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:966:private const IssueBase.IssueFrequency ArtisanCantSellProductsAtAFairPriceIssueFrequency = IssueBase.IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:977:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnStartIssue, typeof(ArtisanCantSellProductsAtAFairPriceIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:981:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(ArtisanCantSellProductsAtAFairPriceIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:356:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:358:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:704:private const IssueBase.IssueFrequency ArtisanOverpricedGoodsIssueFrequency = IssueBase.IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:735:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnStartIssue, typeof(ArtisanOverpricedGoodsIssue), IssueBase.IssueFrequency.Common, keyValuePair));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:739:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(ArtisanOverpricedGoodsIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:97:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:99:return IssueFrequency.Rare;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:826:private const IssueBase.IssueFrequency BettingFraudIssueFrequency = IssueBase.IssueFrequency.Rare;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:911:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnStartIssue, typeof(BettingFraudIssue), IssueBase.IssueFrequency.Rare));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:915:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(BettingFraudIssue), IssueBase.IssueFrequency.Rare));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:189:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:191:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:496:private const IssueBase.IssueFrequency CapturedByBountyHuntersIssueFrequency = IssueBase.IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:538:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnSelected, typeof(CapturedByBountyHuntersIssue), IssueBase.IssueFrequency.Common, selectedHideout));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:542:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(CapturedByBountyHuntersIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:201:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:203:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:852:private const IssueBase.IssueFrequency CaravanAmbushIssueFrequency = IssueBase.IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:867:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnIssueSelected, typeof(CaravanAmbushIssue), IssueBase.IssueFrequency.Common, targetSettlement));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:189:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:191:return IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1333:private const IssueBase.IssueFrequency EscortMerchantCaravanIssueFrequency = IssueBase.IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1414:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnSelected, typeof(EscortMerchantCaravanIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1418:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(EscortMerchantCaravanIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:235:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:237:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:1170:private const IssueBase.IssueFrequency ExtortionByDesertersIssueFrequency = IssueBase.IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:1176:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnIssueSelected, typeof(ExtortionByDesertersIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:1180:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(ExtortionByDesertersIssue), IssueBase.IssueFrequency.Common));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsRecruitsIssueBehavior.cs:149:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsRecruitsIssueBehavior.cs:151:return IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsRecruitsIssueBehavior.cs:503:private const IssueBase.IssueFrequency GangLeaderNeedsRecruitsIssueFrequency = IssueBase.IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsRecruitsIssueBehavior.cs:527:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnSelected, typeof(GangLeaderNeedsRecruitsIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsRecruitsIssueBehavior.cs:531:Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(GangLeaderNeedsRecruitsIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsSpecialWeaponsIssueBehavior.cs:73:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsSpecialWeaponsIssueBehavior.cs:75:return IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsSpecialWeaponsIssueBehavior.cs:500:private const IssueBase.IssueFrequency SpecialWeaponOrderIssueFrequency = IssueBase.IssueFrequency.VeryCommon;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsSpecialWeaponsIssueBehavior.cs:509:Campaign.Current.IssueManager.AddPotentialIssueData(hero, ConditionsHold(hero) ? new PotentialIssueData(OnStartIssue, typeof(GangLeaderNeedsSpecialWeaponsIssue), IssueBase.IssueFrequency.VeryCommon) : new PotentialIssueData(typeof(GangLeaderNeedsSpecialWeaponsIssue), IssueBase.IssueFrequency.VeryCommon));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:276:public override IssueFrequency GetFrequency()
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:278:return IssueFrequency.Common;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:922:private const IssueBase.IssueFrequency GangLeaderNeedsToOffloadStolenGoodsIssueFrequency = IssueBase.IssueFrequency.Common;

 succeeded in 5456ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:495:public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:500:eventReceivers[i].OnTournamentFinished(winner, participants, town, prize);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:209:public virtual void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:131:private readonly MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> _tournamentFinished = new MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:689:public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> TournamentFinished => Instance._tournamentFinished;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1327:_tournamentFinished.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1668:public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1670:Instance._tournamentFinished.Invoke(winner, participants, town, prize);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DefaultLogsCampaignBehavior.cs:36:CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DefaultLogsCampaignBehavior.cs:192:private void OnTournamentFinished(CharacterObject character, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:382:CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\BettingFraudIssueBehavior.cs:403:private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LadysKnightOutIssueBehavior.cs:431:CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LadysKnightOutIssueBehavior.cs:523:private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.TournamentGames\TournamentCampaignBehavior.cs:27:CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.TournamentGames\TournamentCampaignBehavior.cs:160:private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.TournamentGames\TournamentManager.cs:151:CampaignEventDispatcher.Instance.OnTournamentFinished(winner, participantCharacters, tournament.Town, tournament.Prize);

 succeeded in 5457ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:323:private readonly MbEvent<MapEvent> _onPlayerBattleEndEvent = new MbEvent<MapEvent>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:879:public static IMbEvent<MapEvent> OnPlayerBattleEndEvent => Instance._onPlayerBattleEndEvent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1231:_onPlayerBattleEndEvent.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2140:Instance._onPlayerBattleEndEvent.Invoke(mapEvent);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HideoutCampaignBehavior.cs:400:bool num3 = PlayerEncounter.Battle.WinningSide == PlayerEncounter.Current.PlayerSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyHealCampaignBehavior.cs:30:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerVariablesBehavior.cs:15:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors\CommentOnEndPlayerBattleBehavior.cs:11:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnded);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encounters\PlayerEncounter.cs:1318:if (_mapEvent.WinningSide == PlayerSide && Battle.RetreatingSide == BattleSideEnum.None)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encounters\PlayerEncounter.cs:1342:else if (_mapEvent != null && _mapEvent.IsNavalMapEvent && _mapEvent.IsFieldBattle && _mapEvent.WinningSide == BattleSideEnum.None && (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide).AnyQ((MapEventParty x) => x.Party.Ships.Count == 0) || _mapEvent.PartiesOnSide(_mapEvent.PlayerSide.GetOppositeSide()).AnyQ((MapEventParty x) => x.Party.Ships.Count == 0)))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ExtortionByDesertersIssueBehavior.cs:838:if (mapEvent.WinningSide == mapEvent.PlayerSide)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsWeaponsIssueQuestBehavior.cs:602:bool num = PlayerEncounter.Battle.WinningSide == PlayerEncounter.Battle.PlayerSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandLordCompanyOfTroubleIssueBehavior.cs:913:bool num = PlayerEncounter.Battle.WinningSide == PlayerEncounter.Battle.PlayerSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordNeedsAccessToVillageCommonsIssueBehavior.cs:821:if (mapEvent.WinningSide == mapEvent.PlayerSide)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordTrainingForRetainersIssueBehavior.cs:419:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LesserNobleRevoltIssueBehavior.cs:1038:if (PlayerEncounter.Battle.WinningSide == PlayerEncounter.Battle.PlayerSide)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordWantsRivalCapturedIssueBehavior.cs:669:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEventEnded);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordWantsRivalCapturedIssueBehavior.cs:739:if ((mapEvent.IsPlayerMapEvent || mapEvent.IsPlayerSimulation) && mapEvent.PartiesOnSide(mapEvent.GetOtherSide(mapEvent.PlayerSide)).Any((MapEventParty x) => x.Party.Owner == _targetHero) && mapEvent.HasWinner && mapEvent.WinningSide == mapEvent.PlayerSide && !_targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\MerchantArmyOfPoachersIssueBehavior.cs:1078:bool flag = PlayerEncounter.Battle.WinningSide == PlayerEncounter.Battle.PlayerSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\SmugglersIssueBehavior.cs:878:if (mapEvent.WinningSide == mapEvent.PlayerSide)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues.IssueQuestTasks\CaptureAndBringNpcTask.cs:85:CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:216:_playerVictory = playerSide == mapEvent.WinningSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:219:_isEasyPlayerVictory = strengthRatio < 0.5f && troopCasualties * 3 < troopCasualties2 && playerSide == mapEvent.WinningSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:222:_isEasyPlayerVictory = strengthRatio < 0.25f && troopCasualties * 3 < troopCasualties2 && playerSide == mapEvent.WinningSide;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:226:_isAgainstCaravan = playerSide == mapEvent.WinningSide && mobileParty != null && mobileParty.IsCaravan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:1460:if (IsPlayerMapEvent && IsNavalMapEvent && WinningSide == PlayerSide && !_playerFigureheadCalculated)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:1647:bool flag = IsPlayerMapEvent && PlayerSide == WinningSide;

 succeeded in 5455ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:684:public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:689:eventReceivers[i].OnHeroPrisonerTaken(capturer, prisoner);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:317:public virtual void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:171:private readonly MbEvent<PartyBase, Hero> _heroPrisonerTaken = new MbEvent<PartyBase, Hero>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:729:public static IMbEvent<PartyBase, Hero> HeroPrisonerTaken => Instance._heroPrisonerTaken;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1183:_heroPrisonerTaken.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1768:public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1770:Instance._heroPrisonerTaken.Invoke(capturer, prisoner);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\TakePrisonerAction.cs:42:CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(capturerParty, prisonerCharacter);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DefaultLogsCampaignBehavior.cs:27:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandPartyCampaignBehavior.cs:33:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DisbandPartyCampaignBehavior.cs:163:private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnCampaignBehavior.cs:24:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnCampaignBehavior.cs:265:private void OnHeroPrisonerTaken(PartyBase capturerParty, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\MarriageOfferCampaignBehavior.cs:55:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\MarriageOfferCampaignBehavior.cs:207:private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyRolesCampaignBehavior.cs:18:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyRolesCampaignBehavior.cs:34:private void OnHeroPrisonerTaken(PartyBase party, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerCaptivityCampaignBehavior.cs:36:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RansomOfferCampaignBehavior.cs:56:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RansomOfferCampaignBehavior.cs:59:private void OnHeroPrisonerTaken(PartyBase party, Hero hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\TeleportationCampaignBehavior.cs:137:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, HeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\TeleportationCampaignBehavior.cs:352:private void HeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\VassalAndMercenaryOfferCampaignBehavior.cs:48:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\VassalAndMercenaryOfferCampaignBehavior.cs:127:private void OnHeroPrisonerTaken(PartyBase captor, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\HeadmanNeedsGrainIssueBehavior.cs:514:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\HeadmanNeedsGrainIssueBehavior.cs:528:private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordNeedsHorsesIssueBehavior.cs:671:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordNeedsHorsesIssueBehavior.cs:760:private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordsNeedsTutorIssueBehavior.cs:456:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnPrisonerTaken);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LordWantsRivalCapturedIssueBehavior.cs:670:CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnPrisonerTaken);

codex
For the engine side, I found the relevant v1.4.6 types: `IssuesCampaignBehavior`, `IssueManager`, `CampaignEvents`, `QuestBase`, and vanilla issue behaviors that use `OnPlayerBattleEndEvent`. I’m decompiling those exact installed types now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'private void IssueHourlyTick|CheckIssueForHero|AddIssueCoolDownData|HasIssueCoolDown|GetFrequencyScore|IssueOwnerCoolDown|AddPotentialIssueData|OverIssue' -Context 6,16 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.IssueManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'AddPotentialIssueData|HasIssueCoolDown|AddIssueCoolDownData|IssueCoolDownData|Dictionary<string|PotentialIssueData|CreateNewIssue' -Context 4,18 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignEvents 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEndEvent|TournamentFinished|HeroPrisonerTaken|WarDeclared|OnClanChangedKingdomEvent' -Context 3,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.QuestBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'CompleteQuestWithSuccess|CompleteQuestWithCancel|CompleteQuest|IsOngoing|RemoveAllListeners|ClearListeners|FinishQuest|AddLog' -Context 4,18 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:08:54.248176Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.IssueManager in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 880ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.IssueManager in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 2530ms:
  
  	[SaveableProperty(104)]
  	public bool IsTrackEnabled { get; private set; }
  
> 	public bool IsOngoing => _questState == QuestStates.Ongoing;
  
  	public bool IsFinalized => _questState == QuestStates.Finalized;
  
  	public bool IsThereDiscussDialogFlow => DiscussDialogFlow != null;
  
  	public Hero QuestGiver
  	{
  		get
  		{
  			return _questGiver;
  		}
  		private set
  		{
  			_questGiver = value;
  		}
  	}
  
  	public abstract TextObject Title { get; }
  		}
  		CampaignEventDispatcher.Instance.OnQuestStarted(this);
  	}
  
> 	public void CompleteQuestWithSuccess()
  	{
  		OnCompleteWithSuccess();
  		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Success);
  		AfterFinalize();
  	}
  
> 	public void CompleteQuestWithTimeOut(TextObject timeOutLog = null)
  	{
  		bool completeWithSuccess = false;
  		bool doNotResolveTheQuest = false;
  		OnBeforeTimedOut(ref completeWithSuccess, ref doNotResolveTheQuest);
  		if (doNotResolveTheQuest)
  		{
  			return;
  		}
  		if (!completeWithSuccess)
  		{
  			if (!TextObject.IsNullOrEmpty(timeOutLog))
  			{
> 				AddLog(timeOutLog);
  			}
  			OnTimedOut();
  			FinalizeQuest();
  			CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Timeout);
  			AfterFinalize();
  		}
  		else
  		{
> 			CompleteQuestWithSuccess();
  		}
  	}
  
> 	public void CompleteQuestWithFail(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
> 			AddLog(cancelLog);
  		}
  		OnFailed();
  		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Fail);
  		AfterFinalize();
  	}
  
> 	public void CompleteQuestWithBetrayal(TextObject betrayLog = null)
  	{
  		if (betrayLog != null)
  		{
> 			AddLog(betrayLog);
  		}
  		OnBetrayal();
  		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.FailWithBetrayal);
  		AfterFinalize();
  	}
  
> 	public void CompleteQuestWithCancel(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
> 			AddLog(cancelLog);
  		}
  		OnCanceled();
  		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Cancel);
  		AfterFinalize();
  	}
  
  	private void FinalizeQuest()
  	{
  		foreach (QuestTaskBase task in _taskList)
  		{
  			if (task.IsActive)
  			{
  				task.Finish(QuestTaskBase.FinishStates.Cancel);
  			}
  		}
  		_questState = QuestStates.Finalized;
  		OnFinalize();
  		_taskList.Add(task);
  		task.SetReferences();
  	}
  
> 	public JournalLog AddLog(TextObject text, bool hideInformation = false)
  	{
  		JournalLog journalLog = new JournalLog(CampaignTime.Now, text);
  		_journalEntries.Add(journalLog);
  		CampaignEventDispatcher.Instance.OnQuestLogAdded(this, hideInformation);
  		return journalLog;
  	}
  
  	public JournalLog AddDiscreteLog(TextObject text, TextObject taskName, int currentProgress, int targetProgress, TextObject shortText = null, bool hideInformation = false)
  	{
  		JournalLog journalLog = new JournalLog(CampaignTime.Now, text, taskName, currentProgress, targetProgress, LogType.Discreate);
  		_journalEntries.Add(journalLog);
  		CampaignEventDispatcher.Instance.OnQuestLogAdded(this, hideInformation);
  		return journalLog;
  	}
  
  	public JournalLog AddTwoWayContinuousLog(TextObject text, TextObject taskName, int currentProgress, int range, bool hideInformation = false)
  	{
  		JournalLog journalLog = new JournalLog(CampaignTime.Now, text, taskName, currentProgress, range, LogType.TwoWayContinuous);

 succeeded in 2592ms:
  
  	private readonly MbEvent<Town> _tournamentStarted = new MbEvent<Town>();
  
> 	private readonly MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> _warDeclared = new MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>();
  
> 	private readonly MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> _tournamentFinished = new MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>();
  
  	private readonly MbEvent<Town> _tournamentCancelled = new MbEvent<Town>();
  
  	private readonly MbEvent<PartyBase, PartyBase, object, bool> _battleStarted = new MbEvent<PartyBase, PartyBase, object, bool>();
  
  	private readonly MbEvent<Settlement, Clan> _rebellionFinished = new MbEvent<Settlement, Clan>();
  
  	private readonly MbEvent<Town, bool> _townRebelliousStateChanged = new MbEvent<Town, bool>();
  
  	private readonly MbEvent<Kingdom, Clan> _rulingClanChanged = new MbEvent<Kingdom, Clan>();
  
> 	private readonly MbEvent<PartyBase, Hero> _heroPrisonerTaken = new MbEvent<PartyBase, Hero>();
  
  	private readonly MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> _heroPrisonerReleased = new MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool>();
  
  	private readonly MbEvent<Hero, bool> _characterBecameFugitiveEvent = new MbEvent<Hero, bool>();
  
  	private readonly MbEvent<Hero> _playerMetHero = new MbEvent<Hero>();
  
  	private readonly MbEvent<Hero> _playerLearnsAboutHero = new MbEvent<Hero>();
  
  	private readonly MbEvent<MobileParty> _onQuarterDailyPartyTick = new MbEvent<MobileParty>();
  
> 	private readonly MbEvent<MapEvent> _onPlayerBattleEndEvent = new MbEvent<MapEvent>();
  
  	private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
  
  	private readonly MbEvent<Hero> _onChildConceived = new MbEvent<Hero>();
  
  	private readonly MbEvent<Hero, List<Hero>, int> _onGivenBirthEvent = new MbEvent<Hero, List<Hero>, int>();
  
  	private readonly MbEvent<float> _missionTickEvent = new MbEvent<float>();
  
  	public static IMbEvent<Clan, bool> ClanTierIncrease => Instance._clanTierIncrease;
  
> 	public static IMbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool> OnClanChangedKingdomEvent => Instance._clanChangedKingdom;
  
  	public static IMbEvent<Clan, Kingdom, Kingdom> OnClanDefectedEvent => Instance._onClanDefected;
  
  	public static IMbEvent<Clan, bool> OnClanCreatedEvent => Instance._onClanCreatedEvent;
  
  	public static IMbEvent<Hero, MobileParty> OnHeroJoinedPartyEvent => Instance._onHeroJoinedPartyEvent;
  
  	public static IMbEvent<(Hero, PartyBase), (Hero, PartyBase), (int, string), bool> HeroOrPartyTradedGold => Instance._heroOrPartyTradedGold;
  
  	public static IMbEvent<Town> TournamentStarted => Instance._tournamentStarted;
  
> 	public static IMbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> WarDeclared => Instance._warDeclared;
  
> 	public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> TournamentFinished => Instance._tournamentFinished;
  
  	public static IMbEvent<Town> TournamentCancelled => Instance._tournamentCancelled;
  
  	public static IMbEvent<PartyBase, PartyBase, object, bool> BattleStarted => Instance._battleStarted;
  
  	public static IMbEvent<Settlement, Clan> RebellionFinished => Instance._rebellionFinished;
  
  	public static IMbEvent<Town, bool> TownRebelliosStateChanged => Instance._townRebelliousStateChanged;
  
  	public static IMbEvent<Kingdom, Clan> RulingClanChanged => Instance._rulingClanChanged;
  
> 	public static IMbEvent<PartyBase, Hero> HeroPrisonerTaken => Instance._heroPrisonerTaken;
  
  	public static IMbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> HeroPrisonerReleased => Instance._heroPrisonerReleased;
  
  	public static IMbEvent<Hero, bool> CharacterBecameFugitiveEvent => Instance._characterBecameFugitiveEvent;
  
  	public static IMbEvent<Hero> OnPlayerMetHeroEvent => Instance._playerMetHero;
  
  	public static IMbEvent<Hero> OnPlayerLearnsAboutHeroEvent => Instance._playerLearnsAboutHero;
  
  	public static IMbEvent<MobileParty> OnQuarterDailyPartyTick => Instance._onQuarterDailyPartyTick;
  
> 	public static IMbEvent<MapEvent> OnPlayerBattleEndEvent => Instance._onPlayerBattleEndEvent;
  
  	public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
  
  	public static IMbEvent<Hero> OnChildConceivedEvent => Instance._onChildConceived;
  
  	public static IMbEvent<Hero, List<Hero>, int> OnGivenBirthEvent => Instance._onGivenBirthEvent;
  
  	public static IMbEvent<float> MissionTickEvent => Instance._missionTickEvent;
  		_alleyOccupiedByPlayer.ClearListeners(obj);
  		_alleyClearedByPlayer.ClearListeners(obj);
  		_romanticStateChanged.ClearListeners(obj);
> 		_warDeclared.ClearListeners(obj);
  		_battleStarted.ClearListeners(obj);
  		_rebellionFinished.ClearListeners(obj);
  		_townRebelliousStateChanged.ClearListeners(obj);
  		_rebelliousClanDisbandedAtSettlement.ClearListeners(obj);
  		_mobilePartyDestroyed.ClearListeners(obj);
  		_mobilePartyCreated.ClearListeners(obj);
  		_mapInteractableCreated.ClearListeners(obj);
  		_mapInteractableDestroyed.ClearListeners(obj);
  		_mobilePartyQuestStatusChanged.ClearListeners(obj);
  		_heroKilled.ClearListeners(obj);
  		_characterDefeated.ClearListeners(obj);
> 		_heroPrisonerTaken.ClearListeners(obj);
  		_onPartySizeChangedEvent.ClearListeners(obj);
  		_characterBecameFugitiveEvent.ClearListeners(obj);
  		_playerMetHero.ClearListeners(obj);
  		_playerLearnsAboutHero.ClearListeners(obj);
  		_renownGained.ClearListeners(obj);
  		_barterablesRequested.ClearListeners(obj);
  		_crimeRatingChanged.ClearListeners(obj);
  		_newCompanionAdded.ClearListeners(obj);
  		_onPartyJoinedArmyEvent.ClearListeners(obj);
  		_onPartyRemovedFromArmyEvent.ClearListeners(obj);
  		_onMissionEndedEvent.ClearListeners(obj);
> 		_onPlayerBattleEndEvent.ClearListeners(obj);
  		_onPlayerBoardGameOver.ClearListeners(obj);
  		_onRansomOfferedToPlayer.ClearListeners(obj);
  		_onRansomOfferCancelled.ClearListeners(obj);
  		_onPeaceOfferedToPlayer.ClearListeners(obj);
  		_onTradeAgreementSignedEvent.ClearListeners(obj);
  		_onPeaceOfferResolved.ClearListeners(obj);
  		_onMarriageOfferedToPlayerEvent.ClearListeners(obj);
  		_onMarriageOfferCanceledEvent.ClearListeners(obj);
  		_playerEliminatedFromTournament.ClearListeners(obj);
  		_playerStartedTournamentMatch.ClearListeners(obj);
  		_tournamentStarted.ClearListeners(obj);
> 		_tournamentFinished.ClearListeners(obj);
  		_tournamentCancelled.ClearListeners(obj);
  		_playerInventoryExchangeEvent.ClearListeners(obj);
  		_onItemsDiscardedByPlayerEvent.ClearListeners(obj);
  		_onNewItemCraftedEvent.ClearListeners(obj);
  		_craftingPartUnlockedEvent.ClearListeners(obj);
  		_onWorkshopInitializedEvent.ClearListeners(obj);
  		_onWorkshopOwnerChangedEvent.ClearListeners(obj);
  		_onWorkshopTypeChangedEvent.ClearListeners(obj);
  		Instance._tournamentStarted.Invoke(town);
  	}
  
> 	public override void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
  	{
> 		Instance._warDeclared.Invoke(faction1, faction2, declareWarDetail);
  	}
  
> 	public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
  	{
> 		Instance._tournamentFinished.Invoke(winner, participants, town, prize);
  	}
  
  	public override void OnTournamentCancelled(Town town)
  	{
  		Instance._tournamentCancelled.Invoke(town);
  	}
  
  	public override void OnStartBattle(PartyBase attackerParty, PartyBase defenderParty, object subject, bool showNotification)
  		Instance._rulingClanChanged.Invoke(kingdom, oldRulingClan);
  	}
  
> 	public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
  	{
> 		Instance._heroPrisonerTaken.Invoke(capturer, prisoner);
  	}
  
  	public override void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification = true)
  	{
  		Instance._heroPrisonerReleased.Invoke(prisoner, party, capturerFaction, detail, showNotification);
  	}
  
  	public override void OnCharacterBecameFugitive(Hero hero, bool showNotification)
  
  	public override void OnPlayerBattleEnd(MapEvent mapEvent)
  	{
> 		Instance._onPlayerBattleEndEvent.Invoke(mapEvent);
  	}
  
  	public override void OnUnitRecruited(CharacterObject character, int amount)
  	{
  		Instance._onUnitRecruitedEvent.Invoke(character, amount);
  	}
  
  	public override void OnChildConceived(Hero mother)

 succeeded in 2808ms:
  		{
  			if (notable.Issue != null || !notable.CanHaveCampaignIssues())
  			{
  				continue;
  			}
  			List<PotentialIssueData> list = Campaign.Current.IssueManager.CheckForIssues(notable);
> 			int totalFrequencyScore = list.SumQ((PotentialIssueData x) => GetFrequencyScore(x.Frequency));
  			foreach (PotentialIssueData item in list)
  			{
  				PotentialIssueData pid = item;
  				if (pid.IsValid)
  				{
  					float num = CalculateIssueScoreForNotable(in pid, settlement, totalDesiredIssueCount, totalFrequencyScore);
> 					if (num > 0f && !issueManager.HasIssueCoolDown(pid.IssueType, notable))
  					{
  						_cachedIssueDataList.Add(new IssueData(pid, notable, num));
  					}
  				}
  			}
  		}
  		if (_cachedIssueDataList.Count > 0)
  		{
  			List<(IssueData, float)> list2 = new List<(IssueData, float)>();
  			foreach (IssueData cachedIssueData in _cachedIssueDataList)
  			{
  				list2.Add((cachedIssueData, cachedIssueData.Score));
  			}
  			IssueData issueData = MBRandom.ChooseWeighted(list2);
  			Campaign.Current.IssueManager.CreateNewIssue(in issueData.PotentialIssueData, issueData.Hero);
  			_cachedIssueDataList.Clear();
  		{
  			if (aliveLord.Clan == Clan.PlayerClan || !aliveLord.CanHaveCampaignIssues() || !(aliveLord.Age >= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge) || (!aliveLord.IsActive && !aliveLord.IsPrisoner) || aliveLord.Issue != null)
  			{
  				continue;
  			}
  			List<PotentialIssueData> list = Campaign.Current.IssueManager.CheckForIssues(aliveLord);
> 			int totalFrequencyScore = list.SumQ((PotentialIssueData x) => GetFrequencyScore(x.Frequency));
  			foreach (PotentialIssueData item in list)
  			{
  				PotentialIssueData pid = item;
  				if (pid.IsValid)
  				{
  					float num2 = CalculateIssueScoreForClan(in pid, clan, totalDesiredIssueCount, totalFrequencyScore);
> 					if (num2 > num && !issueManager.HasIssueCoolDown(pid.IssueType, aliveLord))
  					{
  						issueData = new IssueData(pid, aliveLord, num2);
  						num = num2;
  					}
  				}
  			}
  		}
  		if (issueData.HasValue)
  		{
  			IssueManager issueManager2 = Campaign.Current.IssueManager;
  			IssueData value = issueData.Value;
  			issueManager2.CreateNewIssue(in value.PotentialIssueData, issueData.Value.Hero);
  			return true;
  		}
  		return false;
  	}
  		}
  		return CalculateIssueScoreInternal(in pid, totalDesiredIssueCount, totalFrequencyScore);
  	}
  
  	private float CalculateIssueScoreInternal(in PotentialIssueData pid, int totalDesiredIssueCount, int totalFrequencyScore)
  	{
> 		float num = (float)GetFrequencyScore(pid.Frequency) / (float)totalFrequencyScore;
  		float num2;
  		if (totalDesiredIssueCount == 0)
  		{
  			num2 = 1f;
  		}
  		else
  		{
  			int num3 = 0;
  			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  			{
  				if (issue.Value.GetType() == pid.IssueType)
  				{
  					num3++;
  				}
  			}
  			num2 = (float)num3 / (float)totalDesiredIssueCount;
  		{
  			num4 *= 0.1f;
  		}
  		return num * num4;
  	}
  
> 	private int GetFrequencyScore(IssueBase.IssueFrequency frequency)
  	{
  		int result = 0;
  		switch (frequency)
  		{
  		case IssueBase.IssueFrequency.VeryCommon:
  			result = 6;
  			break;
  		case IssueBase.IssueFrequency.Common:
  			result = 3;
  			break;
  		case IssueBase.IssueFrequency.Rare:
  			result = 1;
  			break;
  		}
  		return result;
  	}
  			{
  				ChangeRelationAction.ApplyPlayerRelation(issue.IsSolvingWithQuest ? issue.IssueQuest.QuestGiver : issue.IssueOwner, num);
  			}
  		}
  		if (details == IssueBase.IssueUpdateDetails.IssueCancel || details == IssueBase.IssueUpdateDetails.IssueFail || details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess || details == IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal || details == IssueBase.IssueUpdateDetails.IssueTimedOut || details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest || details == IssueBase.IssueUpdateDetails.IssueFinishedByAILord)
  		{
> 			Campaign.Current.IssueManager.AddIssueCoolDownData(issue.GetType(), new HeroRelatedIssueCoolDownData(issue.IssueOwner, CampaignTime.DaysFromNow(Campaign.Current.Models.IssueModel.IssueOwnerCoolDownInDays)));
  		}
  	}
  
  	public override void SyncData(IDataStore dataStore)
  	{
  	}
  
  	private void OnSessionLaunched(CampaignGameStarter starter)
  	{
  		List<Settlement> settlements = Settlement.All.Where((Settlement x) => x.IsTown || x.IsVillage).ToList();
  		DeterministicShuffle(settlements);
  		AddDialogues(starter);
  	}
  
  	private void DeterministicShuffle(List<Settlement> settlements)
  	{

codex
The first decompile confirms the saturation mechanism keys on `pid.IssueType` and active issue `GetType()`. I need the `IssueManager` namespace and the event list implementation next to settle cooldown storage and re-entrancy.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'class IssueManager' -SimpleMatch | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\**\\*.cs' -Pattern 'class MbEvent' -SimpleMatch -ErrorAction SilentlyContinue | Select-Object -First 20 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'public Dictionary<Hero, IssueBase> Issues|IssueManager' -SimpleMatch | Select-Object -First 50 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.Library.dll' | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 444ms:

[32;1mName                  [0m[32;1m FullName[0m
[32;1m----                  [0m [32;1m--------[0m
TaleWorlds.Library.dll E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Libr…


 succeeded in 1420ms:
 succeeded in 1476ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\IssueManager.cs:18:public class IssueManager : CampaignEventReceiver

 succeeded in 15511ms:
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:15327:public class MbEvent : IMbEvent
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:22489:public class MbEvent<T> : IMbEvent<T>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:22825:public class MbEvent<T1, T2> : IMbEvent<T1, T2>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:22909:public class MbEvent<T1, T2, T3> : IMbEvent<T1, T2, T3>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:22993:public class MbEvent<T1, T2, T3, T4> : IMbEvent<T1, T2, T3, T4>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:23077:public class MbEvent<T1, T2, T3, T4, T5> : IMbEvent<T1, T2, T3, T4, T5>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:23161:public class MbEvent<T1, T2, T3, T4, T5, T6> : IMbEvent<T1, T2, T3, T4, T5, T6>, IMbEventBase
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.CampaignSystem.cs:23245:public class MbEvent<T1, T2, T3, T4, T5, T6, T7> : IMbEvent<T1, T2, T3, T4, T5, T6, T7>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:15246:public class MbEvent : IMbEvent
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:22403:public class MbEvent<T> : IMbEvent<T>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:22723:public class MbEvent<T1, T2> : IMbEvent<T1, T2>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:22803:public class MbEvent<T1, T2, T3> : IMbEvent<T1, T2, T3>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:22883:public class MbEvent<T1, T2, T3, T4> : IMbEvent<T1, T2, T3, T4>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:22963:public class MbEvent<T1, T2, T3, T4, T5> : IMbEvent<T1, T2, T3, T4, T5>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:23043:public class MbEvent<T1, T2, T3, T4, T5, T6> : IMbEvent<T1, T2, T3, T4, T5, T6>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:23123:public class MbEvent<T1, T2, T3, T4, T5, T6, T7> : IMbEvent<T1, T2, T3, T4, T5, T6, T7>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:15203:public class MbEvent : IMbEvent
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:22360:public class MbEvent<T> : IMbEvent<T>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:22680:public class MbEvent<T1, T2> : IMbEvent<T1, T2>, IMbEventBase
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.CampaignSystem.cs:22760:public class MbEvent<T1, T2, T3> : IMbEvent<T1, T2, T3>, IMbEventBase

codex
`IssueManager` is under `TaleWorlds.CampaignSystem.Issues`. The event container appears in TaleWorlds.Library, so I’m decompiling that generic event implementation from the installed Library DLL to confirm listener removal during dispatch.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.IssueManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'private Dictionary|public Dictionary|AddPotentialIssueData|CheckForIssues|CreateNewIssue|HasIssueCoolDown|AddIssueCoolDownData|RemoveIssue|OnIssueUpdated' -Context 4,20 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.Library.MbEvent`2 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Library.dll' | Select-String -Pattern 'class MbEvent|AddNonSerializedListener|ClearListeners|Invoke|Remove|delegateList|List' -Context 4,20 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.Library.MbEvent`1 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Library.dll' | Select-String -Pattern 'class MbEvent|AddNonSerializedListener|ClearListeners|Invoke|Remove|delegateList|List' -Context 4,24 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.Library.MbEvent`4 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Library.dll' | Select-String -Pattern 'class MbEvent|AddNonSerializedListener|ClearListeners|Invoke|Remove|delegateList|List' -Context 4,20 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:09:37.282022Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent2 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 723ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent2 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

2026-06-20T18:09:37.288907Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent4 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 729ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent4 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

2026-06-20T18:09:37.301562Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent1 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 743ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.Library.MbEvent1 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 1425ms:
  	[SaveableField(1)]
  	private readonly Dictionary<Hero, IssueBase> _issues;
  
  	[SaveableField(2)]
> 	private Dictionary<string, List<IssueCoolDownData>> _issuesCoolDownData;
  
  	[CachedData]
> 	private Dictionary<Hero, List<PotentialIssueData>> _issueArgs;
  
  	[SaveableField(4)]
  	private TroopRoster _awaitingAlternativeSolutionTroops = TroopRoster.CreateDummyTroopRoster();
  
  	public const string IssueOfferToken = "issue_offer";
  
  	public const string HeroMainOptionsToken = "hero_main_options";
  
  	public const string IssueClassicQuestStartToken = "issue_classic_quest_start";
  
  	public const string IssueDiscussAlternativeSolution = "issue_discuss_alternative_solution";
  
  	private const float IssueCancelChance = 0.2f;
  
  	public IEnumerable<Hero> IssueSolvingCompanionList
  	{
  		get
  		{
  			foreach (KeyValuePair<Hero, IssueBase> issue in Issues)
  			{
  		}
  		ExpireInvalidData();
  	}
  
> 	public bool CreateNewIssue(in PotentialIssueData pid, Hero issueOwner)
  	{
  		IssueBase issueBase = pid.OnStartIssue(in pid, issueOwner);
  		issueBase.StringId = "issue_" + _nextIssueUniqueIndex;
  		_nextIssueUniqueIndex++;
  		issueBase.AfterCreation();
  		_issues.Add(issueOwner, issueBase);
  		issueOwner.OnIssueCreatedForHero(issueBase);
  		if (issueOwner.PartyBelongedTo != null)
  		{
  			issueBase.AddTrackedObject(issueOwner.PartyBelongedTo);
  		}
  		CampaignEventDispatcher.Instance.OnNewIssueCreated(issueBase);
  		return true;
  	}
  
  	public bool StartIssueQuest(Hero issueOwner)
  	{
  		if (Issues[issueOwner].StartIssueWithQuest())
  		{
  			return true;
  			_issueArgs.Add(hero, new List<PotentialIssueData>());
  		}
  	}
  
> 	public void AddPotentialIssueData(Hero hero, PotentialIssueData issueData)
  	{
  		_issueArgs[hero].Add(issueData);
  	}
  
  	private List<PotentialIssueData> GetPotentialIssues(Hero hero)
  	{
  		return _issueArgs[hero];
  	}
  
> 	public List<PotentialIssueData> CheckForIssues(Hero issueOwner)
  	{
  		PrepareIssueArguments(issueOwner);
  		if (!Issues.ContainsKey(issueOwner))
  		{
  			CampaignEventDispatcher.Instance.OnCheckForIssue(issueOwner);
  		}
  		return GetPotentialIssues(issueOwner);
  	}
  
  	public override void DailyTick()
  	{
  		ExpireInvalidData();
  		List<IssueBase> list = new List<IssueBase>();
  		List<IssueBase> list2 = new List<IssueBase>();
  		List<IssueBase> list3 = new List<IssueBase>();
  		foreach (KeyValuePair<Hero, IssueBase> issue in Issues)
  		{
  			IssueBase value = issue.Value;
  			bool flag = false;
  			if (value.IsSolvingWithAlternative)
  			issue.Value.ToggleTrackedObjects(enableTrack);
  		}
  	}
  
> 	public void AddIssueCoolDownData(Type type, IssueCoolDownData data)
  	{
  		string name = type.Name;
  		if (!_issuesCoolDownData.ContainsKey(name))
  		{
  			_issuesCoolDownData.Add(name, new List<IssueCoolDownData>());
  		}
  		_issuesCoolDownData[name].Add(data);
  	}
  
> 	public bool HasIssueCoolDown(Type type, Hero hero)
  	{
  		string name = type.Name;
  		bool result = false;
  		if (_issuesCoolDownData.TryGetValue(name, out var value))
  		{
  			foreach (IssueCoolDownData item in value)
  			{
  				if (item.IsValid() && item.IsRelatedTo(hero))
  				{
  					result = true;
  					break;
  				}
  			}
  		}
  		return result;
  	}
  
  	public override void CanHaveCampaignIssues(Hero hero, ref bool result)
  	{
  		if (Issues.TryGetValue(hero, out var _))

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs' -TotalCount 20" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Library.dll' | Select-String -Pattern 'MbEvent' | Select-Object -First 30 | ForEach-Object { "'$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'MbEvent' | Select-Object -First 30 | ForEach-Object { "'$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path 'E:\\Decompiled_Bannerlord\\_shipping_build\\TaleWorlds.CampaignSystem.cs' | Select-Object -Skip 22395 -First 80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 527ms:
using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

 succeeded in 591ms:
	{
		void ClearListeners(object o);
	}
	public interface IMbEvent<out T> : IMbEventBase
	{
		void AddNonSerializedListener(object owner, Action<T> action);
	}
	public class MbEvent<T> : IMbEvent<T>, IMbEventBase
	{
		internal class EventHandlerRec<TS>
		{
			public EventHandlerRec<TS> Next;

			internal Action<TS> Action { get; private set; }

			internal object Owner { get; private set; }

			public EventHandlerRec(object owner, Action<TS> action)
			{
				Action = action;
				Owner = owner;
			}
		}

		private EventHandlerRec<T> _nonSerializedListenerList;

		public void AddNonSerializedListener(object owner, Action<T> action)
		{
			EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
			EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
			_nonSerializedListenerList = eventHandlerRec;
			eventHandlerRec.Next = nonSerializedListenerList;
		}

		public void Invoke(T t)
		{
			InvokeList(_nonSerializedListenerList, t);
		}

		private void InvokeList(EventHandlerRec<T> list, T t)
		{
			while (list != null)
			{
				list.Action(t);
				list = list.Next;
			}
		}

		public void ClearListeners(object o)
		{
			ClearListenerOfList(ref _nonSerializedListenerList, o);
		}

		private void ClearListenerOfList(ref EventHandlerRec<T> list, object o)
		{
			EventHandlerRec<T> eventHandlerRec = list;
			while (eventHandlerRec != null && eventHandlerRec.Owner != o)
			{
				eventHandlerRec = eventHandlerRec.Next;
			}
			if (eventHandlerRec == null)
			{
				return;
			}
			EventHandlerRec<T> eventHandlerRec2 = list;
			if (eventHandlerRec2 == eventHandlerRec)
			{
				list = eventHandlerRec2.Next;
				return;
			}
			while (eventHandlerRec2 != null)
			{
				if (eventHandlerRec2.Next == eventHandlerRec)
				{
					eventHandlerRec2.Next = eventHandlerRec.Next;
				}
				else
				{
					eventHandlerRec2 = eventHandlerRec2.Next;
				}

 succeeded in 771ms:
 succeeded in 740ms:
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec

codex
The event list implementation is a simple linked list invoked from a local head pointer; clearing a listener removes nodes from the event’s stored head, not the already captured local traversal. That makes “complete during event dispatch” safe from collection mutation exceptions, though it does not prevent later handlers from seeing the completed quest state.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.CampaignSystem.MbEvent`1 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.CampaignSystem.MbEvent`2 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.CampaignSystem.MbEvent`4 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'ilspycmd -t TaleWorlds.CampaignSystem.MbEvent`5 '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:10:02.915500Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent4 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 751ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent4 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

2026-06-20T18:10:02.937542Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent5 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 718ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent5 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

2026-06-20T18:10:02.958921Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent1 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 821ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent1 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

2026-06-20T18:10:02.991189Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent2 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 840ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.MbEvent2 in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

codex
The generic type name needs quoting in PowerShell; I’m rerunning the decompile with the type name as a literal so the backtick arity is preserved.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t 'TaleWorlds.CampaignSystem.MbEvent"'`2'"' 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t 'TaleWorlds.CampaignSystem.MbEvent"'`1'"' 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t 'TaleWorlds.CampaignSystem.MbEvent"'`4'"' 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t 'TaleWorlds.CampaignSystem.MbEvent"'`5'"' 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public class MbEvent|AddNonSerializedListener|public void Invoke|InvokeList|ClearListeners|ClearListenerOfList' -Context 2,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1156ms:
  namespace TaleWorlds.CampaignSystem;
  
> public class MbEvent<T> : IMbEvent<T>, IMbEventBase
  {
  	internal class EventHandlerRec<TS>
  	{
  		public EventHandlerRec<TS> Next;
  
  		internal Action<TS> Action { get; private set; }
  
  		internal object Owner { get; private set; }
  
  		public EventHandlerRec(object owner, Action<TS> action)
  		{
  			Action = action;
  			Owner = owner;
  		}
  	}
  
  	private EventHandlerRec<T> _nonSerializedListenerList;
  
> 	public void AddNonSerializedListener(object owner, Action<T> action)
  	{
  		EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
  		EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
  		_nonSerializedListenerList = eventHandlerRec;
  		eventHandlerRec.Next = nonSerializedListenerList;
  	}
  
> 	public void Invoke(T t)
  	{
> 		InvokeList(_nonSerializedListenerList, t);
  	}
  
> 	private void InvokeList(EventHandlerRec<T> list, T t)
  	{
  		while (list != null)
  		{
  			list.Action(t);
  			list = list.Next;
  		}
  	}
  
> 	public void ClearListeners(object o)
  	{
> 		ClearListenerOfList(ref _nonSerializedListenerList, o);
  	}
  
> 	private void ClearListenerOfList(ref EventHandlerRec<T> list, object o)
  	{
  		EventHandlerRec<T> eventHandlerRec = list;
  		while (eventHandlerRec != null && eventHandlerRec.Owner != o)
  		{
  			eventHandlerRec = eventHandlerRec.Next;
  		}
  		if (eventHandlerRec == null)
  		{
  			return;
  		}
  		EventHandlerRec<T> eventHandlerRec2 = list;
  		if (eventHandlerRec2 == eventHandlerRec)
  		{
  			list = eventHandlerRec2.Next;
  			return;
  		}
  		while (eventHandlerRec2 != null)
  		{
  			if (eventHandlerRec2.Next == eventHandlerRec)
  			{
  				eventHandlerRec2.Next = eventHandlerRec.Next;
  			}

 succeeded in 1174ms:
  namespace TaleWorlds.CampaignSystem;
  
> public class MbEvent<T1, T2> : IMbEvent<T1, T2>, IMbEventBase
  {
  	internal class EventHandlerRec<TS, TQ>
  	{
  		public EventHandlerRec<TS, TQ> Next;
  
  		internal Action<TS, TQ> Action { get; private set; }
  
  		internal object Owner { get; private set; }
  
  		public EventHandlerRec(object owner, Action<TS, TQ> action)
  		{
  			Action = action;
  			Owner = owner;
  		}
  	}
  
  	private EventHandlerRec<T1, T2> _nonSerializedListenerList;
  
> 	public void AddNonSerializedListener(object owner, Action<T1, T2> action)
  	{
  		EventHandlerRec<T1, T2> eventHandlerRec = new EventHandlerRec<T1, T2>(owner, action);
  		EventHandlerRec<T1, T2> nonSerializedListenerList = _nonSerializedListenerList;
  		_nonSerializedListenerList = eventHandlerRec;
  		eventHandlerRec.Next = nonSerializedListenerList;
  	}
  
> 	public void Invoke(T1 t1, T2 t2)
  	{
> 		InvokeList(_nonSerializedListenerList, t1, t2);
  	}
  
> 	private void InvokeList(EventHandlerRec<T1, T2> list, T1 t1, T2 t2)
  	{
  		while (list != null)
  		{
  			list.Action(t1, t2);
  			list = list.Next;
  		}
  	}
  
> 	public void ClearListeners(object o)
  	{
> 		ClearListenerOfList(ref _nonSerializedListenerList, o);
  	}
  
> 	private void ClearListenerOfList(ref EventHandlerRec<T1, T2> list, object o)
  	{
  		EventHandlerRec<T1, T2> eventHandlerRec = list;
  		while (eventHandlerRec != null && eventHandlerRec.Owner != o)
  		{
  			eventHandlerRec = eventHandlerRec.Next;
  		}
  		if (eventHandlerRec == null)
  		{
  			return;
  		}
  		EventHandlerRec<T1, T2> eventHandlerRec2 = list;
  		if (eventHandlerRec2 == eventHandlerRec)
  		{
  			list = eventHandlerRec2.Next;
  			return;
  		}
  		while (eventHandlerRec2 != null)
  		{
  			if (eventHandlerRec2.Next == eventHandlerRec)
  			{
  				eventHandlerRec2.Next = eventHandlerRec.Next;
  			}

 succeeded in 1161ms:
  namespace TaleWorlds.CampaignSystem;
  
> public class MbEvent<T1, T2, T3, T4> : IMbEvent<T1, T2, T3, T4>, IMbEventBase
  {
  	internal class EventHandlerRec<TA, TB, TC, TD>
  	{
  		public EventHandlerRec<TA, TB, TC, TD> Next;
  
  		internal Action<TA, TB, TC, TD> Action { get; private set; }
  
  		internal object Owner { get; private set; }
  
  		public EventHandlerRec(object owner, Action<TA, TB, TC, TD> action)
  		{
  			Action = action;
  			Owner = owner;
  		}
  	}
  
  	private EventHandlerRec<T1, T2, T3, T4> _nonSerializedListenerList;
  
> 	public void AddNonSerializedListener(object owner, Action<T1, T2, T3, T4> action)
  	{
  		EventHandlerRec<T1, T2, T3, T4> eventHandlerRec = new EventHandlerRec<T1, T2, T3, T4>(owner, action);
  		EventHandlerRec<T1, T2, T3, T4> nonSerializedListenerList = _nonSerializedListenerList;
  		_nonSerializedListenerList = eventHandlerRec;
  		eventHandlerRec.Next = nonSerializedListenerList;
  	}
  
> 	public void Invoke(T1 t1, T2 t2, T3 t3, T4 t4)
  	{
> 		InvokeList(_nonSerializedListenerList, t1, t2, t3, t4);
  	}
  
> 	private void InvokeList(EventHandlerRec<T1, T2, T3, T4> list, T1 t1, T2 t2, T3 t3, T4 t4)
  	{
  		while (list != null)
  		{
  			list.Action(t1, t2, t3, t4);
  			list = list.Next;
  		}
  	}
  
> 	public void ClearListeners(object o)
  	{
> 		ClearListenerOfList(ref _nonSerializedListenerList, o);
  	}
  
> 	private void ClearListenerOfList(ref EventHandlerRec<T1, T2, T3, T4> list, object o)
  	{
  		EventHandlerRec<T1, T2, T3, T4> eventHandlerRec = list;
  		while (eventHandlerRec != null && eventHandlerRec.Owner != o)
  		{
  			eventHandlerRec = eventHandlerRec.Next;
  		}
  		if (eventHandlerRec == null)
  		{
  			return;
  		}
  		EventHandlerRec<T1, T2, T3, T4> eventHandlerRec2 = list;
  		if (eventHandlerRec2 == eventHandlerRec)
  		{
  			list = eventHandlerRec2.Next;
  			return;
  		}
  		while (eventHandlerRec2 != null)
  		{
  			if (eventHandlerRec2.Next == eventHandlerRec)
  			{
  				eventHandlerRec2.Next = eventHandlerRec.Next;
  			}

 succeeded in 1175ms:
  namespace TaleWorlds.CampaignSystem;
  
> public class MbEvent<T1, T2, T3, T4, T5> : IMbEvent<T1, T2, T3, T4, T5>, IMbEventBase
  {
  	internal class EventHandlerRec<TA, TB, TC, TD, TE>
  	{
  		public EventHandlerRec<TA, TB, TC, TD, TE> Next;
  
  		internal Action<TA, TB, TC, TD, TE> Action { get; private set; }
  
  		internal object Owner { get; private set; }
  
  		public EventHandlerRec(object owner, Action<TA, TB, TC, TD, TE> action)
  		{
  			Action = action;
  			Owner = owner;
  		}
  	}
  
  	private EventHandlerRec<T1, T2, T3, T4, T5> _nonSerializedListenerList;
  
> 	public void AddNonSerializedListener(object owner, Action<T1, T2, T3, T4, T5> action)
  	{
  		EventHandlerRec<T1, T2, T3, T4, T5> eventHandlerRec = new EventHandlerRec<T1, T2, T3, T4, T5>(owner, action);
  		EventHandlerRec<T1, T2, T3, T4, T5> nonSerializedListenerList = _nonSerializedListenerList;
  		_nonSerializedListenerList = eventHandlerRec;
  		eventHandlerRec.Next = nonSerializedListenerList;
  	}
  
> 	public void Invoke(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5)
  	{
> 		InvokeList(_nonSerializedListenerList, t1, t2, t3, t4, t5);
  	}
  
> 	private void InvokeList(EventHandlerRec<T1, T2, T3, T4, T5> list, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5)
  	{
  		while (list != null)
  		{
  			list.Action(t1, t2, t3, t4, t5);
  			list = list.Next;
  		}
  	}
  
> 	public void ClearListeners(object o)
  	{
> 		ClearListenerOfList(ref _nonSerializedListenerList, o);
  	}
  
> 	private void ClearListenerOfList(ref EventHandlerRec<T1, T2, T3, T4, T5> list, object o)
  	{
  		EventHandlerRec<T1, T2, T3, T4, T5> eventHandlerRec = list;
  		while (eventHandlerRec != null && eventHandlerRec.Owner != o)
  		{
  			eventHandlerRec = eventHandlerRec.Next;
  		}
  		if (eventHandlerRec == null)
  		{
  			return;
  		}
  		EventHandlerRec<T1, T2, T3, T4, T5> eventHandlerRec2 = list;
  		if (eventHandlerRec2 == eventHandlerRec)
  		{
  			list = eventHandlerRec2.Next;
  			return;
  		}
  		while (eventHandlerRec2 != null)
  		{
  			if (eventHandlerRec2.Next == eventHandlerRec)
  			{
  				eventHandlerRec2.Next = eventHandlerRec.Next;
  			}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.QuestBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'private void AfterFinalize|OnFinalize|ClearListeners|RemoveQuest|CompleteQuestWithSuccess|FinalizeQuest|AfterCreation|StartQuest' -Context 3,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.QuestManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'AddQuest|RemoveQuest|CompleteQuest|OnQuestCompleted|QuestBase|ClearListeners|OnGameLoaded' -Context 4,18 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignEventDispatcher 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnQuestCompleted|OnPlayerBattleEnd|OnTournamentFinished|OnHeroPrisonerTaken' -Context 3,16 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignEventReceiver 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnQuestCompleted|OnPlayerBattleEnd|OnTournamentFinished|OnHeroPrisonerTaken' -Context 2,8 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1120ms:
  	}
  
> 	public virtual void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
  	{
  	}
  
  	public virtual void OnTournamentCancelled(Town town)
  	{
  	}
  
  	public virtual void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
  	}
  
> 	public virtual void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
  	{
  	}
  
  	public virtual void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification = true)
  	{
  	}
  
  	public virtual void OnCharacterBecameFugitive(Hero hero, bool showNotification)
  	}
  
> 	public virtual void OnPlayerBattleEnd(MapEvent mapEvent)
  	{
  	}
  
  	public virtual void OnMissionEnded(IMission mission)
  	{
  	}
  
  	public virtual void TickPartialHourlyAi(MobileParty party)
  	}
  
> 	public virtual void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
  	{
  	}
  
  	public virtual void OnQuestStarted(QuestBase quest)
  	{
  	}
  
  	public virtual void OnItemProduced(ItemObject itemObject, Settlement settlement, int count)

 succeeded in 1338ms:
  		_journalEntries = new MBList<JournalLog>();
  	}
  
> 	public void StartQuest()
  	{
  		_questState = QuestStates.Ongoing;
> 		OnStartQuest();
  		RegisterEvents();
  		MapEventHelper.OnConversationEnd();
  		if (QuestGiver != null && !Campaign.Current.QuestManager.TrackedObjects.ContainsKey(QuestGiver))
  		{
  			Campaign.Current.QuestManager.AddTrackedObjectForQuest(QuestGiver, this);
  		}
  		CampaignEventDispatcher.Instance.OnQuestStarted(this);
  	}
  
> 	public void CompleteQuestWithSuccess()
  	{
  		OnCompleteWithSuccess();
> 		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Success);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithTimeOut(TextObject timeOutLog = null)
  	{
  		bool completeWithSuccess = false;
  		bool doNotResolveTheQuest = false;
  		OnBeforeTimedOut(ref completeWithSuccess, ref doNotResolveTheQuest);
  		if (doNotResolveTheQuest)
  		{
  			return;
  		}
  		if (!completeWithSuccess)
  		{
  			if (!TextObject.IsNullOrEmpty(timeOutLog))
  			{
  				AddLog(timeOutLog);
  			}
  			OnTimedOut();
> 			FinalizeQuest();
  			CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Timeout);
  			AfterFinalize();
  		}
  		else
  		{
> 			CompleteQuestWithSuccess();
  		}
  	}
  
  	public void CompleteQuestWithFail(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
  			AddLog(cancelLog);
  		}
  		OnFailed();
> 		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Fail);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithBetrayal(TextObject betrayLog = null)
  	{
  		if (betrayLog != null)
  		{
  			AddLog(betrayLog);
  		}
  		OnBetrayal();
> 		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.FailWithBetrayal);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithCancel(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
  			AddLog(cancelLog);
  		}
  		OnCanceled();
> 		FinalizeQuest();
  		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Cancel);
  		AfterFinalize();
  	}
  
> 	private void FinalizeQuest()
  	{
  		foreach (QuestTaskBase task in _taskList)
  		{
  			if (task.IsActive)
  			{
  				task.Finish(QuestTaskBase.FinishStates.Cancel);
  			}
  		}
  		_questState = QuestStates.Finalized;
> 		OnFinalize();
  		ClearRelatedFields();
  		OfferDialogFlow = null;
  		DiscussDialogFlow = null;
  		QuestCharacterDialogFlow = null;
  		Campaign.Current.QuestManager.OnQuestFinalized(this);
  	}
  
> 	private void AfterFinalize()
  	{
  		RemoveAllTrackedObjects();
  		RemoveAllMapMarkers();
  	}
  
  	protected void InitializeQuestOnCreation()
  	{
  		AddDialogs();
  	}
  
  	public void InitializeQuestOnLoadWithQuestManager()
  	{
  		RegisterEvents();
  		InitializeQuestOnGameLoad();
  		AddDialogs();
  	}
  
  	internal void HourlyTickPartyWithQuestManager(MobileParty mobileParty)
  	{
  		HourlyTickParty(mobileParty);
  	}
  
  	{
  	}
  
> 	protected virtual void OnStartQuest()
  	{
  	}
  
  	protected virtual void OnCompleteWithSuccess()
  	{
  	}
  
> 	protected virtual void OnFinalize()
  	{
  	}
  
  	public virtual void OnFailed()
  	{
  	}
  
  	protected virtual void OnTimedOut()
  	{
  	}
  
  	protected virtual void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest)
  	{
  	}
  
  	protected virtual void OnBetrayal()
  	{
  	}
  
  	public virtual void OnCanceled()
  	{
  	}

 succeeded in 1396ms:
  	public const string CharacterTalkToken = "start";
  
  	public static string PriorQuestName;
  
> 	private MBList<QuestBase> _currentHourlyTickQuestsToTimeout;
  
  	[SaveableField(0)]
> 	private readonly MBList<QuestBase> _quests;
  
  	[SaveableField(10)]
> 	private readonly Dictionary<ITrackableCampaignObject, List<QuestBase>> _trackedObjects;
  
> 	public MBReadOnlyList<QuestBase> Quests => _quests;
  
  	public bool QuestDebugMode { get; set; }
  
> 	public MBReadOnlyDictionary<ITrackableCampaignObject, List<QuestBase>> TrackedObjects { get; private set; }
  
  	internal static void AutoGeneratedStaticCollectObjectsQuestManager(object o, List<object> collectedObjects)
  	{
  		((QuestManager)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		collectedObjects.Add(_quests);
  		collectedObjects.Add(_trackedObjects);
  	}
  
  	internal static object AutoGeneratedGetMemberValue_quests(object o)
  	{
  		return ((QuestManager)o)._quests;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_trackedObjects(object o)
  	}
  
  	public QuestManager()
  	{
> 		_quests = new MBList<QuestBase>();
> 		_trackedObjects = new Dictionary<ITrackableCampaignObject, List<QuestBase>>();
> 		_currentHourlyTickQuestsToTimeout = new MBList<QuestBase>();
  		Initialize();
  	}
  
  	[LoadInitializationCallback]
  	private void OnLoad(MetaData metaData)
  	{
  		Initialize();
  	}
  
  	private void Initialize()
  	{
  		TrackedObjects = _trackedObjects.GetReadOnlyDictionary();
> 		_currentHourlyTickQuestsToTimeout = new MBList<QuestBase>();
  	}
  
> 	public override void OnQuestStarted(QuestBase quest)
  	{
  		_quests.Add(quest);
  	}
  
  	public bool IsThereActiveQuestWithType(Type type)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			if (quest.IsOngoing && (type == quest.GetType() || quest.GetType().IsSubclassOf(type)))
  			{
  				return true;
  			}
  		}
  		return false;
  	}
  
  	public bool IsQuestGiver(Hero offeringHero)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			if (quest.IsOngoing && quest.QuestGiver == offeringHero)
  			{
  				return true;
  			}
  		}
  		return false;
  	}
  
  	internal void PreAfterLoad()
  	{
  		for (int num = Quests.Count - 1; num >= 0; num--)
  		{
  			if (Quests[num] == null)
  			{
  				_quests.RemoveAt(num);
  			}
  		}
  			_quests[i].PreAfterLoadInternal();
  		}
  	}
  
> 	public override void OnGameLoaded(CampaignGameStarter campaignGameStarter)
  	{
> 		List<QuestBase> list = new List<QuestBase>();
  		for (int num = Quests.Count - 1; num >= 0; num--)
  		{
> 			QuestBase questBase = Quests[num];
> 			if (!questBase.IsFinalized)
  			{
  				bool flag = false;
  				foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  				{
> 					if (issue.Value?.IssueQuest == questBase)
  					{
  						flag = true;
  						break;
  					}
  				}
> 				if (flag || questBase.IsSpecialQuest)
  				{
> 					questBase.InitializeQuestOnLoadWithQuestManager();
> 					foreach (QuestTaskBase task in questBase.TaskList)
  					{
  						if (task.IsActive)
  						{
  							task.SetReferences();
  							task.AddTaskDialogs();
  						}
  					}
  				}
  				else
  				{
> 					list.Add(questBase);
> 					Debug.FailedAssert(string.Concat("There is not active issue for quest: ", questBase.Title, " string id: ", questBase.StringId, ". Quest will be canceled."), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\QuestManager.cs", "OnGameLoaded", 140);
  				}
  			}
  		}
> 		foreach (QuestBase item in list)
  		{
> 			item.CompleteQuestWithCancel();
  		}
  		for (int num2 = _trackedObjects.Count - 1; num2 >= 0; num2--)
  		{
  			ITrackableCampaignObject key = _trackedObjects.ElementAt(num2).Key;
  			if (!key.IsReady)
  			{
  				_trackedObjects.Remove(key);
  			}
  		}
  	}
  
  	public override void OnSessionStart(CampaignGameStarter campaignGameStarter)
  	{
  		CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
  	}
  
  	public override void HourlyTick()
  	{
  		for (int num = Quests.Count - 1; num >= 0; num--)
  		{
> 			QuestBase questBase = Quests[num];
> 			if (questBase.IsOngoing && questBase.QuestDueTime.IsPast)
  			{
> 				_currentHourlyTickQuestsToTimeout.Add(questBase);
  			}
  		}
> 		foreach (QuestBase item in _currentHourlyTickQuestsToTimeout)
  		{
  			if (!item.IsFinalized)
  			{
> 				item.CompleteQuestWithTimeOut();
  			}
  		}
  		_currentHourlyTickQuestsToTimeout.Clear();
  		for (int num2 = Quests.Count - 1; num2 >= 0; num2--)
  		{
  			Quests[num2].HourlyTickWithQuestManager();
  		}
  	}
  
  	public override void HourlyTickParty(MobileParty mobileParty)
  	{
  		for (int num = Quests.Count - 1; num >= 0; num--)
  		{
  			Quests[num].HourlyTickPartyWithQuestManager(mobileParty);
  		}
  	}
  
  	public override void DailyTick()
  				if (hero == null)
  				{
  					continue;
  				}
> 				foreach (QuestBase quest in Quests)
  				{
  					if (quest != null && quest.IsOngoing)
  					{
  						if (quest.QuestGiver == hero)
  						{
  							issueQuestFlags = (GameMenuOption.IssueQuestFlags)((int)issueQuestFlags | (quest.IsSpecialQuest ? 4 : 2));
  						}
  						else if (quest.IsTracked(hero))
  						{
  							issueQuestFlags = (GameMenuOption.IssueQuestFlags)((int)issueQuestFlags | (quest.IsSpecialQuest ? 16 : 8));
  						}
  					}
  				}
  			}
  		}
  		foreach (Location currentLocation2 in currentLocations)
  		{
  			issueQuestFlags |= IsLocationsTracked(currentLocation2);
  
  	private GameMenuOption.IssueQuestFlags IsLocationsTracked(Location location)
  	{
  		GameMenuOption.IssueQuestFlags issueQuestFlags = GameMenuOption.IssueQuestFlags.None;
> 		foreach (QuestBase quest in Quests)
  		{
  			issueQuestFlags |= quest.IsLocationTrackedByQuest(location);
  		}
  		return issueQuestFlags;
  	}
  
> 	public void OnQuestFinalized(QuestBase quest)
  	{
  		_quests.Remove(quest);
  	}
  
  	public override void OnPlayerCharacterChanged(Hero oldPlayer, Hero newPlayer, MobileParty newPlayerParty, bool isMainPartyChanged)
  	{
  		for (int num = Quests.Count - 1; num >= 0; num--)
  		{
> 			QuestBase questBase = Quests[num];
> 			if (questBase.IsOngoing && !questBase.IsSpecialQuest)
  			{
> 				questBase.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
  			}
  		}
  	}
  
  	public override void CanHaveCampaignIssues(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			if (quest.IsOngoing && quest.QuestGiver == hero)
  			{
  				result = false;
  				break;
  			}
  			quest.OnHeroCanHaveCampaignIssuesInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanHeroDie(Hero hero, KillCharacterAction.KillCharacterActionDetail causeOfDeath, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanDieInfoIsRequested(hero, causeOfDeath, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanHeroBecomePrisoner(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanBecomePrisonerInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanHeroEquipmentBeChanged(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanBeSelectedInInventoryInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanHeroLeadParty(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanLeadPartyInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanHeroMarry(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanMarryInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanMoveToSettlement(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanMoveToSettlementInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
  	public override void CanBeGovernorOrHavePartyRole(Hero hero, ref bool result)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			quest.OnHeroCanHavePartyRoleOrBeGovernorInfoIsRequested(hero, ref result);
  			if (!result)
  			{
  				break;
  			}
  		}
  	}
  
> 	public void AddTrackedObjectForQuest(ITrackableCampaignObject trackedObject, QuestBase relatedQuest)
  	{
  		if (_trackedObjects.TryGetValue(trackedObject, out var value))
  		{
  			if (!value.Contains(relatedQuest))
  			{
  				value.Add(relatedQuest);
  			}
  			else
  			{
  				Debug.FailedAssert(string.Concat(trackedObject.GetName(), " already contains quest: ", relatedQuest.Title), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\QuestManager.cs", "AddTrackedObjectForQuest", 418);
  			}
  		}
  		else
  		{
> 			_trackedObjects.Add(trackedObject, new List<QuestBase> { relatedQuest });
  		}
  	}
  
> 	public void RemoveTrackedObjectForQuest(ITrackableCampaignObject trackedObject, QuestBase relatedQuest)
  	{
  		if (_trackedObjects.TryGetValue(trackedObject, out var value))
  		{
  			if (value.Contains(relatedQuest))
  			{
  				value.Remove(relatedQuest);
  				if (value.Count == 0)
  				{
  					_trackedObjects.Remove(trackedObject);
  					Campaign.Current.VisualTrackerManager.RemoveTrackedObject(trackedObject);
  				}
  			}
  			else
  			{
  				Debug.FailedAssert(string.Concat(trackedObject.GetName(), " is not tracked by quest: ", relatedQuest.Title), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\QuestManager.cs", "RemoveTrackedObjectForQuest", 442);
  			}
  		}
  		else
  			Debug.FailedAssert(string.Concat(trackedObject.GetName(), " does not track any quests."), "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\QuestManager.cs", "RemoveTrackedObjectForQuest", 447);
  		}
  	}
  
> 	public void RemoveAllTrackedObjectsForQuest(QuestBase quest)
  	{
  		List<ITrackableCampaignObject> list = new List<ITrackableCampaignObject>();
> 		foreach (KeyValuePair<ITrackableCampaignObject, List<QuestBase>> trackedObject in TrackedObjects)
  		{
  			if (trackedObject.Value.Contains(quest))
  			{
  				list.Add(trackedObject.Key);
  			}
  		}
  		foreach (ITrackableCampaignObject item in list)
  		{
  			RemoveTrackedObjectForQuest(item, quest);
  		}
  	}
  
> 	public List<ITrackableCampaignObject> GetAllTrackedObjectsOfAQuest(QuestBase quest)
  	{
  		List<ITrackableCampaignObject> list = new List<ITrackableCampaignObject>();
> 		foreach (KeyValuePair<ITrackableCampaignObject, List<QuestBase>> trackedObject in TrackedObjects)
  		{
  			if (trackedObject.Value.Contains(quest))
  			{
  				list.Add(trackedObject.Key);
  			}
  		}
  		return list;
  	}
  
> 	public IEnumerable<QuestBase> GetQuestGiverQuests(Hero hero)
  	{
> 		foreach (QuestBase quest in Quests)
  		{
  			if (quest.IsOngoing && quest.QuestGiver == hero)
  			{
  				yield return quest;
  			}
  		}
  	}
  
> 	public static bool QuestExistInSettlementNotables(QuestBase questBase, Settlement settlement)
  	{
  		foreach (Hero notable in settlement.Notables)
  		{
> 			if (questBase.QuestGiver == notable)
  			{
  				return true;
  			}
  		}
  		return false;
  	}
  
> 	public static bool QuestExistInClan(QuestBase questBase, Clan clan)
  	{
  		foreach (Hero aliveLord in clan.AliveLords)
  		{
> 			if (questBase.QuestGiver == aliveLord)
  			{
  				return true;
  			}
  		}
  		return false;
  	}
  }

 succeeded in 1521ms:
  		}
  	}
  
> 	public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
> 			eventReceivers[i].OnTournamentFinished(winner, participants, town, prize);
  		}
  	}
  
  	public override void OnTournamentCancelled(Town town)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
  			eventReceivers[i].OnTournamentCancelled(town);
  		}
  	}
  
  	public override void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		}
  	}
  
> 	public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
> 			eventReceivers[i].OnHeroPrisonerTaken(capturer, prisoner);
  		}
  	}
  
  	public override void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification = true)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
  			eventReceivers[i].OnHeroPrisonerReleased(prisoner, party, capturerFaction, detail, showNotification);
  		}
  	}
  
  	public override void OnCharacterBecameFugitive(Hero hero, bool showNotification)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		}
  	}
  
> 	public override void OnPlayerBattleEnd(MapEvent mapEvent)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
> 			eventReceivers[i].OnPlayerBattleEnd(mapEvent);
  		}
  	}
  
  	public override void OnMissionEnded(IMission mission)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
  			eventReceivers[i].OnMissionEnded(mission);
  		}
  	}
  
  	public override void TickPartialHourlyAi(MobileParty party)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		}
  	}
  
> 	public override void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
> 			eventReceivers[i].OnQuestCompleted(quest, detail);
  		}
  	}
  
  	public override void OnQuestStarted(QuestBase quest)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)
  		{
  			eventReceivers[i].OnQuestStarted(quest);
  		}
  	}
  
  	public override void OnItemProduced(ItemObject itemObject, Settlement settlement, int count)
  	{
  		CampaignEventReceiver[] eventReceivers = _eventReceivers;
  		for (int i = 0; i < eventReceivers.Length; i++)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.QuestBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'ClearRelatedFields|RemoveAllTrackedObjects|RemoveAllMapMarkers|CampaignEvents.ClearListeners|ClearListeners\\(this\\)' -Context 3,18 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.QuestBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'protected void ClearRelatedFields|private void ClearRelatedFields|public void ClearRelatedFields|ClearRelatedFields' -Context 0,30 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.IssueBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'IssueQuest|StartIssueWithQuest|GenerateIssueQuest|CompleteIssue|OnQuestCompleted|ClearRelatedFields|Finalize' -Context 3,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.IssuesCampaignBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' 2>&1 | Select-Object -First 20" in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:10:52.110050Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
[31;1mSystem.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.Issues.IssuesCampaignBehavior in type system.[0m
[31;1m   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187[0m
[31;1m   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239[0m

 exited 1 in 846ms:
[31;1mSystem.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.Issues.IssuesCampaignBehavior in type system.[0m
[31;1m   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187[0m
[31;1m   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318[0m
[31;1m   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239[0m

 succeeded in 1329ms:
> 		ClearRelatedFields();
  		OfferDialogFlow = null;
  		DiscussDialogFlow = null;
  		QuestCharacterDialogFlow = null;
  		Campaign.Current.QuestManager.OnQuestFinalized(this);
  	}
  
  	private void AfterFinalize()
  	{
  		RemoveAllTrackedObjects();
  		RemoveAllMapMarkers();
  	}
  
  	protected void InitializeQuestOnCreation()
  	{
  		AddDialogs();
  	}
  
  	public void InitializeQuestOnLoadWithQuestManager()
  	{
  		RegisterEvents();
  		InitializeQuestOnGameLoad();
  		AddDialogs();
  	}
  
  	internal void HourlyTickPartyWithQuestManager(MobileParty mobileParty)
  	{
  		HourlyTickParty(mobileParty);
  	}
  
  	internal void HourlyTickWithQuestManager()
> 	private void ClearRelatedFields()
  	{
  		CampaignEventDispatcher.Instance.RemoveListeners(this);
  		foreach (QuestTaskBase task in _taskList)
  		{
  			CampaignEventDispatcher.Instance.RemoveListeners(task);
  		}
  		Campaign.Current.ConversationManager.RemoveRelatedLines(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenus(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenuOptions(this);
  	}
  
  	private void RemoveAllTrackedObjects()
  	{
  		Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(this);
  	}
  
  	private void RemoveAllMapMarkers()
  	{
  		Campaign.Current.MapMarkerManager.RemoveAllMapMarkersByQuestId(base.StringId);
  	}
  
  	public void AddTrackedObject(ITrackableCampaignObject trackedObject)
  	{
  		Campaign.Current.QuestManager.AddTrackedObjectForQuest(trackedObject, this);
  		if (IsTrackEnabled)
  		{
  			Campaign.Current.VisualTrackerManager.RegisterObject(trackedObject);
  		}
  	}
  

 succeeded in 1343ms:
  		}
  		_questState = QuestStates.Finalized;
  		OnFinalize();
> 		ClearRelatedFields();
  		OfferDialogFlow = null;
  		DiscussDialogFlow = null;
  		QuestCharacterDialogFlow = null;
  		Campaign.Current.QuestManager.OnQuestFinalized(this);
  	}
  
  	private void AfterFinalize()
  	{
> 		RemoveAllTrackedObjects();
> 		RemoveAllMapMarkers();
  	}
  
  	protected void InitializeQuestOnCreation()
  	{
  		AddDialogs();
  	}
  
  	public void InitializeQuestOnLoadWithQuestManager()
  	{
  		RegisterEvents();
  		InitializeQuestOnGameLoad();
  		AddDialogs();
  	}
  
  	internal void HourlyTickPartyWithQuestManager(MobileParty mobileParty)
  	{
  		HourlyTickParty(mobileParty);
  	}
  		}
  	}
  
> 	private void ClearRelatedFields()
  	{
  		CampaignEventDispatcher.Instance.RemoveListeners(this);
  		foreach (QuestTaskBase task in _taskList)
  		{
  			CampaignEventDispatcher.Instance.RemoveListeners(task);
  		}
  		Campaign.Current.ConversationManager.RemoveRelatedLines(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenus(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenuOptions(this);
  	}
  
> 	private void RemoveAllTrackedObjects()
  	{
> 		Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(this);
  	}
  
> 	private void RemoveAllMapMarkers()
  	{
> 		Campaign.Current.MapMarkerManager.RemoveAllMapMarkersByQuestId(base.StringId);
  	}
  
  	public void AddTrackedObject(ITrackableCampaignObject trackedObject)
  	{
  		Campaign.Current.QuestManager.AddTrackedObjectForQuest(trackedObject, this);
  		if (IsTrackEnabled)
  		{
  			Campaign.Current.VisualTrackerManager.RegisterObject(trackedObject);
  		}
  	}
  
  	public void RemoveTrackedObject(ITrackableCampaignObject trackedObject)
  	{
  		Campaign.Current.QuestManager.RemoveTrackedObjectForQuest(trackedObject, this);
  	}
  
  	public bool IsTracked(ITrackableCampaignObject o)
  	{

 succeeded in 1472ms:
  	public enum IssueUpdateDetails
  	{
  		None,
> 		PlayerStartedIssueQuestClassicSolution,
  		PlayerSentTroopsToQuest,
  		SentTroopsFinishedQuest,
  		SentTroopsFailedQuest,
  		IssueFinishedWithSuccess,
  		IssueFinishedWithBetrayal,
  		IssueFinishedByAILord,
  		IssueFail,
  		IssueCancel,
  		IssueTimedOut
  	}
  
  	public enum IssueFrequency
  	{
  		VeryCommon,
  		Common,
  		Rare
  	}
  
  	public const int IssueRelatedConversationPriority = 125;
  
  	[SaveableField(27)]
  	private float _totalTroopXpAmount;
  	[SaveableField(31)]
  	private readonly List<ITrackableCampaignObject> _trackedObjects = new List<ITrackableCampaignObject>();
  
> 	protected virtual bool IssueQuestCanBeDuplicated => false;
  
  	public virtual int RelationshipChangeWithIssueOwner { get; protected set; }
  
  	public abstract TextObject IssueBriefByIssueGiver { get; }
  
  	public abstract TextObject IssueAcceptByPlayer { get; }
  
  	public virtual TextObject IssuePlayerResponseAfterLordExplanation => new TextObject("{=sMCN7eCp}Is there any other way to solve this problem?");
  
  	public virtual TextObject IssuePlayerResponseAfterAlternativeExplanation => new TextObject("{=yrPEqZEa}Any other way?");
  
> 	public abstract TextObject IssueQuestSolutionExplanationByIssueGiver { get; }
  
  	public virtual TextObject IssueAlternativeSolutionExplanationByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionExplanationByIssueGiver => TextObject.GetEmpty();
  
> 	public abstract TextObject IssueQuestSolutionAcceptByPlayer { get; }
  
  	public virtual TextObject IssueAlternativeSolutionAcceptByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueAlternativeSolutionResponseByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionAcceptByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionResponseByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferBriefByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferExplanationByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferAcceptByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferDeclineByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferAcceptResponseByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferDeclineResponseByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueAsRumorInSettlement => TextObject.GetEmpty();
  	public abstract TextObject Title { get; }
  
  	[SaveableProperty(15)]
> 	public QuestBase IssueQuest { get; private set; }
  
  	public Settlement IssueSettlement
  	{
  		get
  		{
  			if (!_issueOwner.IsNotable)
  			{
  				return null;
  			}
  			return IssueOwner.CurrentSettlement;
  		}
  	}
  
  	public abstract TextObject Description { get; }
  
  	[SaveableProperty(22)]
  	public bool IsTriedToSolveBefore { get; private set; }
  
  	public bool IsOngoingWithoutQuest => _issueState == IssueState.Ongoing;
  
  	public bool IsSolvingWithQuest => _issueState == IssueState.SolvingWithQuestSolution;
  
  		collectedObjects.Add(_trackedObjects);
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(AlternativeSolutionReturnTimeForTroops, collectedObjects);
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(AlternativeSolutionIssueEffectClearTime, collectedObjects);
> 		collectedObjects.Add(IssueQuest);
  	}
  
  	internal static object AutoGeneratedGetMemberValueAlternativeSolutionReturnTimeForTroops(object o)
  	{
  		return ((IssueBase)o).AlternativeSolutionReturnTimeForTroops;
  	}
  
  	internal static object AutoGeneratedGetMemberValueAlternativeSolutionIssueEffectClearTime(object o)
  	{
  		return ((IssueBase)o).AlternativeSolutionIssueEffectClearTime;
  	}
  
> 	internal static object AutoGeneratedGetMemberValueIssueQuest(object o)
  	{
> 		return ((IssueBase)o).IssueQuest;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIsTriedToSolveBefore(object o)
  	{
  		return ((IssueBase)o).IsTriedToSolveBefore;
  	}
  
  	internal static object AutoGeneratedGetMemberValueAlternativeSolutionSentTroops(object o)
  	{
  		return ((IssueBase)o).AlternativeSolutionSentTroops;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIssueDueTime(object o)
  	{
  		return ((IssueBase)o).IssueDueTime;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIssueCreationTime(object o)
  	{
  		return ((IssueBase)o).IssueCreationTime;
  	}
  
  
  	protected abstract void HourlyTick();
  
> 	protected abstract QuestBase GenerateIssueQuest(string questId);
  
  	public abstract IssueFrequency GetFrequency();
  
  	protected abstract bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold);
  
  	public abstract bool IssueStayAliveConditions();
  
> 	protected abstract void CompleteIssueWithTimedOutConsequences();
  
  	protected virtual void AfterIssueCreation()
  	{
  	}
  
  	public virtual bool CanBeCompletedByAI()
  	{
  		return true;
  	}
  
> 	protected virtual void OnIssueFinalized()
  	{
  	}
  
  	public virtual void OnHeroCanHaveCampaignIssuesInfoIsRequested(Hero hero, ref bool result)
  	{
  	}
  
  	public virtual void OnHeroCanLeadPartyInfoIsRequested(Hero hero, ref bool result)
  	{
  	}
  
  	public virtual void OnHeroCanHavePartyRoleOrBeGovernorInfoIsRequested(Hero hero, ref bool result)
  	{
  	}
  
  	public virtual void OnHeroCanDieInfoIsRequested(Hero hero, KillCharacterAction.KillCharacterActionDetail causeOfDeath, ref bool result)
  	{
  	}
  
  	public virtual void OnHeroCanBecomePrisonerInfoIsRequested(Hero hero, ref bool result)
  	{
  	}
  	{
  	}
  
> 	public bool StartIssueWithQuest()
  	{
  		_issueDifficultyMultiplier = Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier();
  		_issueState = IssueState.SolvingWithQuestSolution;
> 		IssueQuest = GenerateIssueQuest(base.StringId + "_quest");
  		IsTriedToSolveBefore = true;
  		IssueDueTime = CampaignTime.Never;
> 		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution, Hero.MainHero);
  		return true;
  	}
  
  	public void StartIssueWithAlternativeSolution()
  	{
  		_issueDifficultyMultiplier = Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier();
  		IssueModel issueModel = Campaign.Current.Models.IssueModel;
  		_failureChance = (AlternativeSolutionHasFailureRisk ? issueModel.GetFailureRiskForHero(AlternativeSolutionHero, this) : 0f);
  		if (AlternativeSolutionHasCasualties)
  		{
  			(int, int) causalityForHero = issueModel.GetCausalityForHero(AlternativeSolutionHero, this);
  			_alternativeSolutionCasualtyCount = MBRandom.RandomInt(causalityForHero.Item1, causalityForHero.Item2 + 1);
  		}
  		else
  		{
  			_alternativeSolutionCasualtyCount = 0;
  		}
  		_companionRewardSkill = issueModel.GetIssueAlternativeSolutionSkill(AlternativeSolutionHero, this).Item1;
  		_issueState = IssueState.SolvingWithAlternativeSolution;
  		IsTriedToSolveBefore = true;
  		_totalTroopXpAmount = 1000f + 500f * IssueDifficultyMultiplier;
  		AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(GetTotalAlternativeSolutionDurationInDays());
  		AddLog(new JournalLog(CampaignTime.Now, AlternativeSolutionEndLogDefault));
  	}
  
> 	public void IssueFinalized()
  	{
> 		IssueQuest = null;
  		CampaignEventDispatcher.Instance.RemoveListeners(this);
  		Campaign.Current.IssueManager.DeactivateIssue(this);
  		_areIssueEffectsResolved = true;
  		AlternativeSolutionSentTroops.Clear();
  		RemoveAllTrackedObjects();
> 		OnIssueFinalized();
  	}
  
> 	public void CompleteIssueWithQuest()
  	{
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueFinishedWithSuccess, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithTimedOut()
  	{
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueTimedOut, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithStayAliveConditionsFailed()
  	{
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueCancel, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithBetrayal()
  	{
> 		if (IssueQuest != null && IssueQuest.IsOngoing)
  		{
> 			IssueQuest.CompleteQuestWithBetrayal();
  		}
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueFinishedWithBetrayal, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithFail(TextObject log = null)
  	{
> 		if (IssueQuest != null && IssueQuest.IsOngoing)
  		{
> 			IssueQuest.CompleteQuestWithFail(log);
  		}
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueFail, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithCancel(TextObject log = null)
  	{
> 		if (IssueQuest != null)
  		{
> 			if (IssueQuest.IsOngoing)
  			{
> 				IssueQuest.CompleteQuestWithCancel(log);
  			}
  		}
  		else if (IsSolvingWithAlternative)
  		{
  			AddLog(new JournalLog(CampaignTime.Now, new TextObject("{=V5Za6d4h}Your troops have returned from their mission.")));
  			Campaign.Current.IssueManager.TryToMakeTroopsReturn(this);
  		}
  		else if (IsSolvingWithLordSolution && log != null)
  		{
  			AddLog(new JournalLog(CampaignTime.Now, log));
  		}
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueCancel, IsTriedToSolveBefore ? Hero.MainHero : null);
> 		IssueFinalized();
  	}
  
> 	public void CompleteIssueWithAiLord(Hero issueSolver)
  	{
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueFinishedByAILord, issueSolver);
> 		IssueFinalized();
  	}
  
  	private void AlternativeSolutionEndWithSuccess()
  	{
  		if (AlternativeSolutionHero == null)
  		{
  			Debug.Print("AlternativeSolutionHero is null for " + base.StringId);
  			Debug.Print("AlternativeSolutionSentTroops:");
  			foreach (TroopRosterElement item in AlternativeSolutionSentTroops.GetTroopRoster())
  			{
  				Debug.Print("troop id: " + item.Character.StringId + " count:" + item.Number);
  			}
  		}
  		int totalManCount = AlternativeSolutionSentTroops.TotalManCount;
  		AlternativeSolutionSentTroops.RemoveNumberOfNonHeroTroopsRandomly(_alternativeSolutionCasualtyCount);
  		float num = 0.5f;
  		float num2 = 1.2f - (float)AlternativeSolutionBaseNeededMenCount / (float)AlternativeSolutionSentTroops.TotalManCount;
  		foreach (FlattenedTroopRosterElement item2 in AlternativeSolutionSentTroops.ToFlattenedRoster())
  		{
  			if (AlternativeSolutionBaseNeededMenCount < AlternativeSolutionSentTroops.TotalManCount)
  			{
  				num /= num2 * 0.9f + MBRandom.RandomFloat * 0.1f;
  		}
  		else
  		{
> 			CompleteIssueWithLordSolutionWithRefuseCounterOffer();
  		}
  	}
  
> 	public void CompleteIssueWithAlternativeSolution()
  	{
  		if (MBRandom.RandomFloat > _failureChance)
  		{
  			AlternativeSolutionEndWithSuccessConsequence();
  			AlternativeSolutionEndWithSuccess();
  		}
  		else
  		{
  			AlternativeSolutionEndWithFailureConsequence();
  			AlternativeSolutionEndWithFail();
  		}
  		Campaign.Current.IssueManager.TryToMakeTroopsReturn(this);
> 		IssueFinalized();
  	}
  
  	private void AlternativeSolutionEndWithFail()
  	{
  		int totalManCount = AlternativeSolutionSentTroops.TotalManCount;
  		if (AlternativeSolutionHasCasualties)
  		{
  			AlternativeSolutionSentTroops.RemoveNumberOfNonHeroTroopsRandomly(_alternativeSolutionCasualtyCount);
  			AlternativeSolutionHero.MakeWounded();
  		}
  		TextObject textObject;
  		if (AlternativeSolutionHasCasualties && _alternativeSolutionCasualtyCount > 0)
  		{
  			textObject = new TextObject("{=yxwuGcDo}{COMPANION.LINK} has failed to resolve the matter. Out of {NUMBER1} troops you sent {NUMBER2} troops came back safe and sound.");
  			textObject.SetTextVariable("NUMBER1", totalManCount);
  			textObject.SetTextVariable("NUMBER2", totalManCount - _alternativeSolutionCasualtyCount);
  		}
  		else
  		{
  			textObject = new TextObject("{=k6fpAw92}{COMPANION.LINK} has failed to resolve the matter. {NUMBER} troops came back safe and sound.");
  			textObject.SetTextVariable("NUMBER", totalManCount);
  		}
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.SentTroopsFailedQuest, Hero.MainHero);
  	}
  
> 	public void CompleteIssueWithLordSolutionWithRefuseCounterOffer()
  	{
  		if (!TextObject.IsNullOrEmpty(LordSolutionCounterOfferRefuseLog))
  		{
  			AddLog(new JournalLog(CampaignTime.Now, LordSolutionCounterOfferRefuseLog));
  		}
  		ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -NeededInfluenceForLordSolution);
  		if (RewardGold > 0)
  		{
  			GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold);
  		}
  		LordSolutionConsequenceWithRefuseCounterOffer();
> 		IssueFinalized();
  		CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueUpdateDetails.IssueFinishedWithSuccess, Hero.MainHero);
  	}
  
> 	public void CompleteIssueWithLordSolutionWithAcceptCounterOffer()
  	{
  		if (!TextObject.IsNullOrEmpty(LordSolutionCounterOfferAcceptLog))
  		{
  			AddLog(new JournalLog(CampaignTime.Now, LordSolutionCounterOfferAcceptLog));
  		}
  		LordSolutionConsequenceWithAcceptCounterOffer();
> 		CompleteIssueWithBetrayal();
  	}
  
  	internal bool CheckPreconditions(Hero issueGiver, out TextObject explanation)
  	{
  		explanation = new TextObject("{=!}{EXPLANATION}");
  		if (!IssueStayAliveConditions() && IsOngoingWithoutQuest)
  		{
> 			CompleteIssueWithCancel();
  			return false;
  		}
  		PreconditionFlags flag;
  		Hero relationHero;
  		SkillObject skill;
  		int requiredGold;
  		bool result = CanPlayerTakeQuestConditions(issueGiver, out flag, out relationHero, out skill, out requiredGold);
  		bool flag2 = false;
> 		if (!IssueQuestCanBeDuplicated)
  		{
  			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  			{
  				IssueBase value = issue.Value;
  				if ((value.IsSolvingWithQuest || value.IsSolvingWithAlternative) && value.GetType() == GetType())
  				{
  					flag2 = true;
  					result = false;
  				}
  			}
  		}
  		if ((flag & PreconditionFlags.AtWar) == PreconditionFlags.AtWar)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=21dlZJt6}I don't wish to speak about that. As you know, our factions are at war."));
  		}
  		else if (flag2)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=HvY7wjHt}I don't think you can help me. I think you may have other, similar commitments that could interfere."));
  		}
  		else if ((flag & PreconditionFlags.NotInSameFaction) == PreconditionFlags.NotInSameFaction)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=rBPI2dvX}I don't need the service of strangers. I work only with lords of the realm and loyal mercenaries.[ib:closed][if:convo_grave]"));

codex
One important lifecycle check: `QuestBase.CompleteQuestWithSuccess()` finalizes only the quest. I’m tracing the vanilla issue listener that converts quest completion into `IssueBase.CompleteIssueWithQuest()` so the notable issue is removed and cooldown is applied.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnQuestCompleted|CompleteIssueWithQuest|IssueUpdated|OnIssueUpdated|IssueFinishedWithSuccess|IssueFinalized|QuestCompleteDetails' -Context 4,26 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.IssueManager 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnQuestCompleted|CompleteIssueWithQuest|DeactivateIssue|OnIssueUpdated|IssueFinishedWithSuccess|IssueCancel' -Context 4,24 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'CompleteIssueWithQuest\\(' -SimpleMatch | Select-Object -First 80 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\IssuesCampaignBehavior.cs' -Pattern 'OnQuestCompleted|CompleteIssueWithQuest|OnIssueUpdated' -Context 4,24 | ForEach-Object { \""'$($_.LineNumber):$($_.Line)"; $_.Context.PostContext | ForEach-Object { $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 631ms:
53:		CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, OnIssueUpdated);
		CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
		CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
		CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnSettlementDailyTick);
	}

	private void OnSettlementDailyTick(Settlement settlement)
	{
		float num = 0f;
		for (int i = 0; i < settlement.HeroesWithoutParty.Count; i++)
		{
			if (settlement.HeroesWithoutParty[i].Issue != null)
			{
				num += 1f;
			}
		}
		int num2 = (settlement.IsTown ? 1 : 1);
		int num3 = (settlement.IsTown ? 3 : 2);
		if (!(num < (float)num3) || (!(num < (float)num2) && !(MBRandom.RandomFloat < GetIssueGenerationChance(num, num3))))
		{
			return;
		}
		int num4 = 0;
		foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
		{
396:	private void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver = null)
	{
		if (details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess && issueSolver != null && issueSolver.GetPerkValue(DefaultPerks.Charm.Oratory))
		{
			GainRenownAction.Apply(issueSolver, TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus));
			GainKingdomInfluenceAction.ApplyForDefault(issueSolver, TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus));
		}
		if ((details == IssueBase.IssueUpdateDetails.IssueFail || details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess || details == IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal || details == IssueBase.IssueUpdateDetails.IssueTimedOut || details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest) && issueSolver != null && issue.IssueOwner != null)
		{
			int num = (issue.IsSolvingWithQuest ? issue.IssueQuest.RelationshipChangeWithQuestGiver : issue.RelationshipChangeWithIssueOwner);
			if (num > 0)
			{
				if (issueSolver.GetPerkValue(DefaultPerks.Trade.DistributedGoods) && issue.IssueOwner.IsArtisan)
				{
					num *= (int)DefaultPerks.Trade.DistributedGoods.PrimaryBonus;
				}
				if (issueSolver.GetPerkValue(DefaultPerks.Trade.LocalConnection) && issue.IssueOwner.IsMerchant)
				{
					num *= (int)DefaultPerks.Trade.LocalConnection.PrimaryBonus;
				}
				ChangeRelationAction.ApplyPlayerRelation(issue.IsSolvingWithQuest ? issue.IssueQuest.QuestGiver : issue.IssueOwner, num);
			}
			else if (num < 0)
			{
				ChangeRelationAction.ApplyPlayerRelation(issue.IsSolvingWithQuest ? issue.IssueQuest.QuestGiver : issue.IssueOwner, num);

 succeeded in 743ms:
 succeeded in 1507ms:
  	public const string IssueClassicQuestStartToken = "issue_classic_quest_start";
  
  	public const string IssueDiscussAlternativeSolution = "issue_discuss_alternative_solution";
  
> 	private const float IssueCancelChance = 0.2f;
  
  	public IEnumerable<Hero> IssueSolvingCompanionList
  	{
  		get
  		{
  			foreach (KeyValuePair<Hero, IssueBase> issue in Issues)
  			{
  				if (issue.Value.IsSolvingWithAlternative)
  				{
  					yield return issue.Value.AlternativeSolutionHero;
  				}
  			}
  		}
  	}
  
  	internal static void AutoGeneratedStaticCollectObjectsIssueManager(object o, List<object> collectedObjects)
  	{
  		((IssueManager)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		collectedObjects.Add(_issues);
  		collectedObjects.Add(_issuesCoolDownData);
  		Issues[issueOwner].CompleteIssueWithStayAliveConditionsFailed();
  		return false;
  	}
  
> 	public void DeactivateIssue(IssueBase issue)
  	{
  		if (issue.IssueQuest != null)
  		{
  			issue.IssueQuest?.CompleteQuestWithCancel();
  			return;
  		}
  		issue.IssueOwner.OnIssueDeactivatedForHero();
  		Campaign.Current.ConversationManager.RemoveRelatedLines(issue);
  		if (Issues.ContainsKey(issue.IssueOwner))
  		{
  			_issues.Remove(issue.IssueOwner);
  		}
  	}
  
  	public void ChangeIssueOwner(IssueBase issue, Hero newOwner)
  	{
  		Hero issueOwner = issue.IssueOwner;
  		issueOwner.OnIssueDeactivatedForHero();
  		newOwner.OnIssueCreatedForHero(issue);
  		issue.IssueOwner = newOwner;
  		_issues.Remove(issueOwner);
  		_issues.Add(newOwner, issue);
  		CampaignEventDispatcher.Instance.OnIssueOwnerChanged(issue, issueOwner);
  	}
  		}
  		return issueQuestFlags;
  	}
  
> 	public override void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
  	{
  		foreach (KeyValuePair<Hero, IssueBase> issue in Issues)
  		{
  			if (issue.Value.IssueQuest == quest)
  			{
  				switch (detail)
  				{
  				case QuestBase.QuestCompleteDetails.Success:
> 					issue.Value.CompleteIssueWithQuest();
  					break;
  				case QuestBase.QuestCompleteDetails.Cancel:
  					issue.Value.CompleteIssueWithCancel();
  					break;
  				case QuestBase.QuestCompleteDetails.Fail:
  					issue.Value.CompleteIssueWithFail();
  					break;
  				case QuestBase.QuestCompleteDetails.Timeout:
  					issue.Value.CompleteIssueWithTimedOut();
  					break;
  				case QuestBase.QuestCompleteDetails.FailWithBetrayal:
  					issue.Value.CompleteIssueWithBetrayal();
  					break;
  				default:
> 					issue.Value.CompleteIssueWithQuest();
  					break;
  				}
  				break;
  			}
  		}
  	}
  
  	public override void OnHeroUnregistered(Hero hero)
  	{
  		_issues.Remove(hero);
  	}
  
  	public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
  	{
  		if (party != MobileParty.MainParty)
  		{
  			return;
  		}
  		foreach (Hero item in GetHeroesThatHaveIssueForSettlement(Settlement.CurrentSettlement))
  		{
  			if (item.Issue.IsOngoingWithoutQuest && !item.Issue.IssueStayAliveConditions())
  			{
  				item.Issue.CompleteIssueWithStayAliveConditionsFailed();
  			}

 succeeded in 1508ms:
  	{
  		CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, DailyTickClan);
  		CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
  		CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUpEnd);
> 		CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, OnIssueUpdated);
  		CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
  		CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
  		CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnSettlementDailyTick);
  	}
  
  	private void OnSettlementDailyTick(Settlement settlement)
  	{
  		float num = 0f;
  		for (int i = 0; i < settlement.HeroesWithoutParty.Count; i++)
  		{
  			if (settlement.HeroesWithoutParty[i].Issue != null)
  			{
  				num += 1f;
  			}
  		}
  		int num2 = (settlement.IsTown ? 1 : 1);
  		int num3 = (settlement.IsTown ? 3 : 2);
  		if (!(num < (float)num3) || (!(num < (float)num2) && !(MBRandom.RandomFloat < GetIssueGenerationChance(num, num3))))
  		{
  			return;
  		}
  		int num4 = 0;
  		foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  		{
  			if (!issue.Value.IsTriedToSolveBefore)
  			{
  			}
  		}
  	}
  
> 	private void OnIssueUpdated(IssueBase issue, IssueBase.IssueUpdateDetails details, Hero issueSolver = null)
  	{
> 		if (details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess && issueSolver != null && issueSolver.GetPerkValue(DefaultPerks.Charm.Oratory))
  		{
  			GainRenownAction.Apply(issueSolver, TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus));
  			GainKingdomInfluenceAction.ApplyForDefault(issueSolver, TaleWorlds.Library.MathF.Round(DefaultPerks.Charm.Oratory.PrimaryBonus));
  		}
> 		if ((details == IssueBase.IssueUpdateDetails.IssueFail || details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess || details == IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal || details == IssueBase.IssueUpdateDetails.IssueTimedOut || details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest) && issueSolver != null && issue.IssueOwner != null)
  		{
  			int num = (issue.IsSolvingWithQuest ? issue.IssueQuest.RelationshipChangeWithQuestGiver : issue.RelationshipChangeWithIssueOwner);
  			if (num > 0)
  			{
  				if (issueSolver.GetPerkValue(DefaultPerks.Trade.DistributedGoods) && issue.IssueOwner.IsArtisan)
  				{
  					num *= (int)DefaultPerks.Trade.DistributedGoods.PrimaryBonus;
  				}
  				if (issueSolver.GetPerkValue(DefaultPerks.Trade.LocalConnection) && issue.IssueOwner.IsMerchant)
  				{
  					num *= (int)DefaultPerks.Trade.LocalConnection.PrimaryBonus;
  				}
  				ChangeRelationAction.ApplyPlayerRelation(issue.IsSolvingWithQuest ? issue.IssueQuest.QuestGiver : issue.IssueOwner, num);
  			}
  			else if (num < 0)
  			{
  				ChangeRelationAction.ApplyPlayerRelation(issue.IsSolvingWithQuest ? issue.IssueQuest.QuestGiver : issue.IssueOwner, num);
  			}
  		}
> 		if (details == IssueBase.IssueUpdateDetails.IssueCancel || details == IssueBase.IssueUpdateDetails.IssueFail || details == IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess || details == IssueBase.IssueUpdateDetails.IssueFinishedWithBetrayal || details == IssueBase.IssueUpdateDetails.IssueTimedOut || details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest || details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest || details == IssueBase.IssueUpdateDetails.IssueFinishedByAILord)
  		{
  			Campaign.Current.IssueManager.AddIssueCoolDownData(issue.GetType(), new HeroRelatedIssueCoolDownData(issue.IssueOwner, CampaignTime.DaysFromNow(Campaign.Current.Models.IssueModel.IssueOwnerCoolDownInDays)));
  		}
  	}
  
  	public override void SyncData(IDataStore dataStore)
  	{
  	}
  
  	private void OnSessionLaunched(CampaignGameStarter starter)
  	{
  		List<Settlement> settlements = Settlement.All.Where((Settlement x) => x.IsTown || x.IsVillage).ToList();
  		DeterministicShuffle(settlements);
  		AddDialogues(starter);
  	}
  
  	private void DeterministicShuffle(List<Settlement> settlements)
  	{
  		Random random = new Random(53);
  		for (int i = 0; i < settlements.Count; i++)
  		{
  			int index = random.Next() % settlements.Count;
  			Settlement value = settlements[i];
  			settlements[i] = settlements[index];
  			settlements[index] = value;
  		}

codex
The issue lifecycle is coherent: Quest completion dispatches to `IssueManager.OnQuestCompleted`, which calls `IssueBase.CompleteIssueWithQuest()` and then deactivates the issue. I’m now checking whether the combat counters are semantically equivalent to vanilla issue counters or over-broad.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEnd|WinningSide|PlayerSide|IsPlayerMapEvent|IsPlayerSimulation|CompleteQuestWithSuccess|StartQuest' -Context 5,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEnd|WinningSide|PlayerSide|IsPlayerMapEvent|IsPlayerSimulation|CompleteQuestWithSuccess|StartQuest' -Context 5,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.LandlordNeedsAccessToVillageCommonsIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEnd|WinningSide|PlayerSide|IsPlayerMapEvent|IsPlayerSimulation|CompleteQuestWithSuccess|StartQuest' -Context 5,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.LordWantsRivalCapturedIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEventEnded|OnPrisonerTaken|HeroPrisonerTaken|WinningSide|PlayerSide|IsPlayerMapEvent|IsPlayerSimulation|MapFaction|IsAtWarWith|capturer|CompleteQuestWithSuccess' -Context 5,24 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1451ms:
  			return IssueFrequency.Rare;
  		}
  
  		public override bool IssueStayAliveConditions()
  		{
> 			if (!_targetHero.IsDead && !base.IssueOwner.IsDead && !_targetHero.IsPrisoner && _targetHero.PartyBelongedTo != null && base.IssueOwner.MapFaction != _targetHero.MapFaction)
  			{
  				return base.IssueOwner.Clan != Clan.PlayerClan;
  			}
  			return false;
  		}
  
  		protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flags, out Hero relationHero, out SkillObject skill, out int requiredGold)
  		{
  			relationHero = null;
  			requiredGold = 0;
  			flags = PreconditionFlags.None;
  			if (issueGiver.GetRelationWithPlayer() < -10f)
  			{
  				flags |= PreconditionFlags.Relation;
  				relationHero = issueGiver;
  			}
> 			if (issueGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  			{
  				flags |= PreconditionFlags.AtWar;
  			}
  			if (Clan.PlayerClan.Tier < 2)
  			{
  				flags |= PreconditionFlags.ClanTier;
  			}
  			if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < 50)
  			{
  				flags |= PreconditionFlags.NotEnoughTroops;
  			}
> 			if (issueGiver.MapFaction != Hero.MainHero.MapFaction)
  			{
  				flags |= PreconditionFlags.NotInSameFaction;
  			}
  			skill = null;
  			return flags == PreconditionFlags.None;
  		}
  
  		protected override void OnGameLoad()
  		{
  		}
  
  		protected override void HourlyTick()
  		{
  		}
  
  		protected override QuestBase GenerateIssueQuest(string questId)
  		{
  			return new LordWantsRivalCapturedIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(300f), RewardGold, _targetHero);
  		}
  
  		protected override void CompleteIssueWithTimedOutConsequences()
  		{
  		}
  	}
  		private TextObject WarDeclaredQuestLog
  		{
  			get
  			{
  				TextObject textObject = new TextObject("{=cKz1cyuM}Your clan is now at war with {QUEST_GIVER_SETTLEMENT_FACTION}. Quest is canceled.");
> 				textObject.SetTextVariable("QUEST_GIVER_SETTLEMENT_FACTION", base.QuestGiver.MapFaction.Name);
  				return textObject;
  			}
  		}
  
  		private TextObject TimeOutLogText => new TextObject("{=maXW8tps}You failed to capture and deliver the prisoner in time.");
  
  		internal static void AutoGeneratedStaticCollectObjectsLordWantsRivalCapturedIssueQuest(object o, List<object> collectedObjects)
  		{
  			((LordWantsRivalCapturedIssueQuest)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  		{
  			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  			collectedObjects.Add(_targetHero);
  		}
  
  		internal static object AutoGeneratedGetMemberValue_targetHero(object o)
  		{
  			return ((LordWantsRivalCapturedIssueQuest)o)._targetHero;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_firstCounterOfferMade(object o)
  		{
  			TextObject textObject = new TextObject("{=Mn4bI9Jg}What are you doing? Are you another one of {QUEST_GIVER.NAME}'s lackeys? Do you think you can hunt me like an animal.[if:convo_confused_normal][ib:closed]");
  			StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject);
  			TextObject textObject2 = new TextObject("{=qOhDHFyH}Are you another one of {QUEST_GIVER.NAME}'s lackeys? Did {?QUEST_GIVER.GENDER}she{?}he{\\?} send you to hunt me like an animal? Well, I think you've found you've caught not a hare but a lion.[if:convo_confused_annoyed][ib:aggressive]");
  			StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject2);
  			TextObject textObject3 = new TextObject("{=SyZ75CJ7}I don’t think so. I’m a loyal subject of {?TARGET_HERO_RULER.GENDER}Queen{?}King{\\?} {TARGET_HERO_RULER.NAME}. You can’t touch me. Now move along!");
> 			StringHelpers.SetCharacterProperties("TARGET_HERO_RULER", _targetHero.MapFaction.Leader.CharacterObject, textObject3);
  			return DialogFlow.CreateDialogFlow("start", 125).BeginNpcOptions().NpcOption(textObject2, target_hero_encounter_agressive_condition)
  				.NpcLine(new TextObject("{=TzvGs0v1}Well I guess it's too late to change your mind now, I decided to make an example of you.[if:convo_furious][ib:warrior]"))
  				.CloseDialog()
  				.NpcOption(textObject, target_hero_encounter_default_condition)
  				.BeginPlayerOptions()
  				.PlayerOption(new TextObject("{=WoAgHDrN}I'm bringing you in, one way or the other."))
  				.BeginNpcOptions()
> 				.NpcOption(new TextObject("{=hzCTbbLP}You think you've caught a hare but instead you've caught a lion.[if:convo_furious][ib:warrior]"), () => _targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  				.Consequence(player_target_hero_encounter_consequence)
  				.CloseDialog()
> 				.NpcOption(textObject3, () => !_targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  				.Consequence(delegate
  				{
  					PlayerEncounter.LeaveEncounter = true;
  				})
  				.CloseDialog()
  				.EndNpcOptions()
  				.CloseDialog()
  				.PlayerOption(new TextObject("{=pZm7HRDG}Don't worry I have no intention to attack."))
  				.NpcLine(new TextObject("{=eJuvk4om}Wise decision.[ib:closed][if:convo_bared_teeth]"))
  				.Consequence(delegate
  				{
  					PlayerEncounter.LeaveEncounter = true;
  				})
  				.CloseDialog()
  				.EndPlayerOptions()
  				.EndNpcOptions();
  		}
  
  		private DialogFlow GetTargetHeroCounterOffer1DialogFlow()
  		{
  			TextObject textObject = new TextObject("{=dYa9oVSP}Fate is cruel. So, I am your prisoner. But you don't need to do {QUEST_GIVER.NAME}'s dirty work. For the sake of your honor, {?PLAYER.GENDER}lady{?}sir{\\?}, listen to me![if:convo_nervous2][ib:nervous]");
  			StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject);
  			StringHelpers.SetCharacterProperties("PLAYER", CharacterObject.PlayerCharacter, textObject);
  			TextObject npcText = new TextObject("{=zliqo1Y8}{?PLAYER.GENDER}Madam{?}Sir{\\?}, I will pay double whatever {?QUEST_GIVER.GENDER}she{?}he{\\?} promised to release me. Believe me, you don't want a reputation as {?QUEST_GIVER.GENDER}her{?}his{\\?} kidnapper-for-hire.[if:convo_grave][ib:normal2]");
  
  		private bool target_hero_encounter_agressive_condition()
  		{
  			if (common_first_dialogue_condition())
  			{
> 				return _targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction);
  			}
  			return false;
  		}
  
  		private bool target_hero_encounter_default_condition()
  		{
  			if (common_first_dialogue_condition())
  			{
> 				return !DiplomacyHelper.IsSameFactionAndNotEliminated(_targetHero.MapFaction, Hero.MainHero.MapFaction);
  			}
  			return false;
  		}
  
  		private bool after_taken_prisoner_counter_offer_condition()
  		{
  			if (Campaign.Current.CurrentConversationContext == ConversationContext.CapturedLord)
  			{
  				StringHelpers.SetCharacterProperties("ISSUE_OWNER", base.QuestGiver.CharacterObject);
  				StringHelpers.SetCharacterProperties("PLAYER", CharacterObject.PlayerCharacter);
  				if (Hero.OneToOneConversationHero == _targetHero)
  				{
  					return !_firstCounterOfferMade;
  				}
  				return false;
  			}
  			return false;
  		}
  
  		private bool counter_offer_2_condition()
  		{
  			StringHelpers.SetCharacterProperties("PLAYER", CharacterObject.PlayerCharacter);
  			if (Hero.OneToOneConversationHero == _targetHero && PlayerCapturedTargetHero)
  			{
  			GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold);
  			ChangeRelationAction.ApplyPlayerRelation(_targetHero, -10);
  			RelationshipChangeWithQuestGiver = 15;
  			GainRenownAction.Apply(Hero.MainHero, 6f);
  			AddLog(PlayerDeliveredPrisonerQuestSuccessLogText);
> 			CompleteQuestWithSuccess();
  			if (Hero.OneToOneConversationHero == base.QuestGiver)
  			{
  				TransferPrisonerAction.Apply(_targetHero.CharacterObject, PartyBase.MainParty, (base.QuestGiver.PartyBelongedTo != null) ? base.QuestGiver.PartyBelongedTo.Party : base.QuestGiver.CurrentSettlement.Party);
  			}
  			else
  			{
  				TransferPrisonerAction.Apply(_targetHero.CharacterObject, PartyBase.MainParty, Settlement.CurrentSettlement.Party);
  			}
  		}
  
  		private void QuestAcceptedConsequences()
  		{
  			StartQuest();
  			AddLog(PlayerStartsQuestLogText);
  			AddTrackedObject(_targetHero);
  		}
  
  		protected override void OnTimedOut()
  		{
  			RelationshipChangeWithQuestGiver = -5;
  			AddLog(TimeOutLogText);
  		}
  
  		protected override void InitializeQuestOnGameLoad()
  		{
  		}
  
  		protected override void RegisterEvents()
  		{
> 			CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEventEnded);
> 			CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnPrisonerTaken);
  			CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
  			CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
  			CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
  			CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
  			CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
  			CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
  		}
  
  		private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
  		{
  			if (QuestHelper.CheckMinorMajorCoercion(this, mapEvent, attackerParty))
  			{
  				QuestHelper.ApplyGenericMinorMajorCoercionConsequences(this, mapEvent);
  			}
  		}
  
> 		private void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, EndCaptivityDetail detail, bool showNotification)
  		{
  			if (base.IsOngoing && PlayerCapturedTargetHero && prisoner == _targetHero)
  			{
  				TextObject textObject = new TextObject("{=VuwaQjUN}{TARGET_HERO.LINK} has managed to escape and {?TARGET_HERO.GENDER}she{?}he{\\?} is no longer a prisoner.");
  				StringHelpers.SetCharacterProperties("TARGET_HERO", _targetHero.CharacterObject, textObject);
  				AddLog(textObject);
  				_firstCounterOfferMade = false;
  			}
  		}
  
  		private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
  		{
  			CheckCancelConditions(DiplomacyHelper.IsWarCausedByPlayer(faction1, faction2, detail));
  		}
  
  		private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
  		{
  			CheckCancelConditions();
  		}
  
  		private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
  		{
  			if (victim == _targetHero)
  			{
  				{
  					AddLog(TargetKilledByPlayerLogText);
  					GainRenownAction.Apply(Hero.MainHero, 10f);
  					RelationshipChangeWithQuestGiver = 10;
  					GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold / 2);
> 					CompleteQuestWithSuccess();
  				}
  				else
  				{
  					AddLog(TargetKilledByOthersLogText);
  					CompleteQuestWithCancel();
  				}
  			}
  		}
  
  		private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
  		{
  			if (PlayerCapturedTargetHero && hero == Hero.MainHero && party == MobileParty.MainParty && settlement.IsFortification && settlement.OwnerClan == base.QuestGiver.Clan)
  			{
  				_questGiversAgentCharacterObject = base.QuestGiver.Culture.Guard;
  				CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty), new ConversationCharacterData(_questGiversAgentCharacterObject));
  			}
  		}
  
> 		private void OnPlayerBattleEventEnded(MapEvent mapEvent)
  		{
> 			if ((mapEvent.IsPlayerMapEvent || mapEvent.IsPlayerSimulation) && mapEvent.PartiesOnSide(mapEvent.GetOtherSide(mapEvent.PlayerSide)).Any((MapEventParty x) => x.Party.Owner == _targetHero) && mapEvent.HasWinner && mapEvent.WinningSide == mapEvent.PlayerSide && !_targetHero.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  			{
> 				ChangeRelationAction.ApplyPlayerRelation(_targetHero.MapFaction.Leader, -10);
> 				DeclareWarAction.ApplyByPlayerHostility(Hero.MainHero.MapFaction, _targetHero.MapFaction);
  			}
  		}
  
> 		private void OnPrisonerTaken(PartyBase capturer, Hero prisoner)
  		{
  			if (prisoner != _targetHero)
  			{
  				return;
  			}
> 			if (capturer == PartyBase.MainParty)
  			{
  				AddLog(PlayerCapturedTargetBrief1LogText);
  				AddLog(PlayerCapturedTargetBrief2LogText);
  				AddLog(PlayerCapturedTargetBrief3LogText);
  				return;
  			}
> 			if (base.QuestGiver.PartyBelongedTo != null && capturer == base.QuestGiver.PartyBelongedTo.Party)
  			{
  				if (base.IsOngoing)
  				{
  					AddLog(QuestGiverCapturedTargetHero);
  					CompleteQuestWithCancel();
  				}
  				return;
  			}
  			TextObject textObject = TextObject.GetEmpty();
> 			if (capturer.IsMobile)
  			{
  				textObject = new TextObject("{=gq8sgb2J}{TARGET_HERO.LINK} has been taken prisoner by {OTHER_HERO.LINK}.");
  				StringHelpers.SetCharacterProperties("TARGET_HERO", _targetHero.CharacterObject, textObject);
> 				StringHelpers.SetCharacterProperties("OTHER_HERO", ConversationHelper.GetConversationCharacterPartyLeader(capturer), textObject);
  			}
> 			else if (capturer.IsSettlement)
  			{
  				textObject = new TextObject("{=HkvlHhPN}{TARGET_HERO.LINK} has been taken prisoner and thrown into the dungeons of {SETTLEMENT}.");
  				StringHelpers.SetCharacterProperties("TARGET_HERO", _targetHero.CharacterObject, textObject);
> 				textObject.SetTextVariable("SETTLEMENT", capturer.Settlement.EncyclopediaLinkWithName);
  			}
  			if (!textObject.IsEmpty())
  			{
  				AddLog(textObject);
  			}
  		}
  
  		private void CheckCancelConditions(bool causedByPlayer = false)
  		{
  			if (!base.IsOngoing)
  			{
  				return;
  			}
> 			if (base.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  			{
  				if (causedByPlayer)
  				{
  					CompleteQuestWithFail();
  				}
  				else
  				{
  					CompleteQuestWithCancel();
  				}
  				AddLog(WarDeclaredQuestLog);
  			}
> 			else if (base.QuestGiver.MapFaction == _targetHero.MapFaction)
  			{
  				CompleteQuestWithCancel();
  				AddLog(TargetHeroAndQuestGiverInSameFaction);
  			}
> 			else if (base.QuestGiver.MapFaction != Hero.MainHero.MapFaction)
  			{
  				CompleteQuestWithCancel();
  				AddLog(PlayerAndQuestGiverNotInSameFaction);
  			}
  		}
  	}
  
  	public class LordWantsRivalCapturedIssueTypeDefiner : SaveableTypeDefiner
  	{
  		public LordWantsRivalCapturedIssueTypeDefiner()
  			: base(51400)
  		{
  		}
  
  		protected override void DefineClassTypes()
  		{
  			AddClassDefinition(typeof(LordWantsRivalCapturedIssue), 1);
  			AddClassDefinition(typeof(LordWantsRivalCapturedIssueQuest), 2);
  		}
  	}
  
  	private const IssueBase.IssueFrequency LordWantsRivalCapturedIssueFrequency = IssueBase.IssueFrequency.Rare;
  
  	private const float TargetHeroRelationMaxRelation = -10f;
  	private bool ConditionsHold(Hero issueGiver, out Hero targetHero)
  	{
  		targetHero = null;
  		if (issueGiver.IsLord && !issueGiver.IsMinorFactionHero && issueGiver.Clan.Leader == issueGiver && issueGiver.Clan != Clan.PlayerClan && issueGiver.GetTraitLevel(DefaultTraits.Mercy) <= 0 && issueGiver.GetTraitLevel(DefaultTraits.Honor) <= 0 && issueGiver.GetTraitLevel(DefaultTraits.Valor) <= 0)
  		{
> 			List<Clan> list = Clan.FindAll((Clan x) => x.MapFaction != issueGiver.MapFaction && !x.IsMinorFaction && x.MapFaction.IsAtWarWith(issueGiver.MapFaction) && x.AliveLords.Count > 0).ToList();
  			int count = list.Count;
  			if (count > 0)
  			{
  				int num = MBRandom.RandomInt(count);
  				int num2 = num;
  				bool flag = true;
  				do
  				{
  					Clan clan = list[num2];
  					targetHero = clan.AliveLords.GetRandomElementWithPredicate((Hero noble) => !noble.IsKingdomLeader && noble.PartyBelongedTo != null && noble.PartyBelongedTo.MemberRoster.TotalHealthyCount >= 50 && (float)noble.GetRelation(issueGiver) <= -10f);
  					num2++;
  					if (num2 == count)
  					{
  						num2 = 0;
  					}
  					if (num2 == num)
  					{
  						flag = false;
  					}
  				}
  				while (targetHero == null && flag);
  			}
  		}
  		return targetHero != null;

 succeeded in 1509ms:
  			return false;
  		}
  
  		private void QuestAcceptedConsequences()
  		{
> 			StartQuest();
  			AddLog(QuestStartedLog);
  			_smugglerParty = CreateSmugglerParty();
  			AddTrackedObject(_targetSettlement);
  			AddTrackedObject(_originSettlement);
  		}
  
  		private MobileParty CreateSmugglerParty()
  		{
  			TextObject textObject = new TextObject("{=3dhAfC4k}Smugglers of {ORIGIN_SETTLEMENT}");
  			textObject.SetTextVariable("ORIGIN_SETTLEMENT", _originSettlement.Name);
  			GetAdditionalVisualsForParty(_originSettlement.Culture, out var mountStringId, out var harnessStringId);
  			Hideout nearestHideoutSettlement = SettlementHelper.FindNearestHideoutToSettlement(_originSettlement, MobileParty.NavigationType.Default);
  			int desiredMenCount = (int)TaleWorlds.Library.MathF.Clamp(TaleWorlds.Library.MathF.Ceiling((float)MobileParty.MainParty.MemberRoster.TotalManCount * 0.8f), 15f, 35f);
  			float customPartyBaseSpeed = MobileParty.MainParty.Speed * 1.1f;
  			PartyTemplateObject randomCaravanTemplate = CaravanHelper.GetRandomCaravanTemplate(_originSettlement.Culture, isElite: false, isLand: true);
  			MobileParty mobileParty = CustomPartyComponent.CreateCustomPartyWithTroopRoster(_originSettlement.GatePosition, 0.1f, _originSettlement, textObject, Clan.BanditFactions.FirstOrDefault((Clan faction) => faction.Culture == nearestHideoutSettlement.Settlement.Culture), TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), null, mountStringId, harnessStringId, customPartyBaseSpeed, avoidHostileActions: true);
  			MobilePartyHelper.FillPartyManuallyAfterCreation(mobileParty, randomCaravanTemplate, desiredMenCount);
  			CharacterObject character = MBObjectManager.Instance.GetObject<CharacterObject>("nervous_caravanmaster_" + MBRandom.RandomInt(1, 4));
  			mobileParty.MemberRoster.AddToCounts(character, 1, insertAtFront: true);
  			GiveGoodsToParty(mobileParty);
  			InitializePartyState(mobileParty);
  			mobileParty.SetPartyUsedByQuest(isActivelyUsed: true);
  			GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold);
  			if (_smugglerParty != null && _smugglerParty.IsActive)
  			{
  				DestroyPartyAction.Apply(null, _smugglerParty);
  			}
> 			CompleteQuestWithSuccess();
  		}
  
  		private void FailQuest()
  		{
  			TraitLevelingHelper.OnIssueFailed(Hero.MainHero, new Tuple<TraitObject, int>[2]
  			{
  				new Tuple<TraitObject, int>(DefaultTraits.Honor, -50),
  				new Tuple<TraitObject, int>(DefaultTraits.Valor, -50)
  			});
  			RelationshipChangeWithQuestGiver = -10;
  			RemoveTrackedObject(_targetSettlement);
  			RemoveTrackedObject(_originSettlement);
  			CompleteQuestWithFail(QuestFailedLog);
  			if (_smugglerParty != null && _smugglerParty.IsActive)
  			{
  				DestroyPartyAction.Apply(null, _smugglerParty);
  			}
  		}
  
  		protected override void RegisterEvents()
  		{
  			CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
  			CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
  		}
  
  		private void OnMapEventEnded(MapEvent mapEvent)
  		{
> 			if (mapEvent.IsPlayerMapEvent && mapEvent.InvolvedParties.Contains(_smugglerParty.Party))
  			{
> 				if (mapEvent.WinningSide == mapEvent.PlayerSide)
  				{
  					SucceedQuest(QuestSuccessWithFightLog);
  				}
  				else
  				{
  					FailQuest();
  				}
  			}
  		}
  
  		private void OnHourlyTickParty(MobileParty mobileParty)
  		{
  			if (mobileParty == _smugglerParty && mobileParty.CurrentSettlement != null)
  			{
  				if (_smugglerSettlementWaitCounter >= 4)
  				{
  					_smugglerSettlementWaitCounter = 0;
  					Settlement settlement = ((mobileParty.CurrentSettlement == _targetSettlement) ? _originSettlement : _targetSettlement);
  					SetPartyAiAction.GetActionForVisitingSettlement(mobileParty, settlement, MobileParty.NavigationType.Default, isFromPort: false, isTargetingPort: false);
  				}
  				else
  				{

 succeeded in 1596ms:
  			_rivalMobileParty.Aggressiveness = 0f;
  		}
  
  		private void SuccessWithVillagersSurrender()
  		{
> 			CompleteQuestWithSuccess();
  			AddLog(SuccessWitHVillagerSurrenderQuestLogText);
  			PlayerEncounter.LeaveEncounter = true;
  		}
  
  		private void SpawnHerdersParty()
  		{
  			TextObject textObject = new TextObject("{=tLakpr0a}Herdsmen of {QUEST_GIVER}");
  			textObject.SetTextVariable("QUEST_GIVER", base.QuestGiver.Name);
  			_herdersMobileParty = CustomPartyComponent.CreateCustomPartyWithTroopRoster(base.QuestGiver.CurrentSettlement.GatePosition, 1f, base.QuestGiver.CurrentSettlement, textObject, null, TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), null);
  			_herdersMobileParty.MemberRoster.AddToCounts(base.QuestGiver.Culture.Villager, TaleWorlds.Library.MathF.Ceiling(2f + 5f * _issueDifficultyMultiplier));
  			_herdersMobileParty.InitializePartyTrade(200);
  			_herdersMobileParty.SetPartyUsedByQuest(isActivelyUsed: true);
  			_herdersMobileParty.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("sumpter_horse"), TaleWorlds.Library.MathF.Ceiling(2f + 5f * _issueDifficultyMultiplier));
  			_herdersMobileParty.IgnoreForHours(CampaignTime.HoursInDay * 30);
  			_herdersMobileParty.Ai.SetDoNotMakeNewDecisions(doNotMakeNewDecisions: true);
  			_herdersMobileParty.Party.SetVisualAsDirty();
  			AddTrackedObject(_herdersMobileParty);
  			_herdersMobileParty.Aggressiveness = 0f;
  			CampaignVec2 point = _targetSettlement.GatePosition;
  			int[] invalidTerrainTypesForNavigationType = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default);
  			for (int i = 0; i < 15; i++)
  			{
  			_spawnRivalPartyAfterHours = (int)(distance2 / _herdersMobileParty.Speed) + 3;
  		}
  
  		private void QuestAcceptedConsequences()
  		{
> 			StartQuest();
  			AddLog(PlayerStartsQuestLogText);
  			AddTrackedObject(_targetSettlement);
  			SpawnHerdersParty();
  		}
  
  		protected override void OnCompleteWithSuccess()
  		{
  			base.QuestGiver.AddPower(10f);
  			RelationshipChangeWithQuestGiver = 5;
  			_headmanNotable.AddPower(-10f);
  			ChangeRelationAction.ApplyPlayerRelation(_headmanNotable, -3);
  			TraitLevelingHelper.OnIssueSolvedThroughQuest(base.QuestGiver, new Tuple<TraitObject, int>[2]
  			{
  				new Tuple<TraitObject, int>(DefaultTraits.Honor, 30),
  				new Tuple<TraitObject, int>(DefaultTraits.Mercy, -20)
  			});
  			GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, _rewardGold);
  		}
  
  		public override void OnFailed()
  		{
  			base.QuestGiver.AddPower(-10f);
  
  		private void OnMapEventEnded(MapEvent mapEvent)
  		{
  			if (_rivalMobileParty != null && ((mapEvent.InvolvedParties.Contains(PartyBase.MainParty) && mapEvent.InvolvedParties.Contains(_rivalMobileParty.Party)) || (mapEvent.InvolvedParties.Contains(PartyBase.MainParty) && mapEvent.InvolvedParties.Contains(_rivalMobileParty.Party) && mapEvent.InvolvedParties.Contains(_herdersMobileParty.Party))))
  			{
> 				if (mapEvent.WinningSide == mapEvent.PlayerSide)
  				{
> 					CompleteQuestWithSuccess();
  					AddLog(SuccessWitHWinningTheFightQuestLogText);
  				}
  				else
  				{
  					CompleteQuestWithFail();
  					AddLog(FailWithLosingTheFightQuestLogText);
  				}
  			}
  		}
  
  		protected override void HourlyTick()
  		{
  			if (!base.IsOngoing)
  			{
  				return;
  			}
  			CheckAnSpawnRivalParty();
  			if (_rivalMobileParty != null && _rivalMobileParty.IsActive)
  			{
  				if (_herdersMobileParty?.MapEvent == null && _rivalMobileParty?.MapEvent == null && !_battleStarted && DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(_rivalMobileParty, _herdersMobileParty, MobileParty.NavigationType.Default) < Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius / 6f)
  				{
  					EncounterManager.StartPartyEncounter(_rivalMobileParty.Party, _herdersMobileParty.Party);
  			{
  				if (mobileParty == _rivalMobileParty && (destroyerParty == PartyBase.MainParty || destroyerParty == _herdersMobileParty.Party))
  				{
  					_rivalMobileParty = null;
  					AddLog(SuccessWitHWinningTheFightQuestLogText);
> 					CompleteQuestWithSuccess();
  				}
  				if (mobileParty == _herdersMobileParty)
  				{
  					_herdersMobileParty = null;
  					AddLog(FailWithLosingTheFightQuestLogText);
  					CompleteQuestWithFail();
  				}
  			}
  		}
  
  		private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
  		{
  			if (base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  			{
  				CompleteQuestWithCancel(WarDeclaredCancelLog);
  			}
  		}
  
  		private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
  		{
  			QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, PlayerDeclaredWarQuestLogText, WarDeclaredCancelLog);
  		}

 succeeded in 1696ms:
  			.NpcLine("{=khIuyBAi}Thank you for your help. Farewell.")
  			.Consequence(delegate
  			{
  				ApplyQuestResult(_questResultSuccess1);
  				AddLog(OnQuestSucceededLogText);
> 				CompleteQuestWithSuccess();
  			})
  			.CloseDialog()
  			.PlayerOption("{=xcyr5Oq2}Half of the coin is enough for our needs.")
  			.NpcLine("{=SVrCpvpZ}Thank you {PLAYER.NAME}. Our people are grateful to you for what you have done today. Farewell.[if:convo_relaxed_happy]")
  			.Condition(() => true)
  			.Consequence(delegate
  			{
  				ApplyQuestResult(_questResultSuccess2);
  				AddLog(OnQuestSucceededLogText);
> 				CompleteQuestWithSuccess();
  			})
  			.CloseDialog()
  			.PlayerOption("{=52GFRUTE}Keep your coin, headman. Your people's blessings are enough.")
  			.NpcLine("{=1l5dKk1c}Oh, thank you {PLAYER.NAME}. You will always be remembered by our people. Farewell.[if:convo_merry]")
  			.Condition(() => true)
  			.Consequence(delegate
  			{
  				ApplyQuestResult(_questResultSuccess3);
  				AddLog(OnQuestSucceededLogText);
> 				CompleteQuestWithSuccess();
  			})
  			.CloseDialog()
  			.EndPlayerOptions()
  			.CloseDialog();
  
  		private DialogFlow DeserterPartyAmbushedDialogFlow => DialogFlow.CreateDialogFlow("start", 125).NpcLine("{=s2btPjJf}Who the hell are you? If you live in this village, you'd better rustle up some silver and wine. Look lively, eh?[if:convo_confused_annoyed][ib:warrior]").Condition(() => _deserterMobileParty != null && _deserterMobileParty.IsActive && CharacterObject.OneToOneConversationCharacter == ConversationHelper.GetConversationCharacterPartyLeader(_deserterMobileParty.Party) && _deserterMobileParty.Position.Distance(QuestSettlement.Position) <= Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 1.75f)
  			.PlayerLine("{=Pp3koSqA}This time you'll have to fight for it!")
  			.CloseDialog();
  
  		private int DeserterPartyMenCount => 24 + TaleWorlds.Library.MathF.Ceiling(24f * _questDifficultyMultiplier);
  
  		private int DefenderPartyMenCount => 16 + TaleWorlds.Library.MathF.Ceiling(16f * _questDifficultyMultiplier);
  
  		public Settlement QuestSettlement => base.QuestGiver.CurrentSettlement;
  
  		public override bool IsRemainingTimeHidden => false;
  
  		internal static void AutoGeneratedStaticCollectObjectsExtortionByDesertersIssueQuest(object o, List<object> collectedObjects)
  		{
  			((ExtortionByDesertersIssueQuest)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  				.CloseDialog();
  		}
  
  		private void OnQuestAccepted()
  		{
> 			StartQuest();
  			Campaign.Current.ConversationManager.AddDialogFlow(QuestCompletionDialogFlow, this);
  			Campaign.Current.ConversationManager.AddDialogFlow(DeserterPartyAmbushedDialogFlow, this);
  			AddLog(OnQuestStartedLogText);
  		}
  
  		private void ApplyQuestResult(in ExtortionByDesertersQuestResult result)
  		{
  			int num = (int)(result.GoldMultiplier * (float)RewardGold);
  			if (num > 0)
  			{
  				GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, num);
  			}
  			if (result.QuestGiverPowerChange != 0)
  			{
  				base.QuestGiver.AddPower(result.QuestGiverPowerChange);
  			}
  			if (result.TownSecurityChange != 0)
  			{
  				QuestSettlement.Village.Bound.Town.Security += result.TownSecurityChange;
  			}
  			if (result.TownProsperityChange != 0)
  			{
  			TickDesertersPartyLogic();
  		}
  
  		private void MapEventEnded(MapEvent mapEvent)
  		{
> 			if (!mapEvent.IsPlayerMapEvent || _deserterMobileParty == null || !mapEvent.InvolvedParties.Contains(_deserterMobileParty.Party))
  			{
  				return;
  			}
  			_deserterBattleFinalizedForTheFirstTime = true;
> 			if (mapEvent.WinningSide == mapEvent.PlayerSide)
  			{
  				AddLog(OnDeserterPartyDefeatedLogText);
  				if (!IsTracked(base.QuestGiver))
  				{
  					AddTrackedObject(base.QuestGiver);
  				}
  				if (!IsTracked(QuestSettlement))
  				{
  					AddTrackedObject(QuestSettlement);
  				}
  				MBInformationManager.AddQuickInformation(OnPlayerDefeatedDesertersNotificationText, 0, base.QuestGiver.CharacterObject);
  				_currentState = ExtortionByDesertersQuestState.DesertersAreDefeated;
  			}
  			else
  			{
  				if (_deserterMobileParty.DefaultBehavior != AiBehavior.RaidSettlement)
  				{
  					SetPartyAiAction.GetActionForRaidingSettlement(_deserterMobileParty, QuestSettlement, MobileParty.NavigationType.Default, isFromPort: false, isTargetingPort: false);
  				}
  				_currentState = ExtortionByDesertersQuestState.DesertersDefeatedPlayer;
  			}
  		}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.MapEvents.MapEvent 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'IsPlayerMapEvent|IsPlayerSimulation|PlayerSide|WinningSide|HasWinner|IsSiegeAssault|IsHideoutBattle|MapEventType|IsFieldBattle' -Context 3,12 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'OnPlayerBattleEnd|PlayerSide|WinningSide|IsPlayerMapEvent|IsPlayerSimulation|Finish' -Context 5,24 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'OnPlayerBattleEnd\\(' -SimpleMatch | Select-Object -First 80 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'CampaignEventDispatcher.Instance.OnPlayerBattleEnd' -SimpleMatch | Select-Object -First 20 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 665ms:
 succeeded in 666ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encounters\PlayerEncounter.cs:1498:CampaignEventDispatcher.Instance.OnPlayerBattleEnd(_mapEvent);

 succeeded in 1585ms:
  		}
  	}
  
  	public static BattleState BattleState => Current._mapEvent.BattleState;
  
> 	public static BattleSideEnum WinningSide => Current._mapEvent.WinningSide;
  
  	public static bool BattleChallenge
  	{
  		get
  		{
  			return Current._battleChallenge;
  		}
  		set
  		{
  			Current._battleChallenge = value;
  		}
  	}
  
> 	public static bool PlayerIsDefender => Current.PlayerSide == BattleSideEnum.Defender;
  
> 	public static bool PlayerIsAttacker => Current.PlayerSide == BattleSideEnum.Attacker;
  
  	public static bool LeaveEncounter
  	{
  		get
  		{
  			return Current._leaveEncounter;
  		}
  		set
  		{
  			Current._leaveEncounter = value;
  		}
  	}
  
  	public static bool MeetingDone => Current._meetingDone;
  
  	public static bool PlayerSurrender
  	{
  		get
  		{
  			return Current._playerSurrender;
  		}
  		set
  		{
  			if (value)
  
  	[SaveableProperty(2)]
  	public BattleSideEnum OpponentSide { get; private set; }
  
  	[SaveableProperty(3)]
> 	public BattleSideEnum PlayerSide { get; private set; }
  
  	[SaveableProperty(6)]
  	public bool IsJoinedBattle { get; private set; }
  
  	public static bool InsideSettlement
  	{
  		get
  		{
  			if (MobileParty.MainParty.IsActive)
  			{
  				return MobileParty.MainParty.CurrentSettlement != null;
  			}
  			return false;
  		}
  	}
  
  	public static CampaignBattleResult CampaignBattleResult
  	{
  		get
  		{
  			return Current._campaignBattleResult;
  		}
  		set
  		{
  	public bool InterruptedWhileWaiting { get; set; }
  
  	[SaveableProperty(57)]
  	public bool InterruptedWhileLooting { get; set; }
  
> 	public bool IsNavalEncounterFinishedWithDisengage { get; private set; }
  
  	public List<Ship> CapturedShipsInEncounter { get; private set; } = new List<Ship>();
  
  	internal static void AutoGeneratedStaticCollectObjectsPlayerEncounter(object o, List<object> collectedObjects)
  	{
  		((PlayerEncounter)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		collectedObjects.Add(_campaignBattleResult);
  		collectedObjects.Add(_mapEvent);
  		collectedObjects.Add(_encounteredParty);
  		collectedObjects.Add(_attackerParty);
  		collectedObjects.Add(_defenderParty);
  		collectedObjects.Add(_helpedHeroes);
  		collectedObjects.Add(_capturedHeroes);
  		collectedObjects.Add(_capturedAlreadyPrisonerHeroes);
  		collectedObjects.Add(_alternativeRosterToReceiveLootItems);
  		collectedObjects.Add(_alternativeRosterToReceiveLootPrisoners);
  		collectedObjects.Add(_alternativeRosterToReceiveLootMembers);
  		collectedObjects.Add(_alternativeReceivedLootShips);
  		collectedObjects.Add(EncounterSettlementAux);
  	}
  	internal static object AutoGeneratedGetMemberValueOpponentSide(object o)
  	{
  		return ((PlayerEncounter)o).OpponentSide;
  	}
  
> 	internal static object AutoGeneratedGetMemberValuePlayerSide(object o)
  	{
> 		return ((PlayerEncounter)o).PlayerSide;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIsJoinedBattle(object o)
  	{
  		return ((PlayerEncounter)o).IsJoinedBattle;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIsPlayerEncounterRestartedForRaid(object o)
  	{
  		return ((PlayerEncounter)o).IsPlayerEncounterRestartedForRaid;
  	}
  
  	internal static object AutoGeneratedGetMemberValueEncounterSettlementAux(object o)
  	{
  		return ((PlayerEncounter)o).EncounterSettlementAux;
  	}
  
  	internal static object AutoGeneratedGetMemberValueIsPlayerWaiting(object o)
  	{
  		return ((PlayerEncounter)o).IsPlayerWaiting;
  	}
  
  	internal static object AutoGeneratedGetMemberValueInterruptedWhileWaiting(object o)
  	{
  
  	public static void RestartPlayerEncounter(PartyBase defenderParty, PartyBase attackerParty, bool forcePlayerOutFromSettlement = true, bool isPlayerEncounterRestartedForRaid = false)
  	{
  		if (Current != null)
  		{
> 			Finish(forcePlayerOutFromSettlement);
  		}
  		Start();
  		Current.SetupFields(attackerParty, defenderParty);
  		Current.IsPlayerEncounterRestartedForRaid = isPlayerEncounterRestartedForRaid || Current.IsPlayerEncounterRestartedForRaid;
  	}
  
  	internal void Init(PartyBase attackerParty, PartyBase defenderParty, Settlement settlement = null)
  	{
> 		IsNavalEncounterFinishedWithDisengage = false;
  		InterruptedWhileLooting = false;
  		EncounterSettlementAux = ((settlement != null) ? settlement : (defenderParty.IsSettlement ? defenderParty.Settlement : attackerParty.Settlement));
  		EnemySurrender = false;
  		PlayerPartyInitialStrength = MobileParty.MainParty.Party.CalculateCurrentStrength();
  		SetupFields(attackerParty, defenderParty);
  		if (defenderParty.MapEvent != null && attackerParty != MobileParty.MainParty.Party && defenderParty != MobileParty.MainParty.Party)
  		{
  			_mapEvent = defenderParty.MapEvent;
  			if (_mapEvent.CanPartyJoinBattle(PartyBase.MainParty, BattleSideEnum.Defender))
  			{
  				MobileParty.MainParty.Party.MapEventSide = _mapEvent.DefenderSide;
  			}
  			else if (_mapEvent.CanPartyJoinBattle(PartyBase.MainParty, BattleSideEnum.Attacker))
  			{
  				MobileParty.MainParty.Party.MapEventSide = _mapEvent.AttackerSide;
  			}
  		}
  		bool joinBattle = false;
  		bool startBattle = false;
  		string encounterMenu = Campaign.Current.Models.EncounterGameMenuModel.GetEncounterMenu(attackerParty, defenderParty, out startBattle, out joinBattle);
  		if (!string.IsNullOrEmpty(encounterMenu))
  		{
  			if (startBattle)
  			{
  			EncounterSettlementAux = mobileParty.BesiegerCamp.SiegeEvent.BesiegedSettlement;
  		}
  		_encounteredParty = ((mobileParty != null) ? mobileParty.Party : EncounterSettlementAux?.Party);
  		if (MapEvent.PlayerMapEvent != null)
  		{
> 			PlayerSide = MapEvent.PlayerMapEvent.PlayerSide;
  		}
  		else if (defenderParty == PartyBase.MainParty || (defenderParty.MobileParty != null && defenderParty.MobileParty == MobileParty.MainParty.AttachedTo) || (defenderParty.IsSettlement && (defenderParty.Settlement.MapFaction == MobileParty.MainParty.MapFaction || MobileParty.MainParty.CurrentSettlement == defenderParty.Settlement)))
  		{
> 			PlayerSide = BattleSideEnum.Defender;
  		}
  		else
  		{
> 			PlayerSide = BattleSideEnum.Attacker;
  		}
> 		OpponentSide = PlayerSide.GetOppositeSide();
  	}
  
  	internal void OnPartyJoinEncounter(MobileParty newParty)
  	{
  		if (Battle == null)
  		{
  			return;
  		}
  		if (Battle.CanPartyJoinBattle(newParty.Party, PartyBase.MainParty.Side))
  		{
  			newParty.Party.MapEventSide = PartyBase.MainParty.MapEventSide;
  		}
  		else if (newParty != MobileParty.MainParty || !Battle.IsRaid || Battle.AttackerSide.LeaderParty == MobileParty.MainParty.Party || Battle.DefenderSide.TroopCount != 0)
  		{
  			MobileParty.MainParty.SetMoveModeHold();
  			string newPartyJoinMenu = Campaign.Current.Models.EncounterGameMenuModel.GetNewPartyJoinMenu(newParty);
  			if (Battle.CanPartyJoinBattle(newParty.Party, PartyBase.MainParty.OpponentSide))
  			{
  				newParty.Party.MapEventSide = PartyBase.MainParty.MapEventSide.OtherSide;
  			}
  			if (!string.IsNullOrEmpty(newPartyJoinMenu))
  			{
  				GameMenu.SwitchToMenu(newPartyJoinMenu);
  			}
  		{
  			return;
  		}
  		List<MobileParty> list = new List<MobileParty>();
  		List<MobileParty> list2 = new List<MobileParty>();
> 		foreach (MapEventParty item in _mapEvent.PartiesOnSide(PlayerSide))
  		{
  			if (item.Party.IsMobile)
  			{
  				list.Add(item.Party.MobileParty);
  			}
  		}
> 		foreach (MapEventParty item2 in _mapEvent.PartiesOnSide(PlayerSide.GetOppositeSide()))
  		{
  			if (item2.Party.IsMobile)
  			{
  				list2.Add(item2.Party.MobileParty);
  			}
  		}
  		Current.FindNonAttachedNpcPartiesWhoWillJoinEvent(list, list2);
  		foreach (MobileParty item3 in list)
  		{
> 			_mapEvent.GetMapEventSide(PlayerSide).AddNearbyPartyToPlayerMapEvent(item3);
  		}
  		foreach (MobileParty item4 in list2)
  		{
> 			_mapEvent.GetMapEventSide(PlayerSide.GetOppositeSide()).AddNearbyPartyToPlayerMapEvent(item4);
  		}
  	}
  
  	public static bool IsNavalEncounter()
  	{
  		PlayerEncounter current = Current;
  		if (current == null)
  		{
  			return false;
  		}
  		return current._mapEvent?.IsNavalMapEvent == true;
  	}
  
  	private MapEvent StartBattleInternal()
  	{
  		if (_mapEvent == null)
  		{
  			if (ForceRaid)
  			{
  				_mapEvent = RaidEventComponent.CreateRaidEvent(_attackerParty, _defenderParty).MapEvent;
  			}
  			else if (ForceSallyOut)
  			{
  				_mapEvent = Campaign.Current.MapEventManager.StartSallyOutMapEvent(_attackerParty, _defenderParty);
  		return Current.StartBattleInternal();
  	}
  
  	private void JoinBattleInternal(BattleSideEnum side)
  	{
> 		PlayerSide = side;
  		switch (side)
  		{
  		case BattleSideEnum.Defender:
  			OpponentSide = BattleSideEnum.Attacker;
  			break;
  		case BattleSideEnum.Attacker:
  			OpponentSide = BattleSideEnum.Defender;
  			break;
  		}
  		if (EncounteredBattle != null)
  		{
  			_mapEvent = EncounteredBattle;
> 			_encounteredParty = ((PlayerSide == BattleSideEnum.Attacker) ? EncounteredBattle.DefenderSide.LeaderParty : EncounteredBattle.AttackerSide.LeaderParty);
  			PartiesStrengthRatioBeforePlayerJoin = CalculateStrengthOfParties();
  			PartyBase.MainParty.MapEventSide = EncounteredBattle.GetMapEventSide(side);
  			EncounterSettlementAux = _mapEvent.MapEventSettlement;
> 			if (EncounteredBattle.IsSiegeAssault && PlayerSide == BattleSideEnum.Attacker)
  			{
  				MobileParty.MainParty.BesiegerCamp = _encounteredParty.SiegeEvent.BesiegerCamp;
  			}
  			IsJoinedBattle = true;
  			CheckNearbyPartiesToJoinPlayerMapEvent();
  		}
  		else
  		{
> 			Finish(InsideSettlement);
  		}
  	}
  
  	private float CalculateStrengthOfParties()
  	{
  		MapEvent.PowerCalculationContext contextForPosition = Campaign.Current.Models.MilitaryPowerModel.GetContextForPosition(_mapEvent.Position);
  		float num = 0f;
  		float num2 = 0f;
  		foreach (MapEventParty party in _mapEvent.DefenderSide.Parties)
  		{
  			BattleSideEnum side = BattleSideEnum.Defender;
  			num += party.Party.GetCustomStrength(side, contextForPosition);
  		}
  		foreach (MapEventParty party2 in _mapEvent.AttackerSide.Parties)
  		{
  			BattleSideEnum side2 = BattleSideEnum.Attacker;
  			num2 += party2.Party.GetCustomStrength(side2, contextForPosition);
  		}
  		return num / num2;
  	}
  
  	public static void JoinBattle(BattleSideEnum side)
  	{
  		Current.JoinBattleInternal(side);
  	public static void Start()
  	{
  		Campaign.Current.PlayerEncounter = new PlayerEncounter();
  	}
  
> 	public static void ProtectPlayerSide(float hoursToProtect = 1f)
  	{
  		MobileParty.MainParty.TeleportPartyToOutSideOfEncounterRadius();
  		MobileParty.MainParty.IgnoreForHours(hoursToProtect);
  	}
  
> 	public static void Finish(bool forcePlayerOutFromSettlement = true)
  	{
  		if (MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == EncounteredMobileParty)
  		{
  			Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
  		}
  		if (Campaign.Current.CurrentMenuContext != null)
  		{
  			GameMenu.ExitToLast();
  		}
  		else
  		{
  			Campaign.Current.MapStateData.GameMenuId = null;
  		}
  		int num;
  		if (Current != null)
  		{
> 			if (PlayerSiege.PlayerSiegeEvent != null && PlayerSiege.PlayerSide == BattleSideEnum.Attacker && MobileParty.MainParty.MapEvent != null && !MobileParty.MainParty.MapEvent.IsSiegeAssault && MobileParty.MainParty.MapEvent.HasWinner && MobileParty.MainParty.MapEvent.PlayerSide == BattleSideEnum.Defender && MobileParty.MainParty.BesiegedSettlement != null)
  			{
  				num = (PlayerSiege.PlayerSiegeEvent.BesiegedSettlement.GetInvolvedPartiesForEventType(MobileParty.MainParty.MapEvent.EventType).Any((PartyBase x) => x.NumberOfHealthyMembers > 0) ? 1 : 0);
  				if (num != 0)
  				{
  					goto IL_0111;
  				}
  			}
  			else
  			{
  				num = 0;
  			}
  			if (Current._isSiegeInterruptedByEnemyDefection)
  			{
  				goto IL_0111;
  			}
  			goto IL_016a;
  		}
  		goto IL_023f;
  		IL_0111:
  		if (Hero.MainHero.PartyBelongedToAsPrisoner == null && !Current._leaveEncounter && Current._encounteredParty.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
  		{
  			GameMenu.ActivateGameMenu("continue_siege_after_attack");
  			if (Current._isSiegeInterruptedByEnemyDefection)
  			{
  		{
  			MobileParty.MainParty.BesiegerCamp = null;
  		}
  		Current.FirstInit = true;
  		bool playerIsWinner = Current._mapEvent?.IsWinnerSide(PartyBase.MainParty.Side) ?? false;
> 		EncounterSettlement?.OnPlayerEncounterFinish();
  		Current.FinalizeBattle();
> 		Current.FinishEncounterInternal(playerIsWinner);
  		if (CurrentBattleSimulation != null)
  		{
  			MapState mapState = Game.Current.GameStateManager.LastOrDefault<MapState>();
  			if (mapState != null && mapState.IsSimulationActive)
  			{
  				mapState.EndBattleSimulation();
  			}
  			Current.BattleSimulation = null;
  		}
  		if (InsideSettlement && MobileParty.MainParty.AttachedTo == null && forcePlayerOutFromSettlement)
  		{
  			LeaveSettlement();
  		}
  		goto IL_023f;
  	}
  
> 	private void FinishEncounterInternal(bool playerIsWinner)
  	{
  		if (!playerIsWinner && _encounteredParty != null && _encounteredParty.IsMobile && MobileParty.MainParty.AttachedTo == null && MobileParty.MainParty.IsActive && !LeaveEncounter && FactionManager.IsAtWarAgainstFaction(_encounteredParty.MapFaction, PartyBase.MainParty.MapFaction) && _encounteredParty.MobileParty.IsActive)
  		{
  			MobileParty.MainParty.TeleportPartyToOutSideOfEncounterRadius();
  			_encounteredParty.MobileParty.Ai.SetDoNotAttackMainParty(2);
  		}
  	}
  
  	private void UpdateInternal()
  	{
  		_mapEvent = MapEvent.PlayerMapEvent;
  		if (EnemySurrender && EncounterState == PlayerEncounterState.Begin)
  		{
  			EncounterState = PlayerEncounterState.Wait;
  		}
  		_stateHandled = false;
  		while (!_stateHandled)
  		{
  			if (Current._leaveEncounter)
  			{
> 				Finish();
  				_stateHandled = true;
  			}
  			if (!_stateHandled)
  			{
  				switch (EncounterState)
  				{
  				case PlayerEncounterState.Begin:
  					DoBegin();
  					break;
  				case PlayerEncounterState.Wait:
  					DoWait();
  					break;
  				case PlayerEncounterState.PrepareResults:
  					DoPrepareResults();
  					break;
  				case PlayerEncounterState.ApplyResults:
  					DoApplyMapEventResults();
  					break;
  				case PlayerEncounterState.PlayerVictory:
  					DoPlayerVictory();
  					break;
  				case PlayerEncounterState.PlayerTotalDefeat:
  					DoPlayerDefeat();
  					break;
  	}
  
  	private void ContinueBattle()
  	{
  		Debug.Print("[PlayerEncounter.ContinueBattle Start]");
> 		MapEventSide mapEventSide = _mapEvent.GetMapEventSide(_mapEvent.PlayerSide);
  		MapEventSide otherSide = mapEventSide.OtherSide;
  		_mapEvent.RecalculateStrengthOfSides();
  		if (_mapEvent.IsNavalMapEvent && !_mapEvent.IsRaid && !_mapEvent.IsForcingSupplies && !_mapEvent.IsForcingVolunteers && otherSide.Parties.Sum((MapEventParty x) => x.Ships.Count) == 0)
  		{
  			Debug.FailedAssert("This case should not be called anymore, make sure this is intended", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Encounters\\PlayerEncounter.cs", "ContinueBattle", 1215);
  			Debug.Print("Player side wins according to the strength ratio.");
> 			_mapEvent?.SetOverrideWinner(_mapEvent.PlayerSide);
  			EnemySurrender = true;
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
  		else if (_mapEvent.IsNavalMapEvent && mapEventSide.Parties.Sum((MapEventParty x) => x.Ships.Count) == 0)
  		{
  			Debug.FailedAssert("This case should not be called anymore, make sure this is intended", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Encounters\\PlayerEncounter.cs", "ContinueBattle", 1224);
  			Debug.Print("Other side wins according to the strength ratio.");
  			_mapEvent?.SetOverrideWinner(otherSide.MissionSide);
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
  		else
  		{
  			Debug.Print("Battle continues.");
  			Debug.Print("Other side strength by party:");
  			foreach (MapEventParty party in otherSide.Parties)
  			{
  				Debug.Print(string.Concat("party: ", party.Party.Id, ": ", party.Party.Name, ", strength: ", party.Party.CalculateCurrentStrength(), ", healthy count: ", party.Party.MemberRoster.TotalHealthyCount, ", wounded count: ", party.Party.MemberRoster.TotalWounded));
  			}
  			_mapEvent.ApplyGainedVariablesOnPlayerBattleContinues();
  			_mapEvent.SetOverrideWinner(BattleSideEnum.None);
  			if (_mapEvent.IsSiegeAssault && otherSide == _mapEvent.AttackerSide)
  			{
  				CampaignBattleResult campaignBattleResult = _campaignBattleResult;
  				if (campaignBattleResult != null && campaignBattleResult.EnemyRetreated)
  				{
> 					_mapEvent.FinishBattleAndKeepSiegeEvent();
  					_mapEvent = null;
  					GameMenu.ActivateGameMenu("menu_siege_strategies");
  				}
  			}
  			_campaignBattleResult = null;
  			_stateHandled = true;
  		}
  		Debug.Print("[PlayerEncounter.ContinueBattle End]");
  	}
  
  	private void DoWait()
  	{
  		SetEncounterMenuTexts();
> 		if (CheckIfBattleShouldContinueAfterBattleMission() && !IsNavalEncounterFinishedWithDisengage)
  		{
  			ContinueBattle();
  			return;
  		}
  		if (_mapEvent != null && _mapEvent.IsSiegeAssault)
  		{
  			_mapEvent.CheckIfOneSideHasLost();
  			_campaignBattleResult = CampaignBattleResult.GetResult(_mapEvent.BattleState);
  		}
  		if (_campaignBattleResult != null && _campaignBattleResult.BattleResolved)
  		{
  			if (_campaignBattleResult.PlayerVictory)
  			{
  				_mapEvent?.SetOverrideWinner(PartyBase.MainParty.Side);
  			}
  			else
  			{
  				bool flag = true;
  				if (_mapEvent != null && _mapEvent.IsHideoutBattle)
  				{
  					_mapEvent.MapEventSettlement.Hideout.SetNextPossibleAttackTime(Campaign.Current.Models.HideoutModel.HideoutHiddenDuration);
> 					if (_mapEvent.GetMapEventSide(PlayerSide).RecalculateMemberCountOfSide() > 0)
  					{
  						flag = false;
  					}
  				}
  				if (flag)
  				{
  					_mapEvent?.SetOverrideWinner(PartyBase.MainParty.OpponentSide);
  				}
  			}
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
  		else if (BattleSimulation != null && (BattleState == BattleState.AttackerVictory || BattleState == BattleState.DefenderVictory))
  		{
> 			if (_mapEvent.WinningSide == PlayerSide && Battle.RetreatingSide == BattleSideEnum.None)
  			{
  				EnemySurrender = true;
  			}
  			else
  			{
  				int totalManCount = MobileParty.MainParty.MemberRoster.TotalManCount;
  				int totalWounded = MobileParty.MainParty.MemberRoster.TotalWounded;
  				if (totalManCount - totalWounded == 0)
  				{
  					PlayerSurrender = true;
  				}
  			}
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
> 		else if (BattleSimulation != null && BattleSimulation.IsSimulationFinished && _mapEvent?.MapEventSettlement != null && BattleState == BattleState.None && _mapEvent.IsSiegeAssault && PlayerSiege.PlayerSiegeEvent != null)
  		{
  			_stateHandled = true;
> 			PlayerSiege.PlayerSiegeEvent.BreakSiegeEngine(PlayerSiege.PlayerSiegeEvent.GetSiegeEventSide(_mapEvent.PlayerSide), DefaultSiegeEngineTypes.Preparations);
  		}
  		else if (_mapEvent != null && (!_mapEvent.IsRaid || PlayerSurrender) && _mapEvent.HasWinner)
  		{
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
> 		else if (_mapEvent != null && _mapEvent.IsNavalMapEvent && _mapEvent.IsFieldBattle && _mapEvent.WinningSide == BattleSideEnum.None && (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide).AnyQ((MapEventParty x) => x.Party.Ships.Count == 0) || _mapEvent.PartiesOnSide(_mapEvent.PlayerSide.GetOppositeSide()).AnyQ((MapEventParty x) => x.Party.Ships.Count == 0)))
  		{
> 			IsNavalEncounterFinishedWithDisengage = false;
> 			if (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide).SumQ((MapEventParty x) => x.Party.Ships.Count) == 0)
  			{
> 				_mapEvent.SetOverrideWinner(_mapEvent.PlayerSide.GetOppositeSide());
  				EncounterState = PlayerEncounterState.PrepareResults;
  				return;
  			}
> 			if (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide.GetOppositeSide()).SumQ((MapEventParty x) => x.Party.Ships.Count) == 0)
  			{
> 				_mapEvent.SetOverrideWinner(_mapEvent.PlayerSide);
  				EncounterState = PlayerEncounterState.PrepareResults;
  				return;
  			}
  			List<MapEventParty> list = _mapEvent.AttackerSide.Parties.ToList();
  			list.AddRange(_mapEvent.DefenderSide.Parties);
  			if (MobileParty.MainParty.Ships.Count == 0)
  			{
  				PartyBase.MainParty.MapEventSide = null;
> 				Finish();
  				RaftStateChangeAction.ActivateRaftStateForParty(MobileParty.MainParty);
  			}
  			foreach (MapEventParty item in list)
  			{
  				if (item.Ships.Count != 0 || item.Party == PartyBase.MainParty)
  				{
  					continue;
  				}
  				item.Party.MapEventSide = null;
  				if (item.Party.IsMobile)
  				{
  					if (!item.Party.MobileParty.HasNavalNavigationCapability && item.Party.MobileParty.HasLandNavigationCapability)
  					{
  						RaftStateChangeAction.ActivateRaftStateForParty(item.Party.MobileParty);
  					}
  					else if (!item.Party.MobileParty.HasLandNavigationCapability)
  					{
  						DestroyPartyAction.Apply(null, item.Party.MobileParty);
  					}
  				}
  			}
  			_stateHandled = true;
  		}
  		else
  	public static bool CheckIfLeadingAvaliable()
  	{
  		bool flag = Hero.MainHero.PartyBelongedTo != null && !Hero.MainHero.IsWounded;
  		bool flag2 = Hero.MainHero.PartyBelongedTo != null && Hero.MainHero.PartyBelongedTo.Army != null && Hero.MainHero.PartyBelongedTo.Army.ArmyOwner != Hero.MainHero;
  		bool flag3 = false;
> 		foreach (MapEventParty item in MobileParty.MainParty.MapEvent.PartiesOnSide(MobileParty.MainParty.MapEvent.PlayerSide))
  		{
  			if (item.Party != MobileParty.MainParty.Party && item.Party.LeaderHero != null && item.Party.LeaderHero.Clan.Renown > Clan.PlayerClan.Renown)
  			{
  				flag3 = true;
  				break;
  			}
  		}
  		if (flag)
  		{
  			return flag2 || flag3;
  		}
  		return false;
  	}
  
  	public static Hero GetLeadingHero()
  	{
  		if (Hero.MainHero.PartyBelongedTo != null && Hero.MainHero.PartyBelongedTo.Army != null)
  		{
  			return MobileParty.MainParty.Army.ArmyOwner;
  		}
> 		foreach (MapEventParty item in MobileParty.MainParty.MapEvent.PartiesOnSide(MobileParty.MainParty.MapEvent.PlayerSide))
  		{
  			if (item.Party != MobileParty.MainParty.Party && item.Party.LeaderHero != null && item.Party.LeaderHero.Clan.Renown > Clan.PlayerClan.Renown)
  			{
  				return item.Party.LeaderHero;
  			}
  		}
  		return Hero.MainHero;
  	}
  
  	private void DoPrepareResults()
  	{
  		EncounterState = PlayerEncounterState.ApplyResults;
  	}
  
  	public static void SetPlayerVictorious()
  	{
  		Current.SetPlayerVictoriousInternal();
  	}
  
  	public void SetIsSallyOutAmbush(bool value)
  	{
  		if (Current._isSallyOutAmbush && !value)
  		{
  			_campaignBattleResult = null;
  		Current._isSiegeInterruptedByEnemyDefection = true;
  	}
  
  	private void SetPlayerVictoriousInternal()
  	{
> 		if (PlayerSide == BattleSideEnum.Attacker || PlayerSide == BattleSideEnum.Defender)
  		{
> 			_mapEvent.SetOverrideWinner(PlayerSide);
  		}
  	}
  
  	public static void SetPlayerSiegeContinueWithDefenderPullBack()
  	{
  		Current._mapEvent.SetDefenderPulledBack();
  	}
  
  	private void DoApplyMapEventResults()
  	{
> 		CampaignEventDispatcher.Instance.OnPlayerBattleEnd(_mapEvent);
  		_mapEvent.CalculateAndCommitMapEventResults();
> 		if (_mapEvent.WinningSide == PartyBase.MainParty.Side)
  		{
  			EncounterState = PlayerEncounterState.PlayerVictory;
  		}
> 		else if (_mapEvent.DefeatedSide == PartyBase.MainParty.Side && !IsNavalEncounterFinishedWithDisengage)
  		{
  			EncounterState = PlayerEncounterState.PlayerTotalDefeat;
  		}
  		else
  		{
  			EncounterState = PlayerEncounterState.End;
  		}
  	}
  
  	public static void StartAttackMission()
  	{
  		Current._campaignBattleResult = new CampaignBattleResult();
  	}
  
  	private void DoPlayerVictory()
  	{
  		if (_helpedHeroes != null)
  		{
  			if (_helpedHeroes.Count > 0)
  			{
  				if (_helpedHeroes[0].DeathMark == KillCharacterAction.KillCharacterActionDetail.None)
  				{
  					Campaign.Current.CurrentConversationContext = ConversationContext.PartyEncounter;
  					ConversationCharacterData playerCharacterData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
  
  	private void DoPlayerDefeat()
  	{
  		bool playerSurrender = PlayerSurrender;
  		bool endedByRetreat = _mapEvent.EndedByRetreat;
> 		Finish();
  		if (MobileParty.MainParty.BesiegerCamp != null)
  		{
  			if (MobileParty.MainParty.BesiegerCamp != null)
  			{
  				MobileParty.MainParty.BesiegerCamp = null;
  			}
  			else
  			{
  				PlayerSiege.FinalizePlayerSiege();
  			}
  		}
  		if (Hero.MainHero.DeathMark != KillCharacterAction.KillCharacterActionDetail.DiedInBattle && !endedByRetreat)
  		{
  			GameMenu.ActivateGameMenu(playerSurrender ? "taken_prisoner" : "defeated_and_taken_prisoner");
  		}
  		_stateHandled = true;
  	}
  
  	private void DoCaptureHeroes()
  	{
  		if (_capturedHeroes == null)
  		{
  			_capturedHeroes = RosterToReceiveLootPrisoners.RemoveIf((TroopRosterElement lordElement) => lordElement.Character.IsHero).ToList();
  		}
  		bool num = _mapEvent?.IsSiegeAssault ?? false;
  		bool flag = _mapEvent?.IsSallyOut ?? false;
  		bool isHideoutBattle = _mapEvent.IsHideoutBattle;
  		bool flag2 = num && MobileParty.MainParty.MapEvent != null && MobileParty.MainParty.MapEvent == _mapEvent;
  		bool flag3 = flag && MobileParty.MainParty.MapEvent != null && MobileParty.MainParty.MapEvent == _mapEvent;
> 		bool flag4 = MobileParty.MainParty.MapEvent != null && PlayerSide == BattleSideEnum.Attacker;
> 		bool flag5 = MobileParty.MainParty.MapEvent != null && PlayerSide == BattleSideEnum.Defender;
  		bool isRaid = _mapEvent.IsRaid;
  		bool isForcingVolunteers = _mapEvent.IsForcingVolunteers;
  		bool isForcingSupplies = _mapEvent.IsForcingSupplies;
  		bool isBlockadeSallyOut = _mapEvent.IsBlockadeSallyOut;
> 		bool flag6 = BattleSimulation != null && _mapEvent.WinningSide != PlayerSide;
  		Settlement mapEventSettlement = _mapEvent.MapEventSettlement;
  		BattleState battleState = _mapEvent.BattleState;
  		_stateHandled = true;
  		if (!flag6)
  		{
> 			Finish();
  		}
  		if (num || flag || isBlockadeSallyOut)
  		{
  			if (mapEventSettlement == null)
  			{
  				return;
  			}
  			if (flag2)
  			{
  				if (flag4)
  				{
  					EncounterManager.StartSettlementEncounter((MobileParty.MainParty.Army != null) ? MobileParty.MainParty.Army.LeaderParty : MobileParty.MainParty, mapEventSettlement);
  					GameMenu.SwitchToMenu("menu_settlement_taken");
  				}
  			}
  			else if (flag3)
  			{
  				if (flag5)
  				{
  					EncounterManager.StartSettlementEncounter((MobileParty.MainParty.Army != null) ? MobileParty.MainParty.Army.LeaderParty : MobileParty.MainParty, mapEventSettlement);
  					GameMenu.SwitchToMenu("menu_settlement_taken");
  				}
  			}
  			else if (isBlockadeSallyOut)
  				EncounterManager.StartSettlementEncounter(MobileParty.MainParty, mapEventSettlement);
  				GameMenu.SwitchToMenu("hideout_after_defeated_and_saved");
  				break;
  			}
  		}
> 		else if (IsNavalEncounterFinishedWithDisengage)
  		{
  			GameMenu.ActivateGameMenu("naval_encounter_disengaged");
  		}
  		else if (flag6)
  		{
  			EncounterState = PlayerEncounterState.Begin;
  			GameMenu.SwitchToMenu("encounter");
  		}
  	}
  
  	private bool CheckIfBattleShouldContinueAfterBattleMission()
  	{
  		if (_doesBattleContinue || _campaignBattleResult != null)
  		{
  			_doesBattleContinue = CheckIfBattleShouldContinueAfterBattleMissionInternal(_campaignBattleResult);
  		}
  		return _doesBattleContinue;
  	}
  
  	private bool CheckIfBattleShouldContinueAfterBattleMissionInternal(CampaignBattleResult campaignBattleResult)
  	{
> 		MapEventSide mapEventSide = _mapEvent.GetMapEventSide(PlayerSide);
  		if (PlayerSurrender || campaignBattleResult == null || (campaignBattleResult.EnemyRetreated && (!_mapEvent.IsSiegeAssault || mapEventSide.OtherSide != _mapEvent.AttackerSide)))
  		{
  			return false;
  		}
  		bool flag = _mapEvent.IsSiegeAssault && BattleState == BattleState.AttackerVictory;
  		bool flag2 = !_mapEvent.CheckIfOneSideHasLost();
  		if (_mapEvent.DefeatedSide != BattleSideEnum.None)
  		{
  			MapEventSide mapEventSide2 = _mapEvent.GetMapEventSide(_mapEvent.DefeatedSide);
  			bool num = campaignBattleResult.PlayerDefeat || campaignBattleResult.PlayerVictory || campaignBattleResult.EnemyPulledBack;
  			bool flag3 = mapEventSide2.GetTotalHealthyTroopCountOfSide() + mapEventSide2.GetTotalHealthyHeroCountOfSide() >= 1;
  			flag2 = num && flag3;
  			if (_mapEvent.IsNavalMapEvent && _mapEvent.IsFieldBattle && flag2)
  			{
> 				if (((_mapEvent.DefeatedSide != BattleSideEnum.None) ? _mapEvent.GetMapEventSide(_mapEvent.DefeatedSide).Parties : _mapEvent.PartiesOnSide(_mapEvent.PlayerSide)).SumQ((MapEventParty t) => t.Ships.Count) - CapturedShipsInEncounter.Count <= 0)
  				{
  					return false;
  				}
> 				if (((_mapEvent.DefeatedSide != BattleSideEnum.None) ? _mapEvent.GetMapEventSide(WinningSide).Parties : _mapEvent.GetMapEventSide(_mapEvent.PlayerSide.GetOppositeSide()).Parties).SumQ((MapEventParty t) => t.Ships.Count) == 0)
  				{
  					return false;
  				}
  				_mapEvent.RecalculateStrengthOfSides();
> 				float num2 = _mapEvent.StrengthOfSide[(int)_mapEvent.PlayerSide];
> 				float num3 = _mapEvent.StrengthOfSide[(int)_mapEvent.PlayerSide.GetOppositeSide()];
  				if ((PlayerIsAttacker && num2 * 0.45f >= num3) || (PlayerIsDefender && num2 <= num3 * 0.15f))
  				{
  					return false;
  				}
> 				IsNavalEncounterFinishedWithDisengage = true;
  				return false;
  			}
  		}
  		if (!_mapEvent.IsHideoutBattle && !flag && flag2)
  		{
  			return !mapEventSide.IsSurrendered;
  		}
  		return false;
  	}
  
  	public void FinalizeBattle()
  	{
  		if (_mapEvent != null)
  		{
> 			if (_mapEvent.HasWinner || _mapEvent.DiplomaticallyFinished || _mapEvent.IsSiegeAmbush || (_mapEvent.IsRaid && (_mapEvent.MapEventSettlement.SettlementHitPoints.ApproximatelyEqualsTo(0f) || (_mapEvent.PlayerSide != BattleSideEnum.None && _mapEvent.PartiesOnSide(_mapEvent.PlayerSide).Any((MapEventParty x) => x.Party.MobileParty != null && !x.Party.MobileParty.IsMainParty)))))
  			{
  				_mapEvent.FinalizeEvent();
  				_mapEvent = null;
  			}
  			else
  			{
  				LeaveBattle();
  			}
  		}
  	}
  
> 	public void FindNonAttachedNpcPartiesWhoWillJoinEvent(List<MobileParty> partiesToJoinPlayerSide, List<MobileParty> partiesToJoinEnemySide)
  	{
> 		Campaign.Current.Models.EncounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter(partiesToJoinPlayerSide, partiesToJoinEnemySide);
  	}
  
> 	public void FindAllNpcPartiesWhoWillJoinEvent(List<MobileParty> partiesToJoinPlayerSide, List<MobileParty> partiesToJoinEnemySide)
  	{
> 		FindNonAttachedNpcPartiesWhoWillJoinEvent(partiesToJoinPlayerSide, partiesToJoinEnemySide);
> 		foreach (MobileParty item in partiesToJoinPlayerSide.ToList())
  		{
> 			partiesToJoinPlayerSide.AddRange(item.AttachedParties.Except(partiesToJoinPlayerSide));
  		}
  		foreach (MobileParty item2 in partiesToJoinEnemySide.ToList())
  		{
  			partiesToJoinEnemySide.AddRange(item2.AttachedParties.Except(partiesToJoinEnemySide));
  		}
  	}
  
  	public static void EnterSettlement()
  	{
  		Settlement encounterSettlement = EncounterSettlement;
  		CreateLocationEncounter(encounterSettlement);
  		EnterSettlementAction.ApplyForParty(MobileParty.MainParty, encounterSettlement);
  	}
  
  	private static void CreateLocationEncounter(Settlement settlement)
  	{
  		if (settlement.IsTown)
  		{
  			LocationEncounter = new TownEncounter(settlement);
  		}
  		else if (settlement.IsVillage)
  		{
  			LocationEncounter = new VillageEncounter(settlement);
  		}
  		LeaveSettlementAction.ApplyForParty(MobileParty.MainParty);
  		LocationEncounter = null;
  		PartyBase.MainParty.SetVisualAsDirty();
  	}
  
> 	public static void InitSimulation(FlattenedTroopRoster selectedTroopsForPlayerSide, FlattenedTroopRoster selectedTroopsForOtherSide)
  	{
  		if (Current != null)
  		{
> 			Current.BattleSimulation = new BattleSimulation(selectedTroopsForPlayerSide, selectedTroopsForOtherSide);
  			Current.BattleSimulation.ResetSimulation();
  		}
  	}
  
  	public void InterruptEncounter(string encounterInterrupedType)
  	{
  		_ = Game.Current.GameStateManager.ActiveState;
  		if (MapEvent.PlayerMapEvent != null)
  		{
  			LeaveBattle();
  		}
  		GameMenu.ActivateGameMenu(encounterInterrupedType);
  	}
  
  	public static void StartSiegeAmbushMission()
  	{
  		Settlement mapEventSettlement = Battle.MapEventSettlement;
  		SiegeEvent playerSiegeEvent = PlayerSiege.PlayerSiegeEvent;
  		switch (mapEventSettlement.CurrentSiegeState)
  		{
  		case Settlement.SiegeState.OnTheWalls:
  		{
  			List<MissionSiegeWeapon> preparedAndActiveSiegeEngines = playerSiegeEvent.GetPreparedAndActiveSiegeEngines(playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Attacker));
  			List<MissionSiegeWeapon> preparedAndActiveSiegeEngines2 = playerSiegeEvent.GetPreparedAndActiveSiegeEngines(playerSiegeEvent.GetSiegeEventSide(BattleSideEnum.Defender));
  			bool hasAnySiegeTower = preparedAndActiveSiegeEngines.Exists((MissionSiegeWeapon data) => data.Type == DefaultSiegeEngineTypes.SiegeTower);
  			int wallLevel = mapEventSettlement.Town.GetWallLevel();
> 			CampaignMission.OpenSiegeMissionWithDeployment(mapEventSettlement.LocationComplex.GetLocationWithId("center").GetSceneName(wallLevel), mapEventSettlement.SettlementWallSectionHitPointsRatioList.ToArray(), hasAnySiegeTower, preparedAndActiveSiegeEngines, preparedAndActiveSiegeEngines2, Current.PlayerSide == BattleSideEnum.Attacker, wallLevel, isSallyOut: true);
  			break;
  		}
  		case Settlement.SiegeState.InTheLordsHall:
  		case Settlement.SiegeState.Invalid:
  			Debug.FailedAssert("Siege state is invalid!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Encounters\\PlayerEncounter.cs", "StartSiegeAmbushMission", 2266);
  			break;
  		}
  	}
  
  	public static void StartVillageBattleMission()
  	{
  		Settlement mapEventSettlement = Battle.MapEventSettlement;
  		int upgradeLevel = ((!mapEventSettlement.IsTown) ? 1 : mapEventSettlement.Town.GetWallLevel());
  		CampaignMission.OpenBattleMission(mapEventSettlement.LocationComplex.GetScene("village_center", upgradeLevel), usesTownDecalAtlas: false, "land_raid");
  	}
  
  	public static void StartCombatMissionWithDialogueInTownCenter(CharacterObject characterToTalkTo)
  	{
  		int wallLevel = Settlement.CurrentSettlement.Town.GetWallLevel();
  		CampaignMission.OpenCombatMissionWithDialogue(Settlement.CurrentSettlement.LocationComplex.GetScene("center", wallLevel), characterToTalkTo, wallLevel);
  	}
  
  	public static void StartHostileAction()
  	{
  		}
  	}
  
  	public void GetBattleRewards(out ExplainedNumber renownChange, out ExplainedNumber influenceChange, out ExplainedNumber moraleChange, out float playerEarnedLootRate, out Figurehead playerEarnedFigurehead)
  	{
> 		MapEventParty mapEventParty = _mapEvent.PartiesOnSide(_mapEvent.PlayerSide).Find((MapEventParty x) => x.Party == PartyBase.MainParty);
  		renownChange = mapEventParty.GainedRenownExplained;
  		influenceChange = mapEventParty.GainedInfluenceExplained;
  		moraleChange = mapEventParty.GainedMoraleExplained;
  		playerEarnedFigurehead = PlayerLootedFigurehead;
  		playerEarnedLootRate = _mapEvent.GetPlayerBattleContributionRate();
  	}
  }

 succeeded in 1716ms:
  	private CampaignTime _mapEventStartTime;
  
  	[SaveableField(110)]
> 	private BattleTypes _mapEventType;
  
  	[CachedData]
  	private TerrainType _eventTerrainType;
  
  	[CachedData]
  	public IMapEventVisual MapEventVisual;
  
  	private bool _playerFigureheadCalculated;
  
  	private bool _mapEventResultsApplied;
  
  	private bool _mapEventResultsCalculated;
  
  	public static MapEvent PlayerMapEvent => MobileParty.MainParty?.MapEvent;
  
> 	public BattleSideEnum PlayerSide => PartyBase.MainParty.Side;
  
  	internal IBattleObserver BattleObserver { get; set; }
  
  	[SaveableProperty(105)]
  	public MapEventComponent Component { get; private set; }
  
  	public MapEventState State
  	{
  		get
  		{
  			return _state;
  		}
  		{
  			if (_state != value)
  			{
> 				if (IsPlayerMapEvent)
  				{
  					Debug.Print("Player MapEvent State: " + value);
  				}
  				_state = value;
  			}
  		}
  	}
  
  	public MapEventSide AttackerSide => _sides[1];
  
  	public MapEventSide DefenderSide => _sides[0];
  
  			{
  				return Component.SimulationContext;
  			}
> 			if (_mapEventType == BattleTypes.Siege)
  			{
  				return PowerCalculationContext.Siege;
  			}
  			return Campaign.Current.Models.MilitaryPowerModel.GetContextForPosition(Position);
  		}
  	}
  
  	[SaveableProperty(118)]
  	public CampaignVec2 Position { get; private set; }
  
> 	public BattleTypes EventType => _mapEventType;
  
  	public TerrainType EventTerrainType => _eventTerrainType;
  
  	[SaveableProperty(113)]
  	public bool IsInvulnerable { get; set; }
  
> 	public bool IsFieldBattle => _mapEventType == BattleTypes.FieldBattle;
  
> 	public bool IsRaid => _mapEventType == BattleTypes.Raid;
  
> 	public bool IsForcingVolunteers => _mapEventType == BattleTypes.IsForcingVolunteers;
  
> 	public bool IsForcingSupplies => _mapEventType == BattleTypes.IsForcingSupplies;
  
> 	public bool IsSiegeAssault => _mapEventType == BattleTypes.Siege;
  
> 	public bool IsHideoutBattle => _mapEventType == BattleTypes.Hideout;
  
> 	public bool IsSallyOut => _mapEventType == BattleTypes.SallyOut;
  
> 	public bool IsSiegeOutside => _mapEventType == BattleTypes.SiegeOutside;
  
> 	public bool IsBlockade => _mapEventType == BattleTypes.BlockadeBattle;
  
> 	public bool IsBlockadeSallyOut => _mapEventType == BattleTypes.BlockadeSallyOutBattle;
  
  	public bool IsSiegeAmbush => Component is SiegeAmbushEventComponent;
  
  	public bool IsFinalized => _state == MapEventState.WaitingRemoval;
  
  	public CampaignTime BattleStartTime => _mapEventStartTime;
  
> 	public bool HasWinner
  	{
  		get
  		{
  			if (BattleState != BattleState.AttackerVictory)
  			{
  				return BattleState == BattleState.DefenderVictory;
  			}
  			return true;
  		}
  	}
  
  	[SaveableProperty(123)]
> 	public bool IsPlayerSimulation { get; set; }
  
  	[SaveableProperty(126)]
  	public MBList<BattleSideEnum> WonRounds { get; private set; } = new MBList<BattleSideEnum>();
  
  	public bool IsNavalMapEvent => !Position.IsOnLand;
  
  	public bool WasEverInLootingPhase
  	{
  		get
  		{
  			return _wasEverInLootingPhase;
  		}
  		}
  	}
  
> 	public bool IsPlayerMapEvent => this == PlayerMapEvent;
  
  	public BattleState BattleState
  	{
  		get
  		{
  			return _battleState;
  		}
  		internal set
  		{
  			if (value != _battleState)
  			{
> 				if (IsPlayerMapEvent)
  				{
  					Debug.Print("Player MapEvent BattleState: " + value);
  				}
  				_battleState = value;
  				if (_battleState == BattleState.AttackerVictory || _battleState == BattleState.DefenderVictory)
  				{
  					OnBattleWon();
  				}
  			}
  		}
  	}
  
> 	public BattleSideEnum WinningSide
  	{
  		get
  		{
  			if (BattleState != BattleState.AttackerVictory)
  			{
  				if (BattleState != BattleState.DefenderVictory)
  				{
  					return BattleSideEnum.None;
  				}
  				return BattleSideEnum.Defender;
  			}
  			return BattleSideEnum.Attacker;
  		return ((MapEvent)o).IsInvulnerable;
  	}
  
> 	internal static object AutoGeneratedGetMemberValueIsPlayerSimulation(object o)
  	{
> 		return ((MapEvent)o).IsPlayerSimulation;
  	}
  
  	internal static object AutoGeneratedGetMemberValueWonRounds(object o)
  	{
  		return ((MapEvent)o).WonRounds;
  	}
  
  	internal static object AutoGeneratedGetMemberValueStrengthOfSide(object o)
  	{
  		return ((MapEvent)o).StrengthOfSide;
  	}
  
  		return ((MapEvent)o)._mapEventStartTime;
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_mapEventType(object o)
  	{
> 		return ((MapEvent)o)._mapEventType;
  	}
  
  	internal static object AutoGeneratedGetMemberValueFirstUpdateIsDone(object o)
  	{
  		return ((MapEvent)o).FirstUpdateIsDone;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_wasEverInLootingPhase(object o)
  	{
  		return ((MapEvent)o)._wasEverInLootingPhase;
  	}
  
  	{
  		if (Component == null && MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.0"))
  		{
> 			if (_mapEventType == BattleTypes.Raid)
  			{
  				float nextSettlementDamage = (float)objectLoadData.GetMemberValueBySaveId(109);
  				int lootedItemCount = (int)objectLoadData.GetMemberValueBySaveId(112);
  				float raidDamage = (float)objectLoadData.GetMemberValueBySaveId(115);
  				Component = RaidEventComponent.CreateComponentForOldSaves(this, nextSettlementDamage, lootedItemCount, raidDamage);
  			}
> 			else if (_mapEventType == BattleTypes.IsForcingSupplies)
  			{
  				Component = ForceSuppliesEventComponent.CreateComponentForOldSaves(this);
  			}
> 			else if (_mapEventType == BattleTypes.IsForcingVolunteers)
  			{
  				Component = ForceVolunteersEventComponent.CreateComponentForOldSaves(this);
  			}
> 			else if (_mapEventType == BattleTypes.Hideout)
  			{
  				Component = HideoutEventComponent.CreateComponentForOldSaves(this, isSendTroops: false);
  			}
> 			else if (_mapEventType == BattleTypes.FieldBattle)
  			{
  				Component = FieldBattleEventComponent.CreateComponentForOldSaves(this);
  			}
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0")))
  		{
  			WonRounds = new MBList<BattleSideEnum>();
  			RetreatingSide = BattleSideEnum.None;
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0")))
  		{
  			Vec2 pos = (Vec2)objectLoadData.GetMemberValueBySaveId(111);
  				}
  			}
  		}
> 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0")) && !IsPlayerMapEvent)
  		{
  			CommitXpGains();
  		}
> 		if (((MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.4.1")) || Game.Current.IsDevelopmentMode) && IsRaid && MobileParty.MainParty.MapEvent == this && PlayerSide == BattleSideEnum.Attacker && MapEventSettlement.LastAttackerParty != MobileParty.MainParty)
  		{
  			DiplomaticallyFinished = true;
  			PlayerEncounter.Finish();
  		}
  	}
  
  	private void AddInsideSettlementParties(Settlement relatedSettlement)
  	{
  		List<PartyBase> list = new List<PartyBase>();
> 		foreach (PartyBase item in relatedSettlement.GetInvolvedPartiesForEventType(_mapEventType))
  		{
  			if (item != PartyBase.MainParty && item.MobileParty?.AttachedTo != MobileParty.MainParty)
  			{
  				list.Add(item);
  			}
  		}
  		foreach (PartyBase item2 in list)
  		{
  			if (MapEventSettlement.SiegeEvent != null)
  			{
  				if (MapEventSettlement.SiegeEvent.CanPartyJoinSide(item2, BattleSideEnum.Defender))
  				{
  			TroopUpgradeTracker?.AddParty(mapEventParty);
  		}
  		PartyBase party = mapEventParty.Party;
> 		if (IsSiegeAssault && party.MobileParty != null && party.MobileParty.CurrentSettlement == null && side == BattleSideEnum.Defender)
  		{
> 			_mapEventType = BattleTypes.SiegeOutside;
  		}
  		if (party.MobileParty != null && party.MobileParty.IsGarrison && side == BattleSideEnum.Attacker && (IsSiegeOutside || IsBlockade))
  		{
> 			_mapEventType = (IsSiegeOutside ? BattleTypes.SallyOut : BattleTypes.BlockadeSallyOutBattle);
  			MapEventSettlement = party.MobileParty.CurrentSettlement;
  		}
> 		if (party == MobileParty.MainParty.Party && !IsSiegeAssault && !IsRaid && !IsForcingSupplies && !IsForcingVolunteers)
  		{
  			party.MobileParty.SetMoveModeHold();
  		}
  		if (party == PartyBase.MainParty)
  		{
  			party.MobileParty.ForceAiNoPathMode = false;
  		}
  		RecalculateRenownAndInfluenceValuesOnPartyInvolved(party);
> 		if (IsFieldBattle && party.IsMobile && party.MobileParty.IsCurrentlyAtSea == IsNavalMapEvent && party.MobileParty.BesiegedSettlement == null)
  		{
  			int sideIndex = GetMapEventSide(side).Parties.Count((MapEventParty p) => p.Party.IsMobile) - 1;
  			SetPartyBaseEventLocalPosition(party, side, sideIndex);
  		}
  		party.SetVisualAsDirty();
  		if (party.IsMobile && party.MobileParty.Army != null && party.MobileParty.Army.LeaderParty == party.MobileParty)
  		{
  			foreach (MobileParty attachedParty in party.MobileParty.Army.LeaderParty.AttachedParties)
  			{
  				attachedParty.Party.SetVisualAsDirty();
  			}
  		}
> 		if (HasWinner && party.MapEventSide.MissionSide != WinningSide && party.NumberOfHealthyMembers > 0)
  		{
  			BattleState = BattleState.None;
  		}
  		if (party.IsVisible)
  		{
  			IsVisible = true;
  		}
  		ResetUnsuitablePartiesThatWereTargetingThisMapEvent();
  		Component?.OnPartyAdded(party);
  		CampaignEventDispatcher.Instance.OnPartyAddedToMapEvent(party);
  	}
  
  		IsVisible = isVisible;
  	}
  
> 	internal void Initialize(PartyBase attackerParty, PartyBase defenderParty, MapEventComponent component = null, BattleTypes mapEventType = BattleTypes.None)
  	{
  		Component = component;
  		FirstUpdateIsDone = false;
  		RetreatingSide = BattleSideEnum.None;
  		PursuitRoundNumber = 0;
  		MapEventSettlement = null;
> 		_mapEventType = mapEventType;
  		_sides[0] = new MapEventSide(this, BattleSideEnum.Defender, defenderParty);
  		_sides[1] = new MapEventSide(this, BattleSideEnum.Attacker, attackerParty);
  		if (attackerParty.MobileParty == MobileParty.MainParty || defenderParty.MobileParty == MobileParty.MainParty)
  		{
> 			if (mapEventType == BattleTypes.Raid)
  			{
  				Debug.Print(string.Concat("A raid mapEvent has been started on ", defenderParty.Name, "\n"), 0, Debug.DebugColor.DarkGreen, 64uL);
  			}
  			else if (defenderParty.IsSettlement && defenderParty.Settlement.IsFortification)
  			{
  				Debug.Print(string.Concat("A siege mapEvent has been started on ", defenderParty.Name, "\n"), 0, Debug.DebugColor.DarkCyan, 64uL);
  			}
  		}
  		if (attackerParty.IsMobile && attackerParty.MobileParty.CurrentSettlement != null)
  		{
  			MapEventSettlement = attackerParty.MobileParty.CurrentSettlement;
  		}
  		else
  		{
  			Position = ((IsRaid || IsForcingSupplies || IsForcingVolunteers) ? defenderParty.Position : attackerParty.Position);
> 			if (IsFieldBattle)
  			{
  				MapEventSettlement = null;
  				if (attackerParty.Position.IsOnLand != defenderParty.Position.IsOnLand || (attackerParty.Position.IsOnLand && (attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty)))
  				{
  					float settlementBeingNearFieldBattleRadius = Campaign.Current.Models.EncounterModel.GetSettlementBeingNearFieldBattleRadius;
  					MobileParty mobileParty = attackerParty.MobileParty;
  					PartyBase landParty = ((mobileParty != null && mobileParty.IsCurrentlyAtSea) ? defenderParty : attackerParty);
  					Village village = SettlementHelper.FindNearestVillageToMobileParty(landParty.MobileParty, MobileParty.NavigationType.Default, (Settlement x) => x.Position.DistanceSquared(landParty.Position) < settlementBeingNearFieldBattleRadius * settlementBeingNearFieldBattleRadius);
  					if (village != null)
  					{
  						MapEventSettlement = village.Settlement;
  						if (attackerParty.MobileParty.IsCurrentlyAtSea == defenderParty.MobileParty.IsCurrentlyAtSea)
  		CacheSimulationData();
  		attackerParty.MapEventSide = AttackerSide;
  		defenderParty.MapEventSide = DefenderSide;
> 		if (MapEventSettlement != null && (mapEventType == BattleTypes.Siege || mapEventType == BattleTypes.SiegeOutside || mapEventType == BattleTypes.SallyOut || IsSiegeAmbush))
  		{
> 			foreach (PartyBase item in MapEventSettlement.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(mapEventType))
  			{
  				if (item.MapEventSide == null && (item != PartyBase.MainParty || item.MobileParty.Army != null) && (item.MobileParty.Army == null || item.MobileParty.Army.LeaderParty == item.MobileParty))
  				{
> 					item.MapEventSide = ((mapEventType == BattleTypes.SallyOut) ? defenderParty.MapEventSide : attackerParty.MapEventSide);
  				}
  			}
  		}
  		if (defenderParty.IsMobile && defenderParty.MobileParty.BesiegedSettlement != null)
  		{
> 			List<PartyBase> involvedPartiesForEventType = defenderParty.MobileParty.SiegeEvent.GetInvolvedPartiesForEventType(_mapEventType);
> 			PartyBase partyBase = (IsSiegeAssault ? attackerParty : defenderParty);
  			foreach (PartyBase item2 in involvedPartiesForEventType)
  			{
  				if (item2 != partyBase && item2.IsMobile && item2 != PartyBase.MainParty && item2.MobileParty.BesiegedSettlement == defenderParty.MobileParty.BesiegedSettlement && (item2.MobileParty.Army == null || item2.MobileParty.Army.LeaderParty == item2.MobileParty))
  				{
  					item2.MapEventSide = DefenderSide;
  				}
  			}
  		}
  		State = MapEventState.Wait;
  		_mapEventStartTime = CampaignTime.Now;
  		_nextSimulationTime = CalculateNextSimulationTime();
  		if (MapEventSettlement != null && !IsBlockade)
  				attachedParty.Party.SetVisualAsDirty();
  			}
  		}
> 		if (IsFieldBattle && party.IsMobile && party.MobileParty.BesiegedSettlement == null)
  		{
  			party.MobileParty.EventPositionAdder = Vec2.Zero;
  			MapEventSide[] sides = _sides;
  			foreach (MapEventSide mapEventSide in sides)
  			{
  				for (int j = 0; j < mapEventSide.Parties.Count; j++)
  				{
  					if (mapEventSide.Parties[j].Party.IsMobile && mapEventSide.Parties[j].Party != mapEventSide.LeaderParty)
  					{
  						SetPartyBaseEventLocalPosition(mapEventSide.Parties[j].Party, mapEventSide.MissionSide, j);
  					}
  				}
  			MapEventSide mapEventSide2 = ((MapEventSettlement != null) ? DefenderSide : AttackerSide);
  			if (mapEventSide2.Parties.All((MapEventParty x) => x.Party.MobileParty == null || (MapEventSettlement != null && x.Party.MobileParty.CurrentSettlement == MapEventSettlement)) && MapEventSettlement != null)
  			{
> 				_mapEventType = BattleTypes.Siege;
  			}
  		}
  		if (party == PartyBase.MainParty && State == MapEventState.Wait)
  		{
  			AttackerSide.RemoveNearbyPartiesFromPlayerMapEvent();
  			DefenderSide.RemoveNearbyPartiesFromPlayerMapEvent();
  		}
  		if (party.IsVisible)
  		{
  			PartyVisibilityChanged(party, isPartyVisible: false);
  		}
  		ResetUnsuitablePartiesThatWereTargetingThisMapEvent();
  					break;
  				}
  				bool flag2 = SimulateSingleTroopHit(strikerSide.MissionSide, advantage, strikerSideMorale, struckSideMorale);
> 				_ = IsPlayerSimulation && flag2;
  			}
  		}
  		return flag;
  	}
  
  	private bool SimulateSingleTroopHit(BattleSideEnum side, float strikerAdvantage, float strikerSideMorale, float struckSideMorale)
  	{
  		MapEventSide mapEventSide = _sides[(int)side];
  		MapEventSide mapEventSide2 = _sides[(int)(1 - side)];
  		UniqueTroopDescriptor uniqueTroopDescriptor = mapEventSide.SelectRandomSimulationTroop();
  		UniqueTroopDescriptor uniqueTroopDescriptor2 = mapEventSide2.SelectRandomSimulationTroop();
  		CharacterObject allocatedTroop = mapEventSide.GetAllocatedTroop(uniqueTroopDescriptor);
  		bool flag = false;
  		if (num > 0)
  		{
> 			if (IsPlayerSimulation && allocatedTroopParty2 == PartyBase.MainParty)
  			{
  				float playerTroopsReceivedDamageMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  				num = MBRandom.RoundRandomized((float)num * playerTroopsReceivedDamageMultiplier);
  			}
  			DamageTypes damageType = ((MBRandom.RandomFloat < Campaign.Current.Models.CombatSimulationModel.GetBluntDamageChance(allocatedTroop, allocatedTroop2, allocatedTroopParty, allocatedTroopParty2, this)) ? DamageTypes.Blunt : DamageTypes.Cut);
  			flag = mapEventSide2.ApplySimulationDamageToSelectedTroop(num, damageType, allocatedTroopParty);
  			mapEventSide.ApplySimulatedHitRewardToSelectedTroop(allocatedTroop, allocatedTroop2, num, flag);
> 			if (IsPlayerSimulation && allocatedTroopParty == PartyBase.MainParty && flag)
  			{
  				CampaignEventDispatcher.Instance.OnPlayerPartyKnockedOrKilledTroop(allocatedTroop2);
  			}
  		}
  		return flag;
  	}
  
  	public void FinishBattleAndKeepSiegeEvent()
  	{
  		_keepSiegeEvent = true;
  		AttackerSide.LeaderParty.MobileParty.RecalculateShortTermBehavior();
  		FinishBattle();
  		if (!DiplomaticallyFinished)
  		{
  			Component?.Update(ref finish);
> 			if (((DefenderSide.TroopCount > 0 && AttackerSide.TroopCount > 0) || (!FirstUpdateIsDone && (DefenderSide.TroopCount > 0 || _mapEventType != BattleTypes.Raid))) && _nextSimulationTime.IsPast)
  			{
  				CheckRunAway();
  				SimulateBattleSessionForMapEvent();
  				_nextSimulationTime = CalculateNextSimulationTime();
  				FirstUpdateIsDone = true;
  				finish = RetreatingSide != BattleSideEnum.None && PursuitRoundNumber == 0;
  			}
> 			if ((_mapEventType != BattleTypes.Raid || DefenderSide.Parties.Count > 1) && BattleState != BattleState.None)
  			{
  				finish = true;
  			}
  		}
  		else
  		{
  			finish = true;
  			foreach (PartyBase involvedParty in InvolvedParties)
  			{
  				if (involvedParty.IsMobile && involvedParty.MobileParty != MobileParty.MainParty && (involvedParty.MobileParty.Army == null || involvedParty.MobileParty.Army.LeaderParty == involvedParty.MobileParty))
  				{
  					involvedParty.MobileParty.RecalculateShortTermBehavior();
  		if (finish)
  		{
  			Component?.FinishComponent();
> 			if (!IsPlayerMapEvent || PlayerEncounter.Current == null)
  			{
  				FinishBattle();
  			}
  		}
  	}
  
  	private void OnBattleWon()
  	{
  		CalculateMapEventResults();
> 		if (!IsPlayerMapEvent)
  		{
  			CalculateAndCommitMapEventResults();
  		}
  		BattleObserver?.BattleResultsReady();
  	}
  
  	public void SetOverrideWinner(BattleSideEnum winner)
  	{
  		BattleState = winner switch
  		{
  			BattleSideEnum.Defender => BattleState.DefenderVictory, 
  			BattleSideEnum.Attacker => BattleState.AttackerVictory, 
  
  	public void SimulateBattleSetup(FlattenedTroopRoster[] priorTroops)
  	{
> 		if (IsSiegeAssault)
  		{
  			CheckSiegeStageChange();
  		}
  		MapEventSide[] sides = _sides;
  		foreach (MapEventSide mapEventSide in sides)
  		{
  			FlattenedTroopRoster flattenedTroopRoster = ((priorTroops != null) ? priorTroops[(int)mapEventSide.MissionSide] : null);
  			int sizeOfSide = flattenedTroopRoster?.Count() ?? Campaign.Current.Models.CombatSimulationModel.GetParticipatingTroopCount(mapEventSide);
  			mapEventSide.MakeReadyForSimulation(flattenedTroopRoster, sizeOfSide);
  		}
  		_battleState = BattleState.None;
  	}
  
  	private void CheckSiegeStageChange()
  	{
> 		if (MapEventSettlement != null && IsSiegeAssault)
  		{
  			int num = AttackerSide.Parties.Sum((MapEventParty party) => party.Party.NumberOfHealthyMembers);
  			int num2 = DefenderSide.Parties.Sum((MapEventParty party) => party.Party.NumberOfHealthyMembers);
  			if (num == 0)
  			{
  			}
  		}
  	}
  
  	private void FinishBattle()
  	{
  		_isFinishCalled = true;
  			CalculateWinner(out showResults, sideMorale, sideMorale2);
  			num++;
  		}
> 		if (!HasWinner && PursuitRoundNumber > 0)
  		{
  			PursuitRoundNumber--;
  			if (PursuitRoundNumber == 0)
  			{
  				EndByRunAway();
  			}
  		}
  		if (showResults)
  		{
  			BattleObserver?.BattleResultsReady();
  		}
  		ApplyRoundEffects(troopCasualties, troopCasualties2, shipCasualties, shipCasualties2);
  
  	public bool IsPlayerSergeant()
  	{
> 		if (IsPlayerMapEvent && GetLeaderParty(PlayerSide) != PartyBase.MainParty && MobileParty.MainParty.Army != null)
  		{
  			return MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;
  		}
  		return false;
  	}
  
  	private void CheckSideRunAway(MapEventSide mapEventSide)
  	{
  		if (RetreatingSide == BattleSideEnum.None && Campaign.Current.Models.EncounterModel.GetMapEventSideRunAwayChance(mapEventSide) > MBRandom.RandomFloat)
  		{
  			RetreatingSide = mapEventSide.MissionSide;
  			PursuitRoundNumber = Campaign.Current.Models.CombatSimulationModel.GetPursuitRoundCount(this);
  		if (BattleState == BattleState.AttackerVictory || BattleState == BattleState.DefenderVictory)
  		{
  			MBList<MapEventParty> defeatedParties = GetMapEventSide(DefeatedSide).Parties.ToMBList();
> 			MBList<MapEventParty> winnerParties = GetMapEventSide(WinningSide).Parties.ToMBList();
> 			if (IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage)
  			{
  				LootDefeatedPartyShips(winnerParties, defeatedParties);
  			}
  			else
  			{
  				LootDefeatedPartyCasualties(winnerParties, defeatedParties);
  				LootDefeatedPartyItems(winnerParties, defeatedParties);
  				LootDefeatedPartyPrisoners(winnerParties, defeatedParties);
  				LootDefeatedPartyShips(winnerParties, defeatedParties);
  				CaptureDefeatedPartyMembers(winnerParties, defeatedParties);
  			}
  			CommitCalculatedMapEventResults();
  		if (BattleState == BattleState.AttackerVictory || BattleState == BattleState.DefenderVictory)
  		{
  			MBList<MapEventParty> defeatedParties = GetMapEventSide(DefeatedSide).Parties.ToMBList();
> 			MBList<MapEventParty> winnerParties = GetMapEventSide(WinningSide).Parties.ToMBList();
  			CalculatePlunderedAndLostGoldAmounts(winnerParties, defeatedParties);
  			CalculatePlayerFigureheadShare(defeatedParties, GetMapEventSide(DefeatedSide).LeaderParty);
  			CalculateWinnerPartiesRenownInfluenceAndMoraleShares(winnerParties, defeatedParties);
  		}
  	}
  
  	public bool CanPartyJoinBattle(PartyBase party, BattleSideEnum side)
  	{
  		if (GetMapEventSide(side).Parties.All((MapEventParty x) => x.Party.IsActive && !x.Party.MapFaction.IsAtWarWith(party.MapFaction)))
  		{
  			return GetMapEventSide(GetOtherSide(side)).Parties.All((MapEventParty x) => x.Party.IsActive && x.Party.MapFaction.IsAtWarWith(party.MapFaction));
  		}
  
  	private void CalculatePlayerFigureheadShare(MBList<MapEventParty> defeatedParties, PartyBase defeatedLeaderParty)
  	{
> 		if (IsPlayerMapEvent && IsNavalMapEvent && WinningSide == PlayerSide && !_playerFigureheadCalculated)
  		{
  			_playerFigureheadCalculated = true;
  			Figurehead figureheadLoot = Campaign.Current.Models.BattleRewardModel.GetFigureheadLoot(defeatedParties, defeatedLeaderParty);
  			PlayerEncounter.Current.PlayerLootedFigurehead = figureheadLoot;
  		}
  	}
  
  	public float GetPlayerBattleContributionRate()
  	{
  		float result = 0f;
> 		foreach (MapEventParty item in PartiesOnSide(PlayerSide))
  		{
  			if (item.Party == PartyBase.MainParty)
  			{
> 				int num = GetMapEventSide(PlayerSide).CalculateTotalContribution();
  				result = (float)item.ContributionToBattle / (float)num;
  				break;
  			}
  		}
  		return result;
  	}
  
  	private void CalculatePlunderedAndLostGoldAmounts(MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
  	{
  		if (IsSiegeAmbush)
  		{
  			return;
  	private void LootDefeatedPartyCasualties(MBReadOnlyList<MapEventParty> winnerParties, MBReadOnlyList<MapEventParty> defeatedParties)
  	{
  		float aITradePenalty = Campaign.Current.Models.BattleRewardModel.GetAITradePenalty();
> 		bool flag = IsPlayerMapEvent && PlayerSide == WinningSide;
  		float f = float.MinValue;
  		ItemRoster itemRoster = null;
  		MapEventParty playerBattleParty = (flag ? winnerParties.Find((MapEventParty x) => x.Party == PartyBase.MainParty) : null);
  		foreach (MapEventParty defeatedParty in defeatedParties)
  		{
  			if (defeatedParty.DiedInBattle.Count <= 0 && defeatedParty.WoundedInBattle.Count <= 0)
  			{
  				continue;
  			}
  			PartyBase party = defeatedParty.Party;
  			MBReadOnlyList<KeyValuePair<MapEventParty, float>> lootCasualtyChances = Campaign.Current.Models.BattleRewardModel.GetLootCasualtyChances(winnerParties, party);
  			if (flag)
  			return;
  		}
  		MBList<Ship> mBList = new MBList<Ship>();
> 		if (IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage)
  		{
  			foreach (Ship item in PlayerEncounter.Current.CapturedShipsInEncounter.ToList())
  			{
  				if (item.HitPoints > 0f)
  				{
  					mBList.Add(item);
  				}
  			}
  		}
  		else
  		{
  			foreach (MapEventParty defeatedParty in defeatedParties)
  	{
  		float getEncounterJoiningRadius = Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius;
  		MobileParty.NavigationType navigationCapability = (partyToTeleport.Position.IsOnLand ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval);
> 		PartyBase partyBase = (IsPlayerMapEvent ? PartyBase.MainParty : Winner.LeaderParty);
  		CampaignVec2 position = partyBase.Position;
  		if (partyToTeleport.Position.IsOnLand != partyBase.Position.IsOnLand)
  		{
  			position = partyToTeleport.Position;
  		}
  		partyToTeleport.Position = NavigationHelper.FindReachablePointAroundPosition(position, navigationCapability, getEncounterJoiningRadius * 1.6f, getEncounterJoiningRadius * 1.2f);
  		foreach (MobileParty attachedParty in partyToTeleport.AttachedParties)
  		{
  			attachedParty.Position = partyToTeleport.Position;
  		}
  	}
  
  		bool flag = false;
  		if (MapEventSettlement != null)
  		{
> 			if (BattleState != BattleState.None && (IsSiegeAssault || IsSiegeOutside || IsSallyOut || IsBlockadeSallyOut || IsBlockade) && MapEventSettlement.SiegeEvent != null)
  			{
> 				MapEventSettlement.SiegeEvent.OnBeforeSiegeEventEnd(BattleState, _mapEventType);
  			}
> 			if (!_keepSiegeEvent && (IsSiegeAssault || IsSiegeOutside))
  			{
  				switch (BattleState)
  				{
  				case BattleState.AttackerVictory:
> 					CampaignEventDispatcher.Instance.SiegeCompleted(MapEventSettlement, AttackerSide.LeaderParty.MobileParty, isWin: true, _mapEventType);
  					isWin = true;
  					break;
  				case BattleState.DefenderVictory:
  					MapEventSettlement.SiegeEvent?.BesiegerCamp.RemoveAllSiegeParties();
> 					CampaignEventDispatcher.Instance.SiegeCompleted(MapEventSettlement, AttackerSide.LeaderParty.MobileParty, isWin: false, _mapEventType);
  					break;
  				}
  				if (BattleState == BattleState.AttackerVictory || BattleState == BattleState.DefenderVictory)
  				{
  					flag = true;
  				}
  			}
  			else if (IsSallyOut || IsBlockadeSallyOut)
  			{
  				if (MapEventSettlement.Town != null && MapEventSettlement.Town.GarrisonParty != null && MapEventSettlement.Town.GarrisonParty.IsActive)
  				{
  					MapEventSettlement.Town.GarrisonParty.SetMoveModeHold();
  				switch (BattleState)
  				{
  				case BattleState.DefenderVictory:
> 					CampaignEventDispatcher.Instance.SiegeCompleted(MapEventSettlement, DefenderSide.LeaderParty.MobileParty, isWin: true, _mapEventType);
  					isWin = true;
  					break;
  				case BattleState.AttackerVictory:
  					MapEventSettlement.SiegeEvent?.BesiegerCamp.RemoveAllSiegeParties();
> 					CampaignEventDispatcher.Instance.SiegeCompleted(MapEventSettlement, DefenderSide.LeaderParty.MobileParty, isWin: false, _mapEventType);
  					break;
  				}
  				if (BattleState == BattleState.AttackerVictory || BattleState == BattleState.DefenderVictory)
  				{
  					flag = true;
  				}
  			}
  			else if (IsBlockadeSallyOut || IsBlockade)
  			{
  				BattleState battleState = BattleState;
  				if (battleState == BattleState.AttackerVictory)
  				{
  					MapEventSettlement.SiegeEvent?.BesiegerCamp.RemoveAllSiegeParties();
> 					CampaignEventDispatcher.Instance.SiegeCompleted(MapEventSettlement, DefenderSide.LeaderParty.MobileParty, isWin: false, _mapEventType);
  				}
  			}
  		}
  		Component?.BeforeFinalizeComponent();
  		foreach (PartyBase involvedParty in InvolvedParties)
  		{
  			if (involvedParty.IsMobile)
  			{
  				involvedParty.MobileParty.EventPositionAdder = Vec2.Zero;
  			}
  			involvedParty.SetVisualAsDirty();
  			if (!involvedParty.IsMobile || involvedParty.MobileParty.Army == null || involvedParty.MobileParty.Army.LeaderParty != involvedParty.MobileParty)
  			sides[i].HandleMapEventEnd();
  		}
  		MapEventVisual?.OnMapEventEnd();
> 		if (_mapEventType != BattleTypes.Siege && _mapEventType != BattleTypes.SiegeOutside && _mapEventType != BattleTypes.SallyOut)
  		{
  			foreach (PartyBase involvedParty2 in InvolvedParties)
  			{
  				if (involvedParty2.IsMobile && involvedParty2 != PartyBase.MainParty && involvedParty2.MobileParty.BesiegedSettlement != null && (involvedParty2.MobileParty.Army == null || involvedParty2.MobileParty.Army.LeaderParty == involvedParty2.MobileParty))
  				{
  					if (involvedParty2.IsActive)
  					{
  						EncounterManager.StartSettlementEncounter(involvedParty2.MobileParty, involvedParty2.MobileParty.BesiegedSettlement);
  					}
  					else
  					{
  						involvedParty2.MobileParty.BesiegerCamp = null;
  		Component?.FinalizeComponent();
  		if (flag)
  		{
> 			CampaignEventDispatcher.Instance.AfterSiegeCompleted(MapEventSettlement, AttackerSide.LeaderParty.MobileParty, isWin, _mapEventType);
  		}
  		sides = _sides;
  		for (int i = 0; i < sides.Length; i++)
  		{
  			sides[i].Clear();
  		}
  	}
  
  	public void RecalculateRenownAndInfluenceValuesOnPartyInvolved(PartyBase party)
  	{
  		StrengthOfSide[(int)party.Side] += party.GetCustomStrength(party.Side, SimulationContext);
  		MapEventSide[] sides = _sides;
  			_ = mobileParty.NavigationCapability;
  		}
  		Position = newPosition;
> 		if (IsSiegeAssault)
  		{
  			return;
  		}
  		foreach (PartyBase involvedParty in InvolvedParties)
  		{
  			if (!involvedParty.IsMobile)
  			{
  				continue;
  			}
  			if (involvedParty.MobileParty.Army != null)
  			{
  				if (involvedParty.MobileParty.Army.LeaderParty == involvedParty.MobileParty)
  		}
  		CampaignVec2 closestNavMeshFaceCenterPositionForPosition = NavigationHelper.GetClosestNavMeshFaceCenterPositionForPosition(Position, Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(navigationType));
  		Position = NavigationHelper.FindReachablePointAroundPosition(closestNavMeshFaceCenterPositionForPosition, navigationType, 8f, 1f);
> 		if (!IsFieldBattle && !IsSallyOut && !IsSiegeOutside && !IsSiegeAmbush && !IsBlockade && !IsBlockadeSallyOut)
  		{
  			return;
  		}
  		foreach (PartyBase involvedParty in InvolvedParties)
  		{
  			if (involvedParty.IsMobile && involvedParty.MobileParty.CurrentSettlement == null && involvedParty.MobileParty.BesiegerCamp == null)
  			{
  				involvedParty.MobileParty.SetPositionAfterMapChange(Position);
  			}
  		}
  	}
  

codex
For `DefeatRaids`, the code counts all won player map events; the engine event is raised from `PlayerEncounter.DoApplyMapEventResults` before results are committed, and the map event type can be field, raid, hideout, siege, or simulation. That is broader than vanilla target-party issue handlers, so I’m treating it as a confirmed design nuance unless another source contradicts the as-built docs.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Hero 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'MapFaction|IsLord|PartyBelongedToAsPrisoner|IsPrisoner' -Context 3,14 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Actions.TransferPrisonerAction 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Apply|OnHeroPrisonerTaken|capturer|prisoner|PartyBelongedToAsPrisoner|PrisonRoster' -Context 3,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Actions.TakePrisonerAction 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Apply|OnHeroPrisonerTaken|capturer|prisoner|MapFaction|PartyBelongedToAsPrisoner' -Context 3,22 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'Occupation.Bandit|IsBandit|Bandit' -SimpleMatch:"'$false | Select-Object -First 80 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1653ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs:157:float num = (mobileParty.IsLordParty ? Campaign.Current.EstimatedAverageLordPartyNavalSpeed : (mobileParty.IsCaravan ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : (mobileParty.IsBandit ? Campaign.Current.EstimatedAverageBanditPartyNavalSpeed : (mobileParty.IsVillager ? Campaign.Current.EstimatedAverageVillagerPartyNavalSpeed : (Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer * 0.5f)))));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:314:if (character.Occupation == Occupation.Bandit || character.Occupation == Occupation.Gangster)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:479:else if (flag4 && character.Occupation == Occupation.Bandit)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:500:if (character.Occupation == Occupation.Villager || character.Occupation == Occupation.Townsfolk || character.Occupation == Occupation.Bandit || character.Occupation == Occupation.Gangster || character.Occupation == Occupation.Wanderer)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:582:foreach (Clan item in Clan.All.Where((Clan t) => t != oldClan && !t.IsEliminated && !t.IsMinorFaction && !t.AliveLords.IsEmpty() && t.AliveLords.Any((Hero k) => !k.IsChild) && !t.IsBanditFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:434:list = Clan.NonBanditFactions.Where((Clan x) => x.IsSect).ToList();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:439:list = Clan.NonBanditFactions.Where((Clan x) => x.IsMafia).ToList();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:114:firstPhaseTroopCount = MathF.Min(MathF.Floor((float)num * Campaign.Current.Models.BanditDensityModel.SpawnPercentageForFirstFightInHideoutMission), Campaign.Current.Models.BanditDensityModel.NumberOfMaximumTroopCountForFirstFightInHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:122:int count = flattenedTroopRoster.RemoveIf((FlattenedTroopRosterElement x) => x.Troop.IsHero || x.Troop.Culture.BanditBoss == x.Troop).ToList().Count;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:131:Debug.Print("Picking bandit troops for hideout mission...", 0, Debug.DebugColor.Yellow, 256uL);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:363:CampaignMission.OpenHideoutBattleMission("sea_bandit_a", null, isTutorial: false);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:194:obj14.GetPropertyDefinitionWithId(new MemberTypeId(3, 85)).InitializeForAutoGeneration(Clan.AutoGeneratedGetMemberValueIsBanditFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:999:obj103.GetFieldDefinitionWithId(new MemberTypeId(3, 286)).InitializeForAutoGeneration(PlayerBattleEndedLogEntry.AutoGeneratedGetMemberValue_defeatedSidePartyIsBanditFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1459:CollectObjectsDelegate collectObjectsDelegate171 = BanditPartyComponent.AutoGeneratedStaticCollectObjectsBanditPartyComponent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1461:obj178.GetPropertyDefinitionWithId(new MemberTypeId(4, 1)).InitializeForAutoGeneration(BanditPartyComponent.AutoGeneratedGetMemberValueHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1462:obj178.GetPropertyDefinitionWithId(new MemberTypeId(4, 2)).InitializeForAutoGeneration(BanditPartyComponent.AutoGeneratedGetMemberValueIsBossParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1463:obj178.GetFieldDefinitionWithId(new MemberTypeId(4, 3)).InitializeForAutoGeneration(BanditPartyComponent.AutoGeneratedGetMemberValue_relatedSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1519:obj185.GetFieldDefinitionWithId(new MemberTypeId(2, 290)).InitializeForAutoGeneration(CampaignPeriodicEventManager.AutoGeneratedGetMemberValue_banditMobilePartyPartialHourlyAiEventTicker);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1855:typeDefinition32.GetFieldDefinitionWithId(new MemberTypeId(4, 4)).InitializeForAutoGeneration(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest.AutoGeneratedGetMemberValue_banditParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1871:typeDefinition33.GetFieldDefinitionWithId(new MemberTypeId(4, 5)).InitializeForAutoGeneration(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest.AutoGeneratedGetMemberValue_questBanditMobileParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1874:typeDefinition33.GetFieldDefinitionWithId(new MemberTypeId(4, 26)).InitializeForAutoGeneration(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest.AutoGeneratedGetMemberValue_otherBanditParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1875:typeDefinition33.GetFieldDefinitionWithId(new MemberTypeId(4, 30)).InitializeForAutoGeneration(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest.AutoGeneratedGetMemberValue_questBanditPartyFollowDuration);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1876:typeDefinition33.GetFieldDefinitionWithId(new MemberTypeId(4, 31)).InitializeForAutoGeneration(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest.AutoGeneratedGetMemberValue_otherBanditPartyFollowDuration);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1878:typeDefinition33.GetFieldDefinitionWithId(new MemberTypeId(4, 23)).InitializeForAutoGeneration(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest.AutoGeneratedGetMemberValue_questBanditPartyAlreadyAttacked);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1930:typeDefinition38.GetFieldDefinitionWithId(new MemberTypeId(4, 114)).InitializeForAutoGeneration(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest.AutoGeneratedGetMemberValue_talkedWithBanditLeader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2163:CollectObjectsDelegate collectObjectsDelegate281 = NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue.AutoGeneratedStaticCollectObjectsNearbyBanditBaseIssue;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2165:typeDefinition61.GetFieldDefinitionWithId(new MemberTypeId(4, 100)).InitializeForAutoGeneration(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue.AutoGeneratedGetMemberValue_targetHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2166:typeDefinition61.GetFieldDefinitionWithId(new MemberTypeId(4, 101)).InitializeForAutoGeneration(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue.AutoGeneratedGetMemberValue_issueSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2168:CollectObjectsDelegate collectObjectsDelegate282 = NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest.AutoGeneratedStaticCollectObjectsNearbyBanditBaseIssueQuest;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2170:typeDefinition62.GetFieldDefinitionWithId(new MemberTypeId(4, 100)).InitializeForAutoGeneration(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest.AutoGeneratedGetMemberValue_targetHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:2171:typeDefinition62.GetFieldDefinitionWithId(new MemberTypeId(4, 101)).InitializeForAutoGeneration(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest.AutoGeneratedGetMemberValue_questSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:338:public float EstimatedAverageBanditPartySpeed { get; set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:346:public float EstimatedAverageBanditPartyNavalSpeed { get; set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:491:public MBReadOnlyList<MobileParty> BanditParties => CampaignObjectManager.BanditParties;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:1199:EstimatedAverageBanditPartySpeed = 3.41f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Campaign.cs:1203:EstimatedAverageBanditPartyNavalSpeed = 3.57f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2197:foreach (Clan nonBanditFaction in Clan.NonBanditFactions)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2199:if (nonBanditFaction.MainHeroCrimeRating > 0f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2201:text = string.Concat(text, nonBanditFaction.Name, "   criminal rating: ", nonBanditFaction.MainHeroCrimeRating, "\n");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignData.cs:251:public const string CultureForestHideout = "forest_bandits";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignData.cs:255:public const string CultureMountainHideout = "mountain_bandits";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignData.cs:257:public const string CultureDesertHideout = "desert_bandits";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignData.cs:259:public const string CultureSteppeHideout = "steppe_bandits";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:81:public override void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:86:eventReceivers[i].OnHomeHideoutChanged(banditPartyComponent, oldHomeHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:288:public override void OnBanditPartyRecruited(MobileParty banditParty)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:293:eventReceivers[i].OnBanditPartyRecruited(banditParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:45:public virtual void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:117:public virtual void OnBanditPartyRecruited(MobileParty banditParty)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:39:private readonly MbEvent<BanditPartyComponent, Hideout> _onHomeHideoutChangedEvent = new MbEvent<BanditPartyComponent, Hideout>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:77:private readonly MbEvent<MobileParty> _banditPartyRecruited = new MbEvent<MobileParty>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:597:public static IMbEvent<BanditPartyComponent, Hideout> OnHomeHideoutChangedEvent => Instance._onHomeHideoutChangedEvent;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:635:public static IMbEvent<MobileParty> BanditPartyRecruited => Instance._banditPartyRecruited;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1207:_banditPartyRecruited.ClearListeners(obj);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1438:public override void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1440:Instance._onHomeHideoutChangedEvent.Invoke(banditPartyComponent, oldHomeHideout);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1533:public override void OnBanditPartyRecruited(MobileParty banditParty)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1535:Instance._banditPartyRecruited.Invoke(banditParty);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:235:private MBList<MobileParty> _banditParties;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:258:public MBReadOnlyList<MobileParty> BanditParties => _banditParties;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:287:_banditParties = new MBList<MobileParty>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:334:_banditParties = new MBList<MobileParty>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:551:if (party.IsBandit)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:553:_banditParties.Add(party);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:591:if (party.IsBandit)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignObjectManager.cs:593:_banditParties.Remove(party);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:141:private PeriodicTicker<MobileParty> _banditMobilePartyPartialHourlyAiEventTicker;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:179:_banditMobilePartyPartialHourlyAiEventTicker = new PeriodicTicker<MobileParty>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:194:_banditMobilePartyPartialHourlyAiEventTicker = new PeriodicTicker<MobileParty>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:275:_banditMobilePartyPartialHourlyAiEventTicker.Initialize(MobileParty.AllBanditParties, delegate(MobileParty x)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:384:_banditMobilePartyPartialHourlyAiEventTicker.PeriodicTickSome(DeltaHours * 0.99);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:470:collectedObjects.Add(_banditMobilePartyPartialHourlyAiEventTicker);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:556:internal static object AutoGeneratedGetMemberValue_banditMobilePartyPartialHourlyAiEventTicker(object o)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignPeriodicEventManager.cs:558:return ((CampaignPeriodicEventManager)o)._banditMobilePartyPartialHourlyAiEventTicker;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:300:public bool IsBanditFaction { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:424:public static IEnumerable<Clan> NonBanditFactions
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:430:if (!clan.IsBanditFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:438:public static IEnumerable<Clan> BanditFactions
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:444:if (clan.IsBanditFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:592:internal static object AutoGeneratedGetMemberValueIsBanditFaction(object o)

 succeeded in 3762ms:
  
  namespace TaleWorlds.CampaignSystem.Actions;
  
> public static class TakePrisonerAction
  {
> 	private static void ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled = true)
  	{
> 		if (prisonerCharacter.PartyBelongedTo != null)
  		{
> 			if (prisonerCharacter.PartyBelongedTo.LeaderHero == prisonerCharacter)
  			{
> 				prisonerCharacter.PartyBelongedTo.RemovePartyLeader();
  			}
> 			prisonerCharacter.PartyBelongedTo.MemberRoster.RemoveTroop(prisonerCharacter.CharacterObject);
  		}
> 		prisonerCharacter.CaptivityStartTime = CampaignTime.Now;
> 		prisonerCharacter.ChangeState(Hero.CharacterStates.Prisoner);
> 		capturerParty.AddPrisoner(prisonerCharacter.CharacterObject, 1);
> 		if (prisonerCharacter == Hero.MainHero)
  		{
  			if (MobileParty.MainParty.IsDisorganized)
  			{
  				MobileParty.MainParty.SetDisorganized(isDisorganized: false);
  			}
> 			PlayerCaptivity.StartCaptivity(capturerParty);
  			if (MobileParty.MainParty.IsCurrentlyAtSea)
  			{
  				for (int num = MobileParty.MainParty.Ships.Count - 1; num >= 0; num--)
  				{
> 					DestroyShipAction.Apply(MobileParty.MainParty.Ships[num]);
  				}
  			}
  		}
> 		if (prisonerCharacter.StayingInSettlement != null)
  		{
> 			prisonerCharacter.StayingInSettlement = null;
  		}
  		if (isEventCalled)
  		{
> 			CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(capturerParty, prisonerCharacter);
  		}
  	}
  
> 	public static void Apply(PartyBase capturerParty, Hero prisonerCharacter)
  	{
> 		ApplyInternal(capturerParty, prisonerCharacter);
  	}
  
> 	public static void ApplyByTakenFromPartyScreen(FlattenedTroopRoster roster)
  	{
  		foreach (FlattenedTroopRosterElement item in roster)
  		{
  			if (item.Troop.IsHero)
  			{
> 				ApplyInternal(PartyBase.MainParty, item.Troop.HeroObject);
  			}
  		}
> 		CampaignEventDispatcher.Instance.OnPrisonerTaken(roster);
  	}
  }

 succeeded in 3787ms:
  
  namespace TaleWorlds.CampaignSystem.Actions;
  
> public static class TransferPrisonerAction
  {
> 	private static void ApplyInternal(CharacterObject prisonerTroop, PartyBase prisonerOwnerParty, PartyBase newParty)
  	{
> 		if (prisonerTroop.HeroObject == Hero.MainHero)
  		{
  			PlayerCaptivity.CaptorParty = newParty;
  			return;
  		}
> 		prisonerOwnerParty.PrisonRoster.AddToCounts(prisonerTroop, -1);
> 		newParty.AddPrisoner(prisonerTroop, 1);
  	}
  
> 	public static void Apply(CharacterObject prisonerTroop, PartyBase prisonerOwnerParty, PartyBase newParty)
  	{
> 		ApplyInternal(prisonerTroop, prisonerOwnerParty, newParty);
  	}
  }

 succeeded in 5473ms:
  
  	public bool IsFugitive => HeroState == CharacterStates.Fugitive;
  
> 	public bool IsPrisoner => HeroState == CharacterStates.Prisoner;
  
  	public bool IsReleased => HeroState == CharacterStates.Released;
  
  	public bool IsActive => HeroState == CharacterStates.Active;
  
  	public bool IsNotSpawned => HeroState == CharacterStates.NotSpawned;
  
  	public bool IsDisabled => HeroState == CharacterStates.Disabled;
  
  	public bool IsTraveling => HeroState == CharacterStates.Traveling;
  
  	public bool IsAlive => !IsDead;
  
  	[SaveableProperty(400)]
  		}
  	}
  
> 	public bool IsLord => Occupation == Occupation.Lord;
  
  	public int MaxHitPoints => CharacterObject.MaxHitPoints();
  
  	public int HitPoints
  	{
  		get
  		{
  			return _health;
  		}
  		set
  		{
  			if (_health == value)
  			{
  				return;
  				{
  					PartyBelongedTo.MemberRoster.OnHeroHealthStatusChanged(this);
  				}
> 				if (PartyBelongedToAsPrisoner != null)
  				{
> 					PartyBelongedToAsPrisoner.PrisonRoster.OnHeroHealthStatusChanged(this);
  				}
  			}
  			if (health > WoundedHealthLimit && IsWounded)
  			{
  				CampaignEventDispatcher.Instance.OnHeroWounded(this);
  			}
  		}
  	}
  
  	public CampaignTime BirthDay
  	{
  		get
  		{
  			if (CampaignOptions.IsLifeDeathCycleDisabled)
  		}
  	}
  
> 	public IFaction MapFaction
  	{
  		get
  		{
  			if (Clan != null)
  			{
  				IFaction kingdom = Clan.Kingdom;
  				return kingdom ?? Clan;
  			}
  			if (IsSpecial)
  			{
  				return null;
  			}
  			if (HomeSettlement != null)
  			{
> 				return HomeSettlement.MapFaction;
  			}
  			if (PartyBelongedTo != null)
  			{
> 				return PartyBelongedTo.MapFaction;
  			}
  			return null;
  		}
  	}
  
  	public List<Alley> OwnedAlleys { get; private set; }
  
  	public bool IsFactionLeader
  	{
  		get
  		{
> 			if (MapFaction != null)
  			{
> 				return MapFaction.Leader == this;
  			}
  			return false;
  		}
  	}
  
  	public bool IsKingdomLeader
  	{
  		get
  		{
> 			if (MapFaction != null && MapFaction.IsKingdomFaction)
  			{
> 				return MapFaction.Leader == this;
  			}
  			return false;
  		}
  	}
  
  	public bool IsClanLeader
  	{
  		get
  		{
  			if (Clan != null)
  			{
  				return Clan.Leader == this;
  			}
  			return false;
  	}
  
  	[SaveableProperty(570)]
> 	public PartyBase PartyBelongedToAsPrisoner { get; private set; }
  
  	public Settlement StayingInSettlement
  	{
  		get
  		{
  			return _stayingInSettlement;
  		}
  		set
  		{
  			if (_stayingInSettlement != value)
  			{
  				if (_stayingInSettlement != null)
  				{
  					_stayingInSettlement.RemoveHeroWithoutParty(this);
  			{
  				result = PartyBelongedTo.CurrentSettlement;
  			}
> 			else if (PartyBelongedToAsPrisoner != null)
  			{
> 				result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement : (PartyBelongedToAsPrisoner.IsMobile ? PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement : null));
  			}
  			else if (StayingInSettlement != null)
  			{
  				result = StayingInSettlement;
  			}
  			return result;
  		}
  	}
  
  	public int Gold
  	{
  		get
  		{
  			return _gold;
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(CaptivityStartTime, collectedObjects);
  		collectedObjects.Add(DeathMarkKillerHero);
  		collectedObjects.Add(LastKnownClosestSettlement);
> 		collectedObjects.Add(PartyBelongedToAsPrisoner);
  		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(LastMeetingTimeWithPlayer, collectedObjects);
  	}
  
  	internal static object AutoGeneratedGetMemberValueStaticBodyProperties(object o)
  	{
  		return ((Hero)o).StaticBodyProperties;
  	}
  
  	internal static object AutoGeneratedGetMemberValueWeight(object o)
  	{
  		return ((Hero)o).Weight;
  	}
  
  	internal static object AutoGeneratedGetMemberValueBuild(object o)
  		return ((Hero)o).LastExaminedLogEntryID;
  	}
  
> 	internal static object AutoGeneratedGetMemberValuePartyBelongedToAsPrisoner(object o)
  	{
> 		return ((Hero)o).PartyBelongedToAsPrisoner;
  	}
  
  	internal static object AutoGeneratedGetMemberValueLastMeetingTimeWithPlayer(object o)
  	{
  		return ((Hero)o).LastMeetingTimeWithPlayer;
  	}
  
  	internal static object AutoGeneratedGetMemberValueRandomValue(object o)
  	{
  		return ((Hero)o).RandomValue;
  	}
  
  	internal static object AutoGeneratedGetMemberValueLastTimeStampForActivity(object o)
  	{
  			{
  				_clan.OnLordAdded(this);
  			}
> 			if (CurrentSettlement != null && PartyBelongedTo == null && PartyBelongedToAsPrisoner == null)
  			{
  				CurrentSettlement.AddHeroWithoutParty(this);
  			}
  		}
  		if (MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.0") && FirstName != null && Name != null && this != MainHero)
  		{
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FIRSTNAME"))
  			{
  				Name.SetTextVariable("FIRSTNAME", FirstName.CopyTextObject());
  			}
  			if (Name.Attributes == null || !Name.Attributes.ContainsKey("FEMALE"))
  			{
  				Name.SetTextVariable("FEMALE", IsFemale ? 1 : 0);
  			}
  		{
  			PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  		}
> 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.14")) && PartyBelongedToAsPrisoner != null && StayingInSettlement != null)
  		{
  			StayingInSettlement = null;
> 			if (PartyBelongedToAsPrisoner != null && PartyBelongedToAsPrisoner.PrisonRoster.Contains(CharacterObject))
  			{
> 				PartyBelongedToAsPrisoner.PrisonRoster.RemoveTroop(CharacterObject);
  			}
> 			PartyBelongedToAsPrisoner = null;
  			if (!IsDead && !IsDisabled)
  			{
  				ChangeState(CharacterStates.Fugitive);
  			}
  		}
  		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.4.0") && OriginClan == null)
  		{
  			if (Father != null)
  			{
  				_originClan = Father.Clan;
  			}
  			else
  			{
  				_originClan = Clan;
  			ClearChangedPerks();
  		}
  		HeroDeveloper?.AfterLoad();
> 		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.9.35637")) && GovernorOf != null && (PartyBelongedTo != null || PartyBelongedToAsPrisoner != null))
  		{
  			ChangeGovernorAction.RemoveGovernorOf(this);
  		}
  		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.2.8.31599")))
  		{
> 			if (this != MainHero && IsPrisoner && this != MainHero && IsPrisoner && PartyBelongedToAsPrisoner == null && CurrentSettlement != null)
  			{
> 				PartyBelongedToAsPrisoner = CurrentSettlement.Party;
  			}
  			MobileParty mainParty = MobileParty.MainParty;
  			if (this != MainHero && PartyBelongedTo == mainParty && !mainParty.MemberRoster.Contains(CharacterObject))
  			{
  				MakeHeroFugitiveAction.Apply(this);
  			}
  			if (mainParty.MemberRoster.Contains(CharacterObject) && PartyBelongedTo != mainParty)
  			{
  				mainParty.MemberRoster.RemoveTroop(CharacterObject, mainParty.MemberRoster.GetElementNumber(CharacterObject));
  				if (!Campaign.Current.IssueManager.IssueSolvingCompanionList.Contains(this))
  				{
  					MobileParty partyBelongedTo = PartyBelongedTo;
  					if (partyBelongedTo != null && !partyBelongedTo.IsCaravan)
  					{
  					PartyBelongedTo.MemberRoster.AddToCounts(CharacterObject, -PartyBelongedTo.MemberRoster.GetTroopCount(CharacterObject));
  				}
  			}
> 			if (PartyBelongedToAsPrisoner != null)
  			{
  				EndCaptivityAction.ApplyByDeath(this);
  			}
  			if (IsAlive)
  			{
  				KillCharacterAction.ApplyByRemove(this);
  			}
  			Campaign.Current.CampaignObjectManager.UnregisterDeadHero(this);
  			Campaign.Current.ObjectManager.UnregisterObject(CharacterObject);
  		}
  		if (!MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.4.1.113285")) || this == MainHero || !IsAlive || IsSpecial || !(Age > (float)Campaign.Current.Models.AgeModel.HeroComesOfAge))
  		{
  			return;
  		}
  		StringHelpers.SetCharacterProperties("LORD", o.CharacterObject);
  		MBTextManager.SetTextVariable("TITLE", HeroHelper.GetTitleInIndefiniteCase(o));
  		MBTextManager.SetTextVariable("REPUTATION", CharacterHelper.GetReputationDescription(o.CharacterObject));
> 		MBTextManager.SetTextVariable("FACTION_NAME", GameTexts.FindText("str_neutral_term_for_culture", o.MapFaction.IsMinorFaction ? o.Culture.StringId : o.MapFaction.Culture.StringId));
> 		if (o.MapFaction.Culture.StringId == "empire")
  		{
  			MBTextManager.SetTextVariable("FACTION_NAME", "{=empirefaction}Empire");
  		}
  		MBTextManager.SetTextVariable("CLAN_NAME", o.Clan.Name);
  		if (o.Clan.IsMinorFaction || o.Clan.IsRebelClan)
  		{
  			if (o.Clan == MainHero.Clan)
  			{
  				MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=REWGj2ge}a rising new clan");
  			}
  			else if (o.Clan.IsSect)
  			{
  				MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=IlRC9Drl}a religious sect");
  			}
  			}
  			return new TextObject("{=9Obe3S6L}{LORD.FIRSTNAME} is a member of the {CLAN_NAME}, {CLAN_DESCRIPTION} from the lands of the {FACTION_NAME}. {?LORD.GENDER}She{?}He{\\?} has the reputation of being {REPUTATION}.");
  		}
> 		List<Kingdom> list = Campaign.Current.Kingdoms.Where((Kingdom x) => x.Culture == o.MapFaction.Culture).ToList();
  		if (list.Count > 1)
  		{
> 			MBTextManager.SetTextVariable("RULER", o.MapFaction.Leader.Name);
  		}
  		MBTextManager.SetTextVariable("CLAN_DESCRIPTION", "{=KzSeg8ks}a noble family");
  		if (list.Count == 1)
  		{
  			if (o.Clan.Leader == o)
  			{
  				return new TextObject("{=6d4ZTvGv}{LORD.NAME} is {TITLE} of the {FACTION_NAME} and head of the {CLAN_NAME}, {CLAN_DESCRIPTION} of the realm. {?LORD.GENDER}She{?}He{\\?} has the reputation of being {REPUTATION}.");
  			}
  			return new TextObject("{=o5AUljbW}{LORD.NAME} is a member of the {CLAN_NAME}, {CLAN_DESCRIPTION} of the {FACTION_NAME}. {?LORD.GENDER}She{?}He{\\?} has the reputation of being {REPUTATION}.");
  		}
  		if (list.Count > 1)
  		{
  			if (o.Clan.Leader == o)
  			{
  
  	public bool CanBeGovernorOrHavePartyRole()
  	{
> 		if (IsPrisoner)
  		{
  			return false;
  		}
  		bool result = true;
  		CampaignEventDispatcher.Instance.CanBeGovernorOrHavePartyRole(this, ref result);
  		return result;
  	}
  
  	public bool CanDie(KillCharacterAction.KillCharacterActionDetail causeOfDeath)
  	{
  		if (CampaignOptions.IsLifeDeathCycleDisabled && causeOfDeath == KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge)
  		{
  			return false;
  		}
  
  	public bool CanBecomePrisoner()
  	{
> 		if (!IsLord && !IsPlayerCompanion && !IsSpecial)
  		{
  			Debug.FailedAssert("Only lords, companions and special quest heroes can become prisoners! Check CanBecomePrisoner usage.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Hero.cs", "CanBecomePrisoner", 1922);
  			return false;
  		}
  		if (this != MainHero)
  		{
  			return true;
  		}
  		bool result = true;
  		CampaignEventDispatcher.Instance.CanHeroBecomePrisoner(this, ref result);
  		return result;
  	}
  
  	public bool CanMoveToSettlement()
  
  	internal void OnAddedToPartyAsPrisoner(PartyBase party)
  	{
> 		PartyBelongedToAsPrisoner = party;
  		PartyBelongedTo = null;
  	}
  
  	internal void OnRemovedFromPartyAsPrisoner(PartyBase party)
  	{
> 		PartyBelongedToAsPrisoner = null;
  	}
  
  	public Vec3 GetPositionAsVec3()
  	{
  		return GetCampaignPosition().AsVec3();
  	}
  
  	public CampaignVec2 GetCampaignPosition()
  	{
  		CampaignVec2 result = CampaignVec2.Invalid;
  		if (CurrentSettlement != null)
  		{
  			result = CurrentSettlement.GatePosition;
  		}
> 		else if (IsPrisoner && PartyBelongedToAsPrisoner != null)
  		{
> 			result = (PartyBelongedToAsPrisoner.IsSettlement ? PartyBelongedToAsPrisoner.Settlement.GatePosition : PartyBelongedToAsPrisoner.MobileParty.Position);
  		}
  		else if (PartyBelongedTo != null)
  		{
  			result = PartyBelongedTo.Position;
  		}
  		return result;
  	}
  
  	TextObject ITrackableBase.GetName()
  	{
  		return Name;
  	}
  
  	Vec3 ITrackableBase.GetPosition()
  		{
  			return CurrentSettlement;
  		}
> 		if (IsPrisoner && PartyBelongedToAsPrisoner != null)
  		{
> 			if (!PartyBelongedToAsPrisoner.IsSettlement)
  			{
> 				return PartyBelongedToAsPrisoner.MobileParty;
  			}
> 			return PartyBelongedToAsPrisoner.Settlement;
  		}
  		return PartyBelongedTo;
  	}
  
  	private void SetInitialValuesFromCharacter(CharacterObject characterObject)
  	{
  		foreach (TraitObject item in TraitObject.All)
  		{
  			SetTraitLevel(item, characterObject.GetTraitLevel(item));
  		}
  		Level = characterObject.Level;
  		SetName(characterObject.Name, characterObject.Name);
  		Culture = characterObject.Culture;
  		_defaultAge = characterObject.Age;

codex
For lord capture, `Hero.MapFaction` is null-guarded in TAOM and returns `Clan.Kingdom ?? Clan` for lords, so the null path is handled. The capturer equality is intentionally stricter than “player side won”; allied/army-party captures do not count. Next I’m checking the prisoner-delivery template against vanilla bandit-prisoner logic.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsRecruitsIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Occupation.Bandit|PrisonRoster|RemoveTroop|AddToCounts|IsBandit|bandit|Turn|Success|GangLeaderNeedsRecruitsIssueQuest' -Context 4,24 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Roster.TroopRoster 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'RemoveTroop|AddToCounts\\(|PrisonRoster|Wounded|UniqueTroopDescriptor|TroopRosterElement' -Context 3,18 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Roster.TroopRosterElement 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'public|Character|Number|WoundedNumber' -Context 2,12 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CharacterObject 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Occupation|IsBandit|IsHero|LoadFromXml' -Context 3,16 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1540ms:
  	private static readonly List<TroopRoster> InstanceListForLoadGame = new List<TroopRoster>();
  
  	[SaveableField(0)]
> 	internal TroopRosterElement[] data;
  
  	[SaveableField(1)]
  	internal int _count;
  
  	[CachedData]
  	private int _totalRegulars;
  
  	[CachedData]
> 	private int _totalWoundedRegulars;
  
  	[CachedData]
> 	private int _totalWoundedHeroes;
  
  	[CachedData]
  	private int _totalHeroes;
  
  	[CachedData]
> 	private MBList<TroopRosterElement> _troopRosterElements;
  
  	[CachedData]
> 	private int _troopRosterElementsVersion;
  
  	[CachedData]
  	private bool _isInitialized;
  
  	[SaveableProperty(2)]
  	internal PartyBase OwnerParty { get; private set; }
  
  	public int Count => _count;
  
  	[CachedData]
  	public int VersionNo { get; private set; }
  
  	public int TotalRegulars => _totalRegulars;
  
> 	public int TotalWoundedRegulars => _totalWoundedRegulars;
  
> 	public int TotalWoundedHeroes => _totalWoundedHeroes;
  
  	public int TotalHeroes => _totalHeroes;
  
> 	public int TotalWounded => _totalWoundedRegulars + _totalWoundedHeroes;
  
  	public int TotalManCount => _totalRegulars + _totalHeroes;
  
> 	public int TotalHealthyCount => _totalRegulars + _totalHeroes - (_totalWoundedRegulars + _totalWoundedHeroes);
  
  	internal static void AutoGeneratedStaticCollectObjectsTroopRoster(object o, List<object> collectedObjects)
  	{
  		((TroopRoster)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
  		collectedObjects.Add(data);
  		collectedObjects.Add(OwnerParty);
  	}
  
  	internal static object AutoGeneratedGetMemberValueOwnerParty(object o)
  	{
  		return ((TroopRoster)o).OwnerParty;
  	}
  
  	internal static object AutoGeneratedGetMemberValuedata(object o)
  
  	private TroopRoster()
  	{
> 		data = new TroopRosterElement[4];
  		_count = 0;
> 		_troopRosterElements = new MBList<TroopRosterElement>();
  		InitializeCachedData();
  	}
  
  	public static TroopRoster CreateDummyTroopRoster()
  	{
  		return new TroopRoster();
  	}
  
  	public override int GetHashCode()
  	{
  		return base.GetHashCode();
  	}
  
  	[LoadInitializationCallback]
  	private void OnLoad(MetaData metaData)
  	{
> 		_troopRosterElementsVersion = -1;
> 		_troopRosterElements = new MBList<TroopRosterElement>();
  		InstanceListForLoadGame.Add(this);
  	}
  
  	private void EnsureLength(int length)
  	{
  		if (length > 0 && (data == null || length > data.Length))
  		{
  			int num = 4;
  			if (data != null)
  			{
  				num = data.Length * 2;
  			}
> 			TroopRosterElement[] array = new TroopRosterElement[num];
  			for (int i = 0; i < _count; i++)
  			{
  				array[i] = data[i];
  			}
  			data = array;
  		}
  	}
  
  	public static void CalculateCachedStatsOnLoad()
  	{
  		foreach (TroopRoster item in InstanceListForLoadGame)
  		{
  			item.InitializeCachedData();
  		}
  		InstanceListForLoadGame.Clear();
  	}
  
  	private void InitializeCachedData()
  		int num4 = 0;
  		for (int i = 0; i < _count; i++)
  		{
> 			TroopRosterElement troopRosterElement = data[i];
> 			if (troopRosterElement.Character.IsHero)
  			{
  				num++;
> 				if (troopRosterElement.Character.HeroObject.IsWounded)
  				{
  					num2++;
  				}
  			}
  			else
  			{
  				num3 += data[i].Number;
> 				num4 += data[i].WoundedNumber;
  			}
  		}
> 		_totalWoundedHeroes = num2;
> 		_totalWoundedRegulars = num4;
  		_totalHeroes = num;
  		_totalRegulars = num3;
  	}
  
  	public FlattenedTroopRoster ToFlattenedRoster()
  	{
  		return new FlattenedTroopRoster(TotalManCount) { GetTroopRoster() };
  	}
  
  	public void Add(TroopRoster troopRoster)
  	{
> 		foreach (TroopRosterElement item in troopRoster.GetTroopRoster())
  		{
  			Add(item);
  		}
  	}
  
> 	public void Add(TroopRosterElement troopRosterElement)
  	{
> 		AddToCounts(troopRosterElement.Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber, troopRosterElement.Xp);
  	}
  
> 	public ICollection<TroopRosterElement> RemoveIf(Predicate<TroopRosterElement> match)
  	{
> 		List<TroopRosterElement> list = new List<TroopRosterElement>();
  		for (int num = _count - 1; num >= 0; num--)
  		{
  			if (match(data[num]))
  			{
> 				TroopRosterElement elementCopyAtIndex = GetElementCopyAtIndex(num);
  				list.Add(elementCopyAtIndex);
> 				AddToCountsAtIndex(num, -elementCopyAtIndex.Number, -elementCopyAtIndex.WoundedNumber, -elementCopyAtIndex.Xp);
  			}
  		}
  		return list;
  	}
  
  	public int FindIndexOfTroop(CharacterObject character)
  	{
  		for (int i = 0; i < _count; i++)
  		{
  			if (data[i].Character == character)
  			{
  				return i;
  			}
  		}
  		return -1;
  	}
  
  	private int FlattenedRosterIndexToTroopRosterIndexWithFilter(int indexOfTroop, bool includeHeroes = false, bool countOnlyHealthyOnes = true)
  		{
  			if (includeHeroes || !data[i].Character.IsHero)
  			{
> 				indexOfTroop -= (countOnlyHealthyOnes ? (data[i].Number - data[i].WoundedNumber) : data[i].Number);
  				if (indexOfTroop < 0)
  				{
  					return i;
  				}
  			}
  		}
  		return -1;
  	}
  
  	public TroopRoster RemoveNumberOfNonHeroTroopsRandomly(int numberOfMen)
  	{
  		TroopRoster troopRoster = CreateDummyTroopRoster();
> 		if (numberOfMen > _totalRegulars - _totalWoundedRegulars)
  		{
> 			TaleWorlds.Library.Debug.FailedAssert("numberOfMen > _totalRegulars - _totalWoundedRegulars", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "RemoveNumberOfNonHeroTroopsRandomly", 315);
> 			numberOfMen = _totalRegulars - _totalWoundedRegulars;
  		}
  		for (int i = 0; i < numberOfMen; i++)
  		{
> 			int indexOfTroop = MBRandom.RandomInt(_totalRegulars - _totalWoundedRegulars);
  			int num = FlattenedRosterIndexToTroopRosterIndexWithFilter(indexOfTroop);
  			if (num >= 0)
  			{
> 				troopRoster.AddToCounts(GetCharacterAtIndex(num), 1);
  				AddToCountsAtIndex(num, -1);
  			}
  		}
  		return troopRoster;
  	}
  
  	public void WoundNumberOfNonHeroTroopsRandomly(int numberOfMen)
  	{
  		for (int i = 0; i < numberOfMen; i++)
  		{
> 			int indexOfTroop = MBRandom.RandomInt(_totalRegulars - _totalWoundedRegulars);
  			int num = FlattenedRosterIndexToTroopRosterIndexWithFilter(indexOfTroop);
  			if (num >= 0)
  			{
  				AddToCountsAtIndex(num, 0, 1);
  			}
  		}
  	}
  
  	public void SwapTroopsAtIndices(int firstIndex, int secondIndex)
  	{
  		if (firstIndex < 0 || firstIndex >= Count || secondIndex < 0 || secondIndex >= Count)
  		{
  			TaleWorlds.Library.Debug.FailedAssert("Troop roster swap indices are out of bounds.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "SwapTroopsAtIndices", 358);
  		}
  		else if (firstIndex != secondIndex)
  		{
> 			TroopRosterElement troopRosterElement = data[firstIndex];
  			data[firstIndex] = data[secondIndex];
> 			data[secondIndex] = troopRosterElement;
  		}
  	}
  
  	public void ShiftTroopToIndex(int troopIndex, int targetIndex)
  	{
  		if (troopIndex < 0 || troopIndex >= Count || targetIndex < 0 || targetIndex >= Count)
  		{
  			TaleWorlds.Library.Debug.FailedAssert("Troop roster swap indices are out of bounds.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "ShiftTroopToIndex", 376);
  		}
  		else if (troopIndex != targetIndex)
  		{
  			int num = ((troopIndex < targetIndex) ? 1 : (-1));
> 			TroopRosterElement troopRosterElement = data[troopIndex];
  			for (int i = troopIndex; i != targetIndex; i += num)
  			{
  				data[i] = data[i + num];
  			}
> 			data[targetIndex] = troopRosterElement;
  		}
  	}
  
> 	public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
  	{
  		bool flag = false;
  		CharacterObject character = data[index].Character;
  		bool isHero = character.IsHero;
  		data[index].Number += countChange;
> 		int num = data[index].WoundedNumber + woundedCountChange;
  		if (num > data[index].Number)
  		{
> 			woundedCountChange += data[index].Number - num;
  		}
  		else if (num < 0)
  		{
  			TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCountsAtIndex", 415);
  		}
> 		data[index].WoundedNumber += woundedCountChange;
  		if (xpChange != 0)
  		{
  			SetElementXp(index, data[index].Xp + xpChange);
  		}
  		if (isHero)
  		{
  			_totalHeroes += countChange;
> 			if (character.HeroObject.IsWounded)
  			{
> 				_totalWoundedHeroes += countChange;
  			}
  			if (countChange != 0)
  			{
  				flag = true;
  			}
  		}
  		else
  		{
> 			_totalWoundedRegulars += woundedCountChange;
  			_totalRegulars += countChange;
  		}
  		if (removeDepleted && data[index].Number == 0)
  		{
  			RemoveRange(index, index + 1);
  			index = -1;
  		}
  		if (OwnerParty != null && isHero)
  		{
  			if (countChange > 0)
  			{
  				OwnerParty.OnHeroAdded(character.HeroObject, this);
  			}
  			else if (countChange < 0)
  			{
  				OwnerParty.OnHeroRemoved(character.HeroObject, this);
  			}
  		}
  		{
  			OwnerParty?.OnRosterSizeChanged(this);
  		}
> 		if (countChange != 0 || woundedCountChange != 0)
  		{
  			UpdateVersion();
  		}
  		return index;
  	}
  
  	private void RemoveRange(int p, int p2)
  	{
  		int num = p2 - p;
  		for (int i = p2; i < _count; i++)
  		{
  			data[i - num] = data[i];
  		}
  		for (int j = _count - num; j < _count; j++)
  		{
  			data[j].Clear();
  		}
  		_count -= num;
  		{
  			data[num3] = data[num3 - 1];
  		}
> 		data[num2] = new TroopRosterElement(character);
  		_count = num;
  		return num2;
  	}
  
  	[Conditional("DEBUG_MORE")]
  	public void CheckValidity()
  	{
  		if (data == null)
  		{
  			return;
  		}
  		int num = 0;
  		for (int i = 0; i < data.Length; i++)
  		{
> 			TroopRosterElement troopRosterElement = data[i];
> 			if (troopRosterElement.Character != null)
  			{
> 				_ = troopRosterElement.Number;
> 				_ = troopRosterElement.WoundedNumber;
  				num++;
  			}
  		}
  	}
  
> 	public int AddToCounts(CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)
  	{
> 		if (count != 0 || woundedCount != 0)
  		{
  			UpdateVersion();
  		}
  		if (insertAtFront)
  		{
  			index = 0;
  		}
  		int num = FindIndexOfTroop(character);
  		if (num < 0)
  		{
> 			if (count + woundedCount <= 0)
  			{
  				TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRoster.cs", "AddToCounts", 572);
  				return -1;
  			}
  			num = AddNewElement(character, index);
  		}
  		else if (index != -1 && index != num)
  		{
  			ShiftTroopToIndex(num, index);
  			num = index;
  		}
> 		AddToCountsAtIndex(num, count, woundedCount, xpChange, removeDepleted);
  		return num;
  	}
  
  	public int GetTroopCount(CharacterObject troop)
  	{
  		int num = FindIndexOfTroop(troop);
  		if (num >= 0)
  		{
  			return data[num].Number;
  		}
  		return 0;
  	}
  
  	public void RemoveZeroCounts()
  	{
  		int num = 0;
  		for (int i = 0; i < _count; i++)
  		{
  		UpdateVersion();
  	}
  
> 	public TroopRosterElement GetElementCopyAtIndex(int index)
  	{
  		return data[index];
  	}
  
  	public void SetElementNumber(int index, int number)
  	{
  		if (index < _count)
  		{
  			data[index].Number = number;
  			UpdateVersion();
  			return;
  		}
  		throw new IndexOutOfRangeException();
  	}
  
  	public int GetElementNumber(int index)
  	{
  		if (index >= 0 && index < _count)
  		return GetElementNumber(FindIndexOfTroop(character));
  	}
  
> 	public void SetElementWoundedNumber(int index, int number)
  	{
  		if (index < _count)
  		{
> 			data[index].WoundedNumber = number;
  			UpdateVersion();
  			return;
  		}
  		throw new IndexOutOfRangeException();
  	}
  
> 	public int GetElementWoundedNumber(int index)
  	{
  		if (index < _count)
  		{
> 			return data[index].WoundedNumber;
  		}
  		throw new IndexOutOfRangeException();
  	}
  
  	public void SetElementXp(int index, int number)
  	{
  		if (index >= 0 && index < _count)
  		{
  			data[index].Xp = number;
  			if (OwnerParty != null)
  			{
  				OwnerParty.OnXpChanged(this, ref data[index]);
  			}
  			return;
  		}
  		throw new IndexOutOfRangeException();
  	}
  
  		}
  		for (int i = 0; i < a.Count; i++)
  		{
> 			TroopRosterElement elementCopyAtIndex = a.GetElementCopyAtIndex(i);
  			int num = b.FindIndexOfTroop(elementCopyAtIndex.Character);
  			if (num == -1)
  			{
  				return false;
  			}
> 			TroopRosterElement elementCopyAtIndex2 = b.GetElementCopyAtIndex(num);
  			if (elementCopyAtIndex.Character != elementCopyAtIndex2.Character || elementCopyAtIndex.Number != elementCopyAtIndex2.Number)
  			{
  				return false;
  			}
  		}
  		return true;
  	}
  
  	public bool Contains(CharacterObject character)
  	{
  		for (int i = 0; i < Count; i++)
  		{
  			if (character == GetElementCopyAtIndex(i).Character)
  			{
  				return true;
  			}
  		}
  		return false;
  
  	public void ValidateTroopListCache()
  	{
> 		if (_troopRosterElementsVersion == VersionNo)
  		{
  			return;
  		}
  		lock (this)
  		{
> 			if (_troopRosterElementsVersion != VersionNo)
  			{
> 				_troopRosterElements.Clear();
> 				_troopRosterElements.Capacity = Math.Max(_troopRosterElements.Capacity, Count);
  				for (int i = 0; i < Count; i++)
  				{
> 					_troopRosterElements.Add(GetElementCopyAtIndex(i));
  				}
> 				_troopRosterElementsVersion = VersionNo;
  			}
  		}
  	}
  
> 	public MBList<TroopRosterElement> GetTroopRoster()
  	{
  		ValidateTroopListCache();
> 		return _troopRosterElements;
  	}
  
  	public void Clear()
  	{
  		for (int num = _count - 1; num >= 0; num--)
  		{
> 			AddToCountsAtIndex(num, -data[num].Number, -data[num].WoundedNumber);
  		}
  	}
  
> 	public void RemoveTroop(CharacterObject troop, int numberToRemove = 1, UniqueTroopDescriptor troopSeed = default(UniqueTroopDescriptor), int xp = 0)
  	{
  		int index = FindIndexOfTroop(troop);
  		bool removeDepleted = true;
  		if (PlayerEncounter.CurrentBattleSimulation != null && !troop.IsHero)
  		{
  			removeDepleted = false;
  		}
  		AddToCountsAtIndex(index, -numberToRemove, 0, (!troop.IsHero) ? (-xp) : 0, removeDepleted);
  	}
  
> 	public void WoundTroop(CharacterObject troop, int numberToWound = 1, UniqueTroopDescriptor troopSeed = default(UniqueTroopDescriptor))
  	{
  		int index = FindIndexOfTroop(troop);
  		AddToCountsAtIndex(index, 0, numberToWound);
  	}
  
  	void ISerializableObject.SerializeTo(IWriter writer)
  	{
  		writer.WriteInt(Count);
  		writer.WriteInt(VersionNo);
  		if (data != null)
  		{
  			writer.WriteInt(data.Length);
> 			TroopRosterElement[] array = data;
> 			foreach (TroopRosterElement troopRosterElement in array)
  			{
> 				writer.WriteSerializableObject(troopRosterElement);
  			}
  		}
  		else
  		{
  			writer.WriteInt(0);
  		}
  	}
  
  	void ISerializableObject.DeserializeFrom(IReader reader)
  	{
  		_count = reader.ReadInt();
  		VersionNo = reader.ReadInt();
  		int num = reader.ReadInt();
> 		data = new TroopRosterElement[num];
  		for (int i = 0; i < num; i++)
  		{
> 			data[i] = (TroopRosterElement)(object)reader.ReadSerializableObject();
  		}
  	}
  
> 	public int Sum(Func<TroopRosterElement, int> selector)
  	{
  		int num = 0;
  		for (int i = 0; i < _count; i++)
  		{
  			num += selector(data[i]);
  		}
  		return num;
  	}
  
  	public void OnHeroHealthStatusChanged(Hero hero)
  	{
  		UpdateVersion();
> 		_totalWoundedHeroes += (hero.IsWounded ? 1 : (-1));
  	}
  
  	public void UpdateVersion()
  	{
  		OwnerParty?.MobileParty?.UpdateVersionNo();
  		VersionNo++;
  	}
  
  	public TroopRoster CloneRosterData()
  	{
  		TroopRoster troopRoster = new TroopRoster();
  		if (data != null)
  		{
  			for (int i = 0; i < data.Length; i++)
  			{
> 				TroopRosterElement troopRosterElement = data[i];
> 				if (troopRosterElement.Character != null)
  				{
> 					troopRoster.AddToCounts(data[i].Character, troopRosterElement.Number, insertAtFront: false, troopRosterElement.WoundedNumber);
  				}
  			}
  		}
  		return troopRoster;
  	}
  
  	public void AddXpToTroop(CharacterObject troop, int xpAmount)
  	{
  		int index = FindIndexOfTroop(troop);
  		AddXpToTroopAtIndex(index, xpAmount);
  	}
  
  	public void AddXpToTroopAtIndex(int index, int xpAmount)
  	{
  		if (index >= 0 && index < _count)
  		{
  			xpAmount += GetElementXp(data[index].Character);
  			SetElementXp(index, xpAmount);

 succeeded in 1609ms:
  		protected override int AlternativeSolutionBaseDurationInDaysInternal => 6 + TaleWorlds.Library.MathF.Ceiling(7f * base.IssueDifficultyMultiplier);
  
  		protected override int RewardGold => 2000 + RequestedRecruitCount * 100;
  
> 		public override TextObject IssueBriefByIssueGiver => new TextObject("{=YxtiyxSf}Yes... As you no doubt know, this is rough work, and I've lost a lot of good lads recently. I haven't had much luck replacing them. I need men who understand how things work in our business, and that's not always easy to find. I could use bandits and looters. They usually know their stuff. But if I take them in as prisoners, they'll just slip away as soon as I get the chance. I need volunteers...[ib:hip][if:convo_undecided_closed]");
  
  		public override TextObject IssueAcceptByPlayer => new TextObject("{=jGpBZDvC}I see. What do you want from me?");
  
> 		public override TextObject IssueQuestSolutionExplanationByIssueGiver => new TextObject("{=Qh26ReAv}Look, I know that warriors like you can sometimes recruit bandits to your party. Some of those men might want to take their chances working for me. More comfortable in living in town, where there's always drink and women on hand, then roaming endlessly about the countryside, eh? For each one that signs up with me I'll give you a bounty, more if they have some experience.[if:convo_innocent_smile][ib:hip]");
  
  		public override TextObject IssueQuestSolutionAcceptByPlayer => new TextObject("{=ekLDmgS7}I'll find your recruits.");
  
  		public override TextObject IssueAlternativeSolutionExplanationByIssueGiver => new TextObject("{=bKfaMFVK}You can also send me a recruiter: a trustworthy companion who is good at leading men, and also enough of a rogue to win the trust of other rogues...[if:convo_undecided_open][ib:confident]");
  
  		public override TextObject IssueAlternativeSolutionAcceptByPlayer => new TextObject("{=kxvnA811}All right, I will send you someone from my party who fits your bill.");
  
  		public override TextObject IssueAlternativeSolutionResponseByIssueGiver => new TextObject("{=8sDjwsnW}I'm sure your lieutenant will solve my problem. Thank you for your help.[if:convo_nonchalant][ib:demure2]");
  
  		public override TextObject IssueDiscussAlternativeSolution => new TextObject("{=TkvsBd4H}Your companion seems to have a knack with the local never-do-wells. I hear a lot of fine lads have already signed up.[if:convo_relaxed_happy][ib:hip2]");
  
  		public override bool IsThereAlternativeSolution => true;
  
  		public override bool IsThereLordSolution => false;
  
  		protected override TextObject AlternativeSolutionStartLog
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=wX14wxqF}You asked {COMPANION.LINK} to deliver at least {WANTED_RECRUIT_AMOUNT} looters and bandits to {ISSUE_GIVER.LINK} in {SETTLEMENT}. They should rejoin your party in {RETURN_DAYS} days.");
  				textObject.SetCharacterProperties("ISSUE_GIVER", base.IssueOwner.CharacterObject);
  				textObject.SetCharacterProperties("COMPANION", base.AlternativeSolutionHero.CharacterObject);
  				textObject.SetTextVariable("WANTED_RECRUIT_AMOUNT", RequestedRecruitCount);
  				textObject.SetTextVariable("SETTLEMENT", base.IssueOwner.CurrentSettlement.EncyclopediaLinkWithName);
> 				textObject.SetTextVariable("RETURN_DAYS", GetTotalAlternativeSolutionDurationInDays());
> 				return textObject;
  			}
  		}
  
  		public override TextObject Title => new TextObject("{=rrh7rSLs}Gang Needs Recruits");
  
  		public override TextObject Description
  		{
  			get
  			{
  				TextObject textObject = new TextObject("{=0kYaAb7c}A gang leader needs recruits for {?ISSUE_GIVER.GENDER}her{?}his{\\?} gang.");
  				textObject.SetCharacterProperties("ISSUE_GIVER", base.IssueOwner.CharacterObject);
> 				return textObject;
  			}
  		}
  
  		protected override int CompanionSkillRewardXP => (int)(500f + 700f * base.IssueDifficultyMultiplier);
  
  		internal static void AutoGeneratedStaticCollectObjectsGangLeaderNeedsRecruitsIssue(object o, List<object> collectedObjects)
  		{
  			((GangLeaderNeedsRecruitsIssue)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  		{
  			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		public GangLeaderNeedsRecruitsIssue(Hero issueOwner)
  			: base(issueOwner, CampaignTime.DaysFromNow(30f))
  		{
  		}
  
  		protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
  		{
  			if (issueEffect == DefaultIssueEffects.IssueOwnerPower)
  			{
> 				return -0.1f;
  			}
> 			return 0f;
  		}
  
  		public override (SkillObject, int) GetAlternativeSolutionSkill(Hero hero)
  		{
> 			return ((hero.GetSkillValue(DefaultSkills.Leadership) >= hero.GetSkillValue(DefaultSkills.Roguery)) ? DefaultSkills.Leadership : DefaultSkills.Roguery, 120);
  		}
  
  		public override bool DoTroopsSatisfyAlternativeSolution(TroopRoster troopRoster, out TextObject explanation)
  		{
> 			return QuestHelper.CheckRosterForAlternativeSolution(troopRoster, GetTotalAlternativeSolutionNeededMenCount(), out explanation, 2);
  		}
  
  		public override bool AlternativeSolutionCondition(out TextObject explanation)
  		{
> 			return QuestHelper.CheckRosterForAlternativeSolution(MobileParty.MainParty.MemberRoster, GetTotalAlternativeSolutionNeededMenCount(), out explanation, 2);
  		}
  
  		public override bool IsTroopTypeNeededByAlternativeSolution(CharacterObject character)
  		{
> 			return character.Tier >= 2;
  		}
  
  		protected override void OnGameLoad()
  		{
  		}
  
  		protected override void HourlyTick()
  		{
  		}
  
  		protected override QuestBase GenerateIssueQuest(string questId)
  		{
> 			return new GangLeaderNeedsRecruitsIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(30f), RequestedRecruitCount);
  		}
  
  		public override IssueFrequency GetFrequency()
  		{
> 			return IssueFrequency.VeryCommon;
  		}
  
  		protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
  		{
  			flag = PreconditionFlags.None;
  			relationHero = null;
  			requiredGold = 0;
  			skill = null;
  			if (issueGiver.GetRelationWithPlayer() < -10f)
  			{
  				flag |= PreconditionFlags.Relation;
  				relationHero = issueGiver;
  			}
> 			return flag == PreconditionFlags.None;
  		}
  
  		public override bool IssueStayAliveConditions()
  		{
> 			return true;
  		}
  
  		protected override void CompleteIssueWithTimedOutConsequences()
  		{
  		}
  
> 		protected override void AlternativeSolutionEndWithSuccessConsequence()
  		{
  			TraitLevelingHelper.OnIssueSolvedThroughQuest(base.IssueOwner, new Tuple<TraitObject, int>[1]
  			{
  				new Tuple<TraitObject, int>(DefaultTraits.Honor, 30)
  			});
  			base.IssueOwner.AddPower(10f);
  			RelationshipChangeWithIssueOwner = 5;
  		}
  	}
  
> 	public class GangLeaderNeedsRecruitsIssueQuest : QuestBase
  	{
> 		private const int QuestGiverRelationBonusOnSuccess = 5;
  
> 		private const int QuestGiverNotablePowerBonusOnSuccess = 10;
  
  		private const int QuestGiverRelationPenaltyOnFail = -5;
  
  		private const int NotablePowerPenaltyOnFail = -10;
  
> 		private const int PlayerHonorBonusOnSuccess = 30;
  
  		[SaveableField(1)]
  		private int _requestedRecruitCount;
  
  		[SaveableField(5)]
  		private int _deliveredRecruitCount;
  
  		[SaveableField(6)]
  		private int _rewardGold;
  
  		[SaveableField(9)]
  		private bool _playerReachedRequestedAmount;
  
  		[SaveableField(7)]
  		private JournalLog _questProgressLogTest;
  
  		public override TextObject Title => new TextObject("{=rrh7rSLs}Gang Needs Recruits");
  
  		public override bool IsRemainingTimeHidden => false;
  
  		private TextObject QuestStartedLogText
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=PZI9Smv3}{QUEST_GIVER.LINK}, a gang leader in {SETTLEMENT}, told you that {?QUEST_GIVER.GENDER}she{?}he{\\?} needs recruits for {?QUEST_GIVER.GENDER}her{?}his{\\?} gang. {?QUEST_GIVER.GENDER}She{?}He{\\?} asked you to recruit {NEEDED_RECRUIT_AMOUNT} looters or bandits into your party, then transfer them to {?QUEST_GIVER.GENDER}her{?}him{\\?}. You will be paid for the recruits depending on their experience.");
  				textObject.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject);
  				textObject.SetTextVariable("SETTLEMENT", base.QuestGiver.CurrentSettlement.EncyclopediaLinkWithName);
  				textObject.SetTextVariable("NEEDED_RECRUIT_AMOUNT", _requestedRecruitCount);
> 				return textObject;
  			}
  		}
  
> 		private TextObject QuestSuccessLog
  		{
  			get
  			{
  				TextObject textObject = new TextObject("{=3ApJ6LaX}You have transferred the recruits to {QUEST_GIVER.LINK} as promised.");
  				textObject.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject);
> 				return textObject;
  			}
  		}
  
  		private TextObject QuestFailedWithTimeOutLogText
  		{
  			get
  			{
  				TextObject textObject = new TextObject("{=iUmWTmQz}You have failed to deliver enough recruits in time. {QUEST_GIVER.LINK} must be disappointed.");
  				textObject.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject);
> 				return textObject;
  			}
  		}
  
> 		internal static void AutoGeneratedStaticCollectObjectsGangLeaderNeedsRecruitsIssueQuest(object o, List<object> collectedObjects)
  		{
> 			((GangLeaderNeedsRecruitsIssueQuest)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  		{
  			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  			collectedObjects.Add(_questProgressLogTest);
  		}
  
  		internal static object AutoGeneratedGetMemberValue_requestedRecruitCount(object o)
  		{
> 			return ((GangLeaderNeedsRecruitsIssueQuest)o)._requestedRecruitCount;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_deliveredRecruitCount(object o)
  		{
> 			return ((GangLeaderNeedsRecruitsIssueQuest)o)._deliveredRecruitCount;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_rewardGold(object o)
  		{
> 			return ((GangLeaderNeedsRecruitsIssueQuest)o)._rewardGold;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_playerReachedRequestedAmount(object o)
  		{
> 			return ((GangLeaderNeedsRecruitsIssueQuest)o)._playerReachedRequestedAmount;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_questProgressLogTest(object o)
  		{
> 			return ((GangLeaderNeedsRecruitsIssueQuest)o)._questProgressLogTest;
  		}
  
> 		public GangLeaderNeedsRecruitsIssueQuest(string questId, Hero questGiver, CampaignTime duration, int requestedRecruitCount)
  			: base(questId, questGiver, duration, 0)
  		{
  			_requestedRecruitCount = requestedRecruitCount;
  			_deliveredRecruitCount = 0;
  			_rewardGold = 2000;
  			_playerReachedRequestedAmount = false;
  			SetDialogs();
  			InitializeQuestOnCreation();
  		}
  
  		private void QuestAcceptedConsequences()
  		{
  			StartQuest();
  			AddTrackedObject(base.QuestGiver.CurrentSettlement);
  			_questProgressLogTest = AddDiscreteLog(QuestStartedLogText, new TextObject("{=r8rwl9ZS}Delivered Recruits"), _deliveredRecruitCount, _requestedRecruitCount);
  		}
  
  		protected override void SetDialogs()
  		{
  			OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start").NpcLine(new TextObject("{=0QuAZ8YO}I'll be waiting. Good luck.[if:convo_relaxed_happy][ib:confident]")).Condition(() => Hero.OneToOneConversationHero == base.QuestGiver)
  				.Consequence(QuestAcceptedConsequences)
  				.CloseDialog();
  			TextObject npcDiscussLine = new TextObject("{=!}{GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_DISCUSS}");
  			TextObject npcResponseLine = new TextObject("{=!}{GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_RESPONSE}");
  			bool changeDialogAfterTransfer = false;
  			DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss").BeginNpcOptions().NpcOption(new TextObject("{=BGgDjRcW}I think that's enough. Here is your payment."), () => Hero.OneToOneConversationHero == base.QuestGiver && _playerReachedRequestedAmount)
  				.Consequence(delegate
  				{
> 					ApplyQuestSuccessConsequences();
> 					CompleteQuestWithSuccess();
  				})
  				.CloseDialog()
  				.NpcOption(npcDiscussLine, delegate
  				{
  					if (Hero.OneToOneConversationHero != base.QuestGiver)
  					{
> 						return false;
  					}
  					if (!changeDialogAfterTransfer)
  					{
  						npcDiscussLine.SetTextVariable("GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_DISCUSS", new TextObject("{=1hpeeCJD}Have you found any good men?[ib:confident3]"));
  						changeDialogAfterTransfer = true;
  					}
  					else
  					{
  						npcDiscussLine.SetTextVariable("GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_DISCUSS", new TextObject("{=ds294zxi}Anything else?"));
  						changeDialogAfterTransfer = false;
  					}
> 					return true;
  				})
  				.BeginPlayerOptions()
  				.PlayerOption(new TextObject("{=QbaOoilS}Yes, I have brought you a few men."))
  				.Condition(() => CheckIfThereIsSuitableRecruitInPlayer() && !_playerReachedRequestedAmount && changeDialogAfterTransfer)
  				.NpcLine(npcResponseLine)
  				.Condition(delegate
  				{
  					if (_playerReachedRequestedAmount)
  					{
> 						return false;
  					}
  					npcResponseLine.SetTextVariable("GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_RESPONSE", new TextObject("{=70LnOZzo}Very good. Keep searching. We still need more men.[ib:hip2]"));
> 					return true;
  				})
  				.Consequence(OpenRecruitDeliveryScreen)
  				.PlayerLine(new TextObject("{=IULW8h03}Sure."))
  				.Consequence(delegate
  				{
  					if (_playerReachedRequestedAmount && Campaign.Current.ConversationManager.IsConversationInProgress)
  					{
  						Campaign.Current.ConversationManager.ContinueConversation();
  					}
  				})
  				.GotoDialogState("quest_discuss")
  				.PlayerOption(new TextObject("{=PZqGagXt}No, not yet. I'm still looking for them."))
  				.Condition(() => !_playerReachedRequestedAmount && changeDialogAfterTransfer)
  				.Consequence(delegate
  				{
  					changeDialogAfterTransfer = false;
  				})
  				.NpcLine(new TextObject("{=L1JyetPq}I am glad to hear that.[ib:closed2]"))
  				.CloseDialog()
  				.PlayerOption(new TextObject("{=OlOhuO7X}No thank you. Good day to you."))
  				.Condition(() => !_playerReachedRequestedAmount && !changeDialogAfterTransfer)
  				.CloseDialog()
  				.EndPlayerOptions()
  				.CloseDialog()
  		{
  			PartyScreenHelper.OpenScreenWithCondition(IsTroopTransferable, DoneButtonCondition, DoneClicked, null, PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable, base.QuestGiver.Name, _requestedRecruitCount - _deliveredRecruitCount, showProgressBar: false, isDonating: false, PartyScreenHelper.PartyScreenMode.TroopsManage);
  		}
  
> 		private Tuple<bool, TextObject> DoneButtonCondition(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum)
  		{
  			if (_requestedRecruitCount - _deliveredRecruitCount < leftMemberRoster.TotalManCount)
  			{
  				int num = _requestedRecruitCount - _deliveredRecruitCount;
  				TextObject textObject = new TextObject("{=VOr3uoRZ}You can only transfer {X} recruit{?IS_PLURAL}s{?}{\\?}.");
  				textObject.SetTextVariable("IS_PLURAL", (num > 1) ? 1 : 0);
  				textObject.SetTextVariable("X", num);
> 				return new Tuple<bool, TextObject>(item1: false, textObject);
  			}
> 			return new Tuple<bool, TextObject>(item1: true, null);
  		}
  
> 		private bool DoneClicked(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, FlattenedTroopRoster takenPrisonerRoster, FlattenedTroopRoster releasedPrisonerRoster, bool isForced, PartyBase leftParty, PartyBase rightParty)
  		{
  			foreach (TroopRosterElement item in leftMemberRoster.GetTroopRoster())
  			{
  				_rewardGold += RewardForEachRecruit(item.Character) * item.Number;
  				_deliveredRecruitCount += item.Number;
  			}
  			_questProgressLogTest.UpdateCurrentProgress(_deliveredRecruitCount);
  			_questProgressLogTest.TaskName.SetTextVariable("TOTAL_REWARD", _rewardGold);
  			if (_deliveredRecruitCount == _requestedRecruitCount)
  			{
  				_playerReachedRequestedAmount = true;
  				if (Campaign.Current.ConversationManager.IsConversationInProgress)
  				{
  					Campaign.Current.ConversationManager.ContinueConversation();
  				}
  			}
> 			return true;
  		}
  
  		private int RewardForEachRecruit(CharacterObject recruit)
  		{
> 			return (int)(100f * ((recruit.Tier <= 1) ? 1f : ((recruit.Tier <= 3) ? 1.5f : 2f)));
  		}
  
  		private bool IsTroopTransferable(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
  		{
  			if (_requestedRecruitCount - _deliveredRecruitCount >= 0)
  			{
  				if (side != PartyScreenLogic.PartyRosterSide.Left)
  				{
  					if (MobileParty.MainParty.MemberRoster.Contains(character))
  					{
> 						return character.Occupation == Occupation.Bandit;
  					}
> 					return false;
  				}
> 				return true;
  			}
> 			return false;
  		}
  
  		private bool CheckIfThereIsSuitableRecruitInPlayer()
  		{
  			bool result = false;
  			foreach (TroopRosterElement item in MobileParty.MainParty.MemberRoster.GetTroopRoster())
  			{
> 				if (item.Character.Occupation == Occupation.Bandit)
  				{
  					result = true;
  					break;
  				}
  			}
> 			return result;
  		}
  
> 		private void ApplyQuestSuccessConsequences()
  		{
> 			AddLog(QuestSuccessLog);
  			TraitLevelingHelper.OnIssueSolvedThroughQuest(base.QuestGiver, new Tuple<TraitObject, int>[1]
  			{
  				new Tuple<TraitObject, int>(DefaultTraits.Honor, 30)
  			});
  			GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, _rewardGold);
  			base.QuestGiver.AddPower(10f);
  			RelationshipChangeWithQuestGiver = 5;
  		}
  
> 		protected override void OnBeforeTimedOut(ref bool completeWithSuccess, ref bool doNotResolveTheQuest)
  		{
  			if (_deliveredRecruitCount >= _requestedRecruitCount)
  			{
> 				completeWithSuccess = true;
> 				ApplyQuestSuccessConsequences();
  			}
  		}
  
  		protected override void OnTimedOut()
  		{
  			AddLog(QuestFailedWithTimeOutLogText);
  			base.QuestGiver.AddPower(-10f);
  			RelationshipChangeWithQuestGiver = -5;
  		}
  
  		protected override void InitializeQuestOnGameLoad()
  		{
  			SetDialogs();
  		}
  
  		protected override void HourlyTick()
  		{
  		}
  	}
  
  	public class GangLeaderNeedsRecruitsIssueBehaviorTypeDefiner : SaveableTypeDefiner
  	{
  		public GangLeaderNeedsRecruitsIssueBehaviorTypeDefiner()
  			: base(820000)
  
  		protected override void DefineClassTypes()
  		{
  			AddClassDefinition(typeof(GangLeaderNeedsRecruitsIssue), 1);
> 			AddClassDefinition(typeof(GangLeaderNeedsRecruitsIssueQuest), 2);
  		}
  	}
  
  	private const IssueBase.IssueFrequency GangLeaderNeedsRecruitsIssueFrequency = IssueBase.IssueFrequency.VeryCommon;
  
  	public override void RegisterEvents()
  	{
  		CampaignEvents.OnCheckForIssueEvent.AddNonSerializedListener(this, OnCheckForIssue);
  	}
  
  	public override void SyncData(IDataStore dataStore)
  	{
  	}
  
  	private static bool ConditionsHold(Hero issueGiver)
  	{
  		if (issueGiver.CurrentSettlement != null)
  		{
> 			return issueGiver.IsGangLeader;
  		}
> 		return false;
  	}
  
  	public void OnCheckForIssue(Hero hero)
  	{
  		if (ConditionsHold(hero))
  		{
  			Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(OnSelected, typeof(GangLeaderNeedsRecruitsIssue), IssueBase.IssueFrequency.VeryCommon));
  		}
  		else
  		{
  			Campaign.Current.IssueManager.AddPotentialIssueData(hero, new PotentialIssueData(typeof(GangLeaderNeedsRecruitsIssue), IssueBase.IssueFrequency.VeryCommon));
  		}
  	}
  
  	private static IssueBase OnSelected(in PotentialIssueData pid, Hero issueOwner)
  	{
> 		return new GangLeaderNeedsRecruitsIssue(issueOwner);
  	}
  }

 succeeded in 1658ms:
  namespace TaleWorlds.CampaignSystem.Roster;
  
> public struct TroopRosterElement : ISerializableObject, ISavedStruct
  {
  	private static readonly TroopRosterElement StaticInvalidElement = new TroopRosterElement(null);
  
  	[SaveableField(0)]
> 	private int _number;
  
  	[SaveableField(1)]
> 	private int _woundedNumber;
  
  	[SaveableField(2)]
  	private int _xp;
  
  	[SaveableField(4)]
> 	public CharacterObject Character;
  
> 	public static TroopRosterElement Invalid => StaticInvalidElement;
  
> 	public int Number
  	{
  		get
  		{
> 			return _number;
  		}
  		set
  		{
  			if (value < 0)
  			{
> 				throw new MBUnderFlowException("ItemRosterElement::Character");
  			}
> 			_number = value;
  		}
  	}
  
> 	public int WoundedNumber
  	{
  		get
  		{
> 			if (Character.IsHero)
  			{
> 				if (!Character.HeroObject.IsWounded)
  				{
  					return 0;
  				}
  				return 1;
  			}
> 			return _woundedNumber;
  		}
  		set
  		{
  			if (value < 0)
  			{
> 				throw new MBUnderFlowException("ItemRosterElement::WoundedNumber");
  			}
> 			_woundedNumber = value;
  		}
  	}
  
> 	public int Xp
  	{
  		get
  		{
  			return _xp;
  		}
  		set
  		{
  			if (value < 0)
  			{
  				Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\Roster\\TroopRosterElement.cs", "Xp", 77);
  				_xp = 0;
  			}
  	}
  
> 	public static void AutoGeneratedStaticCollectObjectsTroopRosterElement(object o, List<object> collectedObjects)
  	{
  		((TroopRosterElement)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  	private void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  	{
> 		collectedObjects.Add(Character);
  	}
  
> 	internal static object AutoGeneratedGetMemberValueCharacter(object o)
  	{
> 		return ((TroopRosterElement)o).Character;
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_number(object o)
  	{
> 		return ((TroopRosterElement)o)._number;
  	}
  
> 	internal static object AutoGeneratedGetMemberValue_woundedNumber(object o)
  	{
> 		return ((TroopRosterElement)o)._woundedNumber;
  	}
  
  	internal static object AutoGeneratedGetMemberValue_xp(object o)
  	{
  		return ((TroopRosterElement)o)._xp;
  	}
  
> 	public TroopRosterElement(CharacterObject character)
  	{
> 		Character = character;
> 		_number = 0;
> 		_woundedNumber = 0;
  		_xp = 0;
  	}
  
  	internal void Clear()
  	{
> 		Character = null;
> 		_number = 0;
  	}
  
  	void ISerializableObject.SerializeTo(IWriter writer)
  	{
> 		writer.WriteUInt((Character != null) ? Character.Id.InternalValue : 0u);
> 		writer.WriteInt(_number);
> 		writer.WriteInt(_woundedNumber);
  		writer.WriteInt(_xp);
  	}
  
  	void ISerializableObject.DeserializeFrom(IReader reader)
  	{
  		uint num = reader.ReadUInt();
> 		Character = null;
  		if (num != 0)
  		{
  			MBGUID objectId = new MBGUID(num);
> 			Character = MBObjectManager.Instance.GetObject(objectId) as CharacterObject;
  		}
> 		_number = reader.ReadInt();
> 		_woundedNumber = reader.ReadInt();
  	}
  
> 	public override string ToString()
  	{
> 		return Number + " " + Character;
  	}
  
> 	public override bool Equals(object obj)
  	{
  		if (!(obj is TroopRosterElement other))
  		{
  			return false;
  		}
  		return Equals(other);
  	}
  
> 	public bool Equals(TroopRosterElement other)
  	{
> 		return Character == other.Character;
  	}
  
> 	public override int GetHashCode()
  	{
> 		if (Character == null)
  		{
  			return 0;
  		}
> 		return Character.GetHashCode();
  	}
  
  	bool ISavedStruct.IsDefault()
  	{
> 		if (Character == null && _number == 0 && _woundedNumber == 0)
  		{
  			return _xp == 0;
  		}
  		return false;
  	}
  }

 succeeded in 1667ms:
  
  	private bool _isMariner;
  
> 	private Occupation _occupation;
  
  	public override TextObject Name
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.Name;
  			}
  			return base.Name;
  		}
  	}
  
  	public string EncyclopediaLink
  	{
  		get
  		{
> 			if (!IsHero)
  			{
  				return Campaign.Current.EncyclopediaManager.GetIdentifier(typeof(CharacterObject)) + "-" + base.StringId;
  			}
  			return _heroObject.EncyclopediaLink;
  		}
  	}
  
  	public TextObject EncyclopediaLinkWithName
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return _heroObject.EncyclopediaLinkWithName;
  			}
  			if (Campaign.Current.EncyclopediaManager.GetPageOf(typeof(CharacterObject)).IsValidEncyclopediaItem(this))
  			{
  				return HyperlinkTexts.GetUnitHyperlinkText(EncyclopediaLink, Name);
  			}
  			return Name;
  		}
  	}
  
  	public bool HiddenInEncyclopedia { get; set; }
  
  	public bool IsNotTransferableInPartyScreen => (_characterRestrictionFlags & CharacterRestrictionFlags.NotTransferableInPartyScreen) == CharacterRestrictionFlags.NotTransferableInPartyScreen;
  
  	public bool IsNotTransferableInHideouts => (_characterRestrictionFlags & CharacterRestrictionFlags.CanNotGoInHideout) == CharacterRestrictionFlags.CanNotGoInHideout;
  	{
  		get
  		{
> 			if (!IsHero)
  			{
  				return base.Equipment;
  			}
  			return HeroObject.BattleEquipment;
  		}
  	}
  
  	public override IEnumerable<Equipment> BattleEquipments
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return new List<Equipment> { HeroObject.BattleEquipment }.AsEnumerable();
  			}
  			return base.BattleEquipments;
  		}
  	}
  
  	public override IEnumerable<Equipment> CivilianEquipments
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return new List<Equipment> { HeroObject.CivilianEquipment }.AsEnumerable();
  			}
  			return base.CivilianEquipments;
  		}
  	}
  
  	public IEnumerable<Equipment> StealthEquipments
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return new List<Equipment> { HeroObject.StealthEquipment }.AsEnumerable();
  			}
  			if (Culture.DefaultBattleEquipmentRoster != null)
  			{
  				return Culture.DefaultStealthEquipmentRoster.AllEquipments.AsEnumerable();
  			}
  			return new MBReadOnlyList<Equipment>().AsEnumerable();
  		}
  	}
  
  	public override Equipment FirstBattleEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.BattleEquipment;
  			}
  			return base.FirstBattleEquipment;
  		}
  	}
  
  	public override Equipment FirstCivilianEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.CivilianEquipment;
  			}
  			return base.FirstCivilianEquipment;
  		}
  	}
  
  	public Equipment FirstStealthEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.StealthEquipment;
  			}
  			return Culture.DefaultStealthEquipmentRoster.AllEquipments.First();
  		}
  	}
  
  	public override Equipment RandomBattleEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.BattleEquipment;
  			}
  			return base.RandomBattleEquipment;
  		}
  	}
  
  	public override Equipment RandomCivilianEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.CivilianEquipment;
  			}
  			return base.RandomCivilianEquipment;
  		}
  	}
  
  	public override int HitPoints
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.HitPoints;
  			}
  			return MaxHitPoints();
  		}
  	}
  
  	public Equipment RandomStealthEquipment
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.StealthEquipment;
  			}
  			return Culture.DefaultStealthEquipmentRoster.AllEquipments.GetRandomElement();
  		}
  	}
  
  	public ExplainedNumber MaxHitPointsExplanation => Campaign.Current.Models.CharacterStatsModel.MaxHitpoints(this, includeDescriptions: true);
  
  	public override int Level
  	{
  		get
  		{
> 			if (!IsHero)
  			{
  				return base.Level;
  			}
  			return HeroObject.Level;
  		}
  	}
  
  	public static CharacterObject PlayerCharacter => Game.Current.PlayerTroop as CharacterObject;
  
  	public static CharacterObject OneToOneConversationCharacter => Campaign.Current.ConversationManager.OneToOneConversationCharacter;
  
  	public static IEnumerable<CharacterObject> ConversationCharacters => Campaign.Current.ConversationManager.ConversationCharacters;
  
  	public bool IsMariner => _isMariner;
  
  	public new CultureObject Culture
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.Culture;
  			}
  			return (CultureObject)base.Culture;
  		}
  		private set
  		{
  			base.Culture = value;
  		}
  	}
  
  	public override bool IsFemale
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return HeroObject.IsFemale;
  			}
  			return base.IsFemale;
  		}
  	}
  
  	public bool IsBasicTroop { get; set; }
  
  	public bool IsTemplate { get; private set; }
  
  	public bool IsChildTemplate { get; private set; }
  
  	public override bool IsPlayerCharacter => PlayerCharacter == this;
  
> 	public override bool IsHero => _heroObject != null;
  
  	public bool IsRegular => _heroObject == null;
  
> 	public Occupation Occupation
  	{
  		get
  		{
> 			if (IsHero)
  			{
> 				return HeroObject.Occupation;
  			}
> 			return _occupation;
  		}
  	}
  
  	public override float Age => HeroObject?.Age ?? base.Age;
  
  	public int ConformityNeededToRecruitPrisoner => Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetConformityNeededToRecruitPrisoner(this);
  
  	public CharacterObject[] UpgradeTargets { get; private set; } = new CharacterObject[0];
  
  	public ItemCategory UpgradeRequiresItemFromCategory { get; private set; }
  
  	public override bool IsMounted
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return Equipment[10].Item != null;
  			}
  			return base.IsMounted;
  		}
  	}
  
  	public override bool IsRanged
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				for (int i = 0; i < 4; i++)
  				{
  					ItemObject item = Equipment[i].Item;
  					if (item != null && (item.ItemType == ItemObject.ItemTypeEnum.Bow || item.ItemType == ItemObject.ItemTypeEnum.Crossbow || item.ItemType == ItemObject.ItemTypeEnum.Sling))
  					{
  						return true;
  					}
  				}
  			}
  			return base.IsRanged;
  		}
  	}
  
  	public int TroopWage
  	{
  		get
  		{
> 			if (IsHero)
  			{
  				return 2 + Level * 2;
  			}
  			return Campaign.Current.Models.PartyWageModel.GetCharacterWage(this);
  		}
  	}
  
  	public int Tier => Campaign.Current.Models.CharacterStatsModel.GetTier(this);
  
  	public static MBReadOnlyList<CharacterObject> All => Campaign.Current.Characters;
  
  	internal static void AutoGeneratedStaticCollectObjectsCharacterObject(object o, List<object> collectedObjects)
  	{
  		((CharacterObject)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  	}
  
  
  	private void Init()
  	{
> 		_occupation = Occupation.NotAssigned;
  		_characterTraits = new PropertyOwner<TraitObject>();
  		Level = 1;
  		_characterRestrictionFlags = CharacterRestrictionFlags.None;
  	}
  
  	public static CharacterObject CreateFrom(CharacterObject character, StaticBodyProperties? staticBodyProperties = null)
  	{
  		CharacterObject characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();
  		characterObject._originCharacter = character._originCharacter ?? character;
> 		if (characterObject.IsHero)
  		{
  			if (staticBodyProperties.HasValue)
  			{
  				characterObject.HeroObject.StaticBodyProperties = staticBodyProperties.Value;
  			}
  			else
  			{
> 				characterObject.HeroObject.StaticBodyProperties = (character.IsHero ? character.HeroObject.StaticBodyProperties : character.GetBodyPropertiesMin().StaticProperties);
  			}
  		}
> 		characterObject._occupation = character._occupation;
  		characterObject._persona = character._persona;
  		characterObject._characterTraits = new PropertyOwner<TraitObject>(character._characterTraits);
  		characterObject._isMariner = character.IsMariner;
  		characterObject._civilianEquipmentTemplate = character._civilianEquipmentTemplate;
  		characterObject._battleEquipmentTemplate = character._battleEquipmentTemplate;
  		characterObject.HiddenInEncyclopedia = character.HiddenInEncyclopedia;
  		characterObject.FillFrom(character);
  		return characterObject;
  	}
  
  	public override void AfterRegister()
  	{
  		base.AfterRegister();
  		if (Equipment != null)
  		{
  			Equipment.SyncEquipments = true;
  
  	public override BodyProperties GetBodyPropertiesMin(bool returnBaseValue = false)
  	{
> 		if (IsHero && !returnBaseValue)
  		{
  			return HeroObject.BodyProperties;
  		}
  		return base.GetBodyPropertiesMin();
  	}
  
  	public override BodyProperties GetBodyPropertiesMax(bool returnBaseValue = false)
  	{
> 		if (IsHero && !returnBaseValue)
  		{
  			return HeroObject.BodyProperties;
  		}
  		return base.GetBodyPropertiesMax();
  	}
  
  	public override void UpdatePlayerCharacterBodyProperties(BodyProperties properties, int race, bool isFemale)
  	{
> 		if (IsPlayerCharacter && IsHero)
  		{
  			HeroObject.StaticBodyProperties = properties.StaticProperties;
  			HeroObject.Weight = properties.Weight;
  			HeroObject.Build = properties.Build;
  			base.Race = race;
  			HeroObject.IsFemale = isFemale;
  			CampaignEventDispatcher.Instance.OnPlayerBodyPropertiesChanged();
  		}
  	}
  
> 	public Occupation GetDefaultOccupation()
  	{
> 		return _occupation;
  	}
  
  	public bool HasThrowingWeapon()
  	{
  		for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)
  		{
  			ItemObject item = Equipment[equipmentIndex].Item;
  			if (item != null && item.Type == ItemObject.ItemTypeEnum.Thrown)
  			{
  				return true;
  			}
  		}
  		return false;
  	}
  
  	public int GetUpgradeXpCost(PartyBase party, int index)
  	public void InitializeHeroCharacterOnAfterLoad()
  	{
  		InitializeHeroBasicCharacterOnAfterLoad(_originCharacter);
> 		_occupation = _originCharacter._occupation;
  		_basicName = _originCharacter._basicName;
  		UpgradeTargets = _originCharacter.UpgradeTargets;
  		IsBasicTroop = _originCharacter.IsBasicTroop;
  		UpgradeRequiresItemFromCategory = _originCharacter.UpgradeRequiresItemFromCategory;
  		_civilianEquipmentTemplate = _originCharacter._civilianEquipmentTemplate;
  		_battleEquipmentTemplate = _originCharacter._battleEquipmentTemplate;
  		_isMariner = _originCharacter._isMariner;
  		_persona = _originCharacter._persona;
  		_characterTraits = _originCharacter._characterTraits;
  		DefaultCharacterSkills = _originCharacter.DefaultCharacterSkills;
  		base.IsReady = true;
  	}
  
  	public override void Deserialize(MBObjectManager objectManager, XmlNode node)
  	{
  		base.Deserialize(objectManager, node);
> 		XmlNode xmlNode = node.Attributes["occupation"];
  		if (xmlNode != null)
  		{
> 			_occupation = (Occupation)Enum.Parse(typeof(Occupation), xmlNode.InnerText);
  		}
  		XmlNode xmlNode2 = node.Attributes["is_template"];
  		IsTemplate = xmlNode2 != null && Convert.ToBoolean(xmlNode2.InnerText);
  		XmlNode xmlNode3 = node.Attributes["is_hidden_encyclopedia"];
  		HiddenInEncyclopedia = xmlNode3 != null && Convert.ToBoolean(xmlNode3.InnerText);
  		List<CharacterObject> list = new List<CharacterObject>();
  		foreach (XmlNode childNode in node.ChildNodes)
  		{
  			if (childNode.Name == "Traits")
  			{
  				_characterTraits.Deserialize(objectManager, childNode);
  			}
  			else
  			{
  				if (!(childNode.Name == "upgrade_targets"))
  				{
  
  	public override float GetPower()
  	{
> 		return GetPowerImp(IsHero ? (HeroObject.Level / 4 + 1) : Tier, IsHero, IsMounted);
  	}
  
  	public override float GetBattlePower()
  	{
  		return TaleWorlds.Library.MathF.Max(1f + 0.5f * (GetPower() - GetPowerImp(0)), 1f);
  	}
  
  	public override float GetMoraleResistance()
  	{
> 		int num = (IsHero ? (HeroObject.Level / 4 + 1) : Tier);
> 		return (IsHero ? 1.5f : 1f) * (0.5f * (float)num + 1f);
  	}
  
  	public void GetSimulationAttackPower(out float attackPoints, out float defencePoints, Equipment equipment = null)
  	{
  		if (equipment == null)
  		{
  			equipment = Equipment;
  		}
  		attackPoints = 0f;
  		defencePoints = 0f;
  		float num = 0f;
  		float num2 = 0f;
  		float num3 = 0f;
  		num2 = equipment.GetArmArmorSum() + equipment.GetHeadArmorSum() + equipment.GetHumanBodyArmorSum() + equipment.GetLegArmorSum();
  		num2 = num2 * num2 / equipment.GetTotalWeightOfArmor(forHuman: true);
  		defencePoints += num2 * 10f + 4000f;
  
  	public override BodyProperties GetBodyProperties(Equipment equipment, int seed = -1)
  	{
> 		if (IsHero)
  		{
  			return HeroObject.BodyProperties;
  		}
  		switch (seed)
  		{
  		case -2:
  			return GetBodyPropertiesMin();
  		case -1:
  			seed = base.StringId.GetDeterministicHashCode();
  			break;
  		}
  		return FaceGen.GetRandomBodyProperties(base.Race, IsFemale, GetBodyPropertiesMin(), GetBodyPropertiesMax(), (int)(equipment?.HairCoverType ?? ArmorComponent.HairCoverTypes.None), seed, BodyPropertyRange.HairTags, BodyPropertyRange.BeardTags, BodyPropertyRange.TattooTags, 0f);
  	}
  
  	public void SetTransferableInPartyScreen(bool isTransferable)
  	{
  
  	public void ClearAttributes()
  	{
> 		if (IsHero)
  		{
  			HeroObject.ClearAttributes();
  		}
  	}
  
  	public int GetTraitLevel(TraitObject trait)
  	{
> 		if (IsHero)
  		{
  			return HeroObject.GetTraitLevel(trait);
  		}
  		return _characterTraits.GetPropertyValue(trait);
  	}
  
  	public bool GetPerkValue(PerkObject perk)
  	{
> 		if (IsHero)
  		{
  			return HeroObject.GetPerkValue(perk);
  		}
  		return false;
  	}
  
  	public override int GetSkillValue(SkillObject skill)
  	{
> 		if (IsHero)
  		{
  			return HeroObject.GetSkillValue(skill);
  		}
  		return base.GetSkillValue(skill);
  	}
  
  	public TraitObject GetPersona()
  	{
  		if (_persona == null)
  		{
  			return DefaultTraits.PersonaSoftspoken;
  		}
  		return _persona;
  	}
  
  	public override int GetMountKeySeed()
  	{
> 		if (!IsHero)
  		{
  			return MBRandom.NondeterministicRandomInt;
  		}
  		return HeroObject.RandomValue;
  	}
  
  	public override FormationClass GetFormationClass()
  	{
> 		if (IsHero && Equipment != null)
  		{
  			bool num = Equipment[EquipmentIndex.ArmorItemEndSlot].Item?.HasHorseComponent ?? false;
  			bool flag = Equipment.HasWeaponOfClass(WeaponClass.Bow) || Equipment.HasWeaponOfClass(WeaponClass.Crossbow);
  			if (!num)
  			{
  				if (!flag)
  				{
  					return FormationClass.Infantry;
  				}
  				return FormationClass.Ranged;
  			}
  			if (!flag)
  			{
  				return FormationClass.Cavalry;
  			}
  			return FormationClass.HorseArcher;
  		return All.Where((CharacterObject x) => predicate(x));
  	}
  
> 	private static float GetPowerImp(int tier, bool isHero = false, bool isMounted = false)
  	{
> 		return (float)((2 + tier) * (8 + tier)) * 0.02f * (isHero ? 1.5f : (isMounted ? 1.2f : 1f));
  	}
  }

codex
The prisoner template’s bandit test matches vanilla’s gang-recruit bandit gate (`Occupation.Bandit`), and roster removal via `AddToCounts(..., -take)` preserves the roster’s wounded-count invariant. I’m checking save ids and data/localization consistency now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main\\**\\*.cs -Pattern 'SaveableTypeDefiner|: base\\(|SaveBaseId|AddClassDefinition' -SimpleMatch:"'$false | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Filter *.cs | Select-String -Pattern 'SaveableTypeDefiner|: base\\(|SaveBaseId|AddClassDefinition' | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests -Recurse -Filter *.cs | Select-String -Pattern 'LotrIssueSaveable|726900|AddClassDefinition|SaveBaseId' | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path docs\\features\\lotr-issues.md -Pattern 'per-type|Combat configs|Deliver|saturation|43|Implementation' -Context 1,3 | ForEach-Object { \""'$($_.LineNumber):$($_.Line)"; $_.Context.PostContext | ForEach-Object { $_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 717ms:
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:95:: base("Can not create a tree " + tree.Name + ", received incorrect parameters")
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:104:public AlreadyInRootException(Type type) : base("Error building the tree " + type.Name + " in method, can not go Up, already in root.") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:110:: base("error creating tree " + treeBeingBuild.GetType().Name + ". The tree does not implement the interface " + interfaceType.Name + ", but " + node.GetType().Name + " does.") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:116:: base("error creating tree " + source.GetType().Name + ". The property " + property.Name + " can not be assigned. Is it missing get or set methods?") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTreeWrapper\BehaviorTreeAgentComponent.cs:18:: base(agent)

 succeeded in 624ms:
3:> **Status: IMPLEMENTED (2026-06-20).** All 43 vanilla procedural issues are suppressed and replaced by 43
> TAOM-authored LOTR issues, built on a generic-template + XML-config architecture in `Main/Features/LotrIssues/`.
> See **Implementation (as built)** below for the shipped design; the disposition matrix and risk analysis that
> follow are the original research deliverable, kept for provenance. Engine mechanics are documented in
5:> See **Implementation (as built)** below for the shipped design; the disposition matrix and risk analysis that
> follow are the original research deliverable, kept for provenance. Engine mechanics are documented in
> [issue-and-quest-system.md](../reference/engine/issue-and-quest-system.md).

6:> follow are the original research deliverable, kept for provenance. Engine mechanics are documented in
> [issue-and-quest-system.md](../reference/engine/issue-and-quest-system.md).

## Overview
11:Bannerlord's campaign generates ~43 procedural **issues** — "problems at a notable" the player solves for reward
(deliver grain, clear a bandit base, escort a caravan, recruit gang members, …). Their gameplay is already
culture-relative (troops/items derive from the issue-giver's culture, which in TAOM is a LOTR culture), but their
hard-coded English **flavor text** and a handful of vanilla-specific archetypes don't fit Middle-earth. This plan
12:(deliver grain, clear a bandit base, escort a caravan, recruit gang members, …). Their gameplay is already
culture-relative (troops/items derive from the issue-giver's culture, which in TAOM is a LOTR culture), but their
hard-coded English **flavor text** and a handful of vanilla-specific archetypes don't fit Middle-earth. This plan
**replaces the vanilla issues with custom LOTR issues and disables the vanilla ones**, so the only issues that spawn are
20:- **Vanilla behavior:** 43 issue behaviors register in sandbox play (36 in `SandBoxManager.Initialize`, 7 in
  `SandBoxSubModule`) and spawn continuously at town/village notables and lords.
- **TAOM requirement:** issues that read as Middle-earth (an orc warband raiding the Westfold, seed-corn for a blighted
  Gondorian steading, Corsair smugglers in Pelargir) rather than Calradic generics — and no Calradia-named text.
34:The complete inventory of the 43 sandbox issues with a per-issue disposition. Columns:

| Column | Meaning |
|--------|---------|
50:escort / delivery / economic archetypes that have a clean Middle-earth analog, and `Drop` for niche or odd issues with
no compelling analog. `Reskin` is used only where a text-only override is clearly better than dropping. Every vanilla
issue behavior on the list is **suppressed** regardless of disposition (see Suppression below); `Replace`/`Reskin`
indicate whether a TAOM custom issue takes its slot.
55:**Audit outcome (full 43):** every archetype has a Middle-earth analog, so the audit dispositions **41 Replace, 1
Reskin** (`GangLeaderNeedsSpecialWeapons` — crafting-based, text-only conversion), **0 Drop**. All 43 vanilla behaviors
are suppressed regardless of disposition; `Replace`/`Reskin` only indicates whether a TAOM custom issue fills the slot.
`Src` = `SBM:NNN` (`SandBoxManager.cs`) or `SBS:NN` (`SandBoxSubModule.cs`). A ⚠ in **Deps** flags a vanilla-side item
56:Reskin** (`GangLeaderNeedsSpecialWeapons` — crafting-based, text-only conversion), **0 Drop**. All 43 vanilla behaviors
are suppressed regardless of disposition; `Replace`/`Reskin` only indicates whether a TAOM custom issue fills the slot.
`Src` = `SBM:NNN` (`SandBoxManager.cs`) or `SBS:NN` (`SandBoxSubModule.cs`). A ⚠ in **Deps** flags a vanilla-side item
that returns null / self-disables (all moot once replaced — see Dependency risks below).
65:| 3 | ArtisanOverpricedGoods | `SBM:164` | artisan | Common | Y | Deliver 1 of 6 raw goods (iron/hardwood MCP-false) | N | Replace | Craftsman gouged by merchant cartel on raw materials | gondor, erebor, dale, rohan, dunland, umbar, rhun, harad |
| 4 | CapturedByBountyHunters | `SBM:165` | gangleader | Common | Y | "looter" gate + nearest infested hideout | N | Replace | Crime boss asks player to free captured gang members | dunland, harad, umbar, rhun |
| 5 | CaravanAmbush | `SBM:166` | merchant | Common | Y | ⚠ literal "grain" null-AddToCounts; fish/butter/sumpter ok | N | Replace | Bait decoy caravan to spring counter-ambush on raiders | gondor, dale, erebor, rohan, dunland, rhun, harad, umbar |
| 6 | EscortMerchantCaravan | `SBM:167` | merchant | VeryCommon | Y | ⚠ "hardwood" missing → self-disables whole issue | Y | Replace | Escort merchant's caravan through war-torn roads to towns | gondor, dale, erebor, rohan, umbar, harad, rhun, dunland |
74:| 12 | HeadmanNeedsToDeliverAHerd | `SBM:173` | headman | VeryCommon | Y | sheep/cow/hog + Grain core; all resolve | N | Replace | Drive village livestock herd safely to distant market town | rohan, gondor, dale, dunland |
| 13 | HeadmanVillageNeedsDraughtAnimals | `SBM:174` | headman | VeryCommon | Y | cow/mule/sumpter ok; meat MCP-false (vanilla, runtime ok) | N | Replace | Buy draught animals to replace village's lost livestock | rohan, gondor, dale, dunland, erebor |
| 14 | LadysKnightOut | `SBM:175` | lord | Common | Y | Tournament towns runtime; prize is event payload | N | Replace | Noblewoman's champion in tournament, dedicate victories to her | gondor, rohan, dale, dunland |
| 15 | LandLordCompanyOfTrouble | `SBM:176` | lord | Rare | Y | company_of_trouble_character + random hideout; resolve | N | Replace | Manage troublesome mercenaries, re-sell contract before they turn | all |
80:| 18 | LandLordNeedsManualLaborers | `SBM:179` | rural | VeryCommon | Y | Player bandit prisoners + mine village; core traits | N | Replace | Deliver bandit prisoners as forced mine labor | mordor, isengard, dunland, erebor, harad, rhun |
| 19 | LandlordTrainingForRetainers | `SBM:180` | rural | VeryCommon | Y | Grain + borrowed_troop/veteran + culture; resolve | N | Replace | Train lent green retainers in battle into veterans | rohan, gondor, rhun, harad, dunland |
| 20 | LordNeedsGarrisonTroops | `SBM:181` | lord | Common | Y | Culture basic-troop tree + EliteBasicTroop; dynamic | N | Replace | Bring fresh culture recruits to reinforce a garrison | all |
| 21 | TheConquestOfSettlement | `SBM:182` | lord | VeryCommon | Y | At-war town/castle target; dynamic tokens | N | Replace | Liege orders you to besiege named enemy stronghold | gondor, rohan, mordor, isengard, erebor, dale, rhun, harad, gundabad, dolguldur |
84:| 22 | VillageNeedsCraftingMaterials | `SBM:183` | rural | Rare | Y | IronIngot1/IronIngot2 core; resolve | N | Replace | Deliver iron ingots so village smith can reforge | all |
| 23 | Smugglers | `SBM:184` | lord | Rare | Y | ⚠ literal "grain" returns null (food roster); rest ok | N | Replace | Lord asks you to break a smuggling caravan | gondor, rohan, dale, umbar, harad, dunland |
| 24 | LordNeedsHorses | `SBM:185` | lord | VeryCommon | Y | Culture mount pool + sumpter_horse fallback; resolve | N | Replace | Deliver fresh culture mounts to a horse-starved lord | rohan, gondor, dunland, harad, rhun, isengard |
| 25 | LordsNeedsTutor | `SBM:186` | lord | Common | Y | Jewelry-cat reward + clan young hero; dynamic | N | Replace | Mentor a lord's young heir in arts of war | gondor, rohan, erebor, dale, mirkwood, rivendell, lothlorien, dunland, harad, rhun |
86:| 24 | LordNeedsHorses | `SBM:185` | lord | VeryCommon | Y | Culture mount pool + sumpter_horse fallback; resolve | N | Replace | Deliver fresh culture mounts to a horse-starved lord | rohan, gondor, dunland, harad, rhun, isengard |
| 25 | LordsNeedsTutor | `SBM:186` | lord | Common | Y | Jewelry-cat reward + clan young hero; dynamic | N | Replace | Mentor a lord's young heir in arts of war | gondor, rohan, erebor, dale, mirkwood, rivendell, lothlorien, dunland, harad, rhun |
| 26 | LordWantsRivalCaptured | `SBM:187` | lord | Rare | Y | Enemy lord target + culture Guard; dynamic | N | Replace | Capture a hated rival lord alive, deliver prisoner | all |
| 27 | MerchantArmyOfPoachers | `SBM:188` | merchant | Common | Y | "leather" + "poacher" troop + bandit clan; resolve | N | Replace | Clear merchant's poachers-turned-gang from a bound village | gondor, rohan, dale, dunland, harad |
88:| 26 | LordWantsRivalCaptured | `SBM:187` | lord | Rare | Y | Enemy lord target + culture Guard; dynamic | N | Replace | Capture a hated rival lord alive, deliver prisoner | all |
| 27 | MerchantArmyOfPoachers | `SBM:188` | merchant | Common | Y | "leather" + "poacher" troop + bandit clan; resolve | N | Replace | Clear merchant's poachers-turned-gang from a bound village | gondor, rohan, dale, dunland, harad |
| 28 | MerchantNeedsHelpWithOutlaws | `SBM:189` | merchant | VeryCommon | Y | Nearest infested hideout + bandit parties; no goods | N | Replace | Clear N raiding bands plaguing merchant's trade roads | gondor, rohan, dale, erebor, dunland, mirkwood, lothlorien, umbar, harad, rhun |
| 29 | NearbyBanditBase | `SBM:190` | headman | VeryCommon | Y | Nearest infested hideout; all dynamic | Y | Replace | Clear orc/warg/brigand lair preying on travellers | all |
94:| 32 | VillageNeedsTools | `SBM:193` | headman | VeryCommon | Y | Tools core + village PrimaryProduction; resolve | N | Replace | Deliver tools to help village restore production | all |
| 33 | GangLeaderNeedsRecruits | `SBM:194` | gangleader | VeryCommon | Y | Player bandit-occupation troops; gold only | N | Replace | Deliver recruited outlaws to swell a gang | dunland, umbar, harad, rhun, gundabad |
| 34 | GangLeaderNeedsSpecialWeapons | `SBM:195` | gangleader | VeryCommon | Y | "Dagger" crafting template + ICraftingBehavior + skill | N | Reskin | Forge concealable daggers in a smithy for gang | all |
| 35 | LesserNobleRevolt | `SBM:196` | lord | Rare | Y | Culture elite tree tier5/6 + Grain; resolve | Y | Replace | Put down renegade noble stirring peasant tax-revolt | gondor, rohan, dale, dunland, erebor, harad, rhun |
95:| 33 | GangLeaderNeedsRecruits | `SBM:194` | gangleader | VeryCommon | Y | Player bandit-occupation troops; gold only | N | Replace | Deliver recruited outlaws to swell a gang | dunland, umbar, harad, rhun, gundabad |
| 34 | GangLeaderNeedsSpecialWeapons | `SBM:195` | gangleader | VeryCommon | Y | "Dagger" crafting template + ICraftingBehavior + skill | N | Reskin | Forge concealable daggers in a smithy for gang | all |
| 35 | LesserNobleRevolt | `SBM:196` | lord | Rare | Y | Culture elite tree tier5/6 + Grain; resolve | Y | Replace | Put down renegade noble stirring peasant tax-revolt | gondor, rohan, dale, dunland, erebor, harad, rhun |
| 36 | BettingFraud | `SBM:197` | gangleader | Rare | Y | ⚠ betting_fraud_thug_male/female unverified vanilla ids | Y | Replace | Tournament match-fixing partnership with a crooked bookmaker | gondor, rohan, dale, dunland, umbar, harad, rhun |
105:| 43 | SnareTheWealthy | `SBS:81` | gangleader | Common | Y | ⚠ literal "grain" returns null (caravan/gang cargo); rest ok | N | Replace | Pose as guard, lead corrupt merchant's caravan into ambush | umbar, harad, dunland, mordor, rhun |

### Disposition tally

109:**Replace: 41** · **Reskin: 1** (`GangLeaderNeedsSpecialWeapons`) · **Drop: 0** · **Total: 43** ✓

### Dependency risks (vanilla issues only — moot once replaced)

156:> **Reading a row — `HeadmanNeedsGrain` (#11):** deliver difficulty-scaled grain to a village headman (or send a
> companion + men to buy it). All deps safe — `DefaultItems.Grain` + `DefaultItemCategories.Grain` (engine-core), a
> `SettlementHelper.FindNearest…` supply-town search, generic player-party troops — and **17 text variables**
> (`ISSUE_SETTLEMENT`, `GRAIN_AMOUNT`, `NEARBY_TOWN`, `COMPANION`, …) any reskin must preserve. → **Replace** with a
167:config provider for the LOTR content. This avoids 43 near-duplicate C# classes while keeping per-mechanic quest logic
pure and testable.

### Mechanic classes (C#)
177:| `DeliverGoodsLotrIssue` | accumulate N of an item, deliver | grain/herd/tools/crafting-materials/army-supplies |
| `HuntBanditsLotrIssue` | defeat a spawned warband / clear a base | bandit-base / poachers / deserters / outlaws |
| `EscortLotrIssue` | protect a moving party to a destination | caravan escort / ambush |
| `DefeatRivalLotrIssue` | capture/defeat a named target | rival captured / spy |
201:data-driven dispatcher replaces the 43 vanilla behaviors.

### SaveableTypeDefiner

214:**Recommended: `campaignStarter.RemoveBehaviors<T>()`** (public, `CampaignGameStarter.cs:43`) called in
`SubModule.OnGameStart` for each of the 43 vanilla issue behaviors. They are added by Sandbox (the 36 via
`SandBoxManager.Initialize`, the 7 via `SandBoxSubModule.InitializeGameStarter`) before a later-loading module's
`OnGameStart` runs; removing them means they never subscribe `OnCheckForIssue`, so only LOTR issues spawn. **Keep
215:`SubModule.OnGameStart` for each of the 43 vanilla issue behaviors. They are added by Sandbox (the 36 via
`SandBoxManager.Initialize`, the 7 via `SandBoxSubModule.InitializeGameStarter`) before a later-loading module's
`OnGameStart` runs; removing them means they never subscribe `OnCheckForIssue`, so only LOTR issues spawn. **Keep
`IssuesCampaignBehavior`** (the host spawner). Guard each call so a renamed/removed type after an engine bump is a no-op,
219:not a crash. **Confirm at implementation** that TAOM's `OnGameStart` runs after Sandbox's registration (very likely per
the [submodule lifecycle](../reference/engine/submodule-lifecycle-and-harmony.md): `InitializeGameStarter` fires for all
modules before any `OnGameStart`, and TAOM loads after Sandbox).

256:## Verification (for the eventual implementation)

- **Static/config:** `python tools/validate_moduledata.py` (add a `taom_lotr_issues` schema under `tools/schemas/`);
  `taom-moduledata` MCP (`item_exists` / `troop_exists` / `culture_exists`) for every id the config references.
261:  per reward type) + a **suppression-list test** asserting the removal list equals the authoritative 43-issue set.
  `/verify` for the full gate.
- **Bindings:** `/verify-bindings` after an engine bump to confirm `IssueManager.AddPotentialIssueData`,
  `CampaignGameStarter.RemoveBehaviors`, the `IssueModel` virtuals, and every vanilla issue type name still resolve.
271:## Implementation (as built)

The shipped feature **collapsed the planned 8 mechanic templates to 3**, all validated by deep-review + the Wave-0
Codex pass. Every one of the 43 issues maps onto one of these via XML config — no bespoke per-issue classes.
274:Codex pass. Every one of the 43 issues maps onto one of these via XML config — no bespoke per-issue classes.

| Template (`IssueBase` + paired `QuestBase`) | Mechanic | Issues |
|---|---|---|
278:| **DeliverGoods** | accumulate N of an `item:<id>` trade good, hand in via dialog | 14 (grain/supplies/draught/crafting/tools/horses/herd/artisan×2/offload/revenue/art-of-trade/tutor/special-weapons) |
| **DeliverPersonnel** | hand over N bandit prisoners from the player's `PrisonRoster` | 2 (gang recruits, mine laborers) |
| **Combat** (`variant=`) | event-driven count, auto-completes on N (no turn-in) | 27 — `DefeatRaids` (24, won battles), `CaptureLords` (1, at-war lord taken prisoner), `WinTournaments` (2, tournament won) |

279:| **DeliverPersonnel** | hand over N bandit prisoners from the player's `PrisonRoster` | 2 (gang recruits, mine laborers) |
| **Combat** (`variant=`) | event-driven count, auto-completes on N (no turn-in) | 27 — `DefeatRaids` (24, won battles), `CaptureLords` (1, at-war lord taken prisoner), `WinTournaments` (2, tournament won) |

**Why 3, not 8:** the "Escort-a-moving-party", "EconomicGather", "ConquestMilitary", and "SocialMisc/CraftItem"
283:mechanics from the matrix below were each reframed onto the proven Deliver/Combat mechanics rather than authored as
bespoke blind-built templates (e.g. caravan-ambush/escort → "defeat the raiders on the road"; revenue-farming →
"collect the tithe-in-kind"; lady's-knight/betting-fraud → `WinTournaments`; rescue-the-daughter → "defeat the gang
that holds her"). This is the matrix's documented **deliberately-simplified** trade-off taken to its conclusion: it
295:`ILotrIssueGiverAdapter` / `ILotrIssueRewardAdapter`. Vanilla's 43 issue behaviors are removed in
`SubModule.OnGameStart` via `LotrIssueSuppression.SuppressAll` (`RemoveBehaviors<T>`, each guarded), keeping the host
`IssuesCampaignBehavior` so `OnCheckForIssueEvent` still fires. Saves register at base `726900801`, localIds 101–106
(3 issue/quest pairs).
305:**Known limitation — per-type spawn saturation (accepted v1 trade-off; this doc's Risk #5):** all 27 Combat configs
share `typeof(CombatLotrIssue)` (and all 14 Deliver share `typeof(DeliverGoodsLotrIssue)`), so Bannerlord's
per-issue-**type** over-representation throttle + per-settlement "already has an issue of this type" zero-out treat
each template as a single bucket. Net effect: issues spawn and rotate, but the world hosts fewer simultaneous LOTR
306:share `typeof(CombatLotrIssue)` (and all 14 Deliver share `typeof(DeliverGoodsLotrIssue)`), so Bannerlord's
per-issue-**type** over-representation throttle + per-settlement "already has an issue of this type" zero-out treat
each template as a single bucket. Net effect: issues spawn and rotate, but the world hosts fewer simultaneous LOTR
issues than vanilla's 43 distinct types would, and rare Combat variants surface infrequently. A true per-config type
309:issues than vanilla's 43 distinct types would, and rare Combat variants surface infrequently. A true per-config type
bucket is impossible under the generic-template design without code generation; the deferred mitigation is to split
the high-volume templates into a few `def.Id`-keyed subclasses if in-game observation shows the rate is too low.

344:| `Main/Features/LotrIssues/Templates/{DeliverGoods,DeliverPersonnel,Combat}LotrIssue.cs` | the 3 generic mechanic `IssueBase` + paired `QuestBase` classes |
| `Main/Features/LotrIssues/LotrIssueConfigProvider.cs` | validating XML loader (skips-invalid-and-warns, `FiniteFloatValidator`) |
| `Main/Features/LotrIssues/LotrIssueSuppression.cs` | `RemoveBehaviors<T>` of all 43 vanilla issue behaviors + suppression-list test |
| `Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs` | save registration (base `726900801`, localIds 101–106) |
346:| `Main/Features/LotrIssues/LotrIssueSuppression.cs` | `RemoveBehaviors<T>` of all 43 vanilla issue behaviors + suppression-list test |
| `Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs` | save registration (base `726900801`, localIds 101–106) |
| `Main/Features/LotrIssues/Domain/*.cs` | `LotrIssueDefinition` + `LotrIssueTemplate`/`IssueGiverOccupation`/`IssueFrequencyTier` enums |
| `Main/Adapters/{ILotrIssueGiverAdapter,ILotrIssueRewardAdapter}.cs` (+impls) | sealed-type boundary (ADR-007) |
350:| `Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml` | the 43 issue configs (flavor, cultures, rewards, counts) |
| `Main/_Module/ModuleData/taom_lotr_issue_strings.xml` | 308 localization keys (English source-of-truth) |
| `TAOM.Tests/Features/LotrIssues/*` | config-provider + service + suppression tests (50) |


 succeeded in 810ms:
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:458:// is registered for saving by the auto-discovered CareerQuestSaveableTypeDefiner.
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:95:: base("Can not create a tree " + tree.Name + ", received incorrect parameters")
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:104:public AlreadyInRootException(Type type) : base("Error building the tree " + type.Name + " in method, can not go Up, already in root.") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:110:: base("error creating tree " + treeBeingBuild.GetType().Name + ". The tree does not implement the interface " + interfaceType.Name + ", but " + node.GetType().Name + " does.") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\BehaviorTreesCore.cs:116:: base("error creating tree " + source.GetType().Name + ". The property " + property.Name + " can not be assigned. Is it missing get or set methods?") { }
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\Nodes\BehaviorTreesNodes.cs:44:: base(weight)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\Nodes\BehaviorTreesNodes.cs:87:protected BTTask(BTListener? listener = null, int weight = 100) : base(weight)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\Nodes\BehaviorTreesNodes.cs:127:: base(tree, name, decorator, children, weight)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTrees\Nodes\BehaviorTreesNodes.cs:261:: base(tree, name, decorator, children, weight)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTreeWrapper\BehaviorTreeAgentComponent.cs:18:: base(agent)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTreeWrapper\AbstractDecoratorsListeners\BannerlordBTListener.cs:10:: base(tree, notifies)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTreeWrapper\AbstractDecoratorsListeners\BannerlordBTTickListener.cs:10:: base(tree, notifies)
C:\Users\mikew\source\repos\TAOM\Main\BehaviorTreeWrapper\Decorators\WaitNSecondsTickDecorator.cs:12:: base(timeToWait)
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\BoneCheckDuringAnimation.cs:15:// 2026-05-24 (#219): base() previously received `actionProgressMax` (a 0.0-1.0
C:\Users\mikew\source\repos\TAOM\Main\Features\AdvancedCombat\BoneCheckDuringAnimation.cs:24:: base(agent, targets, boneIds, 100f, boneCollisionRadius, stopAfterFirstHit, onCollisionCallback, onExpiration)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallException.cs:10:public BattleLoadStallException(string message) : base(message) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPersistenceBehavior.cs:27:// Serialize as flat primitive dictionaries to avoid SaveableTypeDefiner requirement
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs:46:: base(questId, questGiver, CampaignTime.DaysFromNow(1000000f), 0)
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs:13:/// The engine global type id is <c>_saveBaseId + localId</c> (SaveableTypeDefiner.AddClassDefinition,
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs:20:public sealed class CareerQuestSaveableTypeDefiner : SaveableTypeDefiner
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs:22:private const int SaveBaseId = 726900701;
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs:24:public CareerQuestSaveableTypeDefiner() : base(SaveBaseId) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs:28:AddClassDefinition(typeof(CareerQuest), 101);
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs:19:public CharacterDeveloperCareerMixin(CharacterDeveloperVM viewModel) : base(viewModel)
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:13:/// case the SaveableTypeDefiner BaseId 726900601 collides with another mod — a parse error
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Models\FormationPresetSaveableTypeDefiner.cs:12:/// First TAOM use of <see cref="SaveableTypeDefiner"/>; CareerSystem deliberately avoided
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Models\FormationPresetSaveableTypeDefiner.cs:16:public class FormationPresetSaveableTypeDefiner : SaveableTypeDefiner
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Models\FormationPresetSaveableTypeDefiner.cs:18:public FormationPresetSaveableTypeDefiner() : base(726900601) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Models\FormationPresetSaveableTypeDefiner.cs:22:AddClassDefinition(typeof(HoNFormationPreset), 101);
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\Models\HoNFormationPreset.cs:9:/// SaveSystem serialization; class id is registered in <see cref="FormationPresetSaveableTypeDefiner"/>
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\DevTriggers\TaomDevTriggerException.cs:9:public TaomDevTriggerException(string message) : base(message) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\CustomBattles\TaomFactionSelectionVM.cs:10:: base(onSelectionChanged)
C:\Users\mikew\source\repos\TAOM\Main\Features\Elephant\ElephantBehaviorTree.cs:37:public ElephantBehaviorTree(Agent agent) : base(10)
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Hooks\EquipmentPresetCampaignBehavior.cs:10:/// SaveBaseId 726900501 (registered by <see cref="PresetSaveableTypeDefiner"/>). On load, prunes
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:8:/// save system. <see cref="SaveableTypeDefiner"/> instances are auto-discovered by TaleWorlds
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:11:/// BaseId 726900501 — verified unique across TAOM at port time (no other SaveableTypeDefiner
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:15:public sealed class PresetSaveableTypeDefiner : SaveableTypeDefiner
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:17:public const int SaveBaseId = 726900501;
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:19:public PresetSaveableTypeDefiner() : base(SaveBaseId)
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:25:AddClassDefinition(typeof(HoNPresetItemReference), 101);
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Models\PresetSaveableTypeDefiner.cs:26:AddClassDefinition(typeof(HoNEquipmentPreset), 102);
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\BannerWidget.cs:44:public BannerWidget(UIContext context) : base(context)
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\FactionImageWidget.cs:20:public FactionImageWidget(UIContext context) : base(context)
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\MapContainerWidget.cs:11:public MapContainerWidget(UIContext context) : base(context) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\PolygonWidget.cs:142:public PolygonWidget(UIContext context) : base(context)
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Widgets\RuntimeSprite.cs:13:: base("RuntimeSprite", width, height, SpriteNinePatchParameters.Empty)
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:12:/// The engine global type id is <c>_saveBaseId + localId</c>. TAOM's definer bases step by 100, so the
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:21:public sealed class LotrIssueSaveableTypeDefiner : SaveableTypeDefiner
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:23:private const int SaveBaseId = 726900801;
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:25:public LotrIssueSaveableTypeDefiner() : base(SaveBaseId) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:30:AddClassDefinition(typeof(DeliverGoodsLotrIssue), 101);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:31:AddClassDefinition(typeof(DeliverGoodsLotrIssueQuest), 102);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:33:AddClassDefinition(typeof(DeliverPersonnelLotrIssue), 103);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:34:AddClassDefinition(typeof(DeliverPersonnelLotrIssueQuest), 104);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:36:AddClassDefinition(typeof(CombatLotrIssue), 105);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:37:AddClassDefinition(typeof(CombatLotrIssueQuest), 106);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesIoC.cs:8:/// the service from IoC) and the <see cref="LotrIssueSaveableTypeDefiner"/> is engine-auto-discovered.
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\CombatLotrIssue.cs:37:: base(issueOwner, CampaignTime.DaysFromNow(30f))
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\CombatLotrIssue.cs:157:: base(questId, giverHero, duration, rewardGold)
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverGoodsLotrIssue.cs:40:: base(issueOwner, CampaignTime.DaysFromNow(30f))
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverGoodsLotrIssue.cs:190:: base(questId, giverHero, duration, rewardGold)
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverPersonnelLotrIssue.cs:38:: base(issueOwner, CampaignTime.DaysFromNow(30f))
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\Templates\DeliverPersonnelLotrIssue.cs:154:: base(questId, giverHero, duration, rewardGold)
C:\Users\mikew\source\repos\TAOM\Main\Features\Messengers\UI\MessengerEncyclopediaMixin.cs:31:public MessengerEncyclopediaMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
C:\Users\mikew\source\repos\TAOM\Main\Features\Siege\ISiegeDefenseService.cs:19:// avoids SaveableTypeDefiner). Each event encoded as "defenderFactionId|deadlineTicks|accepted|rewardClaimed".
C:\Users\mikew\source\repos\TAOM\Main\Features\Siege\SiegeDefenseBehavior.cs:16:// CareerPersistenceBehavior pattern (avoids SaveableTypeDefiner).
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\UI\SpecialResourceMapBarMixin.cs:24:public SpecialResourceMapBarMixin(MapInfoVM viewModel) : base(viewModel)
C:\Users\mikew\source\repos\TAOM\Main\Features\SpecialResources\UI\SpecialResourceSpriteWidget.cs:19:public SpecialResourceSpriteWidget(UIContext context) : base(context)
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\SpiderBehaviorTree.cs:36:public SpiderBehaviorTree(Agent agent) : base(10)
C:\Users\mikew\source\repos\TAOM\Main\Features\Spider\BehaviorTreeElements\OnSpiderDied.cs:28:public OnSpiderDied() : base(SubscriptionPossibilities.OnSelfRemoved) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\TimeAcceleration\UI\TimeAccelerationMixin.cs:17:public TimeAccelerationMixin(MapTimeControlVM viewModel) : base(viewModel)
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\WargBehaviorTree.cs:22:public WargBehaviorTree(Agent agent) : base(10)
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\OnWargDied.cs:28:public OnWargDied() : base(SubscriptionPossibilities.OnSelfRemoved) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\PeriodicallyCheckIfCanAttackAnyone.cs:17:public PeriodicallyCheckIfCanAttackAnyone() : base(0.2) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\WargCanNotFindEnemyDecorator.cs:21:public WargCanNotFindEnemyDecorator(double timeToWait) : base(timeToWait)
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\WargEnemyDiedDecorator.cs:20:public WargEnemyDiedDecorator() : base(SubscriptionPossibilities.OnAgentRemoved) { }
C:\Users\mikew\source\repos\TAOM\Main\Features\Warg\BehaviorTreeElements\WargTryToGoRage.cs:26:public WargTryToGoRage() : base(SubscriptionPossibilities.OnSelfIsHit) { }

 succeeded in 981ms:
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:14:public void BaseId_MatchesPlanned726900501()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:17:var baseIdField = typeof(SaveableTypeDefiner).GetField("_saveBaseId",
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:19:Assert.IsNotNull(baseIdField, "SaveableTypeDefiner._saveBaseId field expected (verified ilspy 2026-05-06)");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:21:Assert.AreEqual(PresetSaveableTypeDefiner.SaveBaseId, baseId);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:22:Assert.AreEqual(726900501, PresetSaveableTypeDefiner.SaveBaseId,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:23:"BaseId 726900501 — verified unique across TAOM at port time. Document range usage if changed.");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\EquipPresets\PresetSaveableTypeDefinerTests.cs:51:var baseIdField = typeof(SaveableTypeDefiner).GetField("_saveBaseId",

codex
The docs’ saturation paragraph has a stale phrase: it says “all 14 Deliver share `DeliverGoods`,” but the implemented table immediately above correctly splits 14 `DeliverGoods` and 2 `DeliverPersonnel`. That looks like a documentation imprecision, not a runtime bug; I’ll call it out only if it materially affects the verdict.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); "rows=$($rows.Count)"; $rows | Group-Object template | ForEach-Object { "template $($_.Name)=$($_.Count)" }; $rows | Where-Object {$_.template -eq '"'Combat'} | Group-Object variant | ForEach-Object { \"combat "'$($_.Name)=$($_.Count)" }; $rows | Group-Object id | Where-Object Count -gt 1 | ForEach-Object { "dup id $($_.Name)" }; $rows | Where-Object { ([int]$_.reward_gold_base -eq 0) -and ([double]$_.reward_gold_per_difficulty -eq 0) -and ([int]$_.reward_renown -eq 0) -and [string]::IsNullOrEmpty($_.reward_item) } | ForEach-Object { "zero reward $($_.id)" }; $rows | Where-Object { $_.template -eq '"'DeliverGoods' } | ForEach-Object { \""'$($_.id):$($_.item_source)" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $rows | Where-Object {$_.cultures} | ForEach-Object { "$($_.id):$($_.cultures)" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $validTemplates='"'DeliverGoods','DeliverPersonnel','Combat'; "'$validGivers='"'Headman','RuralNotable','Merchant','GangLeader','Lord'; "'$validFreq='"'VeryCommon','Common','Rare'; "'$validVariants='"'DefeatRaids','CaptureLords','WinTournaments'; foreach("'$r in $rows){ if($validTemplates -notcontains $r.template){"bad template $($r.id):$($r.template)"}; if($validGivers -notcontains $r.giver_occupation){"bad giver $($r.id):$($r.giver_occupation)"}; if($validFreq -notcontains $r.frequency){"bad freq $($r.id):$($r.frequency)"}; if($r.template -eq '"'Combat' -and "'$validVariants -notcontains $r.variant){"bad variant $($r.id):$($r.variant)"}; if([int]$r.count -le 0){"bad count $($r.id):$($r.count)"}; if([double]$r.count_per_difficulty -lt 0){"bad countdiff $($r.id):$($r.count_per_difficulty)"}; if([int]$r.reward_gold_base -lt 0 -or [double]$r.reward_gold_per_difficulty -lt 0 -or [int]$r.reward_renown -lt 0){"bad reward sign $($r.id)"}; if([string]::IsNullOrEmpty($r.title_key) -or [string]::IsNullOrEmpty($r.description_key)){"missing title/desc $($r.id)"} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $rows | Select-Object -ExpandProperty item_source | Where-Object {$_ -like '"'item:*'} | ForEach-Object {"'$_.Substring(5)} | Sort-Object -Unique' in C:\Users\mikew\source\repos\TAOM
 succeeded in 578ms:
 succeeded in 585ms:
bad giver lotr_artisan_cant_sell:Artisan
bad giver lotr_artisan_overpriced:Artisan

 succeeded in 683ms:
rows=43
template Combat=27
template DeliverGoods=14
template DeliverPersonnel=2
combat CaptureLords=1
combat DefeatRaids=24
combat WinTournaments=2
lotr_headman_needs_grain:item:grain
lotr_army_needs_supplies:item:grain
lotr_village_needs_draught:item:cow
lotr_village_needs_crafting_materials:item:iron
lotr_village_needs_tools:item:tools
lotr_lord_needs_horses:item:sumpter_horse
lotr_deliver_herd:item:sheep
lotr_artisan_cant_sell:item:pottery
lotr_artisan_overpriced:item:leather
lotr_offload_stolen:item:stolen_goods
lotr_revenue_farming:item:grain
lotr_art_of_trade:item:oil
lotr_lords_tutor:item:velvet
lotr_special_weapons:item:iron

2026-06-20T18:13:17.473004Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
cow
grain
iron
leather
oil
pottery
sheep
stolen_goods
sumpter_horse
tools
velvet
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m

 exited 1 in 694ms:
cow
grain
iron
leather
oil
pottery
sheep
stolen_goods
sumpter_horse
tools
velvet
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m
[31;1mSelect-Object: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m … LotrIssue); $rows | [36;1mSelect-Object -ExpandProperty item_source[0m | Where …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                       ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mProperty "item_source" cannot be found.[0m

codex
The first config pass found two `giver_occupation="Artisan"` rows. I used a hand list there, so I’m checking the actual TAOM enum before treating that as a validation failure.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Domain\\IssueGiverOccupation.cs" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Features\\LotrIssues\\Domain\\LotrIssueTemplate.cs; Get-Content -Path Main\\Features\\LotrIssues\\Domain\\IssueFrequencyTier.cs" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Path Main\\Adapters\\LotrIssueGiverAdapter.cs -TotalCount 260" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $rows | Where-Object {$_.template -eq '"'DeliverGoods'} | ForEach-Object { \""'$($_.id):$($_.giver_occupation):$($_.item_source):goldBase=$($_.reward_gold_base):goldPer=$($_.reward_gold_per_difficulty):renown=$($_.reward_renown):item=$($_.reward_item)" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 499ms:
namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// Which kind of notable/hero offers an issue. The behavior's <c>OnCheckForIssue</c> eligibility gate
/// maps the polled hero's vanilla occupation to one of these; an issue only offers to a matching giver.
/// </summary>
public enum IssueGiverOccupation
{
    /// <summary>Village headman.</summary>
    Headman,

    /// <summary>Urban gang leader (criminal underworld).</summary>
    GangLeader,

    /// <summary>Town merchant.</summary>
    Merchant,

    /// <summary>Town artisan.</summary>
    Artisan,

    /// <summary>Rural notable (village landlord/elder, not the headman).</summary>
    RuralNotable,

    /// <summary>A landed lord (clan member with a fief / army).</summary>
    Lord
}

 succeeded in 448ms:
using TaleWorlds.CampaignSystem;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Wraps a polled <see cref="Hero"/> for LOTR-issue eligibility. Maps the hero's vanilla notable role
/// to a <see cref="IssueGiverOccupation"/> (null when the hero gives no issues). All members are
/// computed from public 1.4.6 Hero APIs; guards keep it null-safe for the boundary (ADR-007).
/// </summary>
public class LotrIssueGiverAdapter : ILotrIssueGiverAdapter
{
    private readonly Hero _hero;

    public LotrIssueGiverAdapter(Hero hero)
    {
        _hero = hero;
    }

    public bool IsValid => _hero != null && _hero.IsAlive && _hero.CurrentSettlement != null;

    public IssueGiverOccupation? Occupation
    {
        get
        {
            if (_hero == null) return null;
            // Lord is NOT a notable occupation — Hero.IsNotable is false for lords (verified v1.4.6
            // Hero.IsNotable: Artisan/GangLeader/Preacher/Merchant/RuralNotable/Headman only). So the
            // notable roles gate behind IsNotable, but Lord is checked independently — otherwise every
            // lord-giver issue would be unreachable.
            if (_hero.IsNotable)
            {
                if (_hero.IsHeadman) return IssueGiverOccupation.Headman;
                if (_hero.IsGangLeader) return IssueGiverOccupation.GangLeader;
                if (_hero.IsMerchant) return IssueGiverOccupation.Merchant;
                if (_hero.IsArtisan) return IssueGiverOccupation.Artisan;
                if (_hero.IsRuralNotable) return IssueGiverOccupation.RuralNotable;
            }
            if (_hero.IsLord) return IssueGiverOccupation.Lord;
            return null;
        }
    }

    // Hero.Culture is a saveable field (safe once the hero is non-null), not a computed getter.
    public string CultureStringId => _hero?.Culture?.StringId ?? "";

    public int RelationWithPlayer => _hero == null ? 0 : (int)_hero.GetRelationWithPlayer();
}

 succeeded in 465ms:
namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// The generic mechanic a <see cref="LotrIssueDefinition"/> instantiates. Each value maps to exactly
/// one <c>IssueBase</c>/<c>QuestBase</c> template pair; the XML config supplies the per-issue content
/// (counts, reward, culture filter, text keys). One enum value = one template branch = one test cell.
/// See <c>docs/features/lotr-issues.md</c> for the per-issue mapping.
/// </summary>
public enum LotrIssueTemplate
{
    /// <summary>Accumulate N of a culture/category-derived item and deliver to the giver.</summary>
    DeliverGoods,

    /// <summary>Deliver N bandit prisoners to the giver (gang recruits / forced mine labor).</summary>
    DeliverPersonnel,

    /// <summary>Combat objective: defeat N raids (won battles) or capture N enemy lords. The specific
    /// objective is the definition's <c>Variant</c> ("DefeatRaids" | "CaptureLords"). Covers the vanilla
    /// clear-hideout + defeat/capture-target archetypes.</summary>
    Combat,

    /// <summary>Protect a moving party (caravan/herd) to a destination; ambush variant.</summary>
    Escort,

    /// <summary>Market/price intervention, collection, fencing, or breaking a smuggling run.</summary>
    EconomicGather,

    /// <summary>Besiege / raid / scout fortifications / reinforce a garrison.</summary>
    ConquestMilitary,

    /// <summary>Low-mechanic social errands: go-to, shelter, find, tutor, tournament-dedicate.</summary>
    SocialMisc,

    /// <summary>Forge a crafted (smithing) item and deliver it.</summary>
    CraftItem
}
namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// TAOM-owned mirror of the engine's <c>IssueBase.IssueFrequency</c> (VeryCommon/Common/Rare), kept
/// here so the domain + service + config provider stay free of TaleWorlds types and unit-testable.
/// The template boundary maps this to the engine enum when constructing <c>PotentialIssueData</c>.
/// </summary>
public enum IssueFrequencyTier
{
    Rare,
    Common,
    VeryCommon
}

 succeeded in 470ms:
lotr_headman_needs_grain:Headman:item:grain:goldBase=0:goldPer=1500:renown=1:item=
lotr_army_needs_supplies:Lord:item:grain:goldBase=0:goldPer=2200:renown=2:item=
lotr_village_needs_draught:Headman:item:cow:goldBase=0:goldPer=1100:renown=1:item=
lotr_village_needs_crafting_materials:RuralNotable:item:iron:goldBase=0:goldPer=1400:renown=1:item=
lotr_village_needs_tools:Headman:item:tools:goldBase=0:goldPer=1200:renown=1:item=
lotr_lord_needs_horses:Lord:item:sumpter_horse:goldBase=0:goldPer=2000:renown=2:item=
lotr_deliver_herd:Headman:item:sheep:goldBase=0:goldPer=1300:renown=1:item=
lotr_artisan_cant_sell:Artisan:item:pottery:goldBase=0:goldPer=1300:renown=1:item=
lotr_artisan_overpriced:Artisan:item:leather:goldBase=0:goldPer=1400:renown=1:item=
lotr_offload_stolen:GangLeader:item:stolen_goods:goldBase=0:goldPer=1700:renown=0:item=
lotr_revenue_farming:RuralNotable:item:grain:goldBase=0:goldPer=1600:renown=1:item=
lotr_art_of_trade:RuralNotable:item:oil:goldBase=0:goldPer=1800:renown=1:item=
lotr_lords_tutor:Lord:item:velvet:goldBase=0:goldPer=1700:renown=1:item=
lotr_special_weapons:GangLeader:item:iron:goldBase=0:goldPer=1800:renown=0:item=

codex
`Artisan` is valid in the TAOM enum and adapter, so those two rows are clean. I’m cross-referencing the `item:<id>` values against Bannerlord/TAOM item XML now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids='"'cow','grain','iron','leather','oil','pottery','sheep','stolen_goods','sumpter_horse','tools','velvet'; foreach("'$id in $ids){ $pattern='"'id=\"'+[regex]::Escape("'$id)+'"'\"'; "'$hit=Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern "'$pattern -List | Select-Object -First 1; if($hit){"$id => $($hit.Path):$($hit.LineNumber):$($hit.Line.Trim())"} else {"MISSING $id"} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$rewardIds=([xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw)).LotrIssues.LotrIssue | ForEach-Object { "'$_.reward_item } | Where-Object { -not [string]::IsNullOrEmpty($_) } | Sort-Object -Unique; if($rewardIds){ foreach($id in $rewardIds){ $pattern='"'<(?:Item|CraftedItem)["'^>]*id="'"'+[regex]::Escape("'$id)+'"'\"'; "'$hit=Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern "'$pattern -List | Select-Object -First 1; if($hit){"REWARD_ITEM $id => $($hit.Path):$($hit.LineNumber)"} else {"MISSING_REWARD_ITEM $id"} } } else { '"'no reward_item ids' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids='"'cow','grain','iron','leather','oil','pottery','sheep','stolen_goods','sumpter_horse','tools','velvet'; foreach("'$id in $ids){ $pattern='"'<(?:Item|CraftedItem)["'^>]*id="'"'+[regex]::Escape("'$id)+'"'\"'; "'$hit=Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern "'$pattern -List | Select-Object -First 1; if($hit){"ITEM $id => $($hit.Path):$($hit.LineNumber)"} else {"MISSING_ITEM $id"} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $rows | Select-Object id,template,giver_occupation,frequency,variant,count,count_per_difficulty,reward_gold_per_difficulty,reward_renown | Format-Table -AutoSize' in C:\Users\mikew\source\repos\TAOM
 succeeded in 632ms:
no reward_item ids

 succeeded in 586ms:

[32;1mid                                   [0m[32;1m template        [0m[32;1m giver_occupation[0m[32;1m frequency [0m[32;1m variant       [0m[32;1m count[0m[32;1m count_per_diffi[0m
[32;1m                                     [0m[32;1m                 [0m[32;1m                 [0m[32;1m           [0m[32;1m               [0m[32;1m      [0m[32;1m culty[0m
[32;1m--                                   [0m [32;1m--------        [0m [32;1m----------------[0m [32;1m--------- [0m [32;1m-------       [0m [32;1m-----[0m [32;1m---------------[0m
lotr_headman_needs_grain              DeliverGoods     Headman          Common                    12    120            
lotr_army_needs_supplies              DeliverGoods     Lord             VeryCommon                20    160            
lotr_village_needs_draught            DeliverGoods     Headman          Common                    6     20             
lotr_village_needs_crafting_materials DeliverGoods     RuralNotable     Rare                      8     24             
lotr_village_needs_tools              DeliverGoods     Headman          Common                    10    30             
lotr_lord_needs_horses                DeliverGoods     Lord             VeryCommon                6     18             
lotr_gang_needs_recruits              DeliverPersonnel GangLeader       VeryCommon                5     10             
lotr_landlord_needs_laborers          DeliverPersonnel RuralNotable     Common                    6     12             
lotr_nearby_bandit_base               Combat           Headman          VeryCommon DefeatRaids    1     1              
lotr_merchant_outlaws                 Combat           Merchant         VeryCommon DefeatRaids    3     2              
lotr_merchant_poachers                Combat           Merchant         Common     DefeatRaids    1     1              
lotr_captured_gang                    Combat           GangLeader       Common     DefeatRaids    1     1              
lotr_extortion_deserters              Combat           Headman          Common     DefeatRaids    1     1              
lotr_landlord_commons                 Combat           RuralNotable     Common     DefeatRaids    1     1              
lotr_rival_gang                       Combat           GangLeader       Common     DefeatRaids    1     1              
lotr_spy_party                        Combat           Lord             Rare       DefeatRaids    1     1              
lotr_lesser_noble_revolt              Combat           Lord             Rare       DefeatRaids    1     1              
lotr_company_trouble                  Combat           Lord             Rare       DefeatRaids    1     1              
lotr_prodigal_son                     Combat           Lord             Rare       DefeatRaids    1     1              
lotr_rival_captured                   Combat           Lord             Rare       CaptureLords   1     1              
lotr_caravan_ambush                   Combat           Merchant         Common     DefeatRaids    1     1              
lotr_escort_caravan                   Combat           Merchant         Common     DefeatRaids    2     2              
lotr_deliver_herd                     DeliverGoods     Headman          Common                    8     24             
lotr_snare_wealthy                    Combat           GangLeader       Common     DefeatRaids    1     1              
lotr_artisan_cant_sell                DeliverGoods     Artisan          Common                    10    30             
lotr_artisan_overpriced               DeliverGoods     Artisan          Common                    8     24             
lotr_offload_stolen                   DeliverGoods     GangLeader       Common                    6     18             
lotr_revenue_farming                  DeliverGoods     RuralNotable     Common                    12    100            
lotr_art_of_trade                     DeliverGoods     RuralNotable     Rare                      8     30             
lotr_smugglers                        Combat           Merchant         Common     DefeatRaids    1     1              
lotr_gang_needs_weapons               Combat           GangLeader       Common     DefeatRaids    1     1              
lotr_garrison_troops                  Combat           Lord             Rare       DefeatRaids    2     2              
lotr_conquest                         Combat           Lord             Rare       DefeatRaids    3     2              
lotr_raid_territory                   Combat           Lord             Rare       DefeatRaids    2     2              
lotr_scout_garrisons                  Combat           Lord             Rare       DefeatRaids    2     1              
lotr_ladys_knight                     Combat           RuralNotable     Rare       WinTournaments 1     1              
lotr_lords_tutor                      DeliverGoods     Lord             Rare                      6     20             
lotr_landlord_training                Combat           RuralNotable     Common     DefeatRaids    2     2              
lotr_inn_and_out                      Combat           RuralNotable     Common     DefeatRaids    1     1              
lotr_family_feud                      Combat           Lord             Rare       DefeatRaids    1     1              
lotr_daughter_found                   Combat           Merchant         Rare       DefeatRaids    1     1              
lotr_betting_fraud                    Combat           GangLeader       Rare       WinTournaments 1     1              
lotr_special_weapons                  DeliverGoods     GangLeader       Rare                      6     18             


 succeeded in 59496ms:
cow => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\monsters.xml:422:id="cow"
MISSING grain
MISSING iron
leather => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\item_modifiers_groups.xml:64:id="leather"
oil => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3811:id="oil"
pottery => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3639:id="pottery"
sheep => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\monsters.xml:603:id="sheep"
stolen_goods => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3836:id="stolen_goods"
sumpter_horse => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:405:id="sumpter_horse"
MISSING tools
velvet => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3672:id="velvet"

 succeeded in 66799ms:
MISSING_ITEM cow
MISSING_ITEM grain
MISSING_ITEM iron
MISSING_ITEM leather
MISSING_ITEM oil
MISSING_ITEM pottery
MISSING_ITEM sheep
MISSING_ITEM stolen_goods
MISSING_ITEM sumpter_horse
MISSING_ITEM tools
MISSING_ITEM velvet

codex
The first item search was too literal and matched monster/modifier ids before item definitions, while the second assumed `id` is on the same line as `<Item>`. Bannerlord item XML often splits attributes, so I’m using a small XML parser over the item files instead.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern 'id=\"grain\"|id=\"iron\"|id=\"tools\"|id=\"leather\"|id=\"cow\"|id=\"sheep\"' | Select-Object -First 60 | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids=@('"'cow','grain','iron','leather','oil','pottery','sheep','stolen_goods','sumpter_horse','tools','velvet'); "'$found=@{}; foreach($id in $ids){$found[$id]=$false}; $files=Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue; foreach("'$f in $files){ try{ [xml]$x=Get-Content $f.FullName -Raw -ErrorAction Stop } catch { continue }; $nodes=$x.SelectNodes('"'//*[@id]'); foreach("'$n in $nodes){ $id=$n.id; if($ids -contains $id -and ($n.Name -eq '"'Item' -or "'$n.Name -eq '"'CraftedItem')){ if(-not "'$found[$id]){ "$id => $($f.FullName) <$($n.Name)>" }; $found[$id]=$true } } }; foreach($id in $ids){ if(-not $found[$id]){"MISSING_ITEM $id"} }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ids=@('"'cow','grain','iron','leather','oil','pottery','sheep','stolen_goods','sumpter_horse','tools','velvet'); foreach("'$id in $ids){ Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\items','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern ('id=\"'+"'$id+'"'\"') | Select-Object -First 1 | ForEach-Object { \""'$id line => $($_.Path):$($_.LineNumber):$($_.Line.Trim())" } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\items' -Filter *.xml | Select-Object Name,FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 445ms:

[32;1mName                  [0m[32;1m FullName[0m
[32;1m----                  [0m [32;1m--------[0m
arm_armors.xml         E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\arm_…
banners.xml            E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\bann…
body_armors.xml        E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\body…
head_armors.xml        E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head…
horses_and_others.xml  E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\hors…
leg_armors.xml         E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\leg_…
shields.xml            E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shie…
shoulder_armors.xml    E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shou…
tournament_weapons.xml E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\tour…
weapons.xml            E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weap…


 succeeded in 824ms:
cow line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:4015:id="cow"
leather line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3661:id="leather"
oil line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3811:id="oil"
pottery line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3639:id="pottery"
sheep line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3996:id="sheep"
stolen_goods line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3836:id="stolen_goods"
sumpter_horse line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:405:id="sumpter_horse"
velvet line => E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3672:id="velvet"

 succeeded in 1896ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\item_modifiers_groups.xml:64:id="leather"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\monsters.xml:422:id="cow"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\monsters.xml:603:id="sheep"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3661:id="leather"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:3996:id="sheep"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\horses_and_others.xml:4015:id="cow"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCoreMP\ModuleData\items\horses_and_others.xml:3661:id="leather"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCoreMP\ModuleData\items\horses_and_others.xml:3996:id="sheep"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCoreMP\ModuleData\items\horses_and_others.xml:4015:id="cow"

 succeeded in 7942ms:
MISSING_ITEM cow
MISSING_ITEM grain
MISSING_ITEM iron
MISSING_ITEM leather
MISSING_ITEM oil
MISSING_ITEM pottery
MISSING_ITEM sheep
MISSING_ITEM stolen_goods
MISSING_ITEM sumpter_horse
MISSING_ITEM tools
MISSING_ITEM velvet

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern 'grain|tools|iron' -CaseSensitive:"'$false | Select-Object -First 120 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern 'id=\"grain\"|id=\"tools\"|id=\"iron\"|grain|tools|iron' -CaseSensitive:"'$false | Select-Object -First 120 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.Core.DefaultItems 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll' | Select-String -Pattern 'Grain|Iron|Tools|Initialize|GetObject' -Context 3,12 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'DefaultItems.Grain|GetObject<ItemObject>\\(\"grain\"|grain|Grain' -Context 3,12 | Select-Object -First 80 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 606ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:264:<Settlement id="ADOD_IAF_NOX_town_K3" name="Braavos" owner="Faction.clan_empire_south_4" posX="924.643" posY="1455.174" culture="Culture.Braavosi" gate_posX="925.6251" gate_posY="1465.893" text="Braavos is a marvel of Essosi architecture, a bustling megapolis, a temple to commerce and personal freedoms, it is vibrant, colorful, aromatic, and simply breathtaking to behold. Rumors tell that even one whiff of Braavosi air is like a powerful aphrodisiac, undoubtedly the reason the city is also famous for its one thousand and one brothels. There simply is no place like it in the whole known world. It's most likely known far and wide for the Iron Bank. An establishment that can make Kingdoms rise and fall, all decided within its halls. As we know, all roads lead to Braavos, and it is here that destiny is forged, but if there's one thing worth learning, it's that no matter what gods you follow, in Braavos, 'All Men Must Die.'">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:300:<Village id="ADOD_IAF_NOX_village_comp_K3_2" village_type="VillageType.iron_mine" hearth="139" bound="Settlement.ADOD_IAF_NOX_town_K3" background_crop_position="0.0" background_mesh="gui_bg_village_khuzait" wait_mesh="wait_khuzait_village" castle_background_mesh="gui_bg_castle_khuzait" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:799:<Settlement id="castle_ADOD_IAFBraavosCity10" name="The Iron Bank" owner="Faction.clan_empire_south_4" posX="957.813" posY="1582.286" culture="Culture.Braavosi" gate_posX="958.668" gate_posY="1580.339" text="The Iron Bank of Braavos is the financial cornerstone of the Free City, a powerful institution whose reach extends across the known world. With its vast resources, rigid enforcement of debts, and ability to fund kingdoms and wars, the bank wields unmatched influence over the political and economic landscapes of Essos and Westeros. Its motto, “The Iron Bank will have its due,” serves as a chilling reminder of its unyielding resolve and the far-reaching consequences of defaulting on its loans.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:1286:<Village id="castle_village_comp_K2_1" village_type="VillageType.iron_mine" hearth="194" gate_rotation="0.508" bound="Settlement.castle_K2" background_crop_position="0.0" background_mesh="gui_bg_village_battania" wait_mesh="wait_battania_village" castle_background_mesh="gui_bg_castle_battania" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:1437:<Village id="castle_village_comp_K8_1" village_type="VillageType.iron_mine" hearth="370" gate_rotation="0.508" bound="Settlement.castle_K8" background_crop_position="0.0" background_mesh="gui_bg_village_battania" wait_mesh="wait_khuzait_village" castle_background_mesh="gui_bg_castle_battania" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:1663:<Village id="village_comp_K3_2" village_type="VillageType.iron_mine" hearth="139" bound="Settlement.town_K3" background_crop_position="0.0" background_mesh="gui_bg_village_khuzait" wait_mesh="wait_khuzait_village" castle_background_mesh="gui_bg_castle_khuzait" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:1687:<Settlement id="town_K4" name="Adakhakileki" owner="Faction.clan_sturgia_9" posX="2163.761" posY="984.216" culture="Culture.Dothraki" gate_posX="2163.881" gate_posY="989.5676" text="Adakhakileki, or the City of Cannibals, is a ruined settlement at the northern edges of the Red Waste, near the Poison Sea and the Stone Road. Once known as Hazdahn Mo, it was a modest trading post and foothill settlement that succumbed to famine, desperation, and eventual destruction by the Dothraki. The city’s grim name reflects the legends of its fall, overshadowing its past as a hub of survival and trade. Today, its ruins stand as a stark reminder of the dangers of desperation, the harshness of the environment, and the dominance of the Dothraki Sea.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2056:<Village id="village_comp_NOX_ADOD2_2" village_type="VillageType.iron_mine" hearth="322" gate_rotation="0.168" bound="Settlement.town_NOX_ADOD2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2361:<Village id="village_comp_KR2_2" village_type="VillageType.iron_mine" hearth="322" gate_rotation="0.168" bound="Settlement.town_KR2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2701:<!-- Ironborn Settlements -->
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2703:<Settlement id="town_A10" name="Pyke" owner="Faction.clan_sturgia_1" posX="339.474" posY="1358.921" culture="Culture.Ironborn" gate_posX="338.405" gate_posY="1357.77" text="Ancient Pyke was originally built on a cliff jutting out into the sea, but over time the cliff has eroded, leaving the castle's keeps and towers standing on three barren islands and a dozen small stacks of rock, surrounded by water. The towers are connected by swaying rope bridges. The keep, its towers, and walls are made of the same grey-black stone of which the rest of the island is composed. In the thousands of years the castle has stood, it has become covered with green lichen. Since there is no safe anchorage at Pyke, ships sail to nearby Lordsport. A curtain wall encloses the headland of fifty acres and the cliffs around the foot of the wide stone bridge which extend out to the largest islet. The stables, kennels and livestock are located on the headland. The Great Keep, the Kitchen Keep, and the Guest Keep each sit on their own islands. Towers and outbuildings are located on stacks beyond them, linked to each other by covered archways where the pillars stood close and by long, swaying walks of wood and rope when they did not.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2709:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2710:<Location id="tavern" scene_name="IronIslandsTavern" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2723:<Settlement id="village_A10_2" name="Kraken's Keep" posX="338.312" posY="1340.185" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2725:<Village id="village_comp_A10_2" village_type="VillageType.iron_mine" hearth="297" bound="Settlement.town_A10" background_crop_position="0.145" background_mesh="menu_aserai_village_2" wait_mesh="wait_aserai_village" castle_background_mesh="gui_bg_castle_aserai" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2736:<Settlement id="village_A10_3" name="Misty Harbor" posX="296.749" posY="1338.937" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2749:<Settlement id="ADOD_IAF_NOXIX_town_A7" name="Pebbleton" owner="Faction.ADODClan_NoxID9" posX="312.32" posY="1399.836" culture="Culture.Ironborn" gate_posX="313.8812" gate_posY="1399.665" text="Pebbleton is a small but resliient village located on the eastern coast of Great Wyk, the largest of the Iron Islands. While small in scale compared to the holdings of powerful Ironborn lords, Pebbleton plays an important role in the local economy through its fisheries. At the heart of the village stands a square towerhouse with turrets at each corner, a modest stronghold overseeing the settlement and serving as the seat of local governance.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2755:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2756:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2757:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2769:<Settlement id="ADOD_IAF_NOXIX_village_A7_2" name="Pebblestone" posX="370.791" posY="1372.84" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2782:<Settlement id="ADOD_IAF_NOXIX_village_A7_3" name="Seastone" posX="336.178" posY="1467.947" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2795:<Settlement id="ADOD_IAF_NOXIX_village_A7_4" name="Shoreside Sanctuary" posX="257.417" posY="1405.285" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2808:<Settlement id="town_NOX_ADOD7" name="Ten Towers" owner="Faction.clan_sturgia_3" posX="386.1" posY="1396.767" culture="Culture.Ironborn" gate_posX="385.6877" gate_posY="1395.251" text="Ten Towers is the seat of House Harlaw on the eastern coast of the isle of Harlaw in the Iron Islands.The castle consists of ten towers all shaped differently, with wallwalks, covered bridges, and dungeons. The hall of Ten Towers contains the high seat of House Harlaw, with two giant scythes of beaten silver hanging crossed above the seat. The Book Tower, the fattest of the castle's towers, is octagonal and made of large block stones.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2814:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2815:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2816:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2828:<Settlement id="village_NOX_ADOD7_1" name="Stonecrest Village" posX="406.933" posY="1405.5" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2841:<Settlement id="village_NOX_ADOD7_2" name="Rivertide Retreat" posX="321.333" posY="1333.596" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2854:<Settlement id="town_ADOD_IAFIron1" name="Lordsport" owner="Faction.ADODhouse_10" posX="323.573" posY="1375.049" culture="Culture.Ironborn" gate_posX="323.992" gate_posY="1376.563" text="Lordsport is the beating heart of Pyke and the Iron Islands, a vital hub of trade, industry, and seafaring culture. Dominated by the castle, a sturdy stone fortress that oversees the town and its harbor, of House Botley and renowned for its skilled metalworkers, the town thrives on its fishing industry, natural harbor, and proximity to the seat of House Greyjoy. Though modest compared to mainland towns, Lordsport plays a critical role in sustaining the Ironborn’s economy, military, and cultural identity, standing as a testament to their enduring spirit and resourcefulness.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2856:<Town id="town_comp_ADOD_IAFIron1" is_castle="false" level="3" background_crop_position="0.0" background_mesh="gui_bg_town_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.608" prosperity="3100" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2860:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2861:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2862:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2874:<Settlement id="village_ADOD_IAFIron1_1" name="Shipwright's Shores" posX="336.549" posY="1331.777" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2876:<Village id="village_comp_ADOD_IAFIron1_1" village_type="VillageType.fisherman" hearth="311" gate_rotation="0.008" bound="Settlement.town_ADOD_IAFIron1" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2887:<Settlement id="village_ADOD_IAFIron1_2" name="Ironclad Isle" posX="301.508" posY="1332.522" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2889:<Village id="village_comp_ADOD_IAFIron1_2" village_type="VillageType.iron_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_ADOD_IAFIron1" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2900:<Settlement id="town_ADOD_IAFIron2" name="Hammerhorn" owner="Faction.clan_sturgia_8" posX="284.349" posY="1392.757" culture="Culture.Ironborn" gate_posX="282.8138" gate_posY="1393.089" text="Hammerhorn is a grim and imposing stronghold that reflects the harsh realities of life in the Iron Islands. As the seat of House Goodbrother, it dominates the Hardstone Hills, the surrounding hills and cliffs reinfoced with thick stone walls, its iron gates and spiky battlements make it a formidable fortress. Below the castle’s main entrance lie the mines and caves, essential to Hammerhorn’s wealth. These dark, winding passages are toiled in by smallfolk, extracting valuable resources. The castle’s shadowy halls and damp interiors, The great hall of Hammerhorn is infamous for its dank, smoky atmosphere, with flickering hearthfires casting long shadows on the stone walls, speak to the Ironborn’s pragmatic nature, Hammerhorn remains a vital center of power and tradition in the rugged, unyielding landscape of Great Wyk.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2902:<Town id="town_comp_ADOD_IAFIron2" is_castle="false" level="3" background_crop_position="0.0" background_mesh="gui_bg_town_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.608" prosperity="3100" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2906:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2907:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2908:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2920:<Settlement id="village_ADOD_IAFIron2_1" name="Mistcliff Cove" posX="331.826" posY="1496.049" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2922:<Village id="village_comp_ADOD_IAFIron2_1" village_type="VillageType.salt_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_ADOD_IAFIron2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2933:<Settlement id="village_ADOD_IAFIron2_2" name="Waveswept Wharf" posX="243.615" posY="1422.542" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2935:<Village id="village_comp_ADOD_IAFIron2_2" village_type="VillageType.fisherman" hearth="311" gate_rotation="0.008" bound="Settlement.town_ADOD_IAFIron2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2946:<Settlement id="castle_ADOD_IAFIron1" name="Lonely Light" owner="Faction.ADODClan_NoxID10" posX="130.117" posY="1415.822" culture="Culture.Ironborn" gate_posX="130.8808" gate_posY="1416.1" text="The Lonely Light, the westernmost of the Iron Islands, is a remote and enigmatic isle steeped in myths and mystery. The island's namesake keep, Lonely Light, is perched on the highest point of the island. Its beacon, a blazing fire kept alight at all times, serves as a guide for ships navigating the surrounding waters. The keep is constructed from stone and build to whitstand the harsh weather and seastormes. Home to House Farwynd, known for their peculiar customs and rumored supernatural abilities, the island is both a beacon of Ironborn resilience and a symbol of otherworldly intrigue. Surrounded by wildlife and the vast Sunset Sea, the Lonely Light remains a frontier of the known world, embodying the untamed spirit of the Ironborn and the unknown wonders of the oceans beyond.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2948:<Town id="castle_comp_ADOD_IAFIron1" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2952:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2956:<Settlement id="castle_village_ADOD_IAFIron1_1" name="Lonely Star" posX="357.288" posY="1467.928" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2958:<Village id="castle_village_comp_ADOD_IAFIron1_1" village_type="VillageType.battanian_horse_ranch" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron1" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2969:<Settlement id="castle_ADOD_IAFIron2" name="Sparr" owner="Faction.clan_iron_1_1" posX="307.446" posY="1457.967" culture="Culture.Ironborn" gate_posX="308.1713" gate_posY="1457.411" text="House Sparr of Great Wyk is a noble ironborn house rooted in the traditions of the Old Way. From their lands six leagues from Hammerhorn, the Sparrs maintain their seafaring legacy, martial prowess, and loyalty to the Drowned God. Despite the challenges of their harsh environment and the volatile politics of the Iron Islands, the Sparrs thrive as defenders of Great Wyk’s ironborn culture and traditions, holding their title of 'The Sparr' with pride and honor.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2971:<Town id="castle_comp_ADOD_IAFIron2" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2975:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2979:<Settlement id="castle_village_ADOD_IAFIron2_1" name="Saltwater Haven" posX="259.952" posY="1373.875" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2981:<Village id="castle_village_comp_ADOD_IAFIron2_1" village_type="VillageType.salt_mine" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2992:<Settlement id="castle_ADOD_IAFIron3" name="Kenning" owner="Faction.clan_iron_1_0" posX="367.033" posY="1398.696" culture="Culture.Ironborn" gate_posX="366.4281" gate_posY="1398.011" text="House Kenning of Harlaw is a noble ironborn house steeped in the traditions of the Old Way, known for their distinctive heraldry depicting the Storm God’s hand. From their lands on Harlaw, they contribute to the island’s maritime dominance and cultural heritage. Fierce and independent, the Kennings stand as loyal vassals to House Harlaw, maintaining their place as key players in the politics and power struggles of the Iron Islands while embodying the resilient spirit of the ironborn.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2994:<Town id="castle_comp_ADOD_IAFIron3" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:2998:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3002:<Settlement id="castle_village_ADOD_IAFIron3_1" name="Mossy Hollow" posX="269.839" posY="1351.511" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3004:<Village id="castle_village_comp_ADOD_IAFIron3_1" village_type="VillageType.iron_mine" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron3" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3015:<Settlement id="castle_ADOD_IAFIron4" name="Shepherd" owner="Faction.clan_iron_9" posX="353.271" posY="1446.741" culture="Culture.Ironborn" gate_posX="353.6721" gate_posY="1447.331" text="House Shepherd is a noble house of the Iron Islands, sworn to House Greyjoy of Pyke. Rooted in the traditions of the Old Way and the worship of the Drowned God, they play a vital role in the maritime dominance of the Ironborn. Despite the challenges of their harsh environment and the volatile politics of the islands, House Shepherd stands as a proud and resilient house, embodying the ironborn spirit of strength, loyalty, and independence.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3017:<Town id="castle_comp_ADOD_IAFIron4" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3021:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3025:<Settlement id="castle_village_ADOD_IAFIron4_1" name="Saltspire Point" posX="254.943" posY="1466.035" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3027:<Village id="castle_village_comp_ADOD_IAFIron4_1" village_type="VillageType.salt_mine" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron4" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3038:<Settlement id="castle_ADOD_IAFIron5" name="Tawney" owner="Faction.clan_iron_3" posX="340.104" posY="1413.065" culture="Culture.Ironborn" gate_posX="339.261" gate_posY="1412.712" text="House Tawney of Orkmont is a powerful ironborn house, steadfastly loyal to House Greyjoy and deeply rooted in the Old Way. From their lands on Orkmont, they contribute to the military and maritime strength of the Iron Islands through raiding, shipbuilding, and warfare. Known for their tenacity and seafaring prowess, House Tawney stands as a vital pillar of ironborn culture and a formidable force within the archipelago.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3040:<Town id="castle_comp_ADOD_IAFIron5" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3048:<Settlement id="castle_village_ADOD_IAFIron5_1" name="Seabreak Bay" posX="414.044" posY="1405.72" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3050:<Village id="castle_village_comp_ADOD_IAFIron5_1" village_type="VillageType.iron_mine" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron5" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3061:<Settlement id="castle_ADOD_IAFIron6" name="Sunderly" owner="Faction.ADODClan_NoxID11" posX="298.658" posY="1361.306" culture="Culture.Ironborn" gate_posX="299.5639" gate_posY="1361.426" text="House Sunderly of Saltcliffe is a prominent noble house of the Iron Islands, deeply rooted in the traditions of the Old Way. From their seat on the island of Saltcliffe, they contribute to the naval strength, raiding culture, and maritime dominance of the Ironborn. As staunch followers of the Drowned God, House Sunderly remains a proud and resilient pillar of Ironborn society, navigating the challenges of their harsh environment and volatile political landscape.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3063:<Town id="castle_comp_ADOD_IAFIron6" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3067:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3071:<Settlement id="castle_village_ADOD_IAFIron6_1" name="Seabreeze Settlement" posX="262.492" posY="1391.672" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3073:<Village id="castle_village_comp_ADOD_IAFIron6_1" village_type="VillageType.sheep_farm" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron6" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3084:<Settlement id="castle_ADOD_IAFIron7" name="Codd" owner="Faction.ADODClan_NoxID14" posX="292.898" posY="1375.147" culture="Culture.Ironborn" gate_posX="292.0015" gate_posY="1375.324" text="House Codd of the Iron Islands is a noble house infamous for its poor reputation among the ironborn. Their motto, 'Though All Men Do Despise Us,' reflects their outsider status and acceptance of their infamy. Known for their cowardice, thievery, and incestuous practices, they are viewed as dishonorable outcasts in a culture that prizes strength and honor. ">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3086:<Town id="castle_comp_ADOD_IAFIron7" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3090:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3094:<Settlement id="castle_village_ADOD_IAFIron7_1" name="Fogbound Fen" posX="251.015" posY="1407.585" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3096:<Village id="castle_village_comp_ADOD_IAFIron7_1" village_type="VillageType.cattle_farm" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron7" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3107:<Settlement id="castle_ADOD_IAFIron8" name="Humble" owner="Faction.ADODClan_NoxID15" posX="274.088" posY="1421.568" culture="Culture.Ironborn" gate_posX="274.2181" gate_posY="1422.473" text="House Humble of the Iron Islands is a lesser noble house, based on Great Wyk, known for its modest reputation and steadfast loyalty to House Greyjoy. While their name suggests humility or limited influence, they contribute to the Ironborn way of life through raiding, fishing, and adherence to the traditions of the Drowned God. In a society dominated by ambition and aggression, House Humble exemplifies the resilience and perseverance necessary to endure in the harsh world of the Ironborn.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3109:<Town id="castle_comp_ADOD_IAFIron8" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3113:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3117:<Settlement id="castle_village_ADOD_IAFIron8_1" name="Humblebrook" posX="318.689" posY="1341.33" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3119:<Village id="castle_village_comp_ADOD_IAFIron8_1" village_type="VillageType.trapper" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron8" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3130:<Settlement id="castle_ADOD_IAFIron9" name="Stonehouse" owner="Faction.clan_iron_5" posX="316.588" posY="1414.292" culture="Culture.Ironborn" gate_posX="315.6747" gate_posY="1414.324" text="House Stonehouse of Old Wyk is a prominent noble house of the Iron Islands, steeped in the traditions of the Old Way and the worship of the Drowned God. From their seat on Old Wyk, they contribute to the maritime strength, raiding culture, and religious heritage of the Ironborn. Loyal to House Greyjoy of Pyke, House Stonehouse stands as a key player in the political and cultural landscape of the Iron Islands, embodying the resilience and spirit of their people.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3132:<Town id="castle_comp_ADOD_IAFIron9" is_castle="true" level="2" background_crop_position="0.0" background_mesh="gui_bg_castle_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.908" prosperity="990" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3136:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3140:<Settlement id="castle_village_ADOD_IAFIron9_1" name="Blackrock" posX="257.83" posY="1448.224" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3142:<Village id="castle_village_comp_ADOD_IAFIron9_1" village_type="VillageType.iron_mine" hearth="152" gate_rotation="0.308" bound="Settlement.castle_ADOD_IAFIron9" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3153:<Settlement id="castle_village_ADOD_IAFSummer1_1" name="Ebony Bay" posX="909.222" posY="85.369" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3166:<Settlement id="town_NoxixIronIslands_nox1" name="Harridan Hall" owner="Faction.clan_sturgia_3" posX="384.494" posY="1415.47" culture="Culture.Ironborn" gate_posX="384.7649" gate_posY="1413.923" text="Harridan Hall is a stark and practical stronghold, embodying the rugged determination of its inhabitants and their adherence to Ironborn traditions. Perched atop Harridan Hill, it offers both strategic advantage and a harsh reminder of the Iron Islands’ unforgiving nature. As the seat of a vassal branch of House Harlaw, it contributes to the larger framework of the Ironborn’s raiding culture, while standing as a proud, if modest, center of power on the island of Harlaw.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3168:<Town id="town_comp_NoxixIronIslands_nox1" is_castle="false" level="3" background_crop_position="0.0" background_mesh="gui_bg_town_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.608" prosperity="3100" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3172:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3173:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3174:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3186:<Settlement id="village_NoxixIronIslands_nox1" name="Driftcove" posX="346.414" posY="1472.793" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3188:<Village id="village_comp_NoxixIronIslands_nox1" village_type="VillageType.fisherman" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox1" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3199:<Settlement id="village_NoxixIronIslands_nox01" name="Rimehold" posX="362.971" posY="1467.154" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3201:<Village id="village_comp_NoxixIronIslands_nox01" village_type="VillageType.salt_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox1" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3212:<Settlement id="town_NoxixIronIslands_nox2" name="Orkmont" owner="Faction.ADODhouse_38" posX="345.198" posY="1406.534" culture="Culture.Ironborn" gate_posX="343.6391" gate_posY="1406.343" text="Orkmont, the seat of House Orkwood, stands as a testament to the rugged determination of the Ironborn. Nestled in the Hardstone Hills, with its wooden fortress, surrounding pine forest, and resource-rich mines, which supply the Ironborn with the materials needed to make the tools of their trade, it exemplifies the Ironborn ethos of resilience and self-reliance. Though diminished by overharvesting and its reliance on raiding, Orkmont remains a vital stronghold, embodying the harsh beauty and enduring spirit of the Iron Islands.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3214:<Town id="town_comp_NoxixIronIslands_nox2" is_castle="false" level="3" background_crop_position="0.0" background_mesh="gui_bg_town_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.608" prosperity="3100" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3218:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3219:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3220:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3232:<Settlement id="village_NoxixIronIslands_nox2" name="Deepcrag" posX="375.702" posY="1462.776" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3234:<Village id="village_comp_NoxixIronIslands_nox2" village_type="VillageType.iron_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3245:<Settlement id="village_NoxixIronIslands_nox02" name="Shatterbay" posX="369.925" posY="1375.203" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3247:<Village id="village_comp_NoxixIronIslands_nox02" village_type="VillageType.fisherman" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox2" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3258:<Settlement id="town_NoxixIronIslands_nox3" name="Grey Garden" owner="Faction.clan_sturgia_3" posX="371.407" posY="1427.73" culture="Culture.Ironborn" gate_posX="371.5498" gate_posY="1429.294" text="Grey Garden, the seat of House Harlaw of Grey Garden, is a stark and resilient stronghold perched atop the rugged cliffs of Harlaw a tall, narrow watchtower stands as part of the keep, offering a clear view of the surrounding seas and coastline.. Its stone keep, watchtower, and harsh surroundings exemplify the Ironborn’s unyielding spirit and dependence on the sea. As part of the larger network of House Harlaw’s holdings, Grey Garden serves as a bastion of Ironborn culture, blending its maritime heritage with the austere realities of life in the Iron Islands.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3260:<Town id="town_comp_NoxixIronIslands_nox3" is_castle="false" level="3" background_crop_position="0.0" background_mesh="gui_bg_town_sturgia" wait_mesh="wait_sturgia_town" gate_rotation="0.608" prosperity="3100" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3264:<Location id="arena" scene_name="IronIslandsArena" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3265:<Location id="tavern" scene_name="IronIslandsTavernV2" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3266:<Location id="lordshall" scene_name_1="IronIslandsGenericKeep" scene_name_2="IronIslandsGenericKeep" scene_name_3="IronIslandsGenericKeep" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3278:<Settlement id="village_NoxixIronIslands_nox3" name="Wavebreak" posX="371.63" posY="1376.938" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3280:<Village id="village_comp_NoxixIronIslands_nox3" village_type="VillageType.salt_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox3" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3291:<Settlement id="village_NoxixIronIslands_nox03" name="Ironhead" posX="373.246" posY="1374.819" culture="Culture.Ironborn" text="Placeholder Text.">
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3293:<Village id="village_comp_NoxixIronIslands_nox03" village_type="VillageType.iron_mine" hearth="311" gate_rotation="0.008" bound="Settlement.town_NoxixIronIslands_nox3" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\A Dance of Dragons - Map\ModuleData\settlements.xml:3304:<Settlement id="town_NoxixIronIslands_nox4" name="Saltcliffe" owner="Faction.clan_iron_2" posX="284.835" posY="1361.087" culture="Culture.Ironborn" gate_posX="284.2437" gate_posY="1362.542" text="Saltcliffe, the clifftop seat of House Saltcliffe, is a small but resilient fortress embodying the rugged character of the Ironborn. Perched on the northwestern coast of the isle of Saltcliffe, the castle commands a dramatic view of the Sunset Sea and serves as a hub for fishing, raiding, and maritime life. While modest in size and resources, Saltcliffe’s strategic location and enduring connection to Ironborn traditions make it a vital part of the Iron Islands’ network of strongholds.">

 succeeded in 633ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_battle_scenes.xml:75:<!-- lotrtaom_iron_hills_01_forceatmo removed: scene.xscene CTDs on load, 8/8 attempts
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:314:<Village id="village_comp_gundabad_a" village_type="VillageType.iron_mine" hearth="300" max_prosperity="3000" gate_rotation="0.008" bound="Settlement.town_gundabad" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:351:<Village id="castle_village_comp_gundabad_b" village_type="VillageType.iron_mine" hearth="400" max_prosperity="3000" gate_rotation="0.008" bound="Settlement.castle_gundabad_wall" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\custom_settlements.xml:531:<Village id="castle_village_comp_erebor_a" village_type="VillageType.iron_mine" hearth="400" max_prosperity="3000" gate_rotation="0.008" bound="Settlement.castle_erebor_a" background_crop_position="0.0" background_mesh="gui_bg_village_sturgia" wait_mesh="wait_sturgia_village" castle_background_mesh="gui_bg_castle_sturgia" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:12:<string id="taom_career_black_uruk_captain_desc" text="{=taom_career_black_uruk_captain_desc}An iron-muscled champion of Mordor who leads Black Uruk warbands into the breach, absorbing punishment that would fell lesser warriors and turning the tide through sheer brutish dominance." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:50:<string id="taom_career_balchoth_kan_desc" text="{=taom_career_balchoth_kan_desc}A heavy cavalry commander of the Balchoth who leads unstoppable mounted charges, maintaining iron morale no matter the odds." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:57:<string id="taom_career_ironguard" text="{=taom_career_ironguard}Ironguard" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:58:<string id="taom_career_ironguard_desc" text="{=taom_career_ironguard_desc}An elite dwarven warrior clad in mithril-forged plate who holds the deeps of Erebor against orc and dragon alike." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:84:<string id="taom_career_uruk_crossbow_desc" text="{=taom_career_uruk_crossbow_desc}A disciplined Uruk marksman wielding Saruman's iron crossbows, delivering devastating volleys from Orthanc's forges." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:111:<string id="taom_ability_captain_of_osgiliath_tt" text="{=taom_ability_captain_of_osgiliath_tt}Rally your soldiers with iron discipline — boosts +15% melee damage and +10% damage reduction to allies within 50m for 8s." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:127:<string id="taom_ability_watchman_of_stangard_tt" text="{=taom_ability_watchman_of_stangard_tt}Dig in with iron resolve — boosts +15% melee damage and +10% damage reduction to allies within 50m for 8s." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:153:<string id="taom_ability_balchoth_kan_tt" text="{=taom_ability_balchoth_kan_tt}Invoke the iron discipline of the Balchoth — boosts +25% charge damage, +20% mount speed, and +10% melee damage for 8s." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:160:<string id="taom_ability_ironguard" text="{=taom_ability_ironguard}Mithril Bastion" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:161:<string id="taom_ability_ironguard_tt" text="{=taom_ability_ironguard_tt}Invoke the strength of dwarven craft — boosts +15% melee damage and +10% damage reduction to allies within 50m for 8s." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:186:<string id="taom_ability_uruk_crossbow" text="{=taom_ability_uruk_crossbow}Iron Rain" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:305:<string id="taom_buc_t1_a_key" text="{=taom_buc_t1_a_key}Iron Hide" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:321:<string id="taom_buc_t1_b_p3" text="{=taom_buc_t1_b_p3}Iron Discipline" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:465:<string id="taom_sr_t2_b_p4" text="{=taom_sr_t2_b_p4}Iron Saddle" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:466:<string id="taom_sr_t2_b_p4_desc" text="{=taom_sr_t2_b_p4_desc}Mordor leather and iron rings protect rider and beast. +40 health." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:504:<string id="taom_ohw_t1_b_p3" text="{=taom_ohw_t1_b_p3}Iron Will" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:648:<string id="taom_ew_t2_b_p4" text="{=taom_ew_t2_b_p4}Iron Stirrups" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:676:<string id="taom_ws_t1_a_p2_desc" text="{=taom_ws_t1_a_p2_desc}Years at isolated posts forge you like iron. +25 health." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:700:<string id="taom_ws_t2_a_p4_desc" text="{=taom_ws_t2_a_p4_desc}The isolation of Stangard breeds iron resolve. +35 health." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:709:<string id="taom_ws_t2_b_p4" text="{=taom_ws_t2_b_p4}Border Iron" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:831:<string id="taom_wh_t2_b_p4" text="{=taom_wh_t2_b_p4}Iron Will of the Hunt" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:859:<string id="taom_cr_t1_a_p2_desc" text="{=taom_cr_t1_a_p2_desc}Dunlending mountain ponies are tough as iron. +5% horse health." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:871:<string id="taom_cr_t1_b_p3_desc" text="{=taom_cr_t1_b_p3_desc}The hills forge you like iron. +30 health." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:882:<string id="taom_cr_t2_a_p4" text="{=taom_cr_t2_a_p4}Iron Pony" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1317:<string id="taom_ironguard_root_desc" text="{=taom_ironguard_root_desc}Mithril Bastion grants +25 health, dwarven craft protecting you." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1720:<string id="taom_uruk_crossbow_root_desc" text="{=taom_uruk_crossbow_root_desc}Iron Rain grants +5% ranged damage with Orthanc-forged bolts." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1721:<string id="taom_ucb_t1_a_key" text="{=taom_ucb_t1_a_key}Iron Rain radius increased, covering wider kill zones." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1726:<string id="taom_ucb_t1_b_key" text="{=taom_ucb_t1_b_key}Iron Rain duration extended, sustained barrage." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1731:<string id="taom_ucb_t2_a_key" text="{=taom_ucb_t2_a_key}Iron Rain charge reduced, maintaining constant pressure." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1736:<string id="taom_ucb_t2_b_key" text="{=taom_ucb_t2_b_key}Iron Rain gains additional penetration while active." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1741:<string id="taom_ucb_t3_a_key" text="{=taom_ucb_t3_a_key}Iron Rain radius greatly expanded, inescapable volleys." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_career_strings.xml:1746:<string id="taom_ucb_t3_b_key" text="{=taom_ucb_t3_b_key}Iron Rain charge greatly reduced, relentless fire." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:69:<string id="taom_cc_taom_parent_erebor_4_text" text="{=taom_cc_taom_parent_erebor_4_text}Ironfoot Warrior" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:71:<string id="taom_cc_taom_parent_erebor_4_desc" text="{=taom_cc_taom_parent_erebor_4_desc}Your family fought with Dain Ironfoot's warriors, mastering axe and shield and wielding crossbows to hold Erebor's gates." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:119:<string id="taom_cc_taom_parent_mirkwood_4_desc" text="{=taom_cc_taom_parent_mirkwood_4_desc}Your family were artisans of the Woodland Realm, crafting exquisite weapons and tools from the resources of the forest." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:159:<string id="taom_cc_taom_parent_isengard_2_desc" text="{=taom_cc_taom_parent_isengard_2_desc}Your family crafted tools and weapons within the shadow of Orthanc, forging instruments of Saruman's will." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:183:<string id="taom_cc_taom_parent_gundabad_2_desc" text="{=taom_cc_taom_parent_gundabad_2_desc}Your family were skilled smiths, crafting tools and weapons to equip the defenders of Gundabad." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:263:<string id="taom_cc_taom_parent_empire_4_desc" text="{=taom_cc_taom_parent_empire_4_desc}Your family were craftsmen of the Dunlending clans, fashioning weapons, tools, and ornaments from the resources of the hills." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:383:<string id="taom_cc_taom_parent_khuzait_4_desc" text="{=taom_cc_taom_parent_khuzait_4_desc}Your family worked the fertile valleys near the inland sea of Rhun, growing grain and raising livestock." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:459:<string id="taom_cc_taom_youth_mordor_4_desc" text="{=taom_cc_taom_youth_mordor_4_desc}You drilled in the relentless formations of Sauron's infantry, learning to shoulder-charge and cut with blade and axe in ranks that moved like a single armoured fist. Weakness was punished and strength rewarded in iron." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:477:<string id="taom_cc_taom_youth_erebor_3_text" text="{=taom_cc_taom_youth_erebor_3_text}Trained with the Iron Guard." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:479:<string id="taom_cc_taom_youth_erebor_3_desc" text="{=taom_cc_taom_youth_erebor_3_desc}You joined the Iron Guard, Erebor's shock troops, learning to hurl throwing axes and fight in the tight corridors of the mountain halls." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:619:<string id="taom_cc_taom_youth_dolguldur_5_desc" text="{=taom_cc_taom_youth_dolguldur_5_desc}You drilled in the iron-disciplined ranks of Dol Guldur's dark legions, learning to wield heavy weapons in formation as the corruption of the Necromancer's fortress settled into your stride and your silence." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:755:<string id="taom_cc_taom_youth_battania_3_desc" text="{=taom_cc_taom_youth_battania_3_desc}You moved through the old kingdom's overgrown ruins and moorlands as a ranger scout, learning to live off the wild land and track both beast and enemy across terrain that held memory in its stones. Speed and silence were your only tools." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:827:<string id="taom_cc_taom_education_mordor_2_desc" text="{=taom_cc_taom_education_mordor_2_desc}You moved goods along the shadowed roads of Mordor, learning to barter and bluff your way past toll-keepers and corrupt overseers. The danger sharpened your wits, and you discovered that information was worth more than iron in the Dark Land." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:857:<string id="taom_cc_taom_education_erebor_4_text" text="{=taom_cc_taom_education_erebor_4_text}Drilled with the Ironfoot shieldwall." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:859:<string id="taom_cc_taom_education_erebor_4_desc" text="{=taom_cc_taom_education_erebor_4_desc}You trained in the disciplined close-quarters style of Dain Ironfoot's warriors, learning to brace a shield and swing an axe in tight tunnel formations. Your short stride was an advantage in the corridors where taller folk stumbled." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:971:<string id="taom_cc_taom_education_gundabad_2_desc" text="{=taom_cc_taom_education_gundabad_2_desc}You learned to forge crude but effective arms in the noisy smithing halls of the mountain, learning to trade them for supplies and favors. The work was ugly but the skills were real, and you left the forge knowing both iron and dealing." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:995:<string id="taom_cc_taom_education_dolguldur_2_desc" text="{=taom_cc_taom_education_dolguldur_2_desc}You worked the black-iron forges of Dol Guldur, learning to shape weapons under conditions that would break a lesser youth. The materials were dark and the methods darker, and you left with knowledge that few would admit to having." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1051:<string id="taom_cc_taom_education_empire_4_desc" text="{=taom_cc_taom_education_empire_4_desc}You learned to shape metal and wood in the practical crafts of the Dunlending settlements, making tools, weapons, and fittings for the clans. The work gave you clever hands and an eye for how things were put together and how they could be taken apart." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1171:<string id="taom_cc_taom_education_khuzait_4_desc" text="{=taom_cc_taom_education_khuzait_4_desc}You farmed the fertile valleys around the inland sea, learning to craft tools and make the most of limited materials in the Easterling way. When training with the blade came you took to it with the methodical discipline the farm had given you." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1175:<string id="taom_cc_taom_education_khuzait_5_desc" text="{=taom_cc_taom_education_khuzait_5_desc}You sat with the shamans and healers of the Easterling clans, learning the ritual knowledge and practical medicine that kept the nomadic people alive on the open plains. Words and understanding were your tools long before any weapon." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1189:<string id="taom_cc_taom_adulthood_gondor_2_text" text="{=taom_cc_taom_adulthood_gondor_2_text}You negotiated a vital grain supply contract for Minas Tirith." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1223:<string id="taom_cc_taom_adulthood_mordor_4_desc" text="{=taom_cc_taom_adulthood_mordor_4_desc}When the exhausted laborers put down their tools and refused to move, you stepped in alone, identified the three instigators, and handled the situation before the overseers noticed the delay. The gang went back to work. The instigators did not cause further trouble." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1239:<string id="taom_cc_taom_adulthood_erebor_2_desc" text="{=taom_cc_taom_adulthood_erebor_2_desc}The terms on Erebor's iron exports had not been renegotiated since the Mountain's reclamation. You identified the imbalance, assembled the facts, and presented the case to King Dain's steward with such clarity that the agreement was revised within the month." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1359:<string id="taom_cc_taom_adulthood_gundabad_2_desc" text="{=taom_cc_taom_adulthood_gundabad_2_desc}The southern clan had iron but no smiths. Gundabad had smiths but needed food and furs. You brokered the first exchange, set the terms, and kept both sides from killing each other long enough for the deal to become habit. The warchief gave you a percentage." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1431:<string id="taom_cc_taom_adulthood_empire_2_desc" text="{=taom_cc_taom_adulthood_empire_2_desc}The harvest had failed and the winter stores were low. You went to Isengard alone, spoke to the right man, and came back with grain in exchange for iron ore the clan had been sitting on for years. The chief was astonished; the clan ate." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1535:<string id="taom_cc_taom_adulthood_battania_4_desc" text="{=taom_cc_taom_adulthood_battania_4_desc}The settlement's supply line had been cut and the tools were failing. You assessed what could be recovered, salvaged the best material from broken pieces, and reforged it into working equipment. Three Rangers asked for your work over new supply when the line reopened." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1549:<string id="taom_cc_taom_adulthood_khuzait_2_text" text="{=taom_cc_taom_adulthood_khuzait_2_text}You opened trade with western merchants that gave the clans access to steel tools." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_cc_strings.xml:1551:<string id="taom_cc_taom_adulthood_khuzait_2_desc" text="{=taom_cc_taom_adulthood_khuzait_2_desc}The clans had always traded east, but the best steel came from the west. You made the first contact, found a merchant willing to make the journey to the Sea of Rhun, and negotiated terms that made the exchange worthwhile for both parties. The tools that came back transformed three winters of work." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lord_skill_sets.xml:3:<!-- Generated by tools/apply_culture_skills_traits.py. Do not edit by hand. -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:5:Harvested by tools/_harvest_lotr_issue_strings.py. -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:6:<string id="taom_lotr_issue_grain_title" text="{=taom_lotr_issue_grain_title}{ISSUE_SETTLEMENT} Needs Seed Grain" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:7:<string id="taom_lotr_issue_grain_desc" text="{=taom_lotr_issue_grain_desc}The folk of {ISSUE_SETTLEMENT} have lost their seed grain and will go hungry unless the fields are sown." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:8:<string id="taom_lotr_issue_grain_brief" text="{=taom_lotr_issue_grain_brief}The shadow has been hard on us, traveller. Rats and a bitter winter took our seed-corn. Without grain to sow, we will not last the year." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:9:<string id="taom_lotr_issue_grain_accept" text="{=taom_lotr_issue_grain_accept}Is there nothing to be done?" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:10:<string id="taom_lotr_issue_grain_expl" text="{=taom_lotr_issue_grain_expl}If you could bring us {COUNT} measures of grain, we could sow the fields before the season turns. We have no coin to buy so much ourselves." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:11:<string id="taom_lotr_issue_grain_soln" text="{=taom_lotr_issue_grain_soln}I will find that grain for you." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:12:<string id="taom_lotr_issue_grain_task" text="{=taom_lotr_issue_grain_task}Collect grain ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:14:<string id="taom_lotr_issue_supplies_desc" text="{=taom_lotr_issue_supplies_desc}A lord mustering for war needs grain to feed the gathered host before it marches." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:15:<string id="taom_lotr_issue_supplies_brief" text="{=taom_lotr_issue_supplies_brief}My soldiers gather, but a sword does not fill a belly. Without grain to feed them, this host will scatter before it ever takes the field." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:16:<string id="taom_lotr_issue_supplies_accept" text="{=taom_lotr_issue_supplies_accept}How much grain do you need?" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:17:<string id="taom_lotr_issue_supplies_expl" text="{=taom_lotr_issue_supplies_expl}Bring me {COUNT} measures of grain and my men will march on full stomachs. Do this and the realm will remember it." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:18:<string id="taom_lotr_issue_supplies_soln" text="{=taom_lotr_issue_supplies_soln}I will bring grain for your host." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:19:<string id="taom_lotr_issue_supplies_task" text="{=taom_lotr_issue_supplies_task}Bring grain to the host ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:27:<string id="taom_lotr_issue_crafting_title" text="{=taom_lotr_issue_crafting_title}{ISSUE_SETTLEMENT} Needs Iron" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:28:<string id="taom_lotr_issue_crafting_desc" text="{=taom_lotr_issue_crafting_desc}The smith of {ISSUE_SETTLEMENT} has run dry of iron and cannot mend tool nor blade." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:29:<string id="taom_lotr_issue_crafting_brief" text="{=taom_lotr_issue_crafting_brief}The forge stands cold, traveller. Without iron the smith cannot shoe a horse nor mend a ploughshare, and the village suffers for it." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:30:<string id="taom_lotr_issue_crafting_accept" text="{=taom_lotr_issue_crafting_accept}Where would you get iron?" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:31:<string id="taom_lotr_issue_crafting_expl" text="{=taom_lotr_issue_crafting_expl}Bring us {COUNT} bars of iron and the forge will ring again. We have not the coin to buy so much ourselves." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:32:<string id="taom_lotr_issue_crafting_soln" text="{=taom_lotr_issue_crafting_soln}I will find you iron." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:33:<string id="taom_lotr_issue_crafting_task" text="{=taom_lotr_issue_crafting_task}Deliver iron ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:34:<string id="taom_lotr_issue_tools_title" text="{=taom_lotr_issue_tools_title}{ISSUE_SETTLEMENT} Needs Tools" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:35:<string id="taom_lotr_issue_tools_desc" text="{=taom_lotr_issue_tools_desc}The folk of {ISSUE_SETTLEMENT} lack the tools to work their land and craft their goods." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:36:<string id="taom_lotr_issue_tools_brief" text="{=taom_lotr_issue_tools_brief}Our scythes are rusted through and our hammers broken. Honest work is slow work without honest tools." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:37:<string id="taom_lotr_issue_tools_accept" text="{=taom_lotr_issue_tools_accept}What do you need?" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:38:<string id="taom_lotr_issue_tools_expl" text="{=taom_lotr_issue_tools_expl}If you brought us {COUNT} sets of tools, the village could set itself right and prosper once more." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:39:<string id="taom_lotr_issue_tools_soln" text="{=taom_lotr_issue_tools_soln}I will bring you tools." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:40:<string id="taom_lotr_issue_tools_task" text="{=taom_lotr_issue_tools_task}Deliver tools ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:88:<string id="taom_lotr_issue_deserters_brief" text="{=taom_lotr_issue_deserters_brief}Broken men with swords come for our grain and our coin, and they have spilled blood. We cannot fight them alone." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:181:<string id="taom_lotr_issue_revenue_desc" text="{=taom_lotr_issue_revenue_desc}A landowner is owed a tithe in grain that the outlying farms have not paid." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:182:<string id="taom_lotr_issue_revenue_brief" text="{=taom_lotr_issue_revenue_brief}The farms owe me their tithe and pay it in grain, not coin -- when they pay at all. Collect what is owed and bring it in, and you will have a cut." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:184:<string id="taom_lotr_issue_revenue_expl" text="{=taom_lotr_issue_revenue_expl}Bring in {COUNT} measures of grain as the tithe demands and I will pay you a fair share for the collecting." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:186:<string id="taom_lotr_issue_revenue_task" text="{=taom_lotr_issue_revenue_task}Collect the grain tithe ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:273:<string id="taom_lotr_issue_special_title" text="{=taom_lotr_issue_special_title}Iron for the Forge" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:274:<string id="taom_lotr_issue_special_desc" text="{=taom_lotr_issue_special_desc}A gang leader wants fine iron brought to forge a set of special blades." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:275:<string id="taom_lotr_issue_special_brief" text="{=taom_lotr_issue_special_brief}I want blades made -- proper ones, not the rubbish my lads carry -- and that takes good iron. Bring me the metal and I'll see the smiths put to work." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:276:<string id="taom_lotr_issue_special_accept" text="{=taom_lotr_issue_special_accept}How much iron?" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:277:<string id="taom_lotr_issue_special_expl" text="{=taom_lotr_issue_special_expl}Bring me {COUNT} bars of good iron for the forge and there's coin in it -- the blades are my business." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:278:<string id="taom_lotr_issue_special_soln" text="{=taom_lotr_issue_special_soln}I'll bring you iron." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_lotr_issue_strings.xml:279:<string id="taom_lotr_issue_special_task" text="{=taom_lotr_issue_special_task}Deliver iron ({COUNT})" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:244:<string id="str_culture_description.mordor" text="{=taom_str_culture_description.mordor}The armies of Mordor, forged in the shadow of Mount Doom, are a relentless force of darkness commanded by Sauron, the Dark Lord. Comprised of orcs, trolls, and wicked men, they march to war under the burning Eye, sowing fear and destruction. These legions are driven by cruelty and servitude, wielding jagged blades and crude armor. Bound by Sauron's iron will, the forces of Mordor are a deadly tide seeking to engulf all of Middle-earth in shadow." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:529:<string id="taom_the_fix_GiaTh92Q" text="{=GiaTh92Q}{QUEST_GIVER.LINK}, commanding an army of {QUEST_GIVER_FACTION}, has told you that they need food supplies for their upcoming military campaign. {?QUEST_GIVER.GENDER}She{?}He{\?} wanted you to deliver {GRAIN_AMOUNT} sacks of grain and although it's not necessary, to provide {LIVESTOCK_AMOUNT} live stocks and {WINE_AMOUNT} barrels of wine {?QUEST_GIVER.GENDER}she{?}he{\?} would appreciate it." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:773:<string id="TAOM_company_text" text="{=TAOM_company_text}The Corsair Blades are veteran sea-fighters from the harbours of Umbar who have taken to selling their skills on land. They fight as crossbowmen — the preferred arm of shipboard warfare — and are as comfortable in a siege as they are on a rolling deck. Their captain, a scarred Númenórean exile, keeps them to their contracts with iron discipline, knowing that their reputation is their most valuable cargo." />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:889:<string id="taom_career_rank3_codyan_legionaire" text="{=taom_career_rank3_codyan_legionaire}Ironwall of Rhûn" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:901:<string id="taom_career_rank3_crossbow_master" text="{=taom_career_rank3_crossbow_master}Engine-Lord of the Iron Hills" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:929:<string id="taom_career_rank1_ironguard" text="{=taom_career_rank1_ironguard}Deep Warden" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:930:<string id="taom_career_rank2_ironguard" text="{=taom_career_rank2_ironguard}Ironguard of Erebor" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:931:<string id="taom_career_rank3_ironguard" text="{=taom_career_rank3_ironguard}Shieldwall of Durin" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1017:<string id="taom_career_grp_balchoth_kan_t2_b" text="{=taom_career_grp_balchoth_kan_t2_b}Iron Horseman" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1051:<string id="taom_career_grp_chariot_warlord_t1_b" text="{=taom_career_grp_chariot_warlord_t1_b}Iron Wheel" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1065:<string id="taom_career_grp_codyan_legionaire_t2_b" text="{=taom_career_grp_codyan_legionaire_t2_b}Ironclad Phalanx" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1086:<string id="taom_career_grp_crossbow_master_t1_a" text="{=taom_career_grp_crossbow_master_t1_a}Iron Hills Volley" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1146:<string id="taom_career_grp_ironguard_t1_a" text="{=taom_career_grp_ironguard_t1_a}Hammer of the Deeps" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1147:<string id="taom_career_grp_ironguard_t1_b" text="{=taom_career_grp_ironguard_t1_b}Gatewall Warden" />
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\taom_module_strings.xml:1148:<string id="taom_career_grp_ironguard_t2_a" text="{=taom_career_grp_ironguard_t2_a}Wrath of Durin's Folk" />

2026-06-20T18:15:35.858368Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1.3 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.Core.DefaultItems in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 1296ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.Core.DefaultItems in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 1809ms:
  
  namespace TaleWorlds.CampaignSystem.Issues;
  
> public class HeadmanNeedsGrainIssueBehavior : CampaignBehaviorBase
  {
> 	public class HeadmanNeedsGrainIssue : IssueBase
  	{
  		private const int IssueDuration = 30;
  
  		private const int AlternativeSolutionSuccessRenownBonus = 1;
  
  		private const int AlternativeSolutionSuccessGenerosityBonus = 30;
  
  		private const int AlternativeSolutionFailPowerPenalty = -5;
  
  		private const int QuestTimeLimit = 18;
  
  		private const int AlternativeSolutionSuccessPowerBonus = 10;
  
  		public override AlternativeSolutionScaleFlag AlternativeSolutionScaleFlags => AlternativeSolutionScaleFlag.Duration;
  
> 		private int NeededGrainAmount => (int)(12f + 180f * base.IssueDifficultyMultiplier);
  
> 		private int AlternativeSolutionNeededGold => NeededGrainAmount * AverageGrainPriceInCalradia;
  
  		public override int AlternativeSolutionBaseNeededMenCount => 3 + TaleWorlds.Library.MathF.Ceiling(6f * base.IssueDifficultyMultiplier);
  
  		protected override int AlternativeSolutionBaseDurationInDaysInternal => 2 + TaleWorlds.Library.MathF.Ceiling(6f * base.IssueDifficultyMultiplier);
  
  		protected override int RewardGold => 0;
  
  		[CachedData]
  		private Settlement NearbySuitableSettlement
  		{
  			get
  			{
  					{
  						if (x.Town != null && !x.Town.IsCastle && !x.MapFaction.IsAtWarWith(base.IssueOwner.MapFaction))
  						{
> 							int price = x.Town.MarketData.GetPrice(DefaultItems.Grain, MobileParty.MainParty);
> 							int inStore = x.Town.MarketData.GetCategoryData(DefaultItemCategories.Grain).InStore;
> 							bool num = price > 0 && price < AverageGrainPriceInCalradia * 2;
  							bool flag = inStore < 250;
  							return num && flag;
  						}
  						return false;
  					});
  					_nearbySuitableSettlementCache = nearbySuitableSettlementCache;
  				}
  				return _nearbySuitableSettlementCache;
  			}
  		}
  
  		public override TextObject Title
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=LPMXVHHT}{ISSUE_SETTLEMENT} Needs Grain Seeds");
  				textObject.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
  				return textObject;
  			}
  		}
  
  		public override TextObject Description
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=OJObD61e}The headman of {ISSUE_SETTLEMENT} needs grain seeds for the coming sowing season.");
  				textObject.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
  				return textObject;
  			}
  		}
  
> 		public override TextObject IssueBriefByIssueGiver => new TextObject("{=p1buAbOQ}The harvest has been poor, and rats have eaten much of our stores. We can eat less and tighten our belts, but if we don't have seed grain left over to plant, we'll starve next year.[if:convo_dismayed][ib:demure2]");
  
  		public override TextObject IssueAcceptByPlayer => new TextObject("{=vKwndBbe}Is there a way to prevent this?");
  
  		public override TextObject IssueQuestSolutionExplanationByIssueGiver
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=nG750jQB}Grain will solve our problems. If we had {GRAIN_AMOUNT} bushels, we could use it to sow our fields. But I doubt that {NEARBY_TOWN} has so much to sell at this time of the year. {GRAIN_AMOUNT} bushels of grain costs around {DENAR_AMOUNT}{GOLD_ICON} in the markets, and we don't have that![if:convo_thinking]");
> 				int price = NearbySuitableSettlement.Town.MarketData.GetPrice(DefaultItems.Grain, MobileParty.MainParty);
  				textObject.SetTextVariable("NEARBY_TOWN", NearbySuitableSettlement.Name);
> 				textObject.SetTextVariable("GRAIN_AMOUNT", NeededGrainAmount);
> 				textObject.SetTextVariable("DENAR_AMOUNT", price * NeededGrainAmount);
  				textObject.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
  				return textObject;
  			}
  		}
  
  		public override TextObject IssueAlternativeSolutionExplanationByIssueGiver
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=5NYPqKBj}I know you're busy, but maybe you can ask some of your men to find us that grain? {MEN_COUNT} men should do the job along with {GOLD}{GOLD_ICON}, and I'd reckon the whole affair should take two weeks.{newline}I'm desperate here, {?PLAYER.GENDER}madam{?}sir{\\?}... Don't let our children starve![if:convo_dismayed][ib:demure]");
  				textObject.SetTextVariable("MEN_COUNT", GetTotalAlternativeSolutionNeededMenCount());
  				textObject.SetTextVariable("GOLD", AlternativeSolutionNeededGold);
  				textObject.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
  				textObject.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
  				return textObject;
  			}
  		}
  
> 		public override TextObject IssueQuestSolutionAcceptByPlayer => new TextObject("{=ihfuqu2S}I will find that seed grain for you.");
  
  		public override TextObject IssueAlternativeSolutionAcceptByPlayer
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=HCMsvAFv}I can order one of my companions and {MEN_COUNT} men to find grain for you.");
  				textObject.SetTextVariable("MEN_COUNT", GetTotalAlternativeSolutionNeededMenCount());
  				return textObject;
  			}
  		}
  
> 		public override TextObject IssueDiscussAlternativeSolution => new TextObject("{=W6X5DffB}Thank you for sparing the men to bring us that seed grain, {?PLAYER.GENDER}madam{?}sir{\\?}. That should get us through the hard times ahead.[if:convo_grateful][ib:normal]");
  
  		public override TextObject IssueAsRumorInSettlement
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=WVobv24n}Heaven save us if {QUEST_GIVER.NAME} can't get {?QUEST_GIVER.GENDER}her{?}his{\\?} hands on more grain.");
  				StringHelpers.SetCharacterProperties("QUEST_GIVER", base.IssueOwner.CharacterObject, textObject);
  				return textObject;
  			}
  		}
  
  		public override TextObject IssueAlternativeSolutionResponseByIssueGiver => new TextObject("{=k63ZKmXX}Thank you, {?PLAYER.GENDER}milady{?}sir{\\?}! You are a saviour.[if:convo_grateful][ib:normal]");
  
  		public override bool IsThereAlternativeSolution => true;
  
  		public override bool IsThereLordSolution => false;
  
  		protected override TextObject AlternativeSolutionStartLog
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=a0UTO8tW}{ISSUE_OWNER.LINK}, the headman of {ISSUE_SETTLEMENT}, asked you to deliver {GRAIN_AMOUNT} bushels of grain to {?QUEST_GIVER.GENDER}her{?}him{\\?} to use as seeds. Otherwise the peasants cannot sow their fields and starve in the coming season. You have agreed to send your companion {COMPANION.NAME} along with {MEN_COUNT} men to find some grain and return to the village. Your men should return in {RETURN_DAYS} days.");
  				StringHelpers.SetCharacterProperties("ISSUE_OWNER", base.IssueOwner.CharacterObject, textObject);
  				StringHelpers.SetCharacterProperties("COMPANION", base.AlternativeSolutionHero.CharacterObject, textObject);
  				textObject.SetTextVariable("ISSUE_SETTLEMENT", base.IssueSettlement.Name);
> 				textObject.SetTextVariable("GRAIN_AMOUNT", NeededGrainAmount);
  				textObject.SetTextVariable("RETURN_DAYS", GetTotalAlternativeSolutionDurationInDays());
  				textObject.SetTextVariable("MEN_COUNT", AlternativeSolutionSentTroops.TotalManCount - 1);
  				return textObject;
  			}
  		}
  
  		public override TextObject IssueAlternativeSolutionSuccessLog
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=IYSfevY0}Your companion has delivered {GRAIN_AMOUNT} units of grain to the villagers. They will be able to sow them before the coming winter. You have saved a lot of lives today. The Headman and the villagers are grateful.");
> 				textObject.SetTextVariable("GRAIN_AMOUNT", NeededGrainAmount);
  				return textObject;
  			}
  		}
  
  		protected override int CompanionSkillRewardXP => (int)(500f + 700f * base.IssueDifficultyMultiplier);
  
> 		internal static void AutoGeneratedStaticCollectObjectsHeadmanNeedsGrainIssue(object o, List<object> collectedObjects)
  		{
> 			((HeadmanNeedsGrainIssue)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  		{
  			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
> 		public HeadmanNeedsGrainIssue(Hero issueOwner)
  			: base(issueOwner, CampaignTime.DaysFromNow(30f))
  		{
  		}
  
  		protected override float GetIssueEffectAmountInternal(IssueEffect issueEffect)
  		{
  			if (issueEffect == DefaultIssueEffects.SettlementProsperity)
  			{
  				return -0.2f;
  			}
  			if (issueEffect == DefaultIssueEffects.SettlementLoyalty)
  			{
  		public override void AlternativeSolutionStartConsequence()
  		{
  			GiveGoldAction.ApplyForCharacterToParty(Hero.MainHero, base.IssueSettlement.Party, AlternativeSolutionNeededGold);
> 			TextObject textObject = new TextObject("{=ex6ZhAAv}You gave {DENAR}{GOLD_ICON} to companion to buy {GRAIN_AMOUNT} units of grain for the {ISSUE_OWNER.NAME}.");
> 			textObject.SetTextVariable("GRAIN_AMOUNT", NeededGrainAmount);
  			textObject.SetTextVariable("DENAR", AlternativeSolutionNeededGold);
  			textObject.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
  			StringHelpers.SetCharacterProperties("ISSUE_OWNER", base.IssueOwner.CharacterObject, textObject);
  			MBInformationManager.AddQuickInformation(textObject);
  		}
  
  		protected override void AlternativeSolutionEndWithSuccessConsequence()
  		{
  			TraitLevelingHelper.OnIssueSolvedThroughAlternativeSolution(base.IssueOwner, new Tuple<TraitObject, int>[1]
  			{
  				new Tuple<TraitObject, int>(DefaultTraits.Generosity, 30)
  			});
  		{
  			if (NearbySuitableSettlement != null)
  			{
> 				return NearbySuitableSettlement.Town.MarketData.GetItemCountOfCategory(DefaultItems.Grain.ItemCategory) < 350;
  			}
  			return false;
  		}
  
  		protected override void CompleteIssueWithTimedOutConsequences()
  		{
  		}
  
  		protected override void OnGameLoad()
  		{
  		}
  
  
  		protected override QuestBase GenerateIssueQuest(string questId)
  		{
> 			return new HeadmanNeedsGrainIssueQuest(questId, base.IssueOwner, CampaignTime.DaysFromNow(18f), base.IssueDifficultyMultiplier, RewardGold, NeededGrainAmount);
  		}
  
  		protected override bool CanPlayerTakeQuestConditions(Hero issueGiver, out PreconditionFlags flag, out Hero relationHero, out SkillObject skill, out int requiredGold)
  		{
  			skill = null;
  			relationHero = null;
  			requiredGold = 0;
  			flag = PreconditionFlags.None;
  			if (issueGiver.GetRelationWithPlayer() < -10f)
  			{
  				flag |= PreconditionFlags.Relation;
  				relationHero = issueGiver;
  		}
  	}
  
> 	public class HeadmanNeedsGrainIssueQuest : QuestBase
  	{
  		private const int SuccessRenownBonus = 1;
  
  		private const int SuccessMercyBonus = 70;
  
  		private const int SuccessGenerosityBonus = 50;
  
  		private const int SuccessRelationBonusWithQuestGiver = 5;
  
  		private const int SuccessRelationBonusWithOtherNotables = 1;
  
  		private const int SuccessPowerBonus = 10;
  		private const int TimeOutPowerPenalty = -5;
  
  		[SaveableField(10)]
> 		private readonly int _neededGrainAmount;
  
  		[SaveableField(20)]
  		private int _rewardGold;
  
  		[SaveableField(30)]
  		private JournalLog _playerAcceptedQuestLog;
  
  		[SaveableField(40)]
> 		private JournalLog _playerHasNeededGrainsLog;
  
  		public override TextObject Title
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=LPMXVHHT}{ISSUE_SETTLEMENT} Needs Grain Seeds");
  				textObject.SetTextVariable("ISSUE_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
  				return textObject;
  			}
  		}
  
  		public override bool IsRemainingTimeHidden => false;
  
  		private TextObject PlayerAcceptedQuestLogText
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=5CokRxmL}{QUEST_GIVER.LINK}, the headman of the {QUEST_SETTLEMENT} asked you to deliver {GRAIN_AMOUNT} units of grain to {?QUEST_GIVER.GENDER}her{?}him{\\?} to use as seeds. Otherwise peasants cannot sow their fields and starve in the coming season.{newline}{newline}You have agreed to bring them {GRAIN_AMOUNT} units of grain as soon as possible.");
  				StringHelpers.SetCharacterProperties("QUEST_GIVER", base.QuestGiver.CharacterObject, textObject);
  				textObject.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
> 				textObject.SetTextVariable("GRAIN_AMOUNT", _neededGrainAmount);
  				return textObject;
  			}
  		}
  
> 		private TextObject PlayerHasNeededGrainsLogText
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=vOHc5dxC}You now have enough grain seeds to complete the quest. Return to {QUEST_SETTLEMENT} to hand them over.");
  				textObject.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
  				return textObject;
  			}
  		}
  
  		private TextObject QuestTimeoutLogText
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=brDw7ewN}You have failed to deliver {GRAIN_AMOUNT} units of grain to the villagers. They won't be able to sow them before the coming winter. The Headman and the villagers are doomed.");
> 				textObject.SetTextVariable("GRAIN_AMOUNT", _neededGrainAmount);
  				return textObject;
  			}
  		}
  
  		private TextObject SuccessLog
  		{
  			get
  			{
> 				TextObject textObject = new TextObject("{=GGTxzAtn}You have delivered {GRAIN_AMOUNT} units of grain to the villagers. They will be able to sow them before the coming winter. You have saved a lot of lives today. The Headman and the villagers are grateful.");
> 				textObject.SetTextVariable("GRAIN_AMOUNT", _neededGrainAmount);
  				return textObject;
  			}
  		}
  
  		private TextObject CancelLogOnWarDeclared
  		{
  			get
  			{
  				TextObject textObject = new TextObject("{=8Z4vlcib}Your clan is now at war with the {ISSUE_GIVER.LINK}'s lord. Your agreement with {ISSUE_GIVER.LINK} was canceled.");
  				StringHelpers.SetCharacterProperties("ISSUE_GIVER", base.QuestGiver.CharacterObject, textObject);
  				return textObject;
  			}
  			}
  		}
  
> 		internal static void AutoGeneratedStaticCollectObjectsHeadmanNeedsGrainIssueQuest(object o, List<object> collectedObjects)
  		{
> 			((HeadmanNeedsGrainIssueQuest)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
  		}
  
  		protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
  		{
  			base.AutoGeneratedInstanceCollectObjects(collectedObjects);
  			collectedObjects.Add(_playerAcceptedQuestLog);
> 			collectedObjects.Add(_playerHasNeededGrainsLog);
  		}
  
> 		internal static object AutoGeneratedGetMemberValue_neededGrainAmount(object o)
  		{
> 			return ((HeadmanNeedsGrainIssueQuest)o)._neededGrainAmount;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_rewardGold(object o)
  		{
> 			return ((HeadmanNeedsGrainIssueQuest)o)._rewardGold;
  		}
  
  		internal static object AutoGeneratedGetMemberValue_playerAcceptedQuestLog(object o)
  		{
> 			return ((HeadmanNeedsGrainIssueQuest)o)._playerAcceptedQuestLog;
  		}
  
> 		internal static object AutoGeneratedGetMemberValue_playerHasNeededGrainsLog(object o)
  		{
> 			return ((HeadmanNeedsGrainIssueQuest)o)._playerHasNeededGrainsLog;
  		}
  
> 		public HeadmanNeedsGrainIssueQuest(string questId, Hero giverHero, CampaignTime duration, float difficultyMultiplier, int rewardGold, int neededGrainAmount)
  			: base(questId, giverHero, duration, rewardGold)
  		{
> 			_neededGrainAmount = neededGrainAmount;
  			_rewardGold = rewardGold;
  			SetDialogs();
  			InitializeQuestOnCreation();
  		}
  
  		protected override void InitializeQuestOnGameLoad()
  		{
  			SetDialogs();
  		}
  
  		protected override void HourlyTick()
  		{
  		{
  			if (mobileParty == MobileParty.MainParty)
  			{
> 				_playerAcceptedQuestLog.UpdateCurrentProgress(GetRequiredGrainCountOnPlayer());
> 				CheckIfPlayerReadyToReturnGrains();
  			}
  		}
  
  		private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
  		{
  			if (prisoner == Hero.MainHero)
  			{
> 				_playerAcceptedQuestLog.UpdateCurrentProgress(GetRequiredGrainCountOnPlayer());
> 				CheckIfPlayerReadyToReturnGrains();
  			}
  		}
  
  		private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
  		{
  			if (party == MobileParty.MainParty)
  			{
> 				_playerAcceptedQuestLog.UpdateCurrentProgress(GetRequiredGrainCountOnPlayer());
> 				CheckIfPlayerReadyToReturnGrains();
  			}
  		}
  
  		private void OnVillageBeingRaided(Village village)
  		{
  			bool flag = MobileParty.MainParty.MapEvent != null && MobileParty.MainParty.MapEvent.IsRaid && MobileParty.MainParty.CurrentSettlement == village.Settlement;
  			if (village == base.QuestGiver.CurrentSettlement.Village && !flag)
  			{
  				CompleteQuestWithCancel(CancelLogOnVillageRaided);
  			}
  		}
  
  
  		protected override void SetDialogs()
  		{
> 			TextObject textObject = new TextObject("{=nwIYsJRO}Have you brought our grain {?PLAYER.GENDER}milady{?}sir{\\?}?[if:convo_shocked][ib:demure2]");
  			TextObject textObject2 = new TextObject("{=zsE7ldPY}Thank you, {?PLAYER.GENDER}milady{?}sir{\\?}! You are a saviour.[if:convo_merry][ib:normal2]");
  			TextObject textObject3 = new TextObject("{=0tB3VGE4}We await your success, {?PLAYER.GENDER}milady{?}sir{\\?}.[if:convo_nervous]");
  			textObject.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
  			textObject2.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
  			textObject3.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
  			OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start").NpcLine(textObject2).Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
  				.Consequence(QuestAcceptedConsequences)
  				.CloseDialog();
  			DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss").NpcLine(textObject).Condition(delegate
  			{
> 				MBTextManager.SetTextVariable("GRAIN_AMOUNT", _neededGrainAmount);
  				return CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject;
  			})
  				.BeginPlayerOptions()
> 				.PlayerOption(new TextObject("{=9UABeRWO}Yes. Here is your grain."))
  				.ClickableCondition(CompleteQuestClickableConditions)
  				.NpcLine(textObject2)
  				.Consequence(delegate
  				{
  					Campaign.Current.ConversationManager.ConversationEndOneShot += Success;
  				})
  				.CloseDialog()
  				.PlayerOption(new TextObject("{=PI6ikMsc}I'm working on it."))
  				.NpcLine(textObject3)
  				.CloseDialog()
  				.EndPlayerOptions()
  				.CloseDialog();
  
  		private bool CompleteQuestClickableConditions(out TextObject explanation)
  		{
> 			if (_playerAcceptedQuestLog.CurrentProgress >= _neededGrainAmount)
  			{
  				explanation = null;
  				return true;
  			}
> 			explanation = new TextObject("{=mzabdwoh}You don't have enough grain.");
  			return false;
  		}
  
  		private void QuestAcceptedConsequences()
  		{
  			StartQuest();
> 			int requiredGrainCountOnPlayer = GetRequiredGrainCountOnPlayer();
> 			_playerAcceptedQuestLog = AddDiscreteLog(PlayerAcceptedQuestLogText, new TextObject("{=eEwI880g}Collect Grain"), requiredGrainCountOnPlayer, _neededGrainAmount);
  		}
  
> 		private int GetRequiredGrainCountOnPlayer()
  		{
> 			int itemNumber = PartyBase.MainParty.ItemRoster.GetItemNumber(DefaultItems.Grain);
> 			if (itemNumber <= _neededGrainAmount)
  			{
  				return itemNumber;
  			}
> 			return _neededGrainAmount;
  		}
  
> 		private void CheckIfPlayerReadyToReturnGrains()
  		{
> 			if (_playerHasNeededGrainsLog == null && _playerAcceptedQuestLog.CurrentProgress >= _neededGrainAmount)
  			{
> 				_playerHasNeededGrainsLog = AddLog(PlayerHasNeededGrainsLogText);
> 				TextObject textObject = new TextObject("{=Gtbfm10o}You have enough grain to complete the quest. Return to {QUEST_SETTLEMENT} to hand it over.");
  				textObject.SetTextVariable("QUEST_SETTLEMENT", base.QuestGiver.CurrentSettlement.Name);
  				MBInformationManager.AddQuickInformation(textObject);
  			}
> 			else if (_playerHasNeededGrainsLog != null && _playerAcceptedQuestLog.CurrentProgress < _neededGrainAmount)
  			{
> 				RemoveLog(_playerHasNeededGrainsLog);
> 				_playerHasNeededGrainsLog = null;
  			}
  		}
  
  		private void OnPlayerInventoryExchange(List<(ItemRosterElement, int)> purchasedItems, List<(ItemRosterElement, int)> soldItems, bool isTrading)
  		{
  			bool flag = false;
  			foreach (var (itemRosterElement, _) in purchasedItems)
  			{
> 				if (itemRosterElement.EquipmentElement.Item == DefaultItems.Grain)
  				{
  					flag = true;
  					break;
  				}
  			}
  			if (!flag)
  			{
  				foreach (var (itemRosterElement, _) in soldItems)
  				{
> 					if (itemRosterElement.EquipmentElement.Item == DefaultItems.Grain)
  					{
  						flag = true;
  						break;
  					}
  				}
  			}
  			if (flag)
  			{
> 				_playerAcceptedQuestLog.UpdateCurrentProgress(GetRequiredGrainCountOnPlayer());

codex
`grain`, `iron`, and `tools` are not present as literal item ids in the XML files I searched, but vanilla uses `DefaultItems.Grain`, so I’m verifying the generated `DefaultItems` object ids rather than relying only on XML grep.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'DefaultItems' | Select-Object -First 20 | ForEach-Object { "'$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.ObjectSystem.dll' | Select-String -Pattern 'DefaultItems' | Select-Object -First 20 | ForEach-Object { "'$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -l c 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Core.dll' | Select-String -Pattern 'DefaultItems' | Select-Object -First 20 | ForEach-Object { "'$_.Line }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\Native','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\StoryMode','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern 'id=\"grain\"|id=\"iron\"|id=\"tools\"' -CaseSensitive:"'$false | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 737ms:
Class TaleWorlds.CampaignSystem.DefaultItems

 succeeded in 736ms:
 succeeded in 766ms:
 succeeded in 1221ms:
codex
The `DefaultItems` type is in `TaleWorlds.CampaignSystem`, not Core. I’m decompiling its initializer to see whether `grain`/`iron`/`tools` are actual object ids or only categories/constants.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.DefaultItems 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Grain|Iron|Tools|Initialize|RegisterAll|GetObject|GetObject<ItemObject>|public static ItemObject' -Context 3,20 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.DefaultItemCategories 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'Grain|Iron|Tools|RegisterAll|GetObject|public static ItemCategory' -Context 3,14 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\Native','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBox','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore','Main\\_Module\\ModuleData' -Recurse -Filter *.xml -ErrorAction SilentlyContinue | Select-String -Pattern 'IronIngot|iron_ingot|tools|tool|grain' -CaseSensitive:"'$false | Select-Object -First 120 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\SandBoxCore\\ModuleData\\items' -Recurse -Filter *.xml | Select-String -Pattern 'name=\".*Grain|name=\".*Tools|name=\".*Iron' -CaseSensitive:"'$false | Select-Object -First 80 | ForEach-Object { "$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 558ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\brightness_calibration_screen.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\character_menu_a.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\character_menu.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\character_pose_atmosphere.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_aserai_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_aserai_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_aserai_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_aserai_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_aserai_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_battania_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_battania_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_battania_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_battania_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_battania_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_desert_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_desert_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_desert_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_empire_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_empire_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_empire_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_empire_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_empire_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_forest_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_forest_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_forest_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_khuzait_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_khuzait_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_khuzait_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_khuzait_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_khuzait_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_plains_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_plains_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_plains_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_snow_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_snow_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_snow_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_steppe_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_steppe_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_steppe_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_sturgia_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_sturgia_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_sturgia_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_sturgia_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_sturgia_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_vlandia_lordshall_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_vlandia_tavern_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_vlandia_town_night_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_vlandia_town_noon_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\conv_vlandia_town_sunset_0.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\crafting_thumbnail_atmo.xml:73:<value name="grain_amount" value="1.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\default_atmosphere.xml:73:<value name="grain_amount" value="1.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\default_fall.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\default_spring.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\default_summer.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\default_winter.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\editor_atmosphere.xml:73:<value name="grain_amount" value="1.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\editor_default.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\exposure_calibration_interior.xml:73:<value name="grain_amount" value="1.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\GG_atmosphere.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\ibl_prop_test.xml:73:<value name="grain_amount" value="1.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\ibl_shop.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\ibl_studio.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\interior.xml:73:<value name="grain_amount" value="40.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\inventory.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\item_scene_atmosphere.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\main_menu_a_new.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_01_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_02_30_night.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_04_30_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_06_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_10_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\MP_naval_11_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\night_clear.xml:73:<value name="grain_amount" value="20.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\prefab_atmosphere.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\scene_atmosphere.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\test.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_01_00_HeavyRain.xml:73:<value name="grain_amount" value="20.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_01_00_SemiCloudy.xml:73:<value name="grain_amount" value="20.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_02_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_03_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_04_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_05_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_06_00_Foggy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_06_00_Overcast.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_06_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_06_30_Cloudy_sunset2.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_07_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_08_00_Overcast.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_08_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_09_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_10_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_11_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_12_00_HeavyRain.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_12_00_Overcast.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_12_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_01_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_02_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_02_30_night.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_02_30_rainy_night.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_04_00_night.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_05_00_sunset.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_05_00_sunset2.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_06_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_06_00_Foggy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_06_00_sunset.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_06_30_cloudy_sunset2.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_07_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_08_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_08_00_Cloudy2.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_08_00_rain_storm.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_09_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_09_00_Cloudy2.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_09_00_rain.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_10_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_10_00_rain_storm.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_10_00_semi_cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_10_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_11_00_Cloudy.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_11_00_overcast.xml:73:<value name="grain_amount" value="0.000"/>
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\Atmospheres\TOD_photo_11_00_SemiCloudy.xml:73:<value name="grain_amount" value="0.000"/>

 succeeded in 570ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\banners.xml:905:name="{=7mCYbhbp}Iron Banner"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\banners.xml:1229:name="{=9SbsZJUZ}Banner of Iron Shields"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\body_armors.xml:3823:name="{=camFHYrN}Leather Armor with Iron Plates"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:4400:name="{=AQIH729X}Iron Nasalhelm over Leather"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:4653:name="{=nUarDXVH}Iron Roundkettle over Leather"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:4820:name="{=Cg7vqdPQ}Iron Nasalhelm over Coif"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:4860:name="{=DbavaIis}Iron Nasalhelm over Mail"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:5114:name="{=XuwQvSDo}Iron Roundkettle over Mail"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:5156:name="{=IX3kafcb}Iron Spiked Kettle over Mail"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:5263:name="{=cQ6IsHSL}Iron Nasalhelm over Padding"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\head_armors.xml:5285:name="{=twbUeZph}Iron Legionary Helm over Cloth"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shields.xml:132:name="{=RddFH0BA}Iron Rimmed Large Round Shield"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shields.xml:434:name="{=QodawBSN}Iron Round Shield"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shields.xml:673:name="{=vsI4M5oq}Iron Reinforced Highland Large Shield"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shields.xml:2722:name="{=dWr3nuBq}Reinforced Iron Rimmed Kite Shield"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shields.xml:2843:name="{=WpdmIlU5}Iron Rimmed Kite Shield"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\shoulder_armors.xml:1472:name="{=27GO9o9K}Iron Plate Pauldrons"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:181:name="{=oFEzCTpW}Iron Pitchfork"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2605:name="{=w9RLq82l}Iron Arming Sword"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2655:name="{=fHj6NbWy}Iron Flyssa"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2680:name="{=JPByibJQ}Iron Scimitar"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2705:name="{=QjdyYS0m}Iron Saber"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2755:name="{=habJjtmv}Ridged Iron Broadsword"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:2854:name="{=1CamO1P3}Iron Spatha"
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\items\weapons.xml:4372:name="{=DQaV0i5W}Iron Broadsword"

2026-06-20T18:16:09.268034Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.DefaultItemCategories in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 exited 1 in 768ms:
System.InvalidOperationException: Could not find type definition TaleWorlds.CampaignSystem.DefaultItemCategories in type system.
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileType(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1187
   at ICSharpCode.Decompiler.CSharp.CSharpDecompiler.DecompileTypeAsString(FullTypeName fullTypeName) in /_/ICSharpCode.Decompiler/CSharp/CSharpDecompiler.cs:line 1208
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.Decompile(String assemblyFileName, TextWriter output, String typeName) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 469
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<OnExecuteAsync>g__PerformPerFileAction|90_0(String fileName, <>c__DisplayClass90_0&, <>c__DisplayClass90_1&) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 318
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 239

 succeeded in 1038ms:
  
  	private const int TrashValue = 1;
  
> 	private ItemObject _itemGrain;
  
  	private ItemObject _itemPlanks;
  
  	private ItemObject _itemFelt;
  
  	private ItemObject _itemMeat;
  
  	private ItemObject _itemHides;
  
> 	private ItemObject _itemTools;
  
> 	private ItemObject _itemIronOre;
  
  	private ItemObject _itemHardwood;
  
  	private ItemObject _itemCharcoal;
  
> 	private ItemObject _itemIronIngot1;
  
> 	private ItemObject _itemIronIngot2;
  
> 	private ItemObject _itemIronIngot3;
  
> 	private ItemObject _itemIronIngot4;
  
> 	private ItemObject _itemIronIngot5;
  
> 	private ItemObject _itemIronIngot6;
  
  	private ItemObject _itemTrash;
  
  	private static DefaultItems Instance => Campaign.Current.DefaultItems;
  
> 	public static ItemObject Grain => Instance._itemGrain;
  
> 	public static ItemObject Planks => Instance._itemPlanks;
  
> 	public static ItemObject Felt => Instance._itemFelt;
  
> 	public static ItemObject Meat => Instance._itemMeat;
  
> 	public static ItemObject Hides => Instance._itemHides;
  
> 	public static ItemObject Tools => Instance._itemTools;
  
> 	public static ItemObject IronOre => Instance._itemIronOre;
  
> 	public static ItemObject HardWood => Instance._itemHardwood;
  
> 	public static ItemObject Charcoal => Instance._itemCharcoal;
  
> 	public static ItemObject IronIngot1 => Instance._itemIronIngot1;
  
> 	public static ItemObject IronIngot2 => Instance._itemIronIngot2;
  
> 	public static ItemObject IronIngot3 => Instance._itemIronIngot3;
  
> 	public static ItemObject IronIngot4 => Instance._itemIronIngot4;
  
> 	public static ItemObject IronIngot5 => Instance._itemIronIngot5;
  
> 	public static ItemObject IronIngot6 => Instance._itemIronIngot6;
  
> 	public static ItemObject Trash => Instance._itemTrash;
  
  	public DefaultItems()
  	{
> 		RegisterAll();
  	}
  
> 	private void RegisterAll()
  	{
> 		_itemGrain = Create("grain");
  		_itemFelt = Create("felt");
  		_itemPlanks = Create("planks");
  		_itemMeat = Create("meat");
  		_itemHides = Create("hides");
> 		_itemTools = Create("tools");
> 		_itemIronOre = Create("iron");
  		_itemHardwood = Create("hardwood");
  		_itemCharcoal = Create("charcoal");
> 		_itemIronIngot1 = Create("ironIngot1");
> 		_itemIronIngot2 = Create("ironIngot2");
> 		_itemIronIngot3 = Create("ironIngot3");
> 		_itemIronIngot4 = Create("ironIngot4");
> 		_itemIronIngot5 = Create("ironIngot5");
> 		_itemIronIngot6 = Create("ironIngot6");
  		_itemTrash = Create("trash");
> 		InitializeAll();
  	}
  
  	private ItemObject Create(string stringId)
  	{
  		return Game.Current.ObjectManager.RegisterPresumedObject(new ItemObject(stringId));
  	}
  
> 	private void InitializeAll()
  	{
> 		ItemObject.InitializeTradeGood(_itemGrain, new TextObject("{=Itv3fgJm}Grain{@Plural}loads of grain{\\@}"), "merchandise_grain", DefaultItemCategories.Grain, 10, 10f, ItemObject.ItemTypeEnum.Goods, isFood: true);
> 		ItemObject.InitializeTradeGood(_itemMeat, new TextObject("{=LmwhFv5p}Meat{@Plural}loads of meat{\\@}"), "merchandise_meat", DefaultItemCategories.Meat, 30, 10f, ItemObject.ItemTypeEnum.Goods, isFood: true);
> 		ItemObject.InitializeTradeGood(_itemPlanks, new TextObject("{=5ac8Boz1}Planks{@Plural}loads of planks{\\@}"), "bd_planks_a", DefaultItemCategories.Planks, 180, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemFelt, new TextObject("{=hNwjpCVP}Felt{@Plural}rolls of felt{\\@}"), "merchandise_hides_b", DefaultItemCategories.Felt, 230, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemHides, new TextObject("{=4kvKQuXM}Hides{@Plural}loads of hide{\\@}"), "merchandise_hides_b", DefaultItemCategories.Hides, 50, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemTools, new TextObject("{=n3cjEB0X}Tools{@Plural}loads of tools{\\@}"), "bd_pickaxe_b", DefaultItemCategories.Tools, 250, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronOre, new TextObject("{=Kw6BkhIf}Iron Ore{@Plural}loads of iron ore{\\@}"), "iron_ore", DefaultItemCategories.Iron, 50, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemHardwood, new TextObject("{=ExjMoUiT}Hardwood{@Plural}hardwood logs{\\@}"), "hardwood", DefaultItemCategories.Wood, 25, 10f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemCharcoal, new TextObject("{=iQadPYNe}Charcoal{@Plural}loads of charcoal{\\@}"), "charcoal", DefaultItemCategories.Wood, 50, 5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot1, new TextObject("{=gOpodlt1}Crude Iron{@Plural}loads of crude iron{\\@}"), "crude_iron", DefaultItemCategories.Iron, 20, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot2, new TextObject("{=7HvtT8bm}Wrought Iron{@Plural}loads of wrought iron{\\@}"), "wrought_iron", DefaultItemCategories.Iron, 30, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot3, new TextObject("{=XHmmbnbB}Iron{@Plural}loads of iron{\\@}"), "iron_a", DefaultItemCategories.Iron, 60, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot4, new TextObject("{=UfuLKuaI}Steel{@Plural}loads of steel{\\@}"), "steel", DefaultItemCategories.Iron, 100, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot5, new TextObject("{=azjMBa86}Fine Steel{@Plural}loads of fine steel{\\@}"), "fine_steel", DefaultItemCategories.Iron, 160, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemIronIngot6, new TextObject("{=vLVAfcta}Thamaskene Steel{@Plural}loads of thamaskene steel{\\@}"), "thamaskene_steel", DefaultItemCategories.Iron, 260, 0.5f, ItemObject.ItemTypeEnum.Goods);
> 		ItemObject.InitializeTradeGood(_itemTrash, new TextObject("{=ZvZN6UkU}Trash Item"), "iron_ore", DefaultItemCategories.Unassigned, 1, 1f, ItemObject.ItemTypeEnum.Goods);
  	}
  }

codex
`DefaultItems` confirms `grain`, `tools`, and `iron` are engine-registered presumed objects, so the XML absence is not a config failure. I’m now harvesting every `{=key}` reference from the config and three templates and comparing it to `taom_lotr_issue_strings.xml`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$stringXml=[xml](Get-Content Main'"\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Raw); "'$ids=@{}; foreach($s in $stringXml.strings.string){$ids[$s.id]=$true}; $paths=@('"'Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml','Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs'); "'$refs=@{}; foreach($p in $paths){ $lines=Get-Content $p; for($i=0;$i -lt $lines.Count;$i++){ foreach($m in [regex]::Matches($lines[$i], '"'\\{=(["'^}'"\\s]+)\\}')){ "'$k=$m.Groups[1].Value; if($k -eq '"'"'!'"'){continue}; if(-not "'$refs.ContainsKey($k)){$refs[$k]=@()}; $refs[$k]+=('"'{0}:{1}' -f "'$p,($i+1)) } } }; "refs=$($refs.Count) ids=$($ids.Count)"; $refs.Keys | Sort-Object | Where-Object {-not $ids.ContainsKey($_)} | ForEach-Object { "MISSING_KEY $_ <= $($refs[$_] -join '"',')\" }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$paths=@('"'Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml','Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs'); foreach("'$p in $paths){ $lines=Get-Content $p; for($i=0;$i -lt $lines.Count;$i++){ foreach($m in [regex]::Matches($lines[$i], '"'\\{=(["'^}'"\\s]+)\\}(["'^"<)]*)'"')){ "'$key=$m.Groups[1].Value; if($key -ne '"'"'!'"' -and [string]::IsNullOrEmpty("'$m.Groups[2].Value.Trim())){ "$p:$($i+1): no inline default for {$key}: $($lines[$i].Trim())" } } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$stringXml=[xml](Get-Content Main'"\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Raw); "'$ids=$stringXml.strings.string | ForEach-Object {$_.id}; $dups=$ids | Group-Object | Where-Object Count -gt 1; if($dups){$dups|ForEach-Object{"DUP_STRING $($_.Name)"}} else {'"'no duplicate string ids'}; "'$empty=$stringXml.strings.string | Where-Object {[string]::IsNullOrEmpty($_.text)}; if($empty){$empty|ForEach-Object{"EMPTY_STRING $($_.id)"}} else {'"'no empty string text'}" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main\\_Module\\SubModule.xml'; "'$lines=Get-Content $p; for($i=820;$i -le 834;$i++){ '"'{0}:{1}' -f "'$i,$lines[$i-1] }' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:16:32.534314Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . and [string]::IsNullOrEmpty($m.Groups[2].Value.Trim())){ "[36;1m$p:[0m$($i+1): .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                             ~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mVariable reference is not valid. ':' was not followed by a valid variable name character. Consider using ${} to[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1m[36;1m     | [31;1mdelimit the name.[0m

 exited 1 in 452ms:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . and [string]::IsNullOrEmpty($m.Groups[2].Value.Trim())){ "[36;1m$p:[0m$($i+1): .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                             ~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mVariable reference is not valid. ':' was not followed by a valid variable name character. Consider using ${} to[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1m[36;1m     | [31;1mdelimit the name.[0m

 succeeded in 460ms:
820:      <XmlName id="GameText" path="taom_messenger_strings"/>
821:      <IncludedGameTypes>
822:        <GameType value="Campaign"/>
823:        <GameType value="CampaignStoryMode"/>
824:        <GameType value="CustomGame"/>
825:        <GameType value="EditorGame"/>
826:      </IncludedGameTypes>
827:    </XmlNode>
828:    <!-- LOTR custom-issue player-facing text (LotrIssues feature) -->
829:    <XmlNode>
830:      <XmlName id="GameText" path="taom_lotr_issue_strings"/>
831:      <IncludedGameTypes>
832:        <GameType value="Campaign"/>
833:        <GameType value="CampaignStoryMode"/>
834:      </IncludedGameTypes>

 succeeded in 591ms:
refs=309 ids=308
MISSING_KEY KEY <= Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:6

 succeeded in 523ms:
no duplicate string ids
no empty string text

codex
The only missing localization “key” from the first harvest is the explanatory XML comment `{=KEY}default`, not runtime text. I’m rerunning the no-default check with that comment noise excluded.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$stringXml=[xml](Get-Content Main'"\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Raw); "'$ids=@{}; foreach($s in $stringXml.strings.string){$ids[$s.id]=$true}; $paths=@('"'Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml','Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs'); "'$refs=@{}; foreach($p in $paths){ $lines=Get-Content $p; $inXmlComment=$false; for($i=0;$i -lt $lines.Count;$i++){ $line=$lines[$i]; if($p.EndsWith('"'.xml')){ if("'$line -match '"'<"'!--'"'){"'$inXmlComment=$true}; $skip=$inXmlComment; if($line -match '"'-->'){"'$inXmlComment=$false}; if($skip){continue} }; foreach($m in [regex]::Matches($line, '"'\\{=(["'^}'"\\s]+)\\}')){ "'$k=$m.Groups[1].Value; if($k -eq '"'"'!'"'){continue}; if(-not "'$refs.ContainsKey($k)){$refs[$k]=@()}; $refs[$k]+=('"'{0}:{1}' -f "'$p,($i+1)) } } }; "runtime_refs=$($refs.Count) ids=$($ids.Count)"; $missing=$refs.Keys | Sort-Object | Where-Object {-not $ids.ContainsKey($_)}; if($missing){$missing|ForEach-Object{"MISSING_KEY $_ <= $($refs[$_] -join '"',')\"}} else {'no missing runtime keys'}" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$paths=@('"'Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml','Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs'); foreach("'$p in $paths){ $lines=Get-Content $p; $inXmlComment=$false; for($i=0;$i -lt $lines.Count;$i++){ $line=$lines[$i]; if($p.EndsWith('"'.xml')){ if("'$line -match '"'<"'!--'"'){"'$inXmlComment=$true}; $skip=$inXmlComment; if($line -match '"'-->'){"'$inXmlComment=$false}; if($skip){continue} }; foreach($m in [regex]::Matches($line, '"'\\{=(["'^}'"\\s]+)\\}(["'^"<)]*)'"')){ "'$key=$m.Groups[1].Value; $default=$m.Groups[2].Value.Trim(); if($key -ne '"'"'!'"' -and [string]::IsNullOrEmpty("'$default)){ '"'{0}:{1}: no inline default for {2}: {3}' -f "'$p,($i+1),$key,$line.Trim() } } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$stringXml=[xml](Get-Content Main'"\\_Module\\ModuleData\\taom_lotr_issue_strings.xml -Raw); "'$runtimeKeys=New-Object System.Collections.Generic.HashSet[string]; $paths=@('"'Main\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml','Main\\Features\\LotrIssues\\Templates\\DeliverGoodsLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\DeliverPersonnelLotrIssue.cs','Main\\Features\\LotrIssues\\Templates\\CombatLotrIssue.cs'); foreach("'$p in $paths){ $lines=Get-Content $p; $inXmlComment=$false; foreach($line in $lines){ if($p.EndsWith('"'.xml')){ if("'$line -match '"'<"'!--'"'){"'$inXmlComment=$true}; $skip=$inXmlComment; if($line -match '"'-->'){"'$inXmlComment=$false}; if($skip){continue} }; foreach($m in [regex]::Matches($line, '"'\\{=(["'^}'"\\s]+)\\}')){ "'$k=$m.Groups[1].Value; if($k -ne '"'"'!'"'){[void]"'$runtimeKeys.Add($k)} } } }; $unused=$stringXml.strings.string | Where-Object { -not $runtimeKeys.Contains($_.id) } | Select-Object -ExpandProperty id; "unused_count=$($unused.Count)"; $unused | Select-Object -First 20' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$xml=[xml](Get-Content Main'"\\_Module\\ModuleData\\lotr_issues\\taom_lotr_issues.xml -Raw); "'$rows=@($xml.LotrIssues.LotrIssue); $rows | Where-Object {$_.template -eq '"'Combat'} | ForEach-Object { \""'$($_.id):$($_.variant):title=$($_.title_key.Substring(0, [Math]::Min(45,$_.title_key.Length)))" }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 522ms:
 succeeded in 568ms:
runtime_refs=308 ids=308
no missing runtime keys

 succeeded in 539ms:
unused_count=0

 succeeded in 458ms:
lotr_nearby_bandit_base:DefeatRaids:title={=taom_lotr_issue_banditbase_title}A Lair Nea
lotr_merchant_outlaws:DefeatRaids:title={=taom_lotr_issue_outlaws_title}Outlaws on th
lotr_merchant_poachers:DefeatRaids:title={=taom_lotr_issue_poachers_title}A Gang of Po
lotr_captured_gang:DefeatRaids:title={=taom_lotr_issue_captured_title}My Men in Ch
lotr_extortion_deserters:DefeatRaids:title={=taom_lotr_issue_deserters_title}Deserters P
lotr_landlord_commons:DefeatRaids:title={=taom_lotr_issue_commons_title}Trouble at th
lotr_rival_gang:DefeatRaids:title={=taom_lotr_issue_rivalgang_title}A Rival Mov
lotr_spy_party:DefeatRaids:title={=taom_lotr_issue_spy_title}An Enemy Spy
lotr_lesser_noble_revolt:DefeatRaids:title={=taom_lotr_issue_revolt_title}A Renegade Nob
lotr_company_trouble:DefeatRaids:title={=taom_lotr_issue_company_title}A Company of 
lotr_prodigal_son:DefeatRaids:title={=taom_lotr_issue_prodigal_title}A Kinsman in
lotr_rival_captured:CaptureLords:title={=taom_lotr_issue_rival_title}A Hated Rival
lotr_caravan_ambush:DefeatRaids:title={=taom_lotr_issue_caravanambush_title}An Ambu
lotr_escort_caravan:DefeatRaids:title={=taom_lotr_issue_escortcaravan_title}Clear t
lotr_snare_wealthy:DefeatRaids:title={=taom_lotr_issue_snare_title}A Trap for the 
lotr_smugglers:DefeatRaids:title={=taom_lotr_issue_smugglers_title}Breaking th
lotr_gang_needs_weapons:DefeatRaids:title={=taom_lotr_issue_gangweapons_title}Arms for 
lotr_garrison_troops:DefeatRaids:title={=taom_lotr_issue_garrison_title}Holding the 
lotr_conquest:DefeatRaids:title={=taom_lotr_issue_conquest_title}Breaking the
lotr_raid_territory:DefeatRaids:title={=taom_lotr_issue_raid_title}Carrying the War
lotr_scout_garrisons:DefeatRaids:title={=taom_lotr_issue_scout_title}Eyes on the Ene
lotr_ladys_knight:WinTournaments:title={=taom_lotr_issue_knight_title}A Champion's F
lotr_landlord_training:DefeatRaids:title={=taom_lotr_issue_training_title}Blooding the
lotr_inn_and_out:DefeatRaids:title={=taom_lotr_issue_inn_title}Swindled and Sore
lotr_family_feud:DefeatRaids:title={=taom_lotr_issue_feud_title}An Old Feud
lotr_daughter_found:DefeatRaids:title={=taom_lotr_issue_daughter_title}A Daughter T
lotr_betting_fraud:WinTournaments:title={=taom_lotr_issue_betting_title}A Sure Wager

codex
There is a stronger per-type consequence than the docs currently state: vanilla `IssueBase.CheckPreconditions` blocks taking another active quest of the same issue type unless `IssueQuestCanBeDuplicated` is overridden. I’m verifying whether the TAOM generic issue classes override that gate; if not, this becomes a real finding under suspect 9.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\LotrIssues -Recurse -Filter *.cs | Select-String -Pattern 'IssueQuestCanBeDuplicated|TemplateType|typeof\\(DeliverGoodsLotrIssue\\)|typeof\\(CombatLotrIssue\\)|typeof\\(DeliverPersonnelLotrIssue\\)' | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'IssueQuestCanBeDuplicated' -SimpleMatch | ForEach-Object { \""'$($_.Path):$($_.LineNumber):$($_.Line.Trim())" }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.Issues.IssueBase 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'IssueQuestCanBeDuplicated|other, similar commitments|CheckPreconditions|value.GetType\\(\\) == GetType' -Context 4,32 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll' | Select-String -Pattern 'CalculateIssueScoreForNotable|CalculateIssueScoreForClan|CalculateIssueScoreInternal|GetType\\(\\) == pid.IssueType|totalDesiredIssueCount|issue.Value.GetType' -Context 4,36 | ForEach-Object { "'$_.ToString() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 661ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:30:AddClassDefinition(typeof(DeliverGoodsLotrIssue), 101);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:33:AddClassDefinition(typeof(DeliverPersonnelLotrIssue), 103);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssueSaveableTypeDefiner.cs:36:AddClassDefinition(typeof(CombatLotrIssue), 105);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs:44:var type = TemplateType(def.Template);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs:68:private static Type TemplateType(LotrIssueTemplate t)
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs:72:case LotrIssueTemplate.DeliverGoods: return typeof(DeliverGoodsLotrIssue);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs:73:case LotrIssueTemplate.DeliverPersonnel: return typeof(DeliverPersonnelLotrIssue);
C:\Users\mikew\source\repos\TAOM\Main\Features\LotrIssues\LotrIssuesCampaignBehavior.cs:74:case LotrIssueTemplate.Combat: return typeof(CombatLotrIssue);

 succeeded in 1721ms:
  
  	[SaveableField(31)]
  	private readonly List<ITrackableCampaignObject> _trackedObjects = new List<ITrackableCampaignObject>();
  
> 	protected virtual bool IssueQuestCanBeDuplicated => false;
  
  	public virtual int RelationshipChangeWithIssueOwner { get; protected set; }
  
  	public abstract TextObject IssueBriefByIssueGiver { get; }
  
  	public abstract TextObject IssueAcceptByPlayer { get; }
  
  	public virtual TextObject IssuePlayerResponseAfterLordExplanation => new TextObject("{=sMCN7eCp}Is there any other way to solve this problem?");
  
  	public virtual TextObject IssuePlayerResponseAfterAlternativeExplanation => new TextObject("{=yrPEqZEa}Any other way?");
  
  	public abstract TextObject IssueQuestSolutionExplanationByIssueGiver { get; }
  
  	public virtual TextObject IssueAlternativeSolutionExplanationByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionExplanationByIssueGiver => TextObject.GetEmpty();
  
  	public abstract TextObject IssueQuestSolutionAcceptByPlayer { get; }
  
  	public virtual TextObject IssueAlternativeSolutionAcceptByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueAlternativeSolutionResponseByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionAcceptByPlayer => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionResponseByIssueGiver => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferBriefByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferExplanationByOtherNpc => TextObject.GetEmpty();
  
  	public virtual TextObject IssueLordSolutionCounterOfferAcceptByPlayer => TextObject.GetEmpty();
  		LordSolutionConsequenceWithAcceptCounterOffer();
  		CompleteIssueWithBetrayal();
  	}
  
> 	internal bool CheckPreconditions(Hero issueGiver, out TextObject explanation)
  	{
  		explanation = new TextObject("{=!}{EXPLANATION}");
  		if (!IssueStayAliveConditions() && IsOngoingWithoutQuest)
  		{
  			CompleteIssueWithCancel();
  			return false;
  		}
  		PreconditionFlags flag;
  		Hero relationHero;
  		SkillObject skill;
  		int requiredGold;
  		bool result = CanPlayerTakeQuestConditions(issueGiver, out flag, out relationHero, out skill, out requiredGold);
  		bool flag2 = false;
> 		if (!IssueQuestCanBeDuplicated)
  		{
  			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  			{
  				IssueBase value = issue.Value;
> 				if ((value.IsSolvingWithQuest || value.IsSolvingWithAlternative) && value.GetType() == GetType())
  				{
  					flag2 = true;
  					result = false;
  				}
  			}
  		}
  		if ((flag & PreconditionFlags.AtWar) == PreconditionFlags.AtWar)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=21dlZJt6}I don't wish to speak about that. As you know, our factions are at war."));
  		}
  		else if (flag2)
  		{
> 			explanation.SetTextVariable("EXPLANATION", new TextObject("{=HvY7wjHt}I don't think you can help me. I think you may have other, similar commitments that could interfere."));
  		}
  		else if ((flag & PreconditionFlags.NotInSameFaction) == PreconditionFlags.NotInSameFaction)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=rBPI2dvX}I don't need the service of strangers. I work only with lords of the realm and loyal mercenaries.[ib:closed][if:convo_grave]"));
  		}
  		else if ((flag & PreconditionFlags.MainHeroIsKingdomLeader) == PreconditionFlags.MainHeroIsKingdomLeader || (flag & PreconditionFlags.PlayerIsOwnerOfSettlement) == PreconditionFlags.PlayerIsOwnerOfSettlement)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=dYJKy2mO}Thank you for asking my {?PLAYER.GENDER}lady{?}lord{\\?}, but I can't bother you with such an unimportant issue."));
  		}
  		else if ((flag & PreconditionFlags.ClanTier) == PreconditionFlags.ClanTier)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=QOiPDGbf}I have never heard of your clan. I am not sure if I can rely on you or not.[ib:closed][if:convo_grave]"));
  		}
  		else if ((flag & PreconditionFlags.Renown) == PreconditionFlags.Renown)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=7uJcPQnc}I don't think you can help me. I'm looking for someone with a bit more, shall we say, renown..."));
  		}
  		else if ((flag & PreconditionFlags.Relation) == PreconditionFlags.Relation)
  		{
  			TextObject textObject;
  			if (issueGiver == relationHero)
  			{
  				textObject = new TextObject("{=Cn4lnECZ}You and I do not have a good history... I don't trust you.[ib:closed][if:convo_grave]");
  			}
  			else
  			{
  				textObject = new TextObject("{=5ZJMa7Om}I don't think you can help me. I've heard you have a history with {HERO.LINK}, and, well, that could complicate things...[ib:closed][if:convo_grave]");
  				StringHelpers.SetCharacterProperties("HERO", relationHero.CharacterObject, textObject);
  			}
  			explanation.SetTextVariable("EXPLANATION", textObject);
  		}
  		else if ((flag & PreconditionFlags.Skill) == PreconditionFlags.Skill)

 succeeded in 1726ms:
  		Settlement[] array2 = Town.AllTowns.Select((Town x) => x.Settlement).ToArray();
  		int num2 = TaleWorlds.Library.MathF.Ceiling(0.8f * (float)array2.Length);
  		int num3 = Hero.AllAliveHeroes.Count((Hero x) => x.IsLord && x.Clan != null && !x.Clan.IsBanditFaction && !x.IsChild);
  		int num4 = TaleWorlds.Library.MathF.Ceiling(0.120000005f * (float)num3);
> 		int totalDesiredIssueCount = num + num2 + num4;
  		Campaign.Current.ConversationManager.DisableSentenceSort();
  		_additionalFrequencyScore = -0.4f;
  		array.Shuffle();
> 		CreateRandomSettlementIssues(array, 2, num, totalDesiredIssueCount);
  		array2.Shuffle();
> 		CreateRandomSettlementIssues(array2, 3, num2, totalDesiredIssueCount);
  		Clan[] array3 = Clan.NonBanditFactions.Where((Clan x) => x.Heroes.Count != 0).ToArray();
  		array3.Shuffle();
> 		CreateRandomClanIssues(array3, num4, totalDesiredIssueCount);
  		_additionalFrequencyScore = 0.2f;
  		Campaign.Current.ConversationManager.EnableSentenceSort();
  	}
  
  	private void DailyTickClan(Clan clan)
  	{
  		if (!IsClanSuitableForIssueCreation(clan))
  		{
  			return;
  		}
  		int num = 0;
  		int num2 = 0;
  		for (int i = 0; i < clan.Heroes.Count; i++)
  		{
  			Hero hero = clan.Heroes[i];
  			if (hero.Issue != null)
  			{
  				num++;
  			}
  			if (hero.IsAlive && !hero.IsChild && hero.IsLord)
  			{
  				num2++;
  			}
  		}
  		int num3 = TaleWorlds.Library.MathF.Ceiling((float)num2 * 0.1f);
  		int num4 = TaleWorlds.Library.MathF.Floor((float)num2 * 0.2f);
  		if (num4 <= 0 || num >= num4 || (num >= num3 && !(MBRandom.RandomFloat < GetIssueGenerationChance(num, num4))))
  		{
  			return;
  		}
  		int num5 = 0;
  		foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  		{
  			if (!issue.Value.IsTriedToSolveBefore)
  			{
  				num5++;
  		float num = 1f - currentIssueCount / (float)maxIssueCount;
  		return 0.3f * num * num;
  	}
  
> 	private void CreateRandomSettlementIssues(Settlement[] shuffledSettlementArray, int maxIssueCountPerSettlement, int desiredIssueCount, int totalDesiredIssueCount)
  	{
  		int num = shuffledSettlementArray.Length;
  		int[] array = new int[num];
  		int num2 = 0;
  		int num3 = 0;
  		int num4 = 0;
  		int num5 = 0;
  		while (num2 < num && num4 < desiredIssueCount)
  		{
  			int num6 = (num4 + num2 + num3) % num;
  			if (array[num6] < num5)
  			{
  				num3++;
  			}
> 			else if (array[num6] < maxIssueCountPerSettlement && CreateAnIssueForSettlementNotables(shuffledSettlementArray[num6], totalDesiredIssueCount))
  			{
  				num4++;
  				array[num6]++;
  			}
  			else
  			{
  				num2++;
  			}
  		}
  	}
  
> 	private void CreateRandomClanIssues(Clan[] shuffledClanArray, int desiredIssueCount, int totalDesiredIssueCount)
  	{
  		int num = shuffledClanArray.Length;
  		int num2 = 0;
  		int num3 = 0;
  		while (num2 < num && num3 < desiredIssueCount)
  		{
> 			if (CreateAnIssueForClanNobles(shuffledClanArray[(num3 + num2) % num], totalDesiredIssueCount))
  			{
  				num3++;
  			}
  			else
  			{
  				num2++;
  			}
  		}
  	}
  
> 	private bool CreateAnIssueForSettlementNotables(Settlement settlement, int totalDesiredIssueCount)
  	{
  		IssueManager issueManager = Campaign.Current.IssueManager;
  		foreach (Hero notable in settlement.Notables)
  		{
  			if (notable.Issue != null || !notable.CanHaveCampaignIssues())
  			{
  				continue;
  			}
  			List<PotentialIssueData> list = Campaign.Current.IssueManager.CheckForIssues(notable);
  			int totalFrequencyScore = list.SumQ((PotentialIssueData x) => GetFrequencyScore(x.Frequency));
  			foreach (PotentialIssueData item in list)
  			{
  				PotentialIssueData pid = item;
  				if (pid.IsValid)
  				{
> 					float num = CalculateIssueScoreForNotable(in pid, settlement, totalDesiredIssueCount, totalFrequencyScore);
  					if (num > 0f && !issueManager.HasIssueCoolDown(pid.IssueType, notable))
  					{
  						_cachedIssueDataList.Add(new IssueData(pid, notable, num));
  					}
  				}
  			}
  		}
  		if (_cachedIssueDataList.Count > 0)
  		{
  			List<(IssueData, float)> list2 = new List<(IssueData, float)>();
  			foreach (IssueData cachedIssueData in _cachedIssueDataList)
  			{
  				list2.Add((cachedIssueData, cachedIssueData.Score));
  			}
  			IssueData issueData = MBRandom.ChooseWeighted(list2);
  			Campaign.Current.IssueManager.CreateNewIssue(in issueData.PotentialIssueData, issueData.Hero);
  			_cachedIssueDataList.Clear();
  			return true;
  		}
  		_cachedIssueDataList.Clear();
  		return false;
  	}
  
> 	private bool CreateAnIssueForClanNobles(Clan clan, int totalDesiredIssueCount)
  	{
  		IssueData? issueData = null;
  		float num = 0f;
  		IssueManager issueManager = Campaign.Current.IssueManager;
  		foreach (Hero aliveLord in clan.AliveLords)
  		{
  			if (aliveLord.Clan == Clan.PlayerClan || !aliveLord.CanHaveCampaignIssues() || !(aliveLord.Age >= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge) || (!aliveLord.IsActive && !aliveLord.IsPrisoner) || aliveLord.Issue != null)
  			{
  				continue;
  			}
  			List<PotentialIssueData> list = Campaign.Current.IssueManager.CheckForIssues(aliveLord);
  			int totalFrequencyScore = list.SumQ((PotentialIssueData x) => GetFrequencyScore(x.Frequency));
  			foreach (PotentialIssueData item in list)
  			{
  				PotentialIssueData pid = item;
  				if (pid.IsValid)
  				{
> 					float num2 = CalculateIssueScoreForClan(in pid, clan, totalDesiredIssueCount, totalFrequencyScore);
  					if (num2 > num && !issueManager.HasIssueCoolDown(pid.IssueType, aliveLord))
  					{
  						issueData = new IssueData(pid, aliveLord, num2);
  						num = num2;
  					}
  				}
  			}
  		}
  		if (issueData.HasValue)
  		{
  			IssueManager issueManager2 = Campaign.Current.IssueManager;
  			IssueData value = issueData.Value;
  			issueManager2.CreateNewIssue(in value.PotentialIssueData, issueData.Value.Hero);
  			return true;
  		}
  		return false;
  	}
  
> 	private float CalculateIssueScoreForClan(in PotentialIssueData pid, Clan clan, int totalDesiredIssueCount, int totalFrequencyScore)
  	{
  		foreach (Hero hero in clan.Heroes)
  		{
> 			if (hero.Issue != null && hero.Issue.GetType() == pid.IssueType)
  			{
  				return 0f;
  			}
  		}
> 		return CalculateIssueScoreInternal(in pid, totalDesiredIssueCount, totalFrequencyScore);
  	}
  
> 	private float CalculateIssueScoreForNotable(in PotentialIssueData pid, Settlement settlement, int totalDesiredIssueCount, int totalFrequencyScore)
  	{
  		foreach (Hero notable in settlement.Notables)
  		{
> 			if (notable.Issue != null && notable.Issue.GetType() == pid.IssueType)
  			{
  				return 0f;
  			}
  		}
> 		return CalculateIssueScoreInternal(in pid, totalDesiredIssueCount, totalFrequencyScore);
  	}
  
> 	private float CalculateIssueScoreInternal(in PotentialIssueData pid, int totalDesiredIssueCount, int totalFrequencyScore)
  	{
  		float num = (float)GetFrequencyScore(pid.Frequency) / (float)totalFrequencyScore;
  		float num2;
> 		if (totalDesiredIssueCount == 0)
  		{
  			num2 = 1f;
  		}
  		else
  		{
  			int num3 = 0;
  			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  			{
> 				if (issue.Value.GetType() == pid.IssueType)
  				{
  					num3++;
  				}
  			}
> 			num2 = (float)num3 / (float)totalDesiredIssueCount;
  		}
  		float num4 = 1f + _additionalFrequencyScore - num2 / num;
  		if (num4 < 0f)
  		{
  			num4 = 0f;
  		}
  		else if (num4 < _additionalFrequencyScore)
  		{
  			num4 *= 0.01f;
  		}
  		else if (num4 < _additionalFrequencyScore + 0.4f)
  		{
  			num4 *= 0.1f;
  		}
  		return num * num4;
  	}
  
  	private int GetFrequencyScore(IssueBase.IssueFrequency frequency)
  	{
  		int result = 0;
  		switch (frequency)
  		{
  		case IssueBase.IssueFrequency.VeryCommon:
  			result = 6;
  			break;
  		case IssueBase.IssueFrequency.Common:
  			result = 3;
  			break;
  		case IssueBase.IssueFrequency.Rare:
  			result = 1;
  			break;
  		}
  		return result;
  	}
  
  	private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)

 succeeded in 14866ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsSpecialWeaponsIssueBehavior.cs:27:protected override bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:54:protected override bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\IssueBase.cs:124:protected virtual bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\IssueBase.cs:907:if (!IssueQuestCanBeDuplicated)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\LandlordTrainingForRetainersIssueBehavior.cs:36:protected override bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\NearbyBanditBaseIssueBehavior.cs:176:protected override bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\SmugglersIssueBehavior.cs:55:protected override bool IssueQuestCanBeDuplicated => false;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\VillageNeedsCraftingMaterialsIssueBehavior.cs:42:protected override bool IssueQuestCanBeDuplicated => false;

context compacted
codex
I have the feature and engine evidence loaded; I’m doing one final line-number pass so the review can point at exact code locations rather than paraphrases.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$patterns = @('"'TemplateType','return typeof\\(CombatLotrIssue\\)','return typeof\\(DeliverGoodsLotrIssue\\)','return new LotrIssueDefinition','Attribute\\(\"variant\"','IsCaptureLords','IsWinTournaments','RegisterEvents','IssueQuestCanBeDuplicated','class CombatLotrIssue','class DeliverGoodsLotrIssue','class DeliverPersonnelLotrIssue');
"'$files = @(
'"'Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs',
'Main/Features/LotrIssues/LotrIssueConfigProvider.cs',
'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs',
'Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs',
'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs',
'docs/features/lotr-issues.md'
);
foreach ("'$f in $files) {
  if (Test-Path $f) {
    $i=0; Get-Content $f | ForEach-Object { $i++; foreach ($p in $patterns) { if ($_ -match $p) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ ; break } } }
  }
}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 677ms:
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:29:     public override void RegisterEvents()
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:44:             var type = TemplateType(def.Template);
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:68:     private static Type TemplateType(LotrIssueTemplate t)
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72:             case LotrIssueTemplate.DeliverGoods: return typeof(DeliverGoodsLotrIssue);
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:74:             case LotrIssueTemplate.Combat: return typeof(CombatLotrIssue);
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:167:         return new LotrIssueDefinition(
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170:             el.Attribute("reward_item")?.Value ?? "", el.Attribute("variant")?.Value ?? "", relationMin, text);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:27: public class CombatLotrIssue : IssueBase
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:137: public class CombatLotrIssueQuest : QuestBase
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:152:     private bool IsCaptureLords => string.Equals(_variant, "CaptureLords", StringComparison.OrdinalIgnoreCase);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:153:     private bool IsWinTournaments => string.Equals(_variant, "WinTournaments", StringComparison.OrdinalIgnoreCase);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:192:             var t = new TextObject(IsCaptureLords
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:194:                 : IsWinTournaments
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:210:     protected override void RegisterEvents()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:212:         if (IsCaptureLords)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:214:         else if (IsWinTournaments)
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:28: public class DeliverGoodsLotrIssue : IssueBase
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:160: public class DeliverGoodsLotrIssueQuest : QuestBase
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:252:     protected override void RegisterEvents()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:26: public class DeliverPersonnelLotrIssue : IssueBase
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:138: public class DeliverPersonnelLotrIssueQuest : QuestBase
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:213:     protected override void RegisterEvents()

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files = @('"'Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs','Main/Features/LotrIssues/Templates/CombatLotrIssue.cs','Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs','Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs','docs/features/lotr-issues.md');
foreach ("'$f in $files) {
  Write-Output "--- $f";
  $start=0; $end=0;
  switch ($f) {
    '"'Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs' { "'$start=68; $end=75 }
    '"'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs' { "'$start=210; $end=225 }
    '"'Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs' { "'$start=24; $end=34 }
    '"'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs' { "'$start=22; $end=32 }
    '"'docs/features/lotr-issues.md' { "'$start=305; $end=309 }
  }
  $i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge $start -and $i -le $end) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }
}' in C:\Users\mikew\source\repos\TAOM
 succeeded in 544ms:
--- Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:68:     private static Type TemplateType(LotrIssueTemplate t)
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:69:     {
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:70:         switch (t)
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:71:         {
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72:             case LotrIssueTemplate.DeliverGoods: return typeof(DeliverGoodsLotrIssue);
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:73:             case LotrIssueTemplate.DeliverPersonnel: return typeof(DeliverPersonnelLotrIssue);
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:74:             case LotrIssueTemplate.Combat: return typeof(CombatLotrIssue);
Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:75:             default: return null;
--- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:210:     protected override void RegisterEvents()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:211:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:212:         if (IsCaptureLords)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:213:             CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:214:         else if (IsWinTournaments)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:215:             CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:216:         else
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:217:             CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:218:         CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:219:         CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:220:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:221: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:222:     private void OnPlayerBattleEnd(MapEvent mapEvent)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:223:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:224:         if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None || mapEvent.WinningSide != mapEvent.PlayerSide) return;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:225:         Bump();
--- Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:24: /// (e.g. HeadmanNeedsGrain), driven by a <see cref="LotrIssueDefinition"/> carried in by the behavior's
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:25: /// OnSelected closure. Entry-point layer (ADR-002): engine plumbing lives here; the pure decisions
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:26: /// (count/reward scaling, eligibility, reward grant) delegate to <see cref="ILotrIssueService"/>.
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:27: /// </summary>
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:28: public class DeliverGoodsLotrIssue : IssueBase
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:29: {
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:30:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:31: 
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:32:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:33:     private ILotrIssueService _service;
Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs:34:     private IModLogger _logger;
--- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:22: /// recruits, or a landlord wanting forced mine labor). Same offer/turn-in shape as DeliverGoods, but the
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:23: /// objective tracks bandit prisoners in the player's <c>PrisonRoster</c> rather than an item. Entry-point
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:24: /// layer (ADR-002) — count/reward/eligibility decisions delegate to <see cref="ILotrIssueService"/>.
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:25: /// </summary>
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:26: public class DeliverPersonnelLotrIssue : IssueBase
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:27: {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:28:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:29: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:30:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:31:     private ILotrIssueService _service;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:32:     private IModLogger _logger;
--- docs/features/lotr-issues.md
docs/features/lotr-issues.md:305: **Known limitation — per-type spawn saturation (accepted v1 trade-off; this doc's Risk #5):** all 27 Combat configs
docs/features/lotr-issues.md:306: share `typeof(CombatLotrIssue)` (and all 14 Deliver share `typeof(DeliverGoodsLotrIssue)`), so Bannerlord's
docs/features/lotr-issues.md:307: per-issue-**type** over-representation throttle + per-settlement "already has an issue of this type" zero-out treat
docs/features/lotr-issues.md:308: each template as a single bucket. Net effect: issues spawn and rotate, but the world hosts fewer simultaneous LOTR
docs/features/lotr-issues.md:309: issues than vanilla's 43 distinct types would, and rare Combat variants surface infrequently. A true per-config type

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/LotrIssueConfigProvider.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 150 -and $i -le 172) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 487ms:
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:150:             _logger.LogWarning($"LotrIssues: skipping issue '{id}' — DeliverGoods requires an 'item:<id>' source (got '{el.Attribute("item_source")?.Value}'); 'category:' is not implemented yet.");
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:151:             return null;
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:152:         }
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:153: 
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:154:         var cultures = ParseCultures(el.Attribute("cultures")?.Value);
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:155: 
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:156:         var text = new LotrIssueText(
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:157:             titleKey,
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:158:             descKey,
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:159:             el.Attribute("brief_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:160:             el.Attribute("accept_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:161:             el.Attribute("explanation_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:162:             el.Attribute("solution_accept_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:163:             el.Attribute("task_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:164:             el.Attribute("success_key")?.Value ?? "",
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:165:             el.Attribute("fail_key")?.Value ?? "");
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:166: 
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:167:         return new LotrIssueDefinition(
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:168:             id, template, giver, frequency, cultures, count, countPerDiff,
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:169:             itemSource, troopSource, rewardGoldBase, rewardGoldPerDiff, rewardRenown,
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170:             el.Attribute("reward_item")?.Value ?? "", el.Attribute("variant")?.Value ?? "", relationMin, text);
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:171:     }
Main/Features/LotrIssues/LotrIssueConfigProvider.cs:172: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'docs/features/lotr-issues.md'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 276 -and $i -le 282) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 494ms:
docs/features/lotr-issues.md:276: | Template (`IssueBase` + paired `QuestBase`) | Mechanic | Issues |
docs/features/lotr-issues.md:277: |---|---|---|
docs/features/lotr-issues.md:278: | **DeliverGoods** | accumulate N of an `item:<id>` trade good, hand in via dialog | 14 (grain/supplies/draught/crafting/tools/horses/herd/artisan×2/offload/revenue/art-of-trade/tutor/special-weapons) |
docs/features/lotr-issues.md:279: | **DeliverPersonnel** | hand over N bandit prisoners from the player's `PrisonRoster` | 2 (gang recruits, mine laborers) |
docs/features/lotr-issues.md:280: | **Combat** (`variant=`) | event-driven count, auto-completes on N (no turn-in) | 27 — `DefeatRaids` (24, won battles), `CaptureLords` (1, at-war lord taken prisoner), `WinTournaments` (2, tournament won) |
docs/features/lotr-issues.md:281: 
docs/features/lotr-issues.md:282: **Why 3, not 8:** the "Escort-a-moving-party", "EconomicGather", "ConquestMilitary", and "SocialMisc/CraftItem"

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/_Module/SubModule.xml'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 828 -and $i -le 834) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 517ms:
Main/_Module/SubModule.xml:828:     <!-- LOTR custom-issue player-facing text (LotrIssues feature) -->
Main/_Module/SubModule.xml:829:     <XmlNode>
Main/_Module/SubModule.xml:830:       <XmlName id="GameText" path="taom_lotr_issue_strings"/>
Main/_Module/SubModule.xml:831:       <IncludedGameTypes>
Main/_Module/SubModule.xml:832:         <GameType value="Campaign"/>
Main/_Module/SubModule.xml:833:         <GameType value="CampaignStoryMode"/>
Main/_Module/SubModule.xml:834:       </IncludedGameTypes>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 1 -and $i -le 40) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 501ms:
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:1: using TaleWorlds.SaveSystem;
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:2: using TAOM.Features.LotrIssues.Templates;
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:3: 
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:4: namespace TAOM.Features.LotrIssues;
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:5: 
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:6: /// <summary>
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:7: /// Registers the LOTR issue/quest template classes with the TaleWorlds save system. Auto-discovered by
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:8: /// the engine (no manual registration). BaseId 726900801 — TAOM-unique, next in the 7269008xx series
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:9: /// after CareerQuest (726900701), FormationPreset (726900601), EquipPresets (726900501).
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:10: /// </summary>
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:11: /// <remarks>
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:12: /// The engine global type id is <c>_saveBaseId + localId</c>. TAOM's definer bases step by 100, so the
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:13: /// per-class localId MUST start at 101 — that lands the id in the base+100 century block (726900902+),
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:14: /// clear of CareerQuest's 726900802. localId 1 would yield 726900802 and collide → "An item with the
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:15: /// same key has already been added" at Module.Initialize.
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:16: ///
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:17: /// One (Issue, Quest) pair per template. Wave 0 ships T1 (DeliverGoods) at 101/102. Each later wave
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:18: /// appends its pair at the next free localId (103/104, …) — additive, never renumbered, so older saves
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:19: /// keep loading.
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:20: /// </remarks>
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:21: public sealed class LotrIssueSaveableTypeDefiner : SaveableTypeDefiner
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:22: {
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:23:     private const int SaveBaseId = 726900801;
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:24: 
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:25:     public LotrIssueSaveableTypeDefiner() : base(SaveBaseId) { }
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:26: 
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:27:     protected override void DefineClassTypes()
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:28:     {
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:29:         // T1 — DeliverGoods (Wave 0)
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:30:         AddClassDefinition(typeof(DeliverGoodsLotrIssue), 101);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:31:         AddClassDefinition(typeof(DeliverGoodsLotrIssueQuest), 102);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:32:         // DeliverPersonnel — bandit-prisoner delivery (Wave 1)
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:33:         AddClassDefinition(typeof(DeliverPersonnelLotrIssue), 103);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:34:         AddClassDefinition(typeof(DeliverPersonnelLotrIssueQuest), 104);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:35:         // Combat — defeat-raids / capture-lords (Wave 2)
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:36:         AddClassDefinition(typeof(CombatLotrIssue), 105);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:37:         AddClassDefinition(typeof(CombatLotrIssueQuest), 106);
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:38:     }
Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs:39: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml -Pattern 'variant=' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 496ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:121: <!-- Wave 2 — Combat (event-driven count; variant=DefeatRaids counts won battles, CaptureLords counts at-war lord captures). -->
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:123: <LotrIssue id="lotr_nearby_bandit_base" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:133: <LotrIssue id="lotr_merchant_outlaws" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:143: <LotrIssue id="lotr_merchant_poachers" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:153: <LotrIssue id="lotr_captured_gang" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:163: <LotrIssue id="lotr_extortion_deserters" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:173: <LotrIssue id="lotr_landlord_commons" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:183: <LotrIssue id="lotr_rival_gang" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:193: <LotrIssue id="lotr_spy_party" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:203: <LotrIssue id="lotr_lesser_noble_revolt" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:213: <LotrIssue id="lotr_company_trouble" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:223: <LotrIssue id="lotr_prodigal_son" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:233: <LotrIssue id="lotr_rival_captured" template="Combat" variant="CaptureLords"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:246: <LotrIssue id="lotr_caravan_ambush" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:256: <LotrIssue id="lotr_escort_caravan" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:277: <LotrIssue id="lotr_snare_wealthy" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:345: <LotrIssue id="lotr_smugglers" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:355: <LotrIssue id="lotr_gang_needs_weapons" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:368: <LotrIssue id="lotr_garrison_troops" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:378: <LotrIssue id="lotr_conquest" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:388: <LotrIssue id="lotr_raid_territory" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:398: <LotrIssue id="lotr_scout_garrisons" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:411: <LotrIssue id="lotr_ladys_knight" template="Combat" variant="WinTournaments"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:432: <LotrIssue id="lotr_landlord_training" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:442: <LotrIssue id="lotr_inn_and_out" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:452: <LotrIssue id="lotr_family_feud" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:462: <LotrIssue id="lotr_daughter_found" template="Combat" variant="DefeatRaids"
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml:472: <LotrIssue id="lotr_betting_fraud" template="Combat" variant="WinTournaments"

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if (($i -ge 246 -and $i -le 285) -or ($i -ge 318 -and $i -le 338)) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 490ms:
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:246:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:247:         if (_readyLog == null && (_acceptedLog?.CurrentProgress ?? 0) >= _neededCount)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:248:             _readyLog = AddLog(ReadyLogText);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:249:         else if (_readyLog != null && (_acceptedLog?.CurrentProgress ?? 0) < _neededCount)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:250:         {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:251:             RemoveLog(_readyLog);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:252:             _readyLog = null;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:253:         }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:254:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:255: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:256:     private void OnPlayerBattleEnd(MapEvent mapEvent) => Refresh();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:257: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:258:     private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:259:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:260:         if (party == MobileParty.MainParty) Refresh();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:261:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:262: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:263:     private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:264:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:265:         if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:266:             && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:267:             CompleteQuestWithCancel(CancelLogText);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:268:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:269: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:270:     private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:271:         => QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(this, faction1, faction2, detail, CancelLogText, CancelLogText, forceCancel: true);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:272: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:273:     private TextObject CancelLogText => new TextObject("{=taom_lotr_issue_common_cancel}The agreement has been cancelled.");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:274: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:275:     protected override void OnTimedOut()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:276:         => AddLog(new TextObject("{=taom_lotr_issue_pers_timeout}You failed to deliver the captives in time."));
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:277: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:278:     protected override void SetDialogs()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:279:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:280:         var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:281:         var npcHave = new TextObject("{=taom_lotr_issue_pers_have}Have you brought the captives?");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:282:         var npcThanks = new TextObject("{=taom_lotr_issue_pers_thanks}They'll do nicely. You have my thanks.");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:283:         var npcAwait = new TextObject("{=taom_lotr_issue_common_await}We await your return.");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:284: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:285:         OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:318:     private void QuestAcceptedConsequences()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:319:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:320:         StartQuest();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:321:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:322:         var task = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.TaskKey)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:323:             ? "{=taom_lotr_issue_pers_task}Take bandit captives" : _def.Text.TaskKey);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:324:         task.SetTextVariable("COUNT", _neededCount);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:325:         _acceptedLog = AddDiscreteLog(AcceptedLogText, task, CountBanditPrisoners(), _neededCount);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:326:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:327: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:328:     private void Success()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:329:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:330:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:331:         RemoveBanditPrisoners(_neededCount);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:332:         if (_def != null)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:333:             Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:334:         RelationshipChangeWithQuestGiver = 5;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:335:         CompleteQuestWithSuccess();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:336:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:337: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:338:     private void RemoveBanditPrisoners(int count)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 338 -and $i -le 367) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 490ms:
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:338:     private void RemoveBanditPrisoners(int count)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:339:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:340:         var roster = PartyBase.MainParty.PrisonRoster;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:341:         if (roster == null) return;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:342:         for (int i = roster.Count - 1; i >= 0 && count > 0; i--)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:343:         {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:344:             var el = roster.GetElementCopyAtIndex(i);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:345:             if (el.Character == null || el.Character.Occupation != Occupation.Bandit) continue;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:346:             int take = Math.Min(count, el.Number);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:347:             roster.AddToCounts(el.Character, -take);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:348:             count -= take;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:349:         }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:350:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:351: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 230 -and $i -le 245) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 470ms:
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:230:         int sum = 0;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:231:         for (int i = 0; i < roster.Count; i++)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:232:         {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:233:             var el = roster.GetElementCopyAtIndex(i);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:234:             if (el.Character != null && el.Character.Occupation == Occupation.Bandit) sum += el.Number;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:235:         }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:236:         return sum <= _neededCount ? sum : _neededCount;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:237:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:238: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:239:     private void Refresh()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:240:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:241:         if (_acceptedLog != null) _acceptedLog.UpdateCurrentProgress(CountBanditPrisoners());
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:242:         CheckReady();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:243:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:244: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:245:     private void CheckReady()

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 284 -and $i -le 316) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 451ms:
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:284: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:285:         OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:286:             .NpcLine(npcAccept)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:287:             .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:288:             .Consequence(QuestAcceptedConsequences)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:289:             .CloseDialog();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:290: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:291:         DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:292:             .NpcLine(npcHave)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:293:             .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:294:             .BeginPlayerOptions()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:295:             .PlayerOption(new TextObject("{=taom_lotr_issue_pers_turnin}Yes. Here they are."))
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:296:             .ClickableCondition(TurnInClickableConditions)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:297:             .NpcLine(npcThanks)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:298:             .Consequence(() => Campaign.Current.ConversationManager.ConversationEndOneShot += Success)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:299:             .CloseDialog()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:300:             .PlayerOption(new TextObject("{=taom_lotr_issue_common_working}Not yet -- I'm working on it."))
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:301:             .NpcLine(npcAwait)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:302:             .CloseDialog()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:303:             .EndPlayerOptions()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:304:             .CloseDialog();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:305:     }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:306: 
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:307:     private bool TurnInClickableConditions(out TextObject explanation)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:308:     {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:309:         if (CountBanditPrisoners() >= _neededCount)
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:310:         {
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:311:             explanation = null;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:312:             return true;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:313:         }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:314:         explanation = new TextObject("{=taom_lotr_issue_pers_not_enough}You don't have enough captives yet.");
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:315:         return false;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:316:     }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if (($i -ge 222 -and $i -le 254) -or ($i -ge 265 -and $i -le 280)) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 457ms:
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:222:     private void OnPlayerBattleEnd(MapEvent mapEvent)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:223:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:224:         if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None || mapEvent.WinningSide != mapEvent.PlayerSide) return;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:225:         Bump();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:226:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:227: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:228:     private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:229:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:230:         if (capturer != PartyBase.MainParty || prisoner == null || !prisoner.IsLord) return;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:231:         var playerFaction = Hero.MainHero.MapFaction;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:232:         if (playerFaction == null || prisoner.MapFaction == null || !prisoner.MapFaction.IsAtWarWith(playerFaction)) return;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:233:         Bump();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:234:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:235: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:236:     private void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:237:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:238:         if (winner == CharacterObject.PlayerCharacter) Bump();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:239:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:240: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:241:     private void Bump()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:242:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:243:         if (!IsOngoing) return;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:244:         _progress++;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:245:         if (_progress > _neededCount) _progress = _neededCount;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:246:         _log?.UpdateCurrentProgress(_progress);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:247:         if (_progress >= _neededCount) Success();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:248:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:249: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:250:     private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:251:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:252:         if (base.QuestGiver?.CurrentSettlement?.MapFaction != null
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:253:             && base.QuestGiver.CurrentSettlement.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:254:             CompleteQuestWithCancel(CancelLogText);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:265:     protected override void SetDialogs()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:266:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:267:         var npcAccept = new TextObject("{=taom_lotr_issue_common_offer_accept}Thank you. I will not forget this kindness.");
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:268:         OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:269:             .NpcLine(npcAccept)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:270:             .Condition(() => CharacterObject.OneToOneConversationCharacter == base.QuestGiver.CharacterObject)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:271:             .Consequence(QuestAcceptedConsequences)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:272:             .CloseDialog();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:273:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:274: 
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:275:     private void QuestAcceptedConsequences()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:276:     {
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:277:         StartQuest();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:278:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:279:         var body = new TextObject(_def == null || string.IsNullOrEmpty(_def.Text.DescriptionKey)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:280:             ? "{=taom_lotr_issue_fallback_desc}A foe must be dealt with." : _def.Text.DescriptionKey);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$f='"'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs'; "'$i=0; Get-Content $f | ForEach-Object { $i++; if ($i -ge 286 -and $i -le 302) { '"'{0}:{1}: {2}' -f "'$f,$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 450ms:
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:286:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:287:         if (_def != null)
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:288:             Service.ApplyRewards(_def, _difficulty, new LotrIssueRewardAdapter(Hero.MainHero));
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:289:         RelationshipChangeWithQuestGiver = 5;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:290:         CompleteQuestWithSuccess();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:291:     }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:292: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs','Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'); foreach("'$f in $files){ Write-Output "--- $f"; $i=0; Get-Content $f | ForEach-Object { $i++; if ($_ -match '"'SaveableField|private LotrIssueDefinition _def|private ILotrIssueService _service') { '{0}:{1}: {2}' -f "'$f,$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 499ms:
--- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:29:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:31:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:32:     private ILotrIssueService _service;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:139:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:140:     [SaveableField(2)] private int _neededCount;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:141:     [SaveableField(3)] private int _rewardGold;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:142:     [SaveableField(4)] private float _difficulty;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:143:     [SaveableField(5)] private string _variant;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:144:     [SaveableField(6)] private int _progress;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:145:     [SaveableField(7)] private JournalLog _log;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:147:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:148:     private ILotrIssueService _service;
--- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:28:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:30:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:31:     private ILotrIssueService _service;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:140:     [SaveableField(1)] private string _defId;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:141:     [SaveableField(2)] private int _neededCount;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:142:     [SaveableField(3)] private int _rewardGold;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:143:     [SaveableField(4)] private float _difficulty;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:144:     [SaveableField(5)] private JournalLog _acceptedLog;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:145:     [SaveableField(6)] private JournalLog _readyLog;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:147:     private LotrIssueDefinition _def;
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:148:     private ILotrIssueService _service;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Main/Features/LotrIssues/Templates/CombatLotrIssue.cs','Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs'); foreach("'$f in $files){ Write-Output "--- $f"; $i=0; Get-Content $f | ForEach-Object { $i++; if ($_ -match '"'EnsureDef|DefinitionsById|_def = Service') { '{0}:{1}: {2}' -f "'$f,$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 488ms:
--- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:43:     private void EnsureDef()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:45:         if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:50:         get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:55:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:74:         get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:79:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:98:     protected override void OnGameLoad() => EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:106:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:117:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:168:     private void EnsureDef()
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:170:         if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:177:             EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:204:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:278:         EnsureDef();
Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:286:         EnsureDef();
--- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:44:     private void EnsureDef()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:46:         if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:51:         get { EnsureDef(); return _def == null ? 1 : Service.ComputeTargetCount(_def, base.IssueDifficultyMultiplier); }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:56:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:75:         get { EnsureDef(); return _def == null ? 0 : Service.ComputeRewardGold(_def, base.IssueDifficultyMultiplier); }
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:80:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:99:     protected override void OnGameLoad() => EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:107:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:118:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:164:     private void EnsureDef()
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:166:         if (_def == null && !string.IsNullOrEmpty(_defId)) _def = Service?.GetIssueById(_defId);
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:173:             EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:207:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:321:         EnsureDef();
Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:330:         EnsureDef();

codex
The key engine edge case is confirmed: Bannerlord keys several issue mechanics on the C# issue type, and TAOM collapses many authored rows into three runtime types. I’m tightening that into a calibrated finding and separating it from the already-documented spawn limitation.
2026-06-20T18:22:23.784597Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe';
"'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll';
"'$out='"'C:\\tmp\\taom_ilspy_lotr_review';
if (Test-Path "'$out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.QuestBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.MbEvent`1 $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignEvents $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter $dll | Out-Null
Get-ChildItem -Path $out -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe';
"'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll';
"'$out='"'C:\\tmp\\taom_ilspy_lotr_review';
if (Test-Path "'$out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.QuestBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.MbEvent`1 $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignEvents $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter $dll | Out-Null
Get-ChildItem -Path $out -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName' in C:\Users\mikew\source\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe';
"'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll';
"'$out='"'C:\\tmp\\taom_ilspy_lotr_review';
if (Test-Path "'$out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.QuestBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.MbEvent`1 $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignEvents $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter $dll | Out-Null
Get-ChildItem -Path $out -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe';
"'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll';
"'$out="C:'"\\tmp\\taom_ilspy_lotr_review_"'$([guid]::NewGuid().ToString('"'N'))\";
New-Item -ItemType Directory -Path "'$out | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.QuestBase $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.CampaignEvents $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior $dll | Out-Null
& $ilspy -p -o $out -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter $dll | Out-Null
Write-Output $out
Get-ChildItem -Path $out -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName' in C:\Users\mikew\source\repos\TAOM
2026-06-20T18:24:33.085085Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 120.1 seconds
Output:
command timed out after 120149 milliseconds
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   5 | [0m [36;1mNew-Item -ItemType Directory -Path $out[0m | Out-Null[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\taom_ilspy_lotr_review_e2fdacf7d1ae4e08b34e6787e0f63c44' is denied.[0m
Unhandled exception. System.UnauthorizedAccessException: Access to the path 'C:\tmp\taom_ilspy_lotr_review_e2fdacf7d1ae4e08b34e6787e0f63c44' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 191
   at McMaster.Extensions.CommandLineUtils.SourceGeneration.ReflectionExecuteHandler.InvokeAsyncMethod(Object instance, Object[] arguments)
   at McMaster.Extensions.CommandLineUtils.SourceGeneration.ReflectionExecuteHandler.InvokeAsync(Object model, CommandLineApplication app, CancellationToken cancellationToken)
   at McMaster.Extensions.CommandLineUtils.Conventions.ExecuteMethodConvention.<>c__DisplayClass0_0.<<Apply>b__1>d.MoveNext()
--- End of stack trace from previous location ---
   at McMaster.Extensions.CommandLineUtils.CommandLineApplication.ExecuteAsync(String[] args, CancellationToken cancellationToken)
   at McMaster.Extensions.Hosting.CommandLine.Internal.CommandLineService`1.RunAsync(CancellationToken cancellationToken)
   at McMaster.Extensions.Hosting.CommandLine.Internal.CommandLineLifetime.<>c__DisplayClass10_0.<<WaitForStartAsync>b__0>d.MoveNext()
--- End of stack trace from previous location ---
   at Microsoft.Extensions.Hosting.HostExtensions.RunCommandLineApplicationAsync(IHost host, CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.HostBuilderExtensions.RunCommandLineApplicationAsync[TApp](IHostBuilder hostBuilder, String[] args, Action`1 configure, CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.HostBuilderExtensions.RunCommandLineApplicationAsync[TApp](IHostBuilder hostBuilder, String[] args, CancellationToken cancellationToken)
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<Main>(String[] args)

 exited 124 in 120149ms:
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   5 | [0m [36;1mNew-Item -ItemType Directory -Path $out[0m | Out-Null[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\taom_ilspy_lotr_review_e2fdacf7d1ae4e08b34e6787e0f63c44' is denied.[0m
Unhandled exception. System.UnauthorizedAccessException: Access to the path 'C:\tmp\taom_ilspy_lotr_review_e2fdacf7d1ae4e08b34e6787e0f63c44' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.OnExecuteAsync(CommandLineApplication app) in D:\a\ILSpy\ILSpy\ICSharpCode.ILSpyCmd\IlspyCmdProgram.cs:line 191
   at McMaster.Extensions.CommandLineUtils.SourceGeneration.ReflectionExecuteHandler.InvokeAsyncMethod(Object instance, Object[] arguments)
   at McMaster.Extensions.CommandLineUtils.SourceGeneration.ReflectionExecuteHandler.InvokeAsync(Object model, CommandLineApplication app, CancellationToken cancellationToken)
   at McMaster.Extensions.CommandLineUtils.Conventions.ExecuteMethodConvention.<>c__DisplayClass0_0.<<Apply>b__1>d.MoveNext()
--- End of stack trace from previous location ---
   at McMaster.Extensions.CommandLineUtils.CommandLineApplication.ExecuteAsync(String[] args, CancellationToken cancellationToken)
   at McMaster.Extensions.Hosting.CommandLine.Internal.CommandLineService`1.RunAsync(CancellationToken cancellationToken)
   at McMaster.Extensions.Hosting.CommandLine.Internal.CommandLineLifetime.<>c__DisplayClass10_0.<<WaitForStartAsync>b__0>d.MoveNext()
--- End of stack trace from previous location ---
   at Microsoft.Extensions.Hosting.HostExtensions.RunCommandLineApplicationAsync(IHost host, CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.HostBuilderExtensions.RunCommandLineApplicationAsync[TApp](IHostBuilder hostBuilder, String[] args, Action`1 configure, CancellationToken cancellationToken)
   at Microsoft.Extensions.Hosting.HostBuilderExtensions.RunCommandLineApplicationAsync[TApp](IHostBuilder hostBuilder, String[] args, CancellationToken cancellationToken)
   at ICSharpCode.ILSpyCmd.ILSpyCmdProgram.<Main>(String[] args)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll | Select-String -Pattern '"'CalculateIssueScoreForNotable|CalculateIssueScoreForClan|CalculateIssueScoreInternal|HasIssueCoolDown|IssueType|GetType\\(\\)' -Context 0,8" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1442ms:

> 					float num = [7mCalculateIssueScoreForNotable[0m(in pid, settlement, totalDesiredIssueCount, totalFrequencyScore);
> 					if (num > 0f && !issueManager.[7mHasIssueCoolDown[0m(pid.IssueType, notable))[0m
[7m[0m  					{[0m
[7m[0m  						_cachedIssueDataList.Add(new IssueData(pid, notable, num));[0m
[7m[0m  					}[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		if (_cachedIssueDataList.Count > 0)[0m
[7m[0m  		{[0m
> 					float num2 = [7mCalculateIssueScoreForClan[0m(in pid, clan, totalDesiredIssueCount, totalFrequencyScore);
> 					if (num2 > num && !issueManager.[7mHasIssueCoolDown[0m(pid.IssueType, aliveLord))[0m
[7m[0m  					{[0m
[7m[0m  						issueData = new IssueData(pid, aliveLord, num2);[0m
[7m[0m  						num = num2;[0m
[7m[0m  					}[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
[7m[0m  		if (issueData.HasValue)[0m
> 	private float [7mCalculateIssueScoreForClan[0m(in [0m[7m[0mPotentialIssueData [0m[7m[0mpid, [0m[7m[0mClan [0m[7m[0mclan, [0m[7m[0mint [0m[7m[0mtotalDesiredIssueCount, [0m[7m[0mint [0m
[7m[0mtotalFrequencyScore)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (Hero hero in clan.Heroes)[0m
[7m[0m  		{[0m
> 			if (hero.Issue != null && hero.Issue.[7mGetType()[0m == pid.IssueType)[0m
[7m[0m  			{[0m
[7m[0m  				return 0f;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
> 		return [7mCalculateIssueScoreInternal[0m(in pid, totalDesiredIssueCount, totalFrequencyScore);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private float [7mCalculateIssueScoreForNotable[0m(in [0m[7m[0mPotentialIssueData [0m[7m[0mpid, [0m[7m[0mSettlement [0m[7m[0msettlement, [0m[7m[0mint [0m
[7m[0mtotalDesiredIssueCount, [0m[7m[0mint [0m[7m[0mtotalFrequencyScore)[0m
[7m[0m  	{[0m
[7m[0m  		foreach (Hero notable in settlement.Notables)[0m
[7m[0m  		{[0m
> 			if (notable.Issue != null && notable.Issue.[7mGetType()[0m == pid.IssueType)[0m
[7m[0m  			{[0m
[7m[0m  				return 0f;[0m
[7m[0m  			}[0m
[7m[0m  		}[0m
> 		return [7mCalculateIssueScoreInternal[0m(in pid, totalDesiredIssueCount, totalFrequencyScore);[0m
[7m[0m  	}[0m
[7m[0m  [0m
> 	private float [7mCalculateIssueScoreInternal[0m(in [0m[7m[0mPotentialIssueData [0m[7m[0mpid, [0m[7m[0mint [0m[7m[0mtotalDesiredIssueCount, [0m[7m[0mint [0m
[7m[0mtotalFrequencyScore)[0m
[7m[0m  	{[0m
[7m[0m  		float num = (float)GetFrequencyScore(pid.Frequency) / (float)totalFrequencyScore;[0m
[7m[0m  		float num2;[0m
[7m[0m  		if (totalDesiredIssueCount == 0)[0m
[7m[0m  		{[0m
[7m[0m  			num2 = 1f;[0m
[7m[0m  		}[0m
[7m[0m  		else[0m
> 				if (issue.Value.[7mGetType()[0m == pid.IssueType)[0m
[7m[0m  				{[0m
[7m[0m  					num3++;[0m
[7m[0m  				}[0m
[7m[0m  			}[0m
[7m[0m  			num2 = (float)num3 / (float)totalDesiredIssueCount;[0m
[7m[0m  		}[0m
[7m[0m  		float num4 = 1f + _additionalFrequencyScore - num2 / num;[0m
[7m[0m  		if (num4 < 0f)[0m
> 			Campaign.Current.IssueManager.AddIssueCoolDownData(issue.[7mGetType()[0m, [0m[7m[0mnew [0m
[7m[0mHeroRelatedIssueCoolDownData(issue.IssueOwner, [0m
[7m[0mCampaignTime.DaysFromNow(Campaign.Current.Models.IssueModel.IssueOwnerCoolDownInDays)));[0m
[7m[0m  		}[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	public override void SyncData(IDataStore dataStore)[0m
[7m[0m  	{[0m
[7m[0m  	}[0m
[7m[0m  [0m
[7m[0m  	private void OnSessionLaunched(CampaignGameStarter starter)[0m


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll; $start=($src | Select-String -SimpleMatch '"'private float CalculateIssueScoreInternal').LineNumber; "'$src[($start-1)..($start+35)]' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1364ms:
	private float CalculateIssueScoreInternal(in PotentialIssueData pid, int totalDesiredIssueCount, int totalFrequencyScore)
	{
		float num = (float)GetFrequencyScore(pid.Frequency) / (float)totalFrequencyScore;
		float num2;
		if (totalDesiredIssueCount == 0)
		{
			num2 = 1f;
		}
		else
		{
			int num3 = 0;
			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
			{
				if (issue.Value.GetType() == pid.IssueType)
				{
					num3++;
				}
			}
			num2 = (float)num3 / (float)totalDesiredIssueCount;
		}
		float num4 = 1f + _additionalFrequencyScore - num2 / num;
		if (num4 < 0f)
		{
			num4 = 0f;
		}
		else if (num4 < _additionalFrequencyScore)
		{
			num4 *= 0.01f;
		}
		else if (num4 < _additionalFrequencyScore + 0.4f)
		{
			num4 *= 0.1f;
		}
		return num * num4;
	}

	private int GetFrequencyScore(IssueBase.IssueFrequency frequency)

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior $dll; foreach($name in @('"'private float CalculateIssueScoreForClan','private float CalculateIssueScoreForNotable')){ "'$start=($src | Select-String -SimpleMatch $name).LineNumber; $src[($start-1)..($start+13)] }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1329ms:
	private float CalculateIssueScoreForClan(in PotentialIssueData pid, Clan clan, int totalDesiredIssueCount, int totalFrequencyScore)
	{
		foreach (Hero hero in clan.Heroes)
		{
			if (hero.Issue != null && hero.Issue.GetType() == pid.IssueType)
			{
				return 0f;
			}
		}
		return CalculateIssueScoreInternal(in pid, totalDesiredIssueCount, totalFrequencyScore);
	}

	private float CalculateIssueScoreForNotable(in PotentialIssueData pid, Settlement settlement, int totalDesiredIssueCount, int totalFrequencyScore)
	{
		foreach (Hero notable in settlement.Notables)
	private float CalculateIssueScoreForNotable(in PotentialIssueData pid, Settlement settlement, int totalDesiredIssueCount, int totalFrequencyScore)
	{
		foreach (Hero notable in settlement.Notables)
		{
			if (notable.Issue != null && notable.Issue.GetType() == pid.IssueType)
			{
				return 0f;
			}
		}
		return CalculateIssueScoreInternal(in pid, totalDesiredIssueCount, totalFrequencyScore);
	}

	private float CalculateIssueScoreInternal(in PotentialIssueData pid, int totalDesiredIssueCount, int totalFrequencyScore)
	{
		float num = (float)GetFrequencyScore(pid.Frequency) / (float)totalFrequencyScore;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll; $start=($src | Select-String -SimpleMatch '"'public bool CheckPreconditions').LineNumber; "'$src[($start-1)..($start+65)]' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1346ms:
}
using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.Issues;

public abstract class IssueBase : MBObjectBase
{
	internal enum IssueState
	{
		Ongoing,
		SolvingWithQuestSolution,
		SolvingWithAlternativeSolution,
		SolvingWithLordSolution
	}

	[Flags]
	public enum AlternativeSolutionScaleFlag : uint
	{
		None = 0u,
		Duration = 1u,
		RequiredTroops = 2u,
		Casualties = 4u,
		FailureRisk = 8u
	}

	[Flags]
	protected enum PreconditionFlags : uint
	{
		None = 0u,
		Relation = 1u,
		Skill = 2u,
		Money = 4u,
		Renown = 8u,
		Influence = 0x10u,
		Wounded = 0x20u,
		AtWar = 0x40u,
		ClanTier = 0x80u,
		NotEnoughTroops = 0x100u,
		NotInSameFaction = 0x200u,
		PartySizeLimit = 0x400u,
		ClanIsMercenary = 0x800u,
		MainHeroIsKingdomLeader = 0x4000u,
		PlayerIsOwnerOfSettlement = 0x8000u,
		CompanionLimitReached = 0x10000u
	}

	public enum IssueUpdateDetails
	{
		None,
		PlayerStartedIssueQuestClassicSolution,
		PlayerSentTroopsToQuest,
		SentTroopsFinishedQuest,

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.IssueBase $dll; $src | Select-String -Pattern '"'CheckPreconditions|IssueQuestCanBeDuplicated|other, similar' -Context 3,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1357ms:

  	[SaveableField(31)]
  	private readonly List<ITrackableCampaignObject> _trackedObjects = new List<ITrackableCampaignObject>();
  
> 	protected virtual bool IssueQuestCanBeDuplicated => false;
  
  	public virtual int RelationshipChangeWithIssueOwner { get; protected set; }
  
  	public abstract TextObject IssueBriefByIssueGiver { get; }
  
  	public abstract TextObject IssueAcceptByPlayer { get; }
  
  	public virtual TextObject IssuePlayerResponseAfterLordExplanation => new TextObject("{=sMCN7eCp}Is there any other 
way to solve this problem?");
  		CompleteIssueWithBetrayal();
  	}
  
> 	internal bool CheckPreconditions(Hero issueGiver, out TextObject explanation)
  	{
  		explanation = new TextObject("{=!}{EXPLANATION}");
  		if (!IssueStayAliveConditions() && IsOngoingWithoutQuest)
  		{
  			CompleteIssueWithCancel();
  			return false;
  		}
  		PreconditionFlags flag;
  		int requiredGold;
  		bool result = CanPlayerTakeQuestConditions(issueGiver, out flag, out relationHero, out skill, out requiredGold);
  		bool flag2 = false;
> 		if (!IssueQuestCanBeDuplicated)
  		{
  			foreach (KeyValuePair<Hero, IssueBase> issue in Campaign.Current.IssueManager.Issues)
  			{
  				IssueBase value = issue.Value;
  				if ((value.IsSolvingWithQuest || value.IsSolvingWithAlternative) && value.GetType() == GetType())
  				{
  					flag2 = true;
  					result = false;
  		}
  		else if (flag2)
  		{
> 			explanation.SetTextVariable("EXPLANATION", new TextObject("{=HvY7wjHt}I don't think you can help me. I think you 
may have other, similar commitments that could interfere."));
  		}
  		else if ((flag & PreconditionFlags.NotInSameFaction) == PreconditionFlags.NotInSameFaction)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=rBPI2dvX}I don't need the service of strangers. I 
work only with lords of the realm and loyal mercenaries.[ib:closed][if:convo_grave]"));
  		}
  		else if ((flag & PreconditionFlags.MainHeroIsKingdomLeader) == PreconditionFlags.MainHeroIsKingdomLeader || (flag 
& PreconditionFlags.PlayerIsOwnerOfSettlement) == PreconditionFlags.PlayerIsOwnerOfSettlement)
  		{
  			explanation.SetTextVariable("EXPLANATION", new TextObject("{=dYJKy2mO}Thank you for asking my 
{?PLAYER.GENDER}lady{?}lord{\\?}, but I can't bother you with such an unimportant issue."));


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll; $src | Select-String -Pattern '"'HasIssueCoolDown|AddIssueCoolDownData|OnQuestCompleted|DeactivateIssue|CompleteIssueWithQuest' -Context 2,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 4632ms:

  	}
  
> 	public void DeactivateIssue(IssueBase issue)
  	{
  		if (issue.IssueQuest != null)
  		{
  			issue.IssueQuest?.CompleteQuestWithCancel();
  			return;
  		}
  		issue.IssueOwner.OnIssueDeactivatedForHero();
  		Campaign.Current.ConversationManager.RemoveRelatedLines(issue);
  	}
  
> 	public override void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
  	{
  		foreach (KeyValuePair<Hero, IssueBase> issue in Issues)
  		{
  			if (issue.Value.IssueQuest == quest)
  			{
  				switch (detail)
  				{
  				case QuestBase.QuestCompleteDetails.Success:
> 					issue.Value.CompleteIssueWithQuest();
  					break;
  				case QuestBase.QuestCompleteDetails.Cancel:
  					issue.Value.CompleteIssueWithCancel();
  					break;
  				case QuestBase.QuestCompleteDetails.Fail:
  					issue.Value.CompleteIssueWithFail();
  					break;
  				case QuestBase.QuestCompleteDetails.Timeout:
  					break;
  				default:
> 					issue.Value.CompleteIssueWithQuest();
  					break;
  				}
  				break;
  			}
  		}
  	}
  
  	public override void OnHeroUnregistered(Hero hero)
  	}
  
> 	public void AddIssueCoolDownData(Type type, IssueCoolDownData data)
  	{
  		string name = type.Name;
  		if (!_issuesCoolDownData.ContainsKey(name))
  		{
  			_issuesCoolDownData.Add(name, new List<IssueCoolDownData>());
  		}
  		_issuesCoolDownData[name].Add(data);
  	}
  
> 	public bool HasIssueCoolDown(Type type, Hero hero)
  	{
  		string name = type.Name;
  		bool result = false;
  		if (_issuesCoolDownData.TryGetValue(name, out var value))
  		{
  			foreach (IssueCoolDownData item in value)
  			{
  				if (item.IsValid() && item.IsRelatedTo(hero))


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll; $start=($src | Select-String -SimpleMatch '"'public bool HasIssueCoolDown').LineNumber; "'$src[($start-1)..($start+22)]' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1376ms:
	public bool HasIssueCoolDown(Type type, Hero hero)
	{
		string name = type.Name;
		bool result = false;
		if (_issuesCoolDownData.TryGetValue(name, out var value))
		{
			foreach (IssueCoolDownData item in value)
			{
				if (item.IsValid() && item.IsRelatedTo(hero))
				{
					result = true;
					break;
				}
			}
		}
		return result;
	}

	public override void CanHaveCampaignIssues(Hero hero, ref bool result)
	{
		if (Issues.TryGetValue(hero, out var _))
		{
			result = false;
			return;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.IssueManager $dll; $start=($src | Select-String -SimpleMatch '"'public void AddIssueCoolDownData').LineNumber; "'$src[($start-1)..($start+10)]' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1345ms:
	public void AddIssueCoolDownData(Type type, IssueCoolDownData data)
	{
		string name = type.Name;
		if (!_issuesCoolDownData.ContainsKey(name))
		{
			_issuesCoolDownData.Add(name, new List<IssueCoolDownData>());
		}
		_issuesCoolDownData[name].Add(data);
	}

	public bool HasIssueCoolDown(Type type, Hero hero)
	{

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.QuestBase $dll; foreach($name in @('"'public bool IsOngoing','protected void CompleteQuestWithSuccess','private void FinalizeQuest','private void ClearRelatedFields')){ "'$m=$src | Select-String -SimpleMatch $name | Select-Object -First 1; if($m){ $start=$m.LineNumber; $src[($start-1)..([Math]::Min($start+20,$src.Length-1))] } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1188ms:
	public bool IsOngoing => _questState == QuestStates.Ongoing;

	public bool IsFinalized => _questState == QuestStates.Finalized;

	public bool IsThereDiscussDialogFlow => DiscussDialogFlow != null;

	public Hero QuestGiver
	{
		get
		{
			return _questGiver;
		}
		private set
		{
			_questGiver = value;
		}
	}

	public abstract TextObject Title { get; }

	public abstract bool IsRemainingTimeHidden { get; }

	private void FinalizeQuest()
	{
		foreach (QuestTaskBase task in _taskList)
		{
			if (task.IsActive)
			{
				task.Finish(QuestTaskBase.FinishStates.Cancel);
			}
		}
		_questState = QuestStates.Finalized;
		OnFinalize();
		ClearRelatedFields();
		OfferDialogFlow = null;
		DiscussDialogFlow = null;
		QuestCharacterDialogFlow = null;
		Campaign.Current.QuestManager.OnQuestFinalized(this);
	}

	private void AfterFinalize()
	{
		RemoveAllTrackedObjects();
		RemoveAllMapMarkers();
	private void ClearRelatedFields()
	{
		CampaignEventDispatcher.Instance.RemoveListeners(this);
		foreach (QuestTaskBase task in _taskList)
		{
			CampaignEventDispatcher.Instance.RemoveListeners(task);
		}
		Campaign.Current.ConversationManager.RemoveRelatedLines(this);
		Campaign.Current.GameMenuManager.RemoveRelatedGameMenus(this);
		Campaign.Current.GameMenuManager.RemoveRelatedGameMenuOptions(this);
	}

	private void RemoveAllTrackedObjects()
	{
		Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(this);
	}

	private void RemoveAllMapMarkers()
	{
		Campaign.Current.MapMarkerManager.RemoveAllMapMarkersByQuestId(base.StringId);
	}


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.QuestBase $dll; $src | Select-String -Pattern '"'CompleteQuestWithSuccess|FinalizeQuest\\(\\)|OnQuestCompleted|RemoveListeners' -Context 2,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1189ms:

  	}
  
> 	public void CompleteQuestWithSuccess()
  	{
  		OnCompleteWithSuccess();
> 		FinalizeQuest();
> 		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Success);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithTimeOut(TextObject timeOutLog = null)
  	{
  		bool completeWithSuccess = false;
  		bool doNotResolveTheQuest = false;
  		OnBeforeTimedOut(ref completeWithSuccess, ref doNotResolveTheQuest);
  			}
  			OnTimedOut();
> 			FinalizeQuest();
> 			CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Timeout);
  			AfterFinalize();
  		}
  		else
  		{
> 			CompleteQuestWithSuccess();
  		}
  	}
  
  	public void CompleteQuestWithFail(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
  			AddLog(cancelLog);
  		}
  		OnFailed();
> 		FinalizeQuest();
> 		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Fail);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithBetrayal(TextObject betrayLog = null)
  	{
  		if (betrayLog != null)
  		{
  			AddLog(betrayLog);
  		}
  		OnBetrayal();
> 		FinalizeQuest();
> 		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.FailWithBetrayal);
  		AfterFinalize();
  	}
  
  	public void CompleteQuestWithCancel(TextObject cancelLog = null)
  	{
  		if (cancelLog != null)
  		{
  			AddLog(cancelLog);
  		}
  		OnCanceled();
> 		FinalizeQuest();
> 		CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Cancel);
  		AfterFinalize();
  	}
  
> 	private void FinalizeQuest()
  	{
  		foreach (QuestTaskBase task in _taskList)
  		{
  			if (task.IsActive)
  			{
  				task.Finish(QuestTaskBase.FinishStates.Cancel);
  			}
  		}
  	private void ClearRelatedFields()
  	{
> 		CampaignEventDispatcher.Instance.RemoveListeners(this);
  		foreach (QuestTaskBase task in _taskList)
  		{
> 			CampaignEventDispatcher.Instance.RemoveListeners(task);
  		}
  		Campaign.Current.ConversationManager.RemoveRelatedLines(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenus(this);
  		Campaign.Current.GameMenuManager.RemoveRelatedGameMenuOptions(this);
  	}
  
  	private void RemoveAllTrackedObjects()
  	{


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -l c $dll | Select-String -Pattern '"'MbEvent' -NoEmphasis | Select-Object -First 20" in C:\Users\mikew\source\repos\TAOM
 succeeded in 737ms:

Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.ReferenceMBEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.ReferenceMBEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec
Class TaleWorlds.CampaignSystem.MbEvent.EventHandlerRec


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t '"'TaleWorlds.CampaignSystem.MbEvent"'`1'"' "'$dll | Select-String -Pattern '"'InvokeList|AddNonSerializedListener|ClearListeners' -Context 1,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1064ms:

  
> 	public void AddNonSerializedListener(object owner, Action<T> action)
  	{
  		EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
  		EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
  		_nonSerializedListenerList = eventHandlerRec;
  		eventHandlerRec.Next = nonSerializedListenerList;
  	}
  
  	public void Invoke(T t)
  	{
> 		InvokeList(_nonSerializedListenerList, t);
  	}
  
> 	private void InvokeList(EventHandlerRec<T> list, T t)
  	{
  		while (list != null)
  		{
  			list.Action(t);
  			list = list.Next;
  		}
  	}
  
> 	public void ClearListeners(object o)
  	{
  		ClearListenerOfList(ref _nonSerializedListenerList, o);
  	}
  
  	private void ClearListenerOfList(ref EventHandlerRec<T> list, object o)
  	{
  		EventHandlerRec<T> eventHandlerRec = list;
  		while (eventHandlerRec != null && eventHandlerRec.Owner != o)


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.CampaignEvents $dll | Select-String -Pattern '"'TournamentFinished|HeroPrisonerTaken|OnPlayerBattleEndEvent|WarDeclared|OnClanChangedKingdomEvent' -Context 1,3 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1835ms:

  
> 	private readonly MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> _warDeclared = new 
MbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>();
  
> 	private readonly MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> _tournamentFinished = 
new MbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>();
  
  	private readonly MbEvent<Town> _tournamentCancelled = new MbEvent<Town>();
  
  
> 	private readonly MbEvent<PartyBase, Hero> _heroPrisonerTaken = new MbEvent<PartyBase, Hero>();
  
  	private readonly MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> _heroPrisonerReleased = new 
MbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool>();
  
  
> 	private readonly MbEvent<MapEvent> _onPlayerBattleEndEvent = new MbEvent<MapEvent>();
  
  	private readonly MbEvent<CharacterObject, int> _onUnitRecruitedEvent = new MbEvent<CharacterObject, int>();
  
  
> 	public static IMbEvent<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool> 
OnClanChangedKingdomEvent => Instance._clanChangedKingdom;
  
  	public static IMbEvent<Clan, Kingdom, Kingdom> OnClanDefectedEvent => Instance._onClanDefected;
  
  
> 	public static IMbEvent<IFaction, IFaction, DeclareWarAction.DeclareWarDetail> WarDeclared => Instance._warDeclared;
  
> 	public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject> TournamentFinished => 
Instance._tournamentFinished;
  
  	public static IMbEvent<Town> TournamentCancelled => Instance._tournamentCancelled;
  
  
> 	public static IMbEvent<PartyBase, Hero> HeroPrisonerTaken => Instance._heroPrisonerTaken;
  
  	public static IMbEvent<Hero, PartyBase, IFaction, EndCaptivityDetail, bool> HeroPrisonerReleased => 
Instance._heroPrisonerReleased;
  
  
> 	public static IMbEvent<MapEvent> OnPlayerBattleEndEvent => Instance._onPlayerBattleEndEvent;
  
  	public static IMbEvent<CharacterObject, int> OnUnitRecruitedEvent => Instance._onUnitRecruitedEvent;
  
  		_romanticStateChanged.ClearListeners(obj);
> 		_warDeclared.ClearListeners(obj);
  		_battleStarted.ClearListeners(obj);
  		_rebellionFinished.ClearListeners(obj);
  		_townRebelliousStateChanged.ClearListeners(obj);
  		_characterDefeated.ClearListeners(obj);
> 		_heroPrisonerTaken.ClearListeners(obj);
  		_onPartySizeChangedEvent.ClearListeners(obj);
  		_characterBecameFugitiveEvent.ClearListeners(obj);
  		_playerMetHero.ClearListeners(obj);
  		_onMissionEndedEvent.ClearListeners(obj);
> 		_onPlayerBattleEndEvent.ClearListeners(obj);
  		_onPlayerBoardGameOver.ClearListeners(obj);
  		_onRansomOfferedToPlayer.ClearListeners(obj);
  		_onRansomOfferCancelled.ClearListeners(obj);
  		_tournamentStarted.ClearListeners(obj);
> 		_tournamentFinished.ClearListeners(obj);
  		_tournamentCancelled.ClearListeners(obj);
  		_playerInventoryExchangeEvent.ClearListeners(obj);
  		_onItemsDiscardedByPlayerEvent.ClearListeners(obj);
  
> 	public override void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail 
declareWarDetail)
  	{
> 		Instance._warDeclared.Invoke(faction1, faction2, declareWarDetail);
  	}
  
> 	public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, 
Town town, ItemObject prize)
  	{
> 		Instance._tournamentFinished.Invoke(winner, participants, town, prize);
  	}
  
  	public override void OnTournamentCancelled(Town town)
  
> 	public override void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
  	{
> 		Instance._heroPrisonerTaken.Invoke(capturer, prisoner);
  	}
  
  	public override void OnHeroPrisonerReleased(Hero prisoner, PartyBase party, IFaction capturerFaction, 
EndCaptivityDetail detail, bool showNotification = true)
  	{
> 		Instance._onPlayerBattleEndEvent.Invoke(mapEvent);
  	}
  
  	public override void OnUnitRecruited(CharacterObject character, int amount)


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; "'$src = & $ilspy -t TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior $dll; $src | Select-String -Pattern '"'MapEventEnded|WinningSide|IsPlayerMapEvent|InvolvedParties' -Context 2,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1401ms:

  		protected override void RegisterEvents()
  		{
> 			CampaignEvents.MapEventEnded.AddNonSerializedListener(this, MapEventEnded);
  			CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, GameMenuOpened);
  			CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
  			CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
  			CampaignEvents.GameMenuOptionSelectedEvent.AddNonSerializedListener(this, GameMenuOptionSelected);
  			CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
  			CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
  			CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
  			CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
  		}
  
> 		private void MapEventEnded(MapEvent mapEvent)
  		{
> 			if (!mapEvent.IsPlayerMapEvent || _deserterMobileParty == null || 
!mapEvent.InvolvedParties.Contains(_deserterMobileParty.Party))
  			{
  				return;
  			}
  			_deserterBattleFinalizedForTheFirstTime = true;
> 			if (mapEvent.WinningSide == mapEvent.PlayerSide)
  			{
  				AddLog(OnDeserterPartyDefeatedLogText);
  				if (!IsTracked(base.QuestGiver))
  				{
  					AddTrackedObject(base.QuestGiver);
  				}
  				if (!IsTracked(QuestSettlement))
  				{


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.Encounters.PlayerEncounter $dll | Select-String -Pattern '"'OnPlayerBattleEnd|DoApplyMapEventResults|WinningSide|IsPlayerSimulation' -Context 2,6 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1465ms:

  	public static BattleState BattleState => Current._mapEvent.BattleState;
  
> 	public static BattleSideEnum WinningSide => Current._mapEvent.WinningSide;
  
  	public static bool BattleChallenge
  	{
  		get
  		{
  			return Current._battleChallenge;
  					break;
  				case PlayerEncounterState.ApplyResults:
> 					DoApplyMapEventResults();
  					break;
  				case PlayerEncounterState.PlayerVictory:
  					DoPlayerVictory();
  					break;
  				case PlayerEncounterState.PlayerTotalDefeat:
  					DoPlayerDefeat();
  		else if (BattleSimulation != null && (BattleState == BattleState.AttackerVictory || BattleState == 
BattleState.DefenderVictory))
  		{
> 			if (_mapEvent.WinningSide == PlayerSide && Battle.RetreatingSide == BattleSideEnum.None)
  			{
  				EnemySurrender = true;
  			}
  			else
  			{
  				int totalManCount = MobileParty.MainParty.MemberRoster.TotalManCount;
  			EncounterState = PlayerEncounterState.PrepareResults;
  		}
> 		else if (_mapEvent != null && _mapEvent.IsNavalMapEvent && _mapEvent.IsFieldBattle && _mapEvent.WinningSide == 
BattleSideEnum.None && (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide).AnyQ((MapEventParty x) => x.Party.Ships.Count == 
0) || _mapEvent.PartiesOnSide(_mapEvent.PlayerSide.GetOppositeSide()).AnyQ((MapEventParty x) => x.Party.Ships.Count == 
0)))
  		{
  			IsNavalEncounterFinishedWithDisengage = false;
  			if (_mapEvent.PartiesOnSide(_mapEvent.PlayerSide).SumQ((MapEventParty x) => x.Party.Ships.Count) == 0)
  			{
  				_mapEvent.SetOverrideWinner(_mapEvent.PlayerSide.GetOppositeSide());
  				EncounterState = PlayerEncounterState.PrepareResults;
  	}
  
> 	private void DoApplyMapEventResults()
  	{
> 		CampaignEventDispatcher.Instance.OnPlayerBattleEnd(_mapEvent);
  		_mapEvent.CalculateAndCommitMapEventResults();
> 		if (_mapEvent.WinningSide == PartyBase.MainParty.Side)
  		{
  			EncounterState = PlayerEncounterState.PlayerVictory;
  		}
  		else if (_mapEvent.DefeatedSide == PartyBase.MainParty.Side && !IsNavalEncounterFinishedWithDisengage)
  		{
  			EncounterState = PlayerEncounterState.PlayerTotalDefeat;
  		bool isForcingSupplies = _mapEvent.IsForcingSupplies;
  		bool isBlockadeSallyOut = _mapEvent.IsBlockadeSallyOut;
> 		bool flag6 = BattleSimulation != null && _mapEvent.WinningSide != PlayerSide;
  		Settlement mapEventSettlement = _mapEvent.MapEventSettlement;
  		BattleState battleState = _mapEvent.BattleState;
  		_stateHandled = true;
  		if (!flag6)
  		{
  			Finish();
  					return false;
  				}
> 				if (((_mapEvent.DefeatedSide != BattleSideEnum.None) ? _mapEvent.GetMapEventSide(WinningSide).Parties : 
_mapEvent.GetMapEventSide(_mapEvent.PlayerSide.GetOppositeSide()).Parties).SumQ((MapEventParty t) => t.Ships.Count) == 
0)
  				{
  					return false;
  				}
  				_mapEvent.RecalculateStrengthOfSides();
  				float num2 = _mapEvent.StrengthOfSide[(int)_mapEvent.PlayerSide];
  				float num3 = _mapEvent.StrengthOfSide[(int)_mapEvent.PlayerSide.GetOppositeSide()];


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.MapEvents.MapEvent $dll | Select-String -Pattern '"'IsPlayerSimulation|IsPlayerMapEvent|PlayerSide|IsFieldBattle|IsHideoutBattle|IsSiegeAssault|IsRaid' -Context 0,4 -NoEmphasis | Select-Object -First 80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1607ms:

> 	public BattleSideEnum PlayerSide => PartyBase.MainParty.Side;
  
  	internal IBattleObserver BattleObserver { get; set; }
  
  	[SaveableProperty(105)]
> 				if (IsPlayerMapEvent)
  				{
  					Debug.Print("Player MapEvent State: " + value);
  				}
  				_state = value;
> 	public bool IsFieldBattle => _mapEventType == BattleTypes.FieldBattle;
  
> 	public bool IsRaid => _mapEventType == BattleTypes.Raid;
  
  	public bool IsForcingVolunteers => _mapEventType == BattleTypes.IsForcingVolunteers;
  
  	public bool IsForcingSupplies => _mapEventType == BattleTypes.IsForcingSupplies;
> 	public bool IsSiegeAssault => _mapEventType == BattleTypes.Siege;
  
> 	public bool IsHideoutBattle => _mapEventType == BattleTypes.Hideout;
  
  	public bool IsSallyOut => _mapEventType == BattleTypes.SallyOut;
  
  	public bool IsSiegeOutside => _mapEventType == BattleTypes.SiegeOutside;
> 	public bool IsPlayerSimulation { get; set; }
  
  	[SaveableProperty(126)]
  	public MBList<BattleSideEnum> WonRounds { get; private set; } = new MBList<BattleSideEnum>();
  
> 	public bool IsPlayerMapEvent => this == PlayerMapEvent;
  
  	public BattleState BattleState
  	{
  		get
> 				if (IsPlayerMapEvent)
  				{
  					Debug.Print("Player MapEvent BattleState: " + value);
  				}
  				_battleState = value;
> 	internal static object AutoGeneratedGetMemberValueIsPlayerSimulation(object o)
  	{
> 		return ((MapEvent)o).IsPlayerSimulation;
  	}
  
  	internal static object AutoGeneratedGetMemberValueWonRounds(object o)
  	{
> 				if (IsRaid && MapEventSettlement.Party.MapEvent == null)
  				{
  					FinalizeEvent();
  				}
  				else if (EventType == BattleTypes.Siege && MapEventSettlement.SiegeEvent == null)
> 		if (MBSaveLoad.IsUpdatingGameVersion && 
MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0")) && !IsPlayerMapEvent)
  		{
  			CommitXpGains();
  		}
> 		if (((MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < 
ApplicationVersion.FromString("v1.4.1")) || Game.Current.IsDevelopmentMode) && IsRaid && 
MobileParty.MainParty.MapEvent == this && PlayerSide == BattleSideEnum.Attacker && 
MapEventSettlement.LastAttackerParty != MobileParty.MainParty)
  		{
  			DiplomaticallyFinished = true;
  			PlayerEncounter.Finish();
  		}
> 		if (IsSiegeAssault && party.MobileParty != null && party.MobileParty.CurrentSettlement == null && side == 
BattleSideEnum.Defender)
  		{
  			_mapEventType = BattleTypes.SiegeOutside;
  		}
  		if (party.MobileParty != null && party.MobileParty.IsGarrison && side == BattleSideEnum.Attacker && 
(IsSiegeOutside || IsBlockade))
> 		if (party == MobileParty.MainParty.Party && !IsSiegeAssault && !IsRaid && !IsForcingSupplies && 
!IsForcingVolunteers)
  		{
  			party.MobileParty.SetMoveModeHold();
  		}
  		if (party == PartyBase.MainParty)
> 		if (IsFieldBattle && party.IsMobile && party.MobileParty.IsCurrentlyAtSea == IsNavalMapEvent && 
party.MobileParty.BesiegedSettlement == null)
  		{
  			int sideIndex = GetMapEventSide(side).Parties.Count((MapEventParty p) => p.Party.IsMobile) - 1;
  			SetPartyBaseEventLocalPosition(party, side, sideIndex);
  		}
> 			Position = ((IsRaid || IsForcingSupplies || IsForcingVolunteers) ? defenderParty.Position : 
attackerParty.Position);
> 			if (IsFieldBattle)
  			{
  				MapEventSettlement = null;
  				if (attackerParty.Position.IsOnLand != defenderParty.Position.IsOnLand || (attackerParty.Position.IsOnLand && 
(attackerParty == PartyBase.MainParty || defenderParty == PartyBase.MainParty)))
  				{
> 			PartyBase partyBase = (IsSiegeAssault ? attackerParty : defenderParty);
  			foreach (PartyBase item2 in involvedPartiesForEventType)
  			{
  				if (item2 != partyBase && item2.IsMobile && item2 != PartyBase.MainParty && item2.MobileParty.BesiegedSettlement 
== defenderParty.MobileParty.BesiegedSettlement && (item2.MobileParty.Army == null || 
item2.MobileParty.Army.LeaderParty == item2.MobileParty))
  				{
> 		if (IsFieldBattle && party.IsMobile && party.MobileParty.BesiegedSettlement == null)
  		{
  			party.MobileParty.EventPositionAdder = Vec2.Zero;
  			MapEventSide[] sides = _sides;
  			foreach (MapEventSide mapEventSide in sides)
> 		if (party.IsMobile && !party.MobileParty.IsInRaftState && !party.MobileParty.IsCurrentlyUsedByAQuest && 
(party.MapEvent == null || (!party.MapEvent.IsRaid && !party.MapEvent.IsForcingVolunteers && 
!party.MapEvent.IsForcingSupplies)) && party.SiegeEvent == null && (party.MobileParty.Army == null || 
party.MobileParty.Army.LeaderParty == party.MobileParty))
  		{
  			party.MobileParty.SetMoveModeHold();
  		}
  	}
> 				_ = IsPlayerSimulation && flag2;
  			}
  		}
  		return flag;
  	}
> 			if (IsPlayerSimulation && allocatedTroopParty2 == PartyBase.MainParty)
  			{
  				float playerTroopsReceivedDamageMultiplier = 
Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
  				num = MBRandom.RoundRandomized((float)num * playerTroopsReceivedDamageMultiplier);
  			}
> 			if (IsPlayerSimulation && allocatedTroopParty == PartyBase.MainParty && flag)
  			{
  				CampaignEventDispatcher.Instance.OnPlayerPartyKnockedOrKilledTroop(allocatedTroop2);
  			}
  		}
> 			if (!IsPlayerMapEvent || PlayerEncounter.Current == null)
  			{
  				FinishBattle();
  			}
  		}
> 		if (!IsPlayerMapEvent)
  		{
  			CalculateAndCommitMapEventResults();
  		}
  		BattleObserver?.BattleResultsReady();
> 		if (IsSiegeAssault)
  		{
  			CheckSiegeStageChange();
  		}
  		MapEventSide[] sides = _sides;
> 		if (MapEventSettlement != null && IsSiegeAssault)
  		{
  			int num = AttackerSide.Parties.Sum((MapEventParty party) => party.Party.NumberOfHealthyMembers);
  			int num2 = DefenderSide.Parties.Sum((MapEventParty party) => party.Party.NumberOfHealthyMembers);
  			if (num == 0)
> 		if (IsPlayerMapEvent && GetLeaderParty(PlayerSide) != PartyBase.MainParty && MobileParty.MainParty.Army != null)
  		{
  			return MobileParty.MainParty.Army.LeaderParty != MobileParty.MainParty;
  		}
  		return false;
> 			if (IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage)
  			{
  				LootDefeatedPartyShips(winnerParties, defeatedParties);
  			}
  			else
> 		if (IsPlayerMapEvent && IsNavalMapEvent && WinningSide == PlayerSide && !_playerFigureheadCalculated)
  		{
  			_playerFigureheadCalculated = true;
  			Figurehead figureheadLoot = Campaign.Current.Models.BattleRewardModel.GetFigureheadLoot(defeatedParties, 
defeatedLeaderParty);
  			PlayerEncounter.Current.PlayerLootedFigurehead = figureheadLoot;
> 		foreach (MapEventParty item in PartiesOnSide(PlayerSide))
  		{
  			if (item.Party == PartyBase.MainParty)
  			{
> 				int num = GetMapEventSide(PlayerSide).CalculateTotalContribution();
  				result = (float)item.ContributionToBattle / (float)num;
  				break;
  			}
  		}
> 		bool flag = IsPlayerMapEvent && PlayerSide == WinningSide;
  		float f = float.MinValue;
  		ItemRoster itemRoster = null;
  		MapEventParty playerBattleParty = (flag ? winnerParties.Find((MapEventParty x) => x.Party == PartyBase.MainParty) 
: null);
  		foreach (MapEventParty defeatedParty in defeatedParties)
> 		if (IsPlayerMapEvent && PlayerEncounter.Current.IsNavalEncounterFinishedWithDisengage)
  		{
  			foreach (Ship item in PlayerEncounter.Current.CapturedShipsInEncounter.ToList())
  			{
  				if (item.HitPoints > 0f)
> 		PartyBase partyBase = (IsPlayerMapEvent ? PartyBase.MainParty : Winner.LeaderParty);
  		CampaignVec2 position = partyBase.Position;
  		if (partyToTeleport.Position.IsOnLand != partyBase.Position.IsOnLand)
  		{
  			position = partyToTeleport.Position;
> 		if ((IsRaid || IsForcingSupplies || IsForcingVolunteers) && EndedByRetreat && RetreatingSide == 
BattleSideEnum.Attacker)
  		{
  			foreach (MapEventParty party in AttackerSide.Parties)
  			{
  				if (party.Party.IsMobile && party.Party.IsActive)
> 			if (BattleState != BattleState.None && (IsSiegeAssault || IsSiegeOutside || IsSallyOut || IsBlockadeSallyOut || 
IsBlockade) && MapEventSettlement.SiegeEvent != null)
  			{
  				MapEventSettlement.SiegeEvent.OnBeforeSiegeEventEnd(BattleState, _mapEventType);
  			}
> 			if (!_keepSiegeEvent && (IsSiegeAssault || IsSiegeOutside))
  			{
  				switch (BattleState)
  				{
  				case BattleState.AttackerVictory:
> 		if (IsSiegeAssault)
  		{
  			return;
  		}
  		foreach (PartyBase involvedParty in InvolvedParties)
> 		if (!IsFieldBattle && !IsSallyOut && !IsSiegeOutside && !IsSiegeAmbush && !IsBlockade && !IsBlockadeSallyOut)
  		{
  			return;
  		}
  		foreach (PartyBase involvedParty in InvolvedParties)


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.Hero $dll | Select-String -Pattern '"'public bool IsLord|public IFaction MapFaction' -Context 0,18 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1636ms:

> 	public bool IsLord => Occupation == Occupation.Lord;
  
  	public int MaxHitPoints => CharacterObject.MaxHitPoints();
  
  	public int HitPoints
  	{
  		get
  		{
  			return _health;
  		}
  		set
  		{
  			if (_health == value)
  			{
  				return;
  			}
  			int health = _health;
  			_health = value;
  			if (_health < 0)
> 	public IFaction MapFaction
  	{
  		get
  		{
  			if (Clan != null)
  			{
  				IFaction kingdom = Clan.Kingdom;
  				return kingdom ?? Clan;
  			}
  			if (IsSpecial)
  			{
  				return null;
  			}
  			if (HomeSettlement != null)
  			{
  				return HomeSettlement.MapFaction;
  			}
  			if (PartyBelongedTo != null)
  			{


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.Hero $dll | Select-String -Pattern '"'public IFaction MapFaction' -Context 0,30 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1627ms:

> 	public IFaction MapFaction
  	{
  		get
  		{
  			if (Clan != null)
  			{
  				IFaction kingdom = Clan.Kingdom;
  				return kingdom ?? Clan;
  			}
  			if (IsSpecial)
  			{
  				return null;
  			}
  			if (HomeSettlement != null)
  			{
  				return HomeSettlement.MapFaction;
  			}
  			if (PartyBelongedTo != null)
  			{
  				return PartyBelongedTo.MapFaction;
  			}
  			return null;
  		}
  	}
  
  	public List<Alley> OwnedAlleys { get; private set; }
  
  	public bool IsFactionLeader
  	{
  		get
  		{


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.Actions.TakePrisonerAction $dll | Select-String -Pattern '"'ApplyInternal|OnHeroPrisonerTaken|AddPrisoner' -Context 1,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1068ms:

  {
> 	private static void ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled = true)
  	{
  		if (prisonerCharacter.PartyBelongedTo != null)
  		{
  			if (prisonerCharacter.PartyBelongedTo.LeaderHero == prisonerCharacter)
  			{
  				prisonerCharacter.PartyBelongedTo.RemovePartyLeader();
  			}
  			prisonerCharacter.PartyBelongedTo.MemberRoster.RemoveTroop(prisonerCharacter.CharacterObject);
  		prisonerCharacter.ChangeState(Hero.CharacterStates.Prisoner);
> 		capturerParty.AddPrisoner(prisonerCharacter.CharacterObject, 1);
  		if (prisonerCharacter == Hero.MainHero)
  		{
  			if (MobileParty.MainParty.IsDisorganized)
  			{
  				MobileParty.MainParty.SetDisorganized(isDisorganized: false);
  			}
  			PlayerCaptivity.StartCaptivity(capturerParty);
  			if (MobileParty.MainParty.IsCurrentlyAtSea)
  		{
> 			CampaignEventDispatcher.Instance.OnHeroPrisonerTaken(capturerParty, prisonerCharacter);
  		}
  	}
  
  	public static void Apply(PartyBase capturerParty, Hero prisonerCharacter)
  	{
> 		ApplyInternal(capturerParty, prisonerCharacter);
  	}
  
  	public static void ApplyByTakenFromPartyScreen(FlattenedTroopRoster roster)
  	{
  		foreach (FlattenedTroopRosterElement item in roster)
  		{
  			if (item.Troop.IsHero)
  			{
> 				ApplyInternal(PartyBase.MainParty, item.Troop.HeroObject);
  			}
  		}
  		CampaignEventDispatcher.Instance.OnPrisonerTaken(roster);
  	}
  }


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$ilspy='"'C:\\Users\\mikew\\.dotnet\\tools\\ilspycmd.exe'; "'$dll='"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.CampaignSystem.dll'; & "'$ilspy -t TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsRecruitsIssueBehavior $dll | Select-String -Pattern '"'Occupation.Bandit|AddToCounts|CheckIfThereIsSuitable' -Context 2,8 -NoEmphasis" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1237ms:

  				.BeginPlayerOptions()
  				.PlayerOption(new TextObject("{=QbaOoilS}Yes, I have brought you a few men."))
> 				.Condition(() => CheckIfThereIsSuitableRecruitInPlayer() && !_playerReachedRequestedAmount && 
changeDialogAfterTransfer)
  				.NpcLine(npcResponseLine)
  				.Condition(delegate
  				{
  					if (_playerReachedRequestedAmount)
  					{
  						return false;
  					}
  					npcResponseLine.SetTextVariable("GANG_LEADER_NEEDS_RECRUITS_QUEST_NOTABLE_RESPONSE", new 
TextObject("{=70LnOZzo}Very good. Keep searching. We still need more men.[ib:hip2]"));
  					if (MobileParty.MainParty.MemberRoster.Contains(character))
  					{
> 						return character.Occupation == Occupation.Bandit;
  					}
  					return false;
  				}
  				return true;
  			}
  			return false;
  		}
  
> 		private bool CheckIfThereIsSuitableRecruitInPlayer()
  		{
  			bool result = false;
  			foreach (TroopRosterElement item in MobileParty.MainParty.MemberRoster.GetTroopRoster())
  			{
> 				if (item.Character.Occupation == Occupation.Bandit)
  				{
  					result = true;
  					break;
  				}
  			}
  			return result;
  		}
  


codex
**VANILLA CODE**

Decompiled from installed v1.4.6 DLLs under `E:/Steam/.../Win64_Shipping_Client/`.

```csharp
// MbEvent<T>
public void Invoke(T t) => InvokeList(_nonSerializedListenerList, t);
private void InvokeList(EventHandlerRec<T> list, T t) {
  while (list != null) { list.Action(t); list = list.Next; }
}
public void ClearListeners(object o) => ClearListenerOfList(ref _nonSerializedListenerList, o);

// QuestBase
public void CompleteQuestWithSuccess() {
  OnCompleteWithSuccess();
  FinalizeQuest();
  CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Success);
  AfterFinalize();
}
private void ClearRelatedFields() {
  CampaignEventDispatcher.Instance.RemoveListeners(this);
  ...
}

// CampaignEvents
public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>
  TournamentFinished => Instance._tournamentFinished;
public static IMbEvent<MapEvent> OnPlayerBattleEndEvent => Instance._onPlayerBattleEndEvent;

// vanilla issue map-event filter example
if (!mapEvent.IsPlayerMapEvent || _deserterMobileParty == null ||
    !mapEvent.InvolvedParties.Contains(_deserterMobileParty.Party)) return;
if (mapEvent.WinningSide == mapEvent.PlayerSide) ...

// IssuesCampaignBehavior
if (notable.Issue != null && notable.Issue.GetType() == pid.IssueType) return 0f;
if (issue.Value.GetType() == pid.IssueType) num3++;
float num4 = 1f + _additionalFrequencyScore - num2 / num;

// IssueManager cooldown
string name = type.Name;
_issuesCoolDownData[name].Add(data);

// IssueBase
protected virtual bool IssueQuestCanBeDuplicated => false;
if ((value.IsSolvingWithQuest || value.IsSolvingWithAlternative) &&
    value.GetType() == GetType()) result = false;
```

**KNOWN SUSPECTS VERDICTS**

1. **DISPUTED.** Completing from `MbEvent` dispatch is re-entrancy-safe: `MbEvent<T>` walks a linked-list cursor, and `QuestBase.FinalizeQuest()` removes listeners after state becomes finalized. `Bump()`’s `!IsOngoing` guard at [CombatLotrIssue.cs:241](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:241) is sufficient.

2. **DISPUTED.** Exactly one count source is subscribed per variant via `if / else if / else`; war/clan cancellation hooks are registered after the branch for all variants at [CombatLotrIssue.cs:210](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:210).

3. **CONFIRMED-NUANCE.** `WinningSide == PlayerSide` counts any won player map event, including siege/hideout/simulation cases. Vanilla target quests add target/involved-party filters. Under the built docs’ “won battles” simplification, this is not a new defect.

4. **DISPUTED.** `capturer == PartyBase.MainParty` intentionally excludes allied/army captures. `TakePrisonerAction` fires the exact capturer party. `prisoner.MapFaction` is null-guarded at [CombatLotrIssue.cs:231](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:231); `Hero.MapFaction` may be null but does not NRE.

5. **DISPUTED.** Tournament delegate signature is exactly `(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)`. `winner == CharacterObject.PlayerCharacter` at [CombatLotrIssue.cs:236](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:236) is correct.

6. **DISPUTED.** Vanilla uses `Occupation.Bandit` for bandit recruit checks. `DeliverPersonnel` counts live prison roster state and removes with `TroopRoster.AddToCounts(character, -take)` at [DeliverPersonnelLotrIssue.cs:338](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:338), matching vanilla prisoner transfer style.

7. **DISPUTED.** Saveable ids are unique: base `726900801`, locals `101-106` => `726900902-907`. No collision with CareerQuest `726900802` or other TAOM definers.

8. **DISPUTED.** Combat saves `_defId`, target/reward/difficulty, `_variant`, `_progress`, `_log`; Personnel saves `_defId`, target/reward/difficulty, both logs. `_def` is intentionally non-saveable and re-resolved via `EnsureDef()`.

9. **CONFIRMED, WORSE THAN DOCUMENTED.** Spawn scoring/cooldown key on `IssueType`, but vanilla `IssueBase.CheckPreconditions` also blocks accepting another active same-`GetType()` quest globally. Since TAOM maps 27 Combat rows to `typeof(CombatLotrIssue)` and 14 DeliverGoods rows to `typeof(DeliverGoodsLotrIssue)` at [LotrIssuesCampaignBehavior.cs:72](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72), the player can have only one active quest per template type.

10. **DISPUTED for shipped config.** All 43 rows parse; no duplicate ids; all templates/givers/frequencies parse; Combat variants are one of the 3 implemented; all DeliverGoods item ids resolve; no zero-everything or negative reward rows.

11. **DISPUTED.** Runtime text refs and string ids both count 308; no missing referenced key, no raw `{=KEY}` risk found. `SubModule.xml` includes `taom_lotr_issue_strings` for Campaign and CampaignStoryMode at [SubModule.xml:830](C:/Users/mikew/source/repos/TAOM/Main/_Module/SubModule.xml:830).

**FINDINGS**

[HIGH] [Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72) — Vanilla per-type active quest gate — The documented per-type spawn saturation also blocks accepting another active quest of the same generic template type. Practical effect: max one active Combat quest across 27 configs, one active DeliverGoods across 14, one active DeliverPersonnel across 2. Fix: override `IssueQuestCanBeDuplicated => true` for these generic templates if concurrent per-config quests are intended, or use distinct per-config issue types.

[MEDIUM] [Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170) — Config validation — `variant` is passed through unvalidated; `CombatLotrIssue` treats any unknown value as the `else` battle-win route at [CombatLotrIssue.cs:212](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:212). Current XML is clean, but a typo silently changes mechanics. Fix: validate Combat variants against `DefeatRaids`, `CaptureLords`, `WinTournaments`; skip/log unknown values.

[LOW] [docs/features/lotr-issues.md:305](C:/Users/mikew/source/repos/TAOM/docs/features/lotr-issues.md:305) — Documentation accuracy — Saturation note says “all 14 Deliver share `DeliverGoodsLotrIssue`,” but the implementation table and code have 14 DeliverGoods plus 2 DeliverPersonnel. Fix: update the limitation text to describe all three runtime buckets.

**CONFIG CROSS-REFERENCE**

`taom_lotr_issues.xml`: 43 rows total: 27 Combat, 14 DeliverGoods, 2 DeliverPersonnel. Combat variants: 24 `DefeatRaids`, 1 `CaptureLords`, 2 `WinTournaments`. DeliverGoods sources are all `item:<id>` and resolve: `grain/tools/iron` via `DefaultItems`, and `cow/leather/oil/pottery/sheep/stolen_goods/sumpter_horse/velvet` via sandbox item XML. No provider-skipped row found.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 1  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 12060 (child process of PID 56580) has been terminated.
SUCCESS: The process with PID 56580 (child process of PID 35284) has been terminated.
SUCCESS: The process with PID 35284 (child process of PID 48072) has been terminated.
SUCCESS: The process with PID 48072 (child process of PID 35836) has been terminated.
SUCCESS: The process with PID 35836 (child process of PID 33100) has been terminated.
SUCCESS: The process with PID 53744 (child process of PID 44312) has been terminated.
SUCCESS: The process with PID 44312 (child process of PID 50716) has been terminated.
SUCCESS: The process with PID 50716 (child process of PID 54520) has been terminated.
SUCCESS: The process with PID 54520 (child process of PID 33100) has been terminated.
tokens used
393,767
**VANILLA CODE**

Decompiled from installed v1.4.6 DLLs under `E:/Steam/.../Win64_Shipping_Client/`.

```csharp
// MbEvent<T>
public void Invoke(T t) => InvokeList(_nonSerializedListenerList, t);
private void InvokeList(EventHandlerRec<T> list, T t) {
  while (list != null) { list.Action(t); list = list.Next; }
}
public void ClearListeners(object o) => ClearListenerOfList(ref _nonSerializedListenerList, o);

// QuestBase
public void CompleteQuestWithSuccess() {
  OnCompleteWithSuccess();
  FinalizeQuest();
  CampaignEventDispatcher.Instance.OnQuestCompleted(this, QuestCompleteDetails.Success);
  AfterFinalize();
}
private void ClearRelatedFields() {
  CampaignEventDispatcher.Instance.RemoveListeners(this);
  ...
}

// CampaignEvents
public static IMbEvent<CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject>
  TournamentFinished => Instance._tournamentFinished;
public static IMbEvent<MapEvent> OnPlayerBattleEndEvent => Instance._onPlayerBattleEndEvent;

// vanilla issue map-event filter example
if (!mapEvent.IsPlayerMapEvent || _deserterMobileParty == null ||
    !mapEvent.InvolvedParties.Contains(_deserterMobileParty.Party)) return;
if (mapEvent.WinningSide == mapEvent.PlayerSide) ...

// IssuesCampaignBehavior
if (notable.Issue != null && notable.Issue.GetType() == pid.IssueType) return 0f;
if (issue.Value.GetType() == pid.IssueType) num3++;
float num4 = 1f + _additionalFrequencyScore - num2 / num;

// IssueManager cooldown
string name = type.Name;
_issuesCoolDownData[name].Add(data);

// IssueBase
protected virtual bool IssueQuestCanBeDuplicated => false;
if ((value.IsSolvingWithQuest || value.IsSolvingWithAlternative) &&
    value.GetType() == GetType()) result = false;
```

**KNOWN SUSPECTS VERDICTS**

1. **DISPUTED.** Completing from `MbEvent` dispatch is re-entrancy-safe: `MbEvent<T>` walks a linked-list cursor, and `QuestBase.FinalizeQuest()` removes listeners after state becomes finalized. `Bump()`’s `!IsOngoing` guard at [CombatLotrIssue.cs:241](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:241) is sufficient.

2. **DISPUTED.** Exactly one count source is subscribed per variant via `if / else if / else`; war/clan cancellation hooks are registered after the branch for all variants at [CombatLotrIssue.cs:210](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:210).

3. **CONFIRMED-NUANCE.** `WinningSide == PlayerSide` counts any won player map event, including siege/hideout/simulation cases. Vanilla target quests add target/involved-party filters. Under the built docs’ “won battles” simplification, this is not a new defect.

4. **DISPUTED.** `capturer == PartyBase.MainParty` intentionally excludes allied/army captures. `TakePrisonerAction` fires the exact capturer party. `prisoner.MapFaction` is null-guarded at [CombatLotrIssue.cs:231](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:231); `Hero.MapFaction` may be null but does not NRE.

5. **DISPUTED.** Tournament delegate signature is exactly `(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)`. `winner == CharacterObject.PlayerCharacter` at [CombatLotrIssue.cs:236](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:236) is correct.

6. **DISPUTED.** Vanilla uses `Occupation.Bandit` for bandit recruit checks. `DeliverPersonnel` counts live prison roster state and removes with `TroopRoster.AddToCounts(character, -take)` at [DeliverPersonnelLotrIssue.cs:338](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs:338), matching vanilla prisoner transfer style.

7. **DISPUTED.** Saveable ids are unique: base `726900801`, locals `101-106` => `726900902-907`. No collision with CareerQuest `726900802` or other TAOM definers.

8. **DISPUTED.** Combat saves `_defId`, target/reward/difficulty, `_variant`, `_progress`, `_log`; Personnel saves `_defId`, target/reward/difficulty, both logs. `_def` is intentionally non-saveable and re-resolved via `EnsureDef()`.

9. **CONFIRMED, WORSE THAN DOCUMENTED.** Spawn scoring/cooldown key on `IssueType`, but vanilla `IssueBase.CheckPreconditions` also blocks accepting another active same-`GetType()` quest globally. Since TAOM maps 27 Combat rows to `typeof(CombatLotrIssue)` and 14 DeliverGoods rows to `typeof(DeliverGoodsLotrIssue)` at [LotrIssuesCampaignBehavior.cs:72](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72), the player can have only one active quest per template type.

10. **DISPUTED for shipped config.** All 43 rows parse; no duplicate ids; all templates/givers/frequencies parse; Combat variants are one of the 3 implemented; all DeliverGoods item ids resolve; no zero-everything or negative reward rows.

11. **DISPUTED.** Runtime text refs and string ids both count 308; no missing referenced key, no raw `{=KEY}` risk found. `SubModule.xml` includes `taom_lotr_issue_strings` for Campaign and CampaignStoryMode at [SubModule.xml:830](C:/Users/mikew/source/repos/TAOM/Main/_Module/SubModule.xml:830).

**FINDINGS**

[HIGH] [Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs:72) — Vanilla per-type active quest gate — The documented per-type spawn saturation also blocks accepting another active quest of the same generic template type. Practical effect: max one active Combat quest across 27 configs, one active DeliverGoods across 14, one active DeliverPersonnel across 2. Fix: override `IssueQuestCanBeDuplicated => true` for these generic templates if concurrent per-config quests are intended, or use distinct per-config issue types.

[MEDIUM] [Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/LotrIssueConfigProvider.cs:170) — Config validation — `variant` is passed through unvalidated; `CombatLotrIssue` treats any unknown value as the `else` battle-win route at [CombatLotrIssue.cs:212](C:/Users/mikew/source/repos/TAOM/Main/Features/LotrIssues/Templates/CombatLotrIssue.cs:212). Current XML is clean, but a typo silently changes mechanics. Fix: validate Combat variants against `DefeatRaids`, `CaptureLords`, `WinTournaments`; skip/log unknown values.

[LOW] [docs/features/lotr-issues.md:305](C:/Users/mikew/source/repos/TAOM/docs/features/lotr-issues.md:305) — Documentation accuracy — Saturation note says “all 14 Deliver share `DeliverGoodsLotrIssue`,” but the implementation table and code have 14 DeliverGoods plus 2 DeliverPersonnel. Fix: update the limitation text to describe all three runtime buckets.

**CONFIG CROSS-REFERENCE**

`taom_lotr_issues.xml`: 43 rows total: 27 Combat, 14 DeliverGoods, 2 DeliverPersonnel. Combat variants: 24 `DefeatRaids`, 1 `CaptureLords`, 2 `WinTournaments`. DeliverGoods sources are all `item:<id>` and resolve: `grain/tools/iron` via `DefaultItems`, and `cow/leather/oil/pottery/sheep/stolen_goods/sumpter_horse/velvet` via sandbox item XML. No provider-skipped row found.

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 1  
VERDICT: ISSUES FOUND
