You are performing an adversarial code review of a Bannerlord 1.4.6 total-conversion mod (TAOM). Be skeptical. Confirm or DISPUTE each Known Suspect by reading the actual code + decompiling the installed vanilla DLLs. Report findings with file:line and a concrete fix. Do not rubber-stamp; do not invent bugs.

FEATURE: PlayerAllianceDurability (follow-up to #284, the player-alliance-freedom feature). In-game report: a player who founded their own kingdom formed an alliance via the Kingdom->Diplomacy button, but the encyclopedia showed no ally shortly after. Root cause (claimed, decompile-verified): vanilla AllianceCampaignBehavior.OnWarDeclared calls EndAlliance whenever war is declared between two allied kingdoms, and TAOM's EndAlliance patch protects ONLY Permanent-tier alliances -- a player alliance is Neutral tier -> unprotected -> any war on the pair dissolves it. The fix blocks the involuntary WAR (not EndAlliance): DiplomacyService.IsWarAllowed now returns false when the kingdom the player RULES is one of the pair AND they are currently allied. EndAlliance is left untouched so the player can still manually break the alliance. Plus temporary [Diplomacy][diag] logging.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
NOTE: "rohan" is NOT valid (Rohan = vlandia). "dol_guldur" is NOT valid (use dolguldur).

READ FIRST:
- docs/features/diplomacy.md ("Player Alliance Freedom subsystem" incl. the Durability + Diagnostics bullets)
- docs/reviews/rca-player-alliance-freedom-2026-06-16.md (the 2026-06-17 follow-up section documents this exact fix + the claimed root cause)

FILES TO REVIEW (TAOM source):
- Main/Adapters/IAllianceAdapter.cs + Main/Adapters/AllianceAdapter.cs (new GetPlayerRuledKingdomId; note AreAllied/FindKingdom do Kingdom.All.FirstOrDefault linear scans)
- Main/Features/Diplomacy/DiplomacyService.cs (IsWarAllowed -- the new player+ally war-block branch + a [diag] log)
- Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartAlliance_Patch.cs (NEW temporary diagnostic Postfix, [HarmonyPatchCategory("Patch11_Diplomacy")])
- Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_EndAlliance_Patch.cs (existing Prefix + a new [diag] log line)
- Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs (Prefix that consumes IsWarAllowed via AllianceActionHook)
- Main/Features/Diplomacy/Hooks/AllianceActionHook.cs (ShouldPreventWarDeclaration -> !IsWarAllowed)
- Main/Features/Diplomacy/PlayerKingdomHelper.cs (GetPlayerRuledKingdom)
- Main/SubModule.cs (the new AllianceCampaignBehavior_StartAlliance_Patch.Initialize(logger) ~line 172; the _harmony.PatchCategory("Patch11_Diplomacy") call)
- TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs (4 new IsWarAllowed tests)

VANILLA TARGETS (decompile authoritatively against the INSTALLED v1.4.6 DLLs -- use tools/taom-src.ps1 / ilspycmd; do NOT trust E:\Decompiled_Bannerlord):
- TaleWorlds.CampaignSystem.Actions.DeclareWarAction -- ALL public Apply* entry points + the private ApplyInternal + where FactionManager.DeclareWar and CampaignEventDispatcher.OnWarDeclared are called. The load-bearing question: is ApplyInternal the SINGLE chokepoint through which every war declaration passes, so a Prefix that skips it actually prevents the stance change + the WarDeclared event?
- TaleWorlds.CampaignSystem.CampaignBehaviors.AllianceCampaignBehavior -- OnWarDeclared (does it call EndAlliance on an allied pair?), EndAlliance, StartAlliance, RegisterEvents (does it subscribe OnWarDeclared to CampaignEvents.WarDeclared?), StartCallToWarAgreement / ApplyByCallToWarAgreement.
- TaleWorlds.CampaignSystem.FactionManager.DeclareWar -- does it fire WarDeclared itself, or only set stance? Is it called from anywhere OTHER than DeclareWarAction.ApplyInternal in the alliance/war flow?
- Clan.PlayerClan / Clan.Kingdom / Kingdom.RulingClan / Kingdom.StringId.

KNOWN SUSPECTS (confirm or dispute each with evidence):

1. CHOKEPOINT COMPLETENESS. Blocking DeclareWarAction.ApplyInternal (the private method, intercepted by the existing Patch11 Prefix) is claimed to prevent ALL war declarations + therefore the OnWarDeclared->EndAlliance auto-break. Decompile DeclareWarAction: do all 8-ish public Apply* methods funnel through ApplyInternal? Is FactionManager.DeclareWar called ONLY from inside ApplyInternal? Is there ANY path that declares war (sets StanceType.War) or fires CampaignEvents.WarDeclared WITHOUT going through ApplyInternal -- which would let the player alliance still break? This is the most important question.

2. PLAYER WAR-ON-ALLY TRAP. IsWarAllowed now blocks the player from declaring war on their own current ally. Confirm the player can still END the alliance (vanilla "break alliance" -> AllianceCampaignBehavior.EndAlliance directly, NOT via a war declaration) -- i.e. EndAlliance is reachable without a war, and TAOM's EndAlliance patch does NOT block it for a Neutral-tier player alliance (it only blocks Permanent). Is there any soft-lock where the player is allied + cannot break it + cannot war? Also: when the player tries to declare war on an ally and it's silently blocked (log only, no in-game message), is that acceptable (it matches TAOM's pre-existing Permanent/same-alignment silent block via the same patch) or a real UX trap?

3. CALL-TO-WAR INTERPLAY. AllianceCampaignBehavior.StartAlliance and OnWarDeclared create ProposeCallToWarAgreement / call StartCallToWarAgreement -> ApplyByCallToWarAgreement -> ApplyInternal. Could the new IsWarAllowed block cause a STUCK or inconsistent state when a call-to-war agreement would declare war on a pair where the player rules one side AND is allied with the other? Trace whether this can produce a soft-lock, a swallowed decision, or just (correctly) prevent the player being dragged to war with their own ally.

4. POSTFIX PARAM-NAME BINDING. AllianceCampaignBehavior_StartAlliance_Patch declares Postfix(Kingdom proposerKingdom, Kingdom receiverKingdom). Harmony binds Postfix params by NAME. Decompile StartAlliance and confirm the real parameter names are EXACTLY proposerKingdom + receiverKingdom (a mismatch = silent no-op, defeating the diagnostic). Same check for EndAlliance Prefix (kingdom1/kingdom2).

5. DIAGNOSTIC COST + GATING. IsWarAllowed is called per war declaration (AI war-decision evaluation can probe many pairs). Confirm: (a) the new branch's AreAllied() call (a Kingdom.All linear scan x2 via FindKingdom) is SHORT-CIRCUITED behind `playerKingdomId != null && (a==player || b==player)` so it does NOT run for AI-vs-AI wars; (b) the [diag] string interpolation at the block is INSIDE the if-block (only allocates when actually blocking a player-ally war), not unconditional; (c) GetPlayerRuledKingdomId() runs once per call but is cheap (property reads, no scan/alloc). Is the added cost acceptable at war-declaration frequency, or is there a real hot-path regression?

6. REGRESSION. Confirm IsWarAllowed is behaviorally identical to before for: two AI kingdoms; a vassal/mercenary player (GetPlayerRuledKingdomId returns null); a player-ruled kingdom NOT allied with the other. AI-vs-AI war + the existing Permanent-tier and same-alignment war blocks must be unchanged. Cross-check the 4 new tests actually pin these.

7. GetPlayerRuledKingdomId CORRECTNESS. It returns kingdom.StringId when Clan.PlayerClan != null && kingdom != null && kingdom.RulingClan == Clan.PlayerClan, else null. Confirm: (a) correct for a player who founded their own kingdom (player clan IS the ruling clan); (b) correctly null for a vassal player; (c) the Clan.Kingdom / Kingdom.RulingClan getters are plain fields (not computed getters that NRE before the null guard). Any edge where a player rules but RulingClan != PlayerClan (e.g. player clan leader is not Hero.MainHero)?

REQUIRED OUTPUT SECTIONS:
- VANILLA CODE: paste the decompiled DeclareWarAction.ApplyInternal (+ one public Apply* showing it funnels through), AllianceCampaignBehavior.OnWarDeclared + EndAlliance signatures + RegisterEvents subscription, and StartAlliance signature (for the param-name check).
- KNOWN SUSPECTS: CONFIRMED / DISPUTED verdict for each of the 7, with evidence.
- ADDITIONAL FINDINGS: anything not in the suspect list (null-safety, save-compat, lifecycle, balance, a war path that bypasses the chokepoint, a diag that never fires).
- SEVERITY TABLE: # | Severity (HIGH/MED/LOW) | Finding | File:line | Fix.

QUALITY GATES: cite real file:line for every claim; decompile before asserting any vanilla behavior; if you can't verify a vanilla signature say UNVERIFIED rather than guessing. Distinguish "this change introduced X" from "X pre-existed" (e.g. FindKingdom's linear scan + the silent-block UX pattern both pre-date this change). The diagnostics are explicitly TEMPORARY (to be stripped after in-game sign-off) -- do not flag their existence as a defect, but DO flag if a diag would never fire or logs on a hot path unconditionally.

PRIOR REVIEW LESSONS:
SUCCESSES: vanilla decompilation catches missing gates + chokepoint bypasses; lifecycle tracing catches stale state; independently decompiling to settle a load-bearing assumption beats asserting it.
FAILURES TO AVOID: do NOT assume empire=Rohan (empire=Dunland, empire_w=Gondor). Do NOT flag vanilla-matching code as a bug. Do NOT rate a PRE-EXISTING condition (FindKingdom linear scan; silent-block UX) as a blocking finding for a change that did not introduce it -- note it as pre-existing. Do NOT claim a string allocates "unconditionally" without checking whether it's inside the guarded if-block.

Write your review to docs/reviews/codex-adversarial-player-alliance-durability-2026-06-17.md (this stdout IS that file).
