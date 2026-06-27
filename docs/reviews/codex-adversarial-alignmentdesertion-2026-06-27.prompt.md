Adversarial code review of the TAOM "AlignmentDesertion" feature (new, 2026-06-27). Your job is to find real bugs, not to praise. Be skeptical. For every claim, read the actual source and (for TaleWorlds APIs) verify against the installed v1.4.6 DLLs. Report CONFIRMED or DISPUTED with file:line evidence.

FEATURE
Each in-game day, troops whose CULTURE alignment (Free vs Evil) is OPPOSED to their lord's KINGDOM alignment desert. An Evil-aligned lord sheds Good (Free) troops; a Free-aligned lord sheds Evil troops. Applies to mobile parties (incl. army member parties) and settlement garrisons, for both AI and player, each MCM-gated. Default 50%/day per troop type, minimum 1.

ARCHITECTURE
Pure AlignmentDesertionService (decision matrix, no TaleWorlds types) + thin AlignmentDesertionBehavior (subscribes CampaignEvents.DailyTickPartyEvent + DailyTickSettlementEvent; does roster I/O only). No Harmony patch, no GameModel override, no adapter, no SyncData (recomputed daily). Reuses the Execution IAlignmentService: owner side via GetKingdomSide(kingdom StringId), troop side via the NEW GetCultureSide(culture StringId). alignment.json gained "gondor":"free" and "mordor":"evil" because Gondor/Mordor troops carry Culture.gondor / Culture.mordor while alignment.json previously held only the empire_w / empire_s KINGDOM keys.

TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID -- Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
docs/features/alignment-desertion.md
Main/_Module/ModuleData/execution/alignment.json
Main/_Module/ModuleData/alignment_desertion/alignment_desertion_config.json

KNOWN SUSPECTS (confirm or dispute each, with evidence)
1. Symmetric desertion + count math. AlignmentDesertionService.CalculateDesertion: desertCount = Math.Max(1, (int)(troop.Count * rate)) then Math.Min(desertCount, troop.Count). Verify: min-1 floor never removes from an empty/0 stack; cap never exceeds the stack; rate is always finite [0,1] from BOTH the MCM slider and the validated JSON; a rate of 0 with a non-empty opposed stack still removes 1 (is that intended? the feature's "Enabled=false" is the off switch, not rate=0 -- is min-1-at-rate-0 a surprise?).
2. Owner side resolution + exclusions. Party path uses party.LeaderHero?.Clan?.Kingdom; garrison path uses settlement.OwnerClan?.Kingdom; both early-return on kingdom==null. Confirm mercenary clans (serving a kingdom as mercenaries) resolve to the EMPLOYER kingdom's side -- is shedding a mercenary's opposed troops intended, or should mercenaries be exempt? Confirm bandit/caravan/villager/militia parties are excluded (no LeaderHero.Clan or no Kingdom). Confirm a rebel/independent clan with kingdom==null is correctly skipped.
3. GetCultureSide shares the GetKingdomSide dictionary. Both delegate to a private GetSide(id) over one _kingdomSides dict. Is there any id that is BOTH a kingdom StringId and a DIFFERENT culture's StringId with a different intended side? Walk every key in alignment.json. (Gondor/Mordor are the known mismatches and are handled by explicit gondor/mordor entries.) Could a troop whose culture is a vanilla id (vlandia/empire/aserai/khuzait/sturgia/battania) resolve to the wrong side?
4. Cross-feature interaction with AlignmentRecruitment. Both read execution/alignment.json via IAlignmentService. Confirm the 2 new culture keys (gondor/mordor) are INERT for AlignmentRecruitment, which only ever passes kingdom StringIds (buyerHero.Clan.Kingdom.StringId, sellerHero.CurrentSettlement.MapFaction.StringId) -- never "gondor"/"mordor". Read Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs.
5. Roster mutation safety. AlignmentDesertionBehavior snapshots the roster into POCOs, then mutates via CharacterObject.Find + FindIndexOfTroop + AddToCounts(character, -toRemove). Confirm: no mutation-while-iterating; the re-read of GetElementNumber(index) before clamping handles a stack that shrank between snapshot and apply; heroes/companions (IsHero) are never removed; a troop present in the snapshot but gone at apply time (index<0) is skipped; AddToCounts with a negative count is the correct removal idiom and does not corrupt the roster.
6. Toggle semantics. 6 MCM props (EnableAlignmentDesertion, AlignmentDesertionRate, EnableAlignmentDesertionPlayer/Ai/Parties/Garrisons). Confirm each gates real behavior and the hint text matches the implementation (e.g. "Apply To Mobile Parties OFF" must still let garrisons desert, and vice versa). Confirm a player who is also a kingdom ruler is treated as player-owned (isPlayerOwned = clan == Clan.PlayerClan), not AI.
7. Lifecycle / save-load. No SyncData. Confirm desertion is fully recomputed each day with no persisted state, so a save mid-day and reload cannot double-apply or skip. Confirm the DailyTick events cannot fire before Campaign.Current is initialized (Clan.PlayerClan dereferences Campaign.Current).

FILES -- production
Main/Features/AlignmentDesertion/AlignmentDesertionService.cs
Main/Features/AlignmentDesertion/IAlignmentDesertionService.cs
Main/Features/AlignmentDesertion/AlignmentDesertionConfig.cs
Main/Features/AlignmentDesertion/AlignmentDesertionConfigProvider.cs
Main/Features/AlignmentDesertion/IAlignmentDesertionConfigProvider.cs
Main/Features/AlignmentDesertion/AlignmentDesertionSettingsProvider.cs
Main/Features/AlignmentDesertion/IAlignmentDesertionSettingsProvider.cs
Main/Features/AlignmentDesertion/AlignmentDesertionIoC.cs
Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs
Main/Features/Execution/AlignmentService.cs
Main/Features/Execution/IAlignmentService.cs
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs

FILES -- config + wiring
Main/_Module/ModuleData/execution/alignment.json
Main/_Module/ModuleData/alignment_desertion/alignment_desertion_config.json
Main/Features/TaomSettings.cs  (group "World/Alignment Desertion", around the EnableAlignmentDesertion* properties)
Main/IoC.cs  (AlignmentDesertionIoC.RegisterAlignmentDesertionFeature)
Main/SubModule.cs  (campaignStarter.AddBehavior(new ...AlignmentDesertionBehavior))

FILES -- tests
TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionServiceTests.cs
TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionConfigProviderTests.cs
TAOM.Tests/Features/Execution/AlignmentServiceTests.cs

REQUIRED SECTIONS IN YOUR OUTPUT
1. KNOWN SUSPECTS -- CONFIRMED/DISPUTED verdict for each of the 7 above, with file:line evidence.
2. ENGINE BEHAVIOR -- for the TroopRoster mutation + DailyTick event lifecycle, state what the installed v1.4.6 engine actually does (decompile if needed) and whether the code relies on it correctly.
3. DECISION MATRIX -- independently re-derive the desert/keep decision for these cases and check the code agrees: Evil owner + Free troop; Free owner + Evil troop; same-side; Neutral owner (umbar); Neutral-culture troop; hero troop; kingdomless owner; rate=0; rate=1; count=1.
4. CONFIG CROSS-REFERENCE -- every id in alignment.json + alignment_desertion_config.json against the cheatsheet; flag any wrong/typo id.
5. ANYTHING THE DEEP-REVIEW MISSED -- the 5-agent Claude deep-review found 0 HIGH/MED and 3 LOW (localization gap, pending issue number, a now-fixed missing config-provider test). Find what it did not.
6. FINDINGS table: # | Severity | File:Line | Bug | Fix.

QUALITY GATES
Do not flag vanilla-matching code as a bug. Do not assume an API exists -- verify against the installed DLLs. Use the cheatsheet; empire=Dunland NOT Rohan. If you find nothing in a section, say so explicitly -- do not skip a section. Prefer a few high-confidence findings over many speculative ones.

OUTPUT
Write your full review to stdout (it is being captured to docs/reviews/codex-adversarial-alignmentdesertion-2026-06-27.md).
