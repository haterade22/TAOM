# Codex pre-review: hero agents use their hero's culture (#1157)

You are an independent reviewer. Read only; change no file. Report findings with file:line evidence, severity (CRITICAL, HIGH, MEDIUM, LOW) and a failure scenario. Missing engine evidence stays UNVERIFIED.

## Scope

The uncommitted change in this worktree (`git diff` against HEAD `ef959062f`, branch `fix/1157-hero-agent-culture`, plus two untracked files):

- `Main/Adapters/CharacterCulture.cs` (new): `Of(BasicCharacterObject?)` returns `(character as CharacterObject)?.Culture ?? character?.Culture`; `IdOf` returns its `StringId`.
- Nine reads switched from `agent.Character?.Culture` (or a `BasicCharacterObject` local's `.Culture`) to the helper: `AgentAggressionApplier.CultureOf`, `MountChargeDamageApplier.RiderCultureOf`, `RaceAbilityRuntime.ProfileOf`, `RaceAbilitiesMissionLogic.OnAgentRemoved`, `FormationBannerHeraldry.CollectCultureIds` (two), `FormationRoutingSubscriber.Route`, `CustomBattleRoleAssigner.BannerFor`, `HornBlowerActions.Sound`.
- `TAOM.Tests/Adapters/CharacterCultureTests.cs` (new): six helper tests and a source gate that fails on `.Character.Culture` or `.Character?.Culture` in the code roots.
- `TAOM.Tests/Features/CombatMechanics/MountChargeDamageBindingTests.cs`: the IL test now expects a call to `IdOf` instead of `get_Culture` and `get_StringId`.
- `docs/features/race-abilities.md:70-73`: the culture source.

Intent: an agent's `Character` is a `BasicCharacterObject`, so `agent.Character.Culture` binds to the base property and reads the raw field, while `CharacterObject.Culture` is a `new` property that returns `HeroObject.Culture` for a hero. For the player's hero the raw field holds `main_hero`'s ModuleData culture. These are facts read in the v1.5.7 decompile: `TaleWorlds.Core.BasicCharacterObject.cs:124-134`, `TaleWorlds.CampaignSystem.CharacterObject.cs:264-278`, `CharacterCreationContent.SetSelectedCulture` writes only `Hero.MainHero.Culture` and `Clan.PlayerClan.Culture` (`:137-138`), and SandBox `lords.xml` gives `main_hero` `culture="Culture.battania"`.

## Questions

1. Is `Hero.Culture` the right answer for every hero kind that reaches these readers: the player, a born child, a wanderer, companion or notable made from a template, a lord, a hero whose culture TAOM's culture conversion changed, a co-op joined player? Does any TAOM feature or doc rely on the template or ModuleData culture for a hero agent on purpose?
2. `AgentAggressionApplier.CultureOf` and `MountChargeDamageApplier` run inside `Agent.UpdateAgentProperties`, which can run on the async AI thread, and `TaomBattleMoraleModel.CanPanicDueToMorale` runs on TWParallel workers. Is the new read (`CharacterObject.IsHero`, `HeroObject`, `Hero.Culture`) safe and cheap there?
3. Can any custom battle character be a `CharacterObject`, so the change alters custom battle? Check how `CustomGame` registers `NPCCharacter`.
4. Do other reads of an agent's culture go through a `BasicCharacterObject` by a route the gate regex misses (`Agent.Origin.Troop`, `AgentBuildData.AgentCharacter`, a `BasicCharacterObject` parameter or local), and should they use the helper?
5. Does any code compare the culture these readers now return with a culture taken from another route (party, clan, roster `CharacterObject.Culture`, a cached profile) in a way that the change makes inconsistent, for example a kin or ally check, a profile cache keyed on the old answer, or a value captured at spawn and compared at removal?
6. Is the source gate sound: false positives, false negatives, and do the roots of `RepoPaths.CSharpFiles()` cover the code that matters?
7. Does the edited IL test still prove that the mount lookup reads the rider's `Character`, not the mount's?
8. Is the doc text accurate?

Engine source: the installed DLLs are the truth (`pwsh tools/taom-src.ps1 path <Type>`); the dump at `E:\Decompiled_Bannerlord\_modules_build` and `_shipping_build` is v1.5.7.
