You are an adversarial reviewer for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion for Mount & Blade II: Bannerlord, C# .NET Framework 4.7.2. Assume the authors missed something. Read AGENTS.md and .ai/review-reference.md first and follow the reviewer role: report defects with evidence; do NOT edit any file.

Scope: the six commits 259ae2cba..86ef5c7af on branch feature/career-forks (base 9bb78ae5b). Use `git log -p 9bb78ae5b..86ef5c7af`. The installed game was just updated to v1.5.5 (engine bump is another session's job); engine facts must come from the installed DLLs under E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client and Modules\SandBox\bin\Win64_Shipping_Client (decompile with ilspycmd), not from memory. E:\Decompiled_Bannerlord is an older dump: browse only.

What changed:
- #766: CareerConfigProvider gives each nested choice its enclosing ChoiceGroup id (it was empty for all 2,100 shipped group choices), switching on the career screen tier gate, KeystoneExclusivityRule and the foreign-choice repair pass. CareerRegistry gives no owner to a group/root claimed by two careers or a choice id parsed twice, so RepairForeignChoices keeps it. CareerCampaignBehavior shows a refund message.
- #767: SpecialResourceService.ScaleEarned (public, on ISpecialResourceService) scales every earning path; BuildUpkeepLines is the single upkeep computation used by the daily breakdown and desertion; map-bar per-event rows render through ScaleEarned.
- #768: IPartySizeCacheInvalidator (InvalidateAll for the MCM watcher, InvalidateLedBy for careers) busts PartyBase.PartySizeLimit's roster-version cache after CareerPassiveService.RefreshCache; registered in IoC.RegisterCoreServices. TaomPartySizeModel applies the career PartySize to party.LeaderHero only and after every AddFactor. Rohan text and translation rows reset. taom.career_perks probe lines.
- #770: SpecialResourceEarnPolicy.PaysBattleCredit(BattleTypes, beatAFieldParty); OnMapEventEnded pays the battle credit unless a raid/extortion event beat no field party (non-militia, non-villager mobile party with HealthyManCountAtStart > 0); OnRaidCompleted pays per_raid only when the village state is Looted and the player won. Tournament and hideout toasts silent on zero.
- #771: SpecialResourcesBehavior keeps one static ScreenManager.OnPushScreen handler per process.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: docs/reviews/rca-career-e1e3-2026-10-08.md (what a 50-agent Claude review already found and fixed; do not re-report those unless the fix is wrong), docs/features/career-system.md, docs/features/special-resources.md.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence):
1. #766 makes RepairForeignChoices delete group choices on session launch. Is there any legitimate save state (Player Switcher hero, co-op joiner via JoinReconciliationService, career switch, the commented far_harad_halftroll career) in which a hero holds a choice that now resolves to a different career and is wrongly deleted?
2. #770: is beatAFieldParty right in every raid/extortion shape on v1.5.5 (player raids a village with a lord inside, player defends a village, player in an AI army raid, bandits, caravans inside a village)? Is the village state already Looted when RaidCompleted fires, and are defeated parties still in enemySide.Parties at OnMapEventEnded? Can a hideout "send troops" clear pay per_hideout_clear twice on v1.5.5 (HideoutCampaignBehavior raises the completed event, then HideoutEventComponent may raise it again)?
3. #768: InvalidateLedBy uses the union of the previous and new passive-cache hero ids. Can a hero's PartySize change without that hero being in the set (career switch, ClearCareer, hero death, Player Switcher), leaving a stale cached limit? Is any caller of RefreshCache off the main thread?
4. #771: does the static handler field misbehave across a new campaign, a save load, Game Over, or the test suite?
5. #767: does ScaleEarned's always-multiply plus finite-and-positive gate change any shipped payout at gain 0, and does BuildUpkeepLines keep float parity with the old breakdown?

REQUIRED SECTIONS:
- VANILLA CODE: paste the decompiled v1.5.5 members you relied on (MapEvent.FinalizeEventAux order, RaidEventComponent loot and RaidCompleted, Village.VillageState, MapEventParty.HealthyManCountAtStart, PartyBase.PartySizeLimit, TroopRoster.UpdateVersion, ScreenManager.OnPushScreen).
- DEEP ANALYSIS of the five suspects with concrete scenarios.
- CONFIG CROSS-REFERENCE: special_resources_config.xml per_raid / per_hideout_clear values against the docs.
- FINDINGS: each with severity (P1/P2/P3), file:line, evidence, and a fix; say UNVERIFIED where you could not prove it.

QUALITY GATES: every claim cites a file:line you read; no finding about code that matches vanilla behaviour on purpose; no style-only findings.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-career-e1e3-2026-10-09.md; do NOT write that file yourself.
