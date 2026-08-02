Adversarial code review of TAOM's co-op interop layer. Repo root is the working directory.

CONTEXT

TAOM is a Lord of the Rings total conversion for Mount and Blade II Bannerlord v1.4.7. A third-party co-op mod called BannerlordCoop (Steam Workshop 3770450698, launcher module id "Coop") lets 2+ players share one campaign. TAOM previously shipped interop for a DIFFERENT co-op mod (BannerlordTogether). This change set adds detection and host-authority gating for BannerlordCoop.

The whole design rests on one external fact, already verified against the installed v1.4.7 DLLs and against BannerlordCoop's own decompiled source. Do NOT re-derive it, but DO tell me if any code contradicts it:

  Campaign.Tick() makes TWO separate calls. (A) _campaignPeriodicEventManager.OnTick(dt) -> SignalPeriodicEvents() drives the GLOBAL events CampaignEvents.DailyTickEvent / HourlyTickEvent / WeeklyTickEvent / QuarterHourlyTickEvent. (B) _campaignPeriodicEventManager.TickPeriodicEvents() drives the PER-ENTITY events DailyTickParty/Settlement/Town/Hero/Clan, HourlyTickParty/Settlement/Clan, QuarterDailyParty.
  BannerlordCoop's PartyTickPatch prefixes ONLY (B) plus MobilePartyHourlyTick and MobileParty.HourlyTick, each returning ModInformation.IsServer.
  CONSEQUENCE: on a co-op CLIENT the per-entity events never fire, but the GLOBAL events DO fire. Any TAOM behaviour on a global tick that mutates shared world state runs twice unless TAOM gates it.

Also established, do not re-litigate: no save-definer collision (Coop uses base ids 44177000/44182000, TAOM uses 726900501/601/701/801); Coop registers zero GameModels; Coop keys network identity on "{TypeName}_{StringId}" built per peer.

READ FIRST
- docs/features/coop-interop.md -- what TAOM does and what it deliberately does not do
- docs/research/bannerlordcoop-internals.md -- BannerlordCoop internals, incl. the tick split and the client object-creation crash chain
- docs/reviews/rca-coop-authority-gating-2026-08-01.md -- a prior deep review of THIS change set, which already found and fixed two HIGH gate bypasses

FILES TO REVIEW

Detection and shields (TAOM.Dependencies assembly):
- Dependencies/Foundation/CoopPresence.cs
- Dependencies/Foundation/CoopPresencePolicy.cs
- Dependencies/Foundation/CoopModuleList.cs
- Dependencies/Foundation/PatchShieldPolicy.cs
- Dependencies/SubModule.cs
- Dependencies/_Module/coop-modules.txt
- Dependencies/_Module/SubModule.xml
- Main/_Module/SubModule.xml

Authority layer (TAOM main assembly):
- Main/Features/CoopInterop/CoopSessionPolicy.cs
- Main/Features/CoopInterop/CoopSessionProvider.cs
- Main/Features/CoopInterop/ICoopSessionProvider.cs
- Main/Features/CoopInterop/ICoopPresenceProvider.cs
- Main/Features/CoopInterop/CoopPresenceProvider.cs
- Main/Features/CoopInterop/CoopUiRegistrationPolicy.cs
- Main/Features/CoopInterop/CoopSuppressedUiAttribute.cs
- Main/Features/CoopInterop/CoopInteropIoC.cs

Gated behaviours:
- Main/Features/CultureConversion/Hooks/CultureConversionBehavior.cs
- Main/Features/RaceAge/RaceAgeBehavior.cs
- Main/Features/Diplomacy/WarOfTheRingBehavior.cs
- Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs
- Main/Features/Messengers/MessengerCampaignBehavior.cs
- Main/Features/Siege/SiegeDefenseBehavior.cs
- Main/Features/Siege/SiegeDefenseService.cs
- Main/Features/Siege/ISiegeDefenseService.cs
- Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs
- Main/Features/Diplomacy/Hooks/AllianceActionHook.cs
- Main/Features/Diplomacy/Hooks/PeaceActionHook.cs
- Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs
- Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs
- Main/Features/TimeAcceleration/TimeAccelerationService.cs
- Main/SubModule.cs (behaviour construction sites only)

Tests:
- TAOM.Tests/Features/CoopInterop/CoopSessionPolicyTests.cs
- TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs
- TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs
- TAOM.Tests/Features/CoopInterop/CoopUiRegistrationPolicyTests.cs
- TAOM.Tests/Features/Dependencies/CoopPresencePolicyTests.cs
- TAOM.Tests/Infrastructure/Dependencies/AssemblyRedirectListTests.cs

KNOWN SUSPECTS -- CONFIRM or DISPUTE each with file:line evidence

S1. GATE COMPLETENESS. A prior review found two HIGH bypasses: a behaviour gated its tick handler but reached the SAME mutating service method from a sibling handler (WarOfTheRingBehavior.OnSessionLaunched, and 6 of 8 handlers on WarOfTheRingMomentumBehavior). Both are now gated. HYPOTHESIS: there is at least one MORE bypass of this shape still present. For EVERY gated behaviour, enumerate every handler registered in RegisterEvents, follow each to its service calls, and report any handler that reaches a mutating method without the authority gate. Also check non-event entry points: game menu options, dialog consequences, public service methods reachable from UI.

S2. SIEGE SPLIT CORRECTNESS. SiegeDefenseService.OnHourlyTick was just split into OnHourlyTickShared (authority-gated) and OnHourlyTickLocalPlayer (runs on every peer). HYPOTHESIS: the split is unsound. Check specifically whether OnHourlyTickLocalPlayer mutates state that is persisted to the _taom_siege_active_events SyncData key. GrantReward sets evt.RewardClaimed on an entry in the shared _activeEvents dictionary. If that flag round-trips through SnapshotForSave/RestoreFromSave, then a client granting its own reward DOES mutate shared saved state, and the split is leaky. Trace it and give a verdict.

S3. FAIL-OPEN DIRECTION. CoopSessionPolicy.IsAuthority = !sessionActive || isServer, and every failure path in CoopSessionProvider is meant to yield IsAuthority == true so singleplayer is unaffected. HYPOTHESIS: some path yields false in singleplayer, silently disabling TAOM features for a solo player. Check especially: Common.ModInformation.IsServer defaults to false and is never reset on session teardown; GameInterface.ContainerProvider.Alive is a permanently-false inline static initialiser (the code deliberately binds TryGetContainer instead). Verify the reflection binder cannot produce a false negative.

S4. REFLECTION BINDER SAFETY. CoopSessionProvider binds another mod's internals by reflection and is called from campaign tick handlers. It uses double-checked locking with a volatile _bound flag and volatile MethodInfo/PropertyInfo fields. HYPOTHESIS: an exception, a partially-bound state, or a torn read can escape into a campaign tick. Also assess whether MethodInfo.Invoke per hourly tick (24x per in-game day) is an acceptable cost, and whether the CoopPresence.IsActive early-out genuinely short-circuits before any reflection for solo players.

S5. ASSEMBLY REDIRECT DELETION. Dependencies/SubModule.cs previously redirected 22 simple assembly names to TAOM's loaded copy, matching on simple name and DISCARDING the requested version. Five were removed because BannerlordCoop ships them at higher versions (Serilog 2.0.0.0 vs 4.2.0.0, System.Runtime.CompilerServices.Unsafe 4.0.4.1 vs 6.0.1.0, System.Memory, System.Buffers, System.Numerics.Vectors). HYPOTHESIS: removing them breaks something in TAOM that depended on the redirect -- in particular ButterLib, which references Serilog. Check whether TAOM or any bundled BUTR assembly needs the Serilog redirect to resolve, and whether the remaining 17 entries contain another name BannerlordCoop also ships at a higher version.

S6. DETECTION COUPLING. The module id "Coop" is declared in FOUR places: CoopPresence.CompiledModuleDefaults, Dependencies/_Module/coop-modules.txt, and ModulesToLoadAfterThis in BOTH SubModule.xml manifests. HYPOTHESIS: the tests pinning this coupling have a hole -- e.g. they check one direction only, or the parser used by the test differs from CoopModuleList's real parser. Verify AssemblyRedirectListTests.CompiledModuleDefaults_MatchesTheShippedCoopModulesFile and BundledDependencyManifestTests together actually make drift impossible.

ADDITIONAL DEEP ANALYSIS

A. CLIENT-SIDE OBJECT CREATION. Constructing any MBObjectBase on a co-op client is believed to throw: Coop prefixes the MBObjectBase.StringId setter and returns false when its sync policy disallows the write, so MBObjectManager.CreateObject leaves StringId null and ObjectTypeRecord.RegisterObject calls Dictionary<string,T>.TryGetValue(null) -> ArgumentNullException. Grep ALL of Main/ for runtime object creation (MBObjectManager.CreateObject, HeroCreator.CreateNotable, HeroCreator.CreateChild, HeroCreator.CreateSpecialHero, MobileParty.CreateParty, new CareerQuest / any QuestBase subclass) and report every call site reachable on a client that is NOT authority-gated. CareerQuestCampaignBehavior is deliberately ungated -- assess whether its `new CareerQuest(...)` on player acceptance is safe, given QuestBase derives from MBObjectBase.

B. MCM SETTINGS DIVERGENCE. BannerlordCoop syncs no mod settings. TAOM has 284 SettingProperty entries in Main/Features/TaomSettings.cs, most gameplay-affecting. Identify the settings whose divergence between two peers would cause the WORST outcome given the gating now in place -- i.e. settings read on the authority side that change shared world state, versus settings read locally that only change that player's view.

C. SAVE ROUND-TRIP. A joining client loads the HOST's save through the normal SaveManager pipeline, so every TAOM CampaignBehaviorBase.SyncData runs on the client. For each gated behaviour, check whether its SyncData or OnGameLoaded path writes state that the gate then prevents it from maintaining -- i.e. a behaviour that loads records it will never process, producing a store that grows or goes stale on a client.

QUALITY GATES
- Every finding MUST cite file:line and quote the actual code. No finding without evidence.
- Where you assert vanilla engine behaviour, say which DLL/type you checked. If you could not check, mark the finding UNVERIFIED rather than asserting.
- Do NOT flag code that merely mirrors vanilla behaviour as a bug.
- Do NOT propose a fix that introduces shared mutable state across threads.
- Rank findings HIGH / MEDIUM / LOW by player-visible consequence, and say explicitly whether each affects SINGLEPLAYER, CO-OP ONLY, or BOTH. A regression that only manifests in co-op is less severe than one that breaks solo play, because co-op is unverified and opt-in.
- If a Known Suspect is wrong, say DISPUTED and explain why. A confident wrong finding costs more than a missed one here.

PRIOR REVIEW LESSONS
SUCCESSES: cross-referencing config IDs caught real mismatches; decompiling vanilla caught missing gates; lifecycle tracing caught stale caches.
FAILURES: prior runs assumed empire=Rohan (it is Dunland; Rohan is vlandia); flagged vanilla-matching code as bugs; skipped the hardest section. Do not skip sections -- if you cannot complete one, say so explicitly.

OUTPUT
Structured findings with severity, file:line, quoted code, consequence, and suggested fix. End with a verdict on whether this change set is safe to ship given that co-op itself is unverified and opt-in.
