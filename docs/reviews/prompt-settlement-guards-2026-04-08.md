# Codex Adversarial Review Prompt: Settlement Guards

Copy everything below this line and dispatch to Codex CLI.

---

Adversarial review of SettlementGuards.

Per-settlement guard customization system. Harmony prefixes on two private methods in vanilla GuardsCampaignBehavior (SandBox.dll) to inject custom guard troops per settlement via XML config. Uses settlement->clan->culture fallback chain with weighted random selection and spawn-point filtering. Risk profile: medium (reflection on private methods, equipment assembly delegation). Config is XML-driven with 14 Gondor settlements + 16 culture spear mappings. 27 tests passing.

TAOM ID CHEATSHEET (prevents false positives from ID confusion):
Kingdom StringIds: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture StringIds (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture StringIds (XSLT/vanilla): vlandia (Rohan), empire (Dunland), empire_s (Mordor-region), empire_w (Gondor-region), battania (Khand), aserai (Harad), khuzait (Easterlings), sturgia (Dale)
NOTE: Kingdom IDs and Culture IDs differ! "rohan" is NOT a valid ID. Rohan's kingdom=vlandia, culture=vlandia.

READ FIRST (required context):
a) docs/research/settlement-guard-system.md -- full research document on vanilla guard spawning
b) Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml -- the XML config

FILES (service -- business logic):
a) Main/Features/SettlementGuards/ISettlementGuardService.cs
b) Main/Features/SettlementGuards/SettlementGuardService.cs
c) Main/Features/SettlementGuards/ISettlementGuardConfigProvider.cs
d) Main/Features/SettlementGuards/SettlementGuardConfigProvider.cs

FILES (domain):
a) Main/Features/SettlementGuards/Domain/GuardEntry.cs
b) Main/Features/SettlementGuards/Domain/GuardPool.cs
c) Main/Features/SettlementGuards/Domain/SettlementGuardContext.cs

FILES (entry points -- Harmony patches):
a) Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs -- Prefix on private TakeGuardAgentDataFromGarrisonTroopList
b) Main/Features/SettlementGuards/Hooks/GuardsCampaignBehavior_GetSuitableSpear_Patch.cs -- Prefix on private static GetSuitableSpear

FILES (IoC + registration):
a) Main/Features/SettlementGuards/SettlementGuardsIoC.cs
b) Main/IoC.cs (line with SettlementGuardsIoC)
c) Main/SubModule.cs (manual patch registration -- search for "SettlementGuard")

FILES (config):
a) Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml

FILES (tests -- 27 tests, 100% service + config provider coverage):
a) TAOM.Tests/Features/SettlementGuards/SettlementGuardConfigProviderTests.cs (13 tests)
b) TAOM.Tests/Features/SettlementGuards/SettlementGuardServiceTests.cs (14 tests)

=== KNOWN SUSPECTS (confirm or dispute each with evidence) ===

1. REFLECTION CALL ON PrepareGuardAgentDataFromGarrison: The TakeGuardAgentData patch calls AccessTools.Method to find PrepareGuardAgentDataFromGarrison and invokes it via reflection on EVERY guard spawn. Is this method actually static in v1.3? Does the reflection lookup get cached, or is it re-resolved every call? Read the decompiled method signature to confirm.

2. DEAD HarmonyPatchCategory ATTRIBUTE: Both patch classes have [HarmonyPatchCategory("Patch28_SettlementGuards")] but SubModule.cs uses manual _harmony.Patch() instead of _harmony.PatchCategory(). Confirm whether the attribute is dead code and whether having both attribute + manual patch could cause double-patching.

3. SPAWN POINT TAG AMBIGUITY: Vanilla calls TakeGuardAgentDataFromGarrisonTroopList(culture, spear=true) for BOTH sp_guard_castle and sp_guard_with_spear guards. The patch cannot distinguish these two cases from the boolean parameters alone. The current fix returns null for spawnPointTag when spear=true, falling back to the full pool. Is this the correct approach, or should we track the calling context differently?

4. TROOP ID VALIDITY: Cross-reference every troop ID in settlement_guards_config.xml against Main/_Module/ModuleData/troops/troops_gondor.xml. Do all referenced troops actually exist? Check: gondor_mt_fountain_guard, gondor_osg_dome_guard, gondor_pel_anchor_guard, gondor_da_swan_guard, gondor_lg_haven_guard, gondor_ca_warden, gondor_ith_captain, gondor_loss_vet_guard, gondor_har_frontier_guard, gondor_anf_guardsman, gondor_met_glaive_guard, gondor_ring_guardsman, gondor_lin_high_guard.

5. SETTLEMENT ID VALIDITY: Cross-reference every settlement ID in settlement_guards_config.xml against Main/_Module/ModuleData/settlements.xml. Do town_EW1 through town_EW7, castle_EW2/4/6/7/8/11/12/13/14/15/16 all exist?

6. CULTURE ID vs SETTLEMENT CULTURE: The patch uses culture?.StringId from the CultureObject parameter passed by vanilla. In campaign mode, vanilla uses settlement.MapFaction.Culture (line 63 of GuardsCampaignBehavior). If a Gondor settlement is conquered by Mordor, the culture parameter will be "mordor" not "gondor". Does the fallback chain handle this correctly? The config has <Culture id="gondor"> but an occupied settlement would pass culture="mordor". Is the SettlementId lookup sufficient to handle this case?

=== REQUIRED SECTIONS (missing section = incomplete review) ===

SECTION 1: VANILLA CODE
Read these files from E:\Decompiled_Bannerlord\ and paste the relevant methods into your output as code blocks:
a) Find GuardsCampaignBehavior.cs in Modules/SandBox/ -- paste TakeGuardAgentDataFromGarrisonTroopList() and PrepareGuardAgentDataFromGarrison() signatures
b) Find GuardsCampaignBehavior.cs -- paste GetSuitableSpear()
c) Find GuardsCampaignBehavior.cs -- paste AddGuardsFromGarrison() to verify spawn point flow

This section MUST contain code blocks with decompiled C#. Prose descriptions are NOT sufficient.

SECTION 2: VANILLA ANALYSIS
Using the code from Section 1, answer:
a) Is PrepareGuardAgentDataFromGarrison static or instance? What are its exact parameters?
b) When AddGuardsFromGarrison calls CreateCastleGuard vs CreateStandGuardWithSpear, do both pass overrideWeaponWithSpear=true to TakeGuardAgentDataFromGarrisonTroopList?
c) What happens when the garrison is empty -- does TakeGuardAgentDataFromGarrisonTroopList fall back to culture.Guard? Will TAOM's prefix intercept this before the fallback?

SECTION 3: REFLECTION AND EQUIPMENT ASSEMBLY
a) The patch calls PrepareGuardAgentDataFromGarrison via reflection. If the method's signature changed in a Bannerlord update, what happens? Is there a null check on the MethodBase?
b) Does the reflection call pass the correct boxed parameters for bool values?
c) The AccessTools.Method call is NOT cached -- it runs on every guard spawn. With ~20 guards per settlement entry, that's ~20 reflection lookups. Is this a performance concern?

SECTION 4: CONFIG CROSS-REFERENCE (required)
a) List every settlement ID key in settlement_guards_config.xml
b) Cross-reference each against Main/_Module/ModuleData/settlements.xml -- do they exist?
c) List every troop ID in settlement_guards_config.xml
d) Cross-reference each against Main/_Module/ModuleData/troops/troops_gondor.xml -- do they exist?
e) List every culture ID in the Spears section
f) Cross-reference each against taom_spcultures.xml and spcultures.xslt
g) Check for DEAD CONFIG -- are there config keys that exist but are never read at runtime?

SECTION 5: FINDINGS OR OBSERVATIONS
If bugs found -- each finding MUST include:
a) TAOM code (file:line)
b) Vanilla code (quote from Section 1)
c) Evidence of divergence
d) Severity: CRITICAL / HIGH / MEDIUM / LOW

If approve verdict -- you MUST still provide an OBSERVATIONS subsection.

=== QUALITY GATES ===
a) Section 1 MUST have code blocks. No code blocks = section fails.
b) Section 4 MUST name the cross-reference file for EACH ID. "Config looks valid" without citing a file = section fails.
c) Section on Known Suspects MUST say CONFIRMED or DISPUTED for each. Skipping a suspect = section fails.
d) Each finding MUST cite both TAOM file:line AND vanilla code. Findings without both = rejected.

Prior review lessons (from 18 reviews at 81% accuracy):
a) Do NOT assume empire=Rohan. empire=Dunland. Rohan=vlandia.
b) Do NOT flag TAOM code that matches vanilla behavior as a bug.
c) Do NOT claim something is missing without grepping the codebase.
d) Prior Codex reviews found real bugs in: config ID mismatches (most common), missing vanilla gates, reflection targeting wrong type, fail-safe default direction.

Output to: docs/reviews/codex-adversarial-settlement-guards-2026-04-08.md

---

Dispatch this prompt to Codex:
```
/codex:adversarial-review --background
```

When Codex finishes writing to `docs/reviews/codex-adversarial-settlement-guards-2026-04-08.md`, run:
```
/review-codex docs/reviews/codex-adversarial-settlement-guards-2026-04-08.md
```
