# Adversarial Review: SettlementEconomy (#317) -- tunable town market-gold regeneration

You are performing an adversarial code review of a new TAOM feature. Your job is to find real bugs, not to be agreeable. Dispute anything that does not hold up against the actual code.

FEATURE: `TaomSettlementEconomyModel : DefaultSettlementEconomyModel` overrides ONLY `GetTownGoldChange(Town)` so town market gold regenerates toward a higher, JSON-tunable target. Motivation: users report towns drain to 0 gold and never recover (TAOM drains run ~2x vanilla: ~2.2x computed LOTRLOME item values + 22% more villager deliveries). Shipped config buffs the base term (25000 vs vanilla 10000); slope (12) and rate (0.25) stay vanilla. MCM master toggle ON by default; OFF = base-call passthrough. Two Python data tools accompany it (secondary scope).

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".
(This feature has no culture/kingdom IDs in its config -- the cheatsheet is for context only.)

VERSION NOTE: The installed game is Bannerlord 1.4.6. The dump at E:\Decompiled_Bannerlord\ is 1.4.5 (fine for browsing). A verified 1.4.6 decompile cache exists at C:\Users\mikew\.taom-src\v1.4.6\ -- PREFER those files for signatures. For types not cached, run: pwsh tools/taom-src.ps1 path <Full.Type.Name> (from the repo root) or ilspycmd against E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll.

READ FIRST:
- docs/features/settlement-economy.md (feature doc: architecture, defaults rationale, castle-exclusion claim)
- docs/reference/engine/settlement-economy-food-prosperity.md (new "Town gold" section: drain/inflow map)
- Main/_Module/ModuleData/settlement_economy/settlement_economy_config.json
- docs/reviews/rca-settlement-economy-2026-07-02.md (deep-review findings already fixed -- do not re-report these)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence):
1. ROUNDING PARITY: SettlementEconomyService.ComputeTownGoldChange claims bit-identical parity with vanilla when configured 10000/12/0.25. Vanilla: `float num = 10000f + town.Prosperity * 12f - (float)town.Gold; return MathF.Round(0.25f * num);` where TaleWorlds.Library.MathF.Round(float f) = (int)Math.Round(f). TAOM: `float deficit = baseGold + prosperity * perProsperity - currentGold; return (int)Math.Round(rate * deficit);`. Check the float op order and the int-to-float conversion of currentGold: is `... - currentGold` (int operand, implicit conversion) bit-identical to `... - (float)town.Gold` (explicit)? Any case where operand promotion differs (double vs float intermediate)?
2. MCM TIMING FAIL-OPEN: `Enabled => TaomSettings.Instance?.EnableSettlementEconomyTuning ?? true`. If TaomSettings.Instance is null when the first DailyTickTown fires (early campaign load, MCM not yet initialized), the feature silently runs ENABLED. Is fail-open correct here, and can Instance actually be null at that point? Compare with the sibling TaomSettlementFoodModel which uses the identical pattern.
3. CASTLE EXCLUSION: The feature deliberately has NO castle gate, claiming GetTownGoldChange is never called for castles (sole caller ItemConsumptionBehavior.UpdateTownGold via DailyTickTownEvent over Town.AllTowns, plus MakeConsumptionAllTowns also Town.AllTowns only). Independently verify on 1.4.6: enumerate ALL callers of GetTownGoldChange across TaleWorlds.CampaignSystem, SandBox, StoryMode, CustomBattle assemblies. If ANY caller can pass a castle, the model would apply the buffed 25000 base to castles -- report as HIGH.
4. VANILLA-CONSTANT DRIFT: SettlementEconomyService hardcodes vanilla constants (10000/12/0.25) for the null-config defensive path only; the DISABLED path uses base.GetTownGoldChange (drift-safe). Confirm the null-config path is genuinely unreachable in production (provider never returns null) and that no code path calls the service with a null config other than tests.
5. NEGATIVE REGEN + ChangeGold CLAMP: Above the target the formula returns a negative change (mean-reversion, intentionally unclamped). SettlementComponent.ChangeGold clamps gold at >= 0. With a MISCONFIGURED but validation-passing config (e.g. base=0, rate=1), can the model produce pathological oscillation or a permanent 0-gold state that vanilla could not? Is the validation range ([0,200000], [0,100], [0,1]) actually sufficient to prevent a config that reintroduces the "towns stay broke" bug?
6. REGISTRATION ORDER / LAST-WINS: campaignStarter.AddModel(new TaomSettlementEconomyModel(...)) is added in RegisterCulturalFeatModels. Confirm on 1.4.6 that GameModelsManager/CampaignGameStarter resolves the LAST-registered SettlementEconomyModel (reverse iteration) and that no other mod-loaded or vanilla model registered later could shadow it within TAOM's own registration sequence.

FILES (primary scope, C#):
- Main/Features/SettlementEconomy/SettlementEconomyConfig.cs
- Main/Features/SettlementEconomy/ISettlementEconomyConfigProvider.cs
- Main/Features/SettlementEconomy/SettlementEconomyConfigProvider.cs
- Main/Features/SettlementEconomy/ISettlementEconomyService.cs
- Main/Features/SettlementEconomy/SettlementEconomyService.cs
- Main/Features/SettlementEconomy/SettlementEconomyIoC.cs
- Main/Features/SettlementEconomy/Models/TaomSettlementEconomyModel.cs
- Main/IoC.cs (SettlementEconomyIoC.RegisterSettlementEconomyFeature call)
- Main/SubModule.cs (AddModel line in RegisterCulturalFeatModels, ~line 518)
- Main/Features/TaomSettings.cs (EnableSettlementEconomyTuning, "Settlement Economy" group)
- TAOM.Tests/Features/SettlementEconomy/SettlementEconomyServiceTests.cs
- TAOM.Tests/Features/SettlementEconomy/SettlementEconomyConfigProviderTests.cs
- Main/_Module/ModuleData/settlement_economy/settlement_economy_config.json

FILES (secondary scope, Python tools -- already deep-reviewed, idempotency fixed; look for what that review missed):
- tools/rebalance_settlement_prosperity.py (quantile-map math, regex write discipline against the LIVE external TAOM_Map settlements.xml)
- tools/analyze_settlement_prosperity.py (read-only report)

REQUIRED SECTIONS:

VANILLA CODE: Decompile and paste as code blocks (1.4.6 cache preferred): DefaultSettlementEconomyModel.GetTownGoldChange; SettlementEconomyModel (abstract base -- method visibility/virtuality); ItemConsumptionBehavior.DailyTickTown + UpdateTownGold + MakeConsumptionAllTowns; SettlementComponent.ChangeGold + Gold property; the CampaignPeriodicEventManager DailyTickTownEvent ticker initialization. Enumerate GetTownGoldChange callers across all shipped assemblies.

DEEP ANALYSIS (concrete scenarios):
A. A town at prosperity 3500, gold 0, feature ON, shipped config: compute the daily change sequence for 10 days and confirm convergence to 67000. Then toggle OFF mid-campaign: confirm the next tick uses vanilla math on the accumulated gold (negative change expected -- gold above the 52000 vanilla target) and that this is benign (no clamp weirdness, no save impact).
B. Save/load: Town.Gold is SaveableProperty(50). Model re-registered per campaign start. Any path where a save made with the feature ON misbehaves when loaded with the feature OFF or with the module removed entirely?
C. The player sells 40000 gold of loot to a town holding 30000: SellItemsAction/InventoryScreenHelper wallet mechanics -- does the raised regen interact with the 70% town commission skim (SellItemsAction tax lines) in any way the feature doc's "drains are goods-bounded" claim gets wrong?
D. MCM toggle flip DURING the same day tick window: Enabled is read per call -- any torn-state risk across the town loop (some towns vanilla, some buffed, same tick)? Is that acceptable?

CONFIG CROSS-REFERENCE: settlement_economy_config.json keys vs SettlementEconomyConfig properties vs validation ranges vs docs/features/settlement-economy.md table vs the MCM hint text in TaomSettings.cs. Report ANY drift.

FINDINGS OR OBSERVATIONS: Severity P1 (ship-blocking) / P2 (should fix) / P3 (nice-to-have). For each: file, line, evidence, concrete failure scenario, suggested fix. If a Known Suspect is DISPUTED, show the disproving code.

QUALITY GATES:
- Paste decompiled vanilla code for every claim about engine behavior. No assertion without code.
- Grep before claiming anything is "missing".
- If you cannot verify something, say UNVERIFIED -- do not guess.
- Do not re-report the already-fixed deep-review findings (tool idempotency flags, null-town guard, BOM idiom) unless the FIX itself is wrong.

PRIOR REVIEW LESSONS:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Cross-party propagation caught by decompiling MobileParty (NavalTravel review 62).
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Write your review to stdout (it is redirected to docs/reviews/codex-adversarial-settlement-economy-2026-07-02.md). Start with a one-paragraph verdict, then Known Suspects verdicts, then the required sections.
