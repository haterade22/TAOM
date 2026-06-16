You are performing an adversarial code review of a Bannerlord 1.4.6 total-conversion mod (TAOM). Be skeptical. Confirm or DISPUTE each Known Suspect by reading the actual code. Report findings with file:line and a concrete fix. Do not rubber-stamp.

FEATURE: PlayerAllianceFreedom. Player-founded kingdoms previously could not form alliances in TAOM. Two vanilla gates excluded them: (1) the player can never INITIATE an alliance (no UI path), and (2) a new player kingdom can't clear DefaultAllianceModel's >=50f acceptance score wall, so AI never offers and the vanilla Kingdom->Diplomacy "Propose/Enact Alliance" button stays greyed. This feature: (A) makes any alliance score/permission check involving the player's kingdom bypass the lore "Hostile" block and score +1000 so the vanilla button lights up and AI will offer; (B) adds a conversation dialog so a player kingdom-ruler can propose an alliance to another kingdom's ruler, forming it directly. Design intent confirmed by the user: FULL FREEDOM for the player (ignore lore Hostile), keep structural gates (at-war, already-allied). AI-vs-AI behavior must be unchanged.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
NOTE: "rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid (use "dolguldur").

READ FIRST:
- docs/features/diplomacy.md (existing Diplomacy feature doc)
- docs/reviews/rca-player-alliance-freedom-2026-06-16.md (the deep-review RCA already written for this work -- it documents one HIGH finding ALREADY FIXED: a duplicate <string id="taom_alliance_formed"> collision, renamed to taom_player_alliance_formed. Verify the fix is actually correct + complete; do not re-report it as open unless it is still broken.)

FILES TO REVIEW (TAOM source):
- Main/Features/Diplomacy/IDiplomacyService.cs
- Main/Features/Diplomacy/DiplomacyService.cs
- Main/Features/Diplomacy/Models/TaomAllianceModel.cs
- Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs
- Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs   (NEW)
- Main/Adapters/AllianceAdapter.cs + IAllianceAdapter.cs (consumed by the new service methods)
- Main/SubModule.cs (only the new "new PlayerAllianceProposalBehavior(...)" AddBehavior line near the DiplomacyBehavior registration)
- Main/_Module/ModuleData/taom_module_strings.xml (new taom_alliance_* keys near line 816; note the pre-existing taom_alliance_formed at line ~371)
- TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs (new tests)

VANILLA TARGETS (decompile authoritatively against the INSTALLED v1.4.6 DLLs -- use the project tooling, do NOT trust E:\Decompiled_Bannerlord which may be a different build):
- TaleWorlds.CampaignSystem.GameComponents.DefaultAllianceModel -- especially GetScoreOfStartingAlliance(Kingdom,Kingdom,out TextObject,bool), CanMakeAlliance(Kingdom,Kingdom,Clan,out TextObject,bool), MaxNumberOfAlliances, MaxDurationOfAlliance.
- TaleWorlds.CampaignSystem.GameComponents.DefaultKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms(Kingdom,Kingdom,out TextObject).
- TaleWorlds.CampaignSystem.Election.StartAllianceDecision -- IsAllowed(), CanMakeDecision(out,bool), ApplyChosenOutcome.
- TaleWorlds.CampaignSystem.CampaignBehaviors.AllianceCampaignBehavior (or the IAllianceCampaignBehavior interface) -- StartAlliance(Kingdom,Kingdom). Does StartAlliance require the kingdoms to be at peace? Does it handle the war state, or could it create an allied-AND-at-war contradiction?

KNOWN SUSPECTS (confirm or dispute each, with evidence from the code):

1. SCORE COMPLETENESS. TaomAllianceModel.GetScoreOfStartingAlliance returns base + (+1000 when involvesPlayer). Decompile DefaultAllianceModel.CanMakeAlliance and confirm +1000 actually clears EVERY gate a player kingdom hits for BOTH directions: AI proposes to player (CanMakeAlliance(kingdom=ai, target=player)) AND player proposes via the vanilla button (CanMakeAlliance(kingdom=player, target=ai)). Is there any gate (e.g. a hidden score threshold other than 50f, a per-clan support vote, mercenary check, "already has max alliances" using a per-kingdom count rather than the model's MaxNumberOfAlliances) that the +1000 does NOT bypass? Could base.GetScoreOfStartingAlliance ever return a value below -950 such that +1000 fails to reach 50?

2. MAXNUMBEROFALLIANCES SCOPE. TaomAllianceModel overrides MaxNumberOfAlliances => int.MaxValue. This is a MODEL-GLOBAL property -- it removes the alliance cap for ALL kingdoms, not just the player. Is that an unintended balance side effect on AI-vs-AI diplomacy (AI kingdoms forming unlimited alliances)? NOTE: this override PRE-DATES this feature; report it as an observation but classify whether this feature's changes worsen it.

3. INVOLVESPLAYER SYMMETRY. InvolvesPlayerKingdom (duplicated in both TaomAllianceModel and TaomKingdomDecisionPermissionModel) returns true if EITHER kingdom == Clan.PlayerClan?.Kingdom. Confirm it is null-safe (player has no kingdom) and order-independent. Is duplicating this helper across two model classes a problem (drift risk)? Should it live in the service?

4. DIALOG vs BUTTON COST ASYMMETRY. The dialog (PlayerAllianceProposalBehavior -> FormPlayerAlliance -> IAllianceAdapter.StartAlliance) forms the alliance DIRECTLY with zero influence cost and no kingdom-decision vote. The vanilla Kingdom-screen button routes through StartAllianceDecision (influence cost + force-decision). Is the direct StartAlliance path safe -- does it leave any vanilla invariant unset (e.g. alliance end-time, both-directions ally list, stance) that the StartAllianceDecision path would set? Could forming via the dialog while the vanilla decision system also has a pending StartAllianceDecision for the same pair cause a double-apply or contradictory state?

5. DIALOG GATING. PlayerAllianceProposalBehavior.GetPlayerLedKingdom requires Clan.PlayerClan to be the kingdom's RulingClan; GetConversationRulerKingdom requires the conversation hero's clan to be its kingdom's RulingClan. Confirm this correctly EXCLUDES: a vassal player (not ruler), a non-ruler lord conversation partner, and the player's own kingdom's ruler. Confirm the null-safe chains can't NRE on a clanless/kingdomless conversation hero (wanderer, notable, minor-faction hero). Is "hero_main_options" the right dialog input token, and does adding a player line there risk conflicting with other TAOM dialog registrations on the same token (Messengers, CareerSwitch)?

6. FORMPLAYERALLIANCE GUARD. FormPlayerAlliance re-checks CanPlayerProposeAlliance before StartAlliance. CanPlayerProposeAlliance only checks: non-empty ids, distinct, !AreAtWar, !AreAllied. It does NOT check kingdom existence (AllianceAdapter.FindKingdom returns null -> StartAlliance no-ops silently). It does NOT check the lore Hostile tier (intentional -- full freedom). Is the silent no-op on a bad id acceptable, or should it log/guard? Is there any way the dialog can call FormPlayerAlliance with a stale kingdom (e.g. kingdom eliminated mid-conversation)?

7. REGRESSION. Confirm GetAllianceScoreModifier(a,b,involvesPlayer:false) and IsAllianceDecisionAllowed(a,b,involvesPlayer:false) are behaviorally identical to the pre-feature 2-arg methods, and that the retained 2-arg GetAllianceScoreModifier forwards correctly. Any existing caller of the 2-arg form that now behaves differently?

8. STRING KEYS. Verify the in-session fix: the new dialog notification key is taom_player_alliance_formed (unique), NOT the colliding taom_alliance_formed. Confirm there are no OTHER duplicate <string id> collisions among the 4 new keys (taom_alliance_player_freedom, taom_alliance_propose, taom_alliance_accept, taom_player_alliance_formed) against the rest of taom_module_strings.xml. Confirm every {=key} used in the C# matches an id present in the XML, and no new XML key is dead.

REQUIRED OUTPUT SECTIONS:
- VANILLA CODE: paste the decompiled CanMakeAlliance, StartAllianceDecision.IsAllowed/CanMakeDecision, and AllianceCampaignBehavior.StartAlliance bodies you relied on.
- KNOWN SUSPECTS: CONFIRMED / DISPUTED verdict for each of the 8 above, with evidence.
- ADDITIONAL FINDINGS: anything not in the suspect list (null-safety, lifecycle, save-compat, balance, convention).
- SEVERITY TABLE: # | Severity (HIGH/MED/LOW) | Finding | File:line | Fix.

QUALITY GATES: cite real file:line for every claim; decompile before asserting any vanilla behavior; if you can't verify a vanilla signature, say UNVERIFIED rather than guessing. Distinguish "this feature introduced X" from "X pre-existed."

PRIOR REVIEW LESSONS:
SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; string-key duplication caught by data-flow trace.
FAILURES TO AVOID: do NOT assume empire=Rohan (empire=Dunland, empire_w=Gondor). Do NOT flag vanilla-matching code as a bug. Do NOT skip the hard vanilla-decompile sections. Do NOT re-report the already-fixed duplicate-key issue as open unless it is genuinely still broken.

Write your review to docs/reviews/codex-adversarial-player-alliance-freedom-2026-06-16.md (this stdout IS that file).
