TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1111 in the working tree of branch `bannerlord-1.5.x` at HEAD `3b805cf38`. This is a read-only review: do not edit, stage or build anything.

## Engine version (read this first)

- The installed game is Bannerlord v1.5.7 (changeset 125802, a Steam update on 2026-11-10). Trunk still pins v1.5.6 (`.claude/pinned-game-version.txt`); the engine bump runs in a separate worktree and is not part of this change.
- Read engine code from the installed v1.5.7 DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file (cache `C:\Users\mikew\.taom-src\v1.5.7-202611101605`). The v1.5.6 to v1.5.7 text diff is under `E:/Decompiled_Bannerlord/_diff_1.5.6_to_1.5.7/`.
- Trunk does not compile against v1.5.7 today: `TaomPartySizeModel.GetPartyMemberSizeLimit(PartyBase, bool)` fails with CS0115. That file is outside this change and the engine bump fixes it. Do not report it.

## The request and the maintainer's decisions

A player reported about three orc lords serving Gondor in the public build and asked that good peoples serve only Free or Neutral realms, and evil peoples only Evil or Neutral realms. The maintainer also sees Free lords who are friends with Evil lords. The maintainer decided (2026-11-10):

1. AI lords only. The player's clan, and a realm a player leads, keep the base game's rules.
2. New joins only. A clan that already serves an opposed realm stays.
3. At new-game creation, every positive relation between a Free lord and an Evil lord becomes a negative relation of the same size. Same-side rivalries and Neutral lords keep their roll.
4. One GitHub issue (#1111).

Do not re-litigate these four decisions.

## Implementer's design choices (open to challenge)

- An MCM switch `KingdomAllegianceEnabled`, default on, a new key.
- The veto value is `-100000000f`, which the implementer read as the value vanilla's own `GetScoreOfClanToJoinKingdom` gives a realm's ruling clan.
- `PlayerControl.Unknown` counts as a player, so doubt keeps vanilla's score.
- A clan's side is `FactionSides.Of(clan)`: the clan id in `Main/_Module/ModuleData/execution/alignment.json`, then the clan's culture. It never reads `MapFaction`.
- The friendship flip is a Harmony postfix on the private `CharacterRelationCampaignBehavior.UpdateFriendshipAndEnemies(CampaignGameStarter)`, not a TAOM listener on `OnNewGameCreatedEvent`, because the implementer read that listeners run newest first, so a TAOM listener would run before vanilla's roll.
- `AllegianceFactsReader` is a public, non-sealed boundary class and not an `I*Adapter`, because `ServiceSignatureGateTests` fails any `I*Adapter` or `*Service` member that names a TaleWorlds class.

## Files in scope (42)

Show a modified file's change with `git diff HEAD -- <path>`; read a new file whole.

New:
- `Main/Features/KingdomAllegiance/AllegianceFacts.cs`, `IKingdomAllegianceSettingsProvider.cs`, `KingdomAllegianceSettingsProvider.cs`, `KingdomAllegianceService.cs`, `KingdomAllegianceModule.cs`, `Hooks/Patch145_FriendshipSeeding_Postfix.cs`, `Models/AllegianceFactsReader.cs`
- `Main/Adapters/IAllegianceRelationAdapter.cs`, `Main/Adapters/AllegianceRelationAdapter.cs`
- `TAOM.Tests/Features/KingdomAllegiance/KingdomAllegianceServiceTests.cs`, `TaomDiplomacyModelAllegianceTests.cs`, `KingdomAllegianceWiringTests.cs`
- `docs/features/kingdom-allegiance.md`, `tools/ingame_checks/1111.toml`

Modified:
- `Main/Adapters/FactionSides.cs` (adds `OfOwnClan`), `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` (two overrides, two constructor arguments), `Main/SubModule.cs` (the `new TaomDiplomacyModel(` line only), `Main/Composition/FeatureModules.cs` (one line), `Main/Features/TaomSettings.cs` (the `KingdomAllegianceEnabled` block only)
- `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 `Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml` files (two rows each)
- `TAOM.Tests/Features/Relations/TaomDiplomacyModelPersonalRelationsTests.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs`
- `docs/features/coop-interop.md`, `docs/features/bannerlord-together-compat.md`, `docs/reference/feature-map.md`, `docs/reference/gamemodel-registry.md`, `docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/gamemodel-bases.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`

Context outside the diff: `Main/Features/Execution/FactionSideRules.cs` (`AreOpposed`), the `IAlignmentService.ResolveSide(SideIds)` extension, `Main/Features/CoopInterop/IPlayerControlQuery.cs` and `CoopPlayerControlQuery.cs`, `Main/Core/Logging/IOutcomeLog.cs`.

## Known suspects: CONFIRM or DISPUTE each, with file:line evidence (v1.5.7 decompile for engine claims)

1. **Membership paths.** List every v1.5.7 call site of `ChangeKingdomAction.ApplyByJoinToKingdom`, `ApplyByJoinToKingdomByDefection` and `ApplyByJoinFactionAsMercenary`, and any other path that puts a clan into a realm (kingdom decisions, rebellion, clan merges through marriage, quests, the barter screen with an AI ruler). For each, say whether its decision reads `GetScoreOfClanToJoinKingdom` or `GetScoreOfMercenaryToJoinKingdom`. Name each path where an AI clan can still enter an opposed AI realm.
2. **Other readers of the two scores.** List every v1.5.7 caller of the two scores. Does the veto change anything other than join, defection and hire: a UI value, a weight in choosing a realm, a leave decision, or a barter where the player takes part but is neither the clan nor the realm's leader?
3. **Arithmetic.** The barterables return values that the behaviors sum and compare. Show the v1.5.7 sums and comparisons. Can `-100000000` overflow, wrap, or be clamped into a value that still accepts the join?
4. **Which pairs the flip touches.** Which heroes does vanilla's `UpdateFriendshipAndEnemies` pair in v1.5.7? The adapter flips pairs among alive heroes with `IsLord`, not `Hero.MainHero`, with a `MapFaction` whose `Leader` is not the main hero. Does it flip pairs that vanilla never rolled, such as relations from XML, family, or another TAOM seeder? Would it invert a Free-vs-Evil friendship that TAOM or the data sets on purpose?
5. **Relation storage.** In v1.5.7, is `CharacterRelationManager` storage symmetric for a hero pair? What does `Hero.SetPersonalRelation` clamp to? Does a raw write skip a cache or a list (friends and enemies, the encyclopedia) that later reads stale?
6. **Patch145 target.** Does `AccessTools.Method(typeof(CharacterRelationCampaignBehavior), "UpdateFriendshipAndEnemies", new[] { typeof(CampaignGameStarter) })` resolve to exactly one private method in v1.5.7? Is the patch category (applied at `ApplyPhase.ProcessLoad`) patched before `OnNewGameCreatedEvent` fires for a new campaign?
7. **Co-op.** The BannerlordCoop mod's `GameInterface.dll` names `CharacterRelationCampaignBehavior`. With `ICoopSessionProvider.ShouldDeferToHost` true, both rules keep vanilla. Is that deferral correct for a co-op client and a co-op host? Can a client compute these scores or run the roll?
8. **The reader on a hot path.** `AllegianceFactsReader.Read` logs a line on each exception. The scores run for each clan and realm during the daily clan tick. Can a persistent fault flood the log? What does `IPlayerControlQuery.OfHero` return for a realm whose `Leader` is null?
9. **The in-game manifest.** `tools/ingame_checks/1111.toml` lists `result=skip` under `fail`. Under `tools/ingame_verdict.py` and `docs/features/ingame-verdicts.md`, does a session with the switch off, or a co-op client that defers to the host, then get a false FAIL instead of NOT REACHED?
10. **Threads.** Does any v1.5.7 caller run either score off the main thread (for example inside `TWParallel`)? The counter is an `Interlocked` increment; check the reader, the alignment service and `IPlayerControlQuery`.

## What NOT to flag

- Files outside the 42 above (retopology tools, `dist/`, `docs/audits/armory-ref-audit.md`, other review prompts and RCAs): other sessions own them.
- `TaomPartySizeModel` CS0115 against v1.5.7 (the engine bump owns it).
- The four maintainer decisions.
- English text in the 12 language rows: the translator's `--seed-only` pass wrote placeholders, and the paid translation is tracked in #763.

## Output

Report P1, P2 and P3 findings. Cite `file:line` for every claim, and the v1.5.7 decompiled file and line for every engine claim. Mark a claim UNVERIFIED when you cannot read the evidence. State explicitly where you disagree with a design choice above.
