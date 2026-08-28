You are performing an ADVERSARIAL code review of a new feature in TAOM, a Mount & Blade II: Bannerlord 1.4.8 total-conversion mod. Your job is to find real bugs, not to praise the design. Assume the author was confident and therefore careless somewhere.

## The feature

"Player Switcher" (#514), branch `feat/player-switcher`, five commits `a9f3b0b5..ac6593cf`. At the character creation face generator, a panel lists existing lords of the chosen culture. Picking one hands the campaign over to that lord: their face, gear, skills, clan, fiefs and kingdom. It is a reimplementation of a feature from LOTRAOM, a predecessor mod targeting Bannerlord 1.2.12.

Read `docs/features/player-switcher.md` first for the intended design, then attack the implementation.

## Where to look

- `Main/Features/PlayerSwitcher/**` (services, domain, hooks, UI, behaviors)
- `Main/Adapters/{IHeroPickerAdapter,HeroPickerAdapter,IPlayerIdentityAdapter,PlayerIdentityAdapter,IKingdomJoinAdapter,KingdomJoinAdapter}.cs`
- `Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs` (one early return added)
- `Main/SubModule.cs`, `Main/IoC.cs`, `Main/Features/TaomSettings.cs`
- `Main/_Module/GUI/Prefabs/FacGen/PreBuildCharacterSelection.xml` (used unchanged)
- Tests: `TAOM.Tests/Features/PlayerSwitcher/**`

Engine sources for verification: `E:\Decompiled_Bannerlord\_categories_v1.4.8\` (v1.4.8 decompile). The installed DLLs are at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`.

## Load-bearing claims to attack specifically

The design rests on these. Each is a place where being wrong is expensive and silent. Verify each against the decompiled engine, and say CONFIRMED or REFUTED with evidence.

1. **Handler priority 1100 is safe and sufficient.** The handover registers an `ICharacterCreationContentHandler` at 1100. TAOM's own handler is 1050, vanilla core is 800. The claim is that at 1100, `CharacterCreationManager.ApplyFinalEffects` has already applied `Renown = 0`, `ApplyCulture` (which rewrites `BornSettlement` and calls `ResetPlayerHomeAndFactionMidSettlement`), the culture teleport, and all TAOM grants, all against a throwaway hero and clan that the handover then deletes. Verify by reading `ApplyFinalEffects` and the handler dispatch. Is anything in that method NOT applied to the throwaway pair? Is anything applied AFTER the handler loop that the swap invalidates? What about `FinalizeCharacterCreationState`?

2. **The step ordering that prevents an orphan clan.** `ReassignPlayerClan` must precede `RemoveOriginalHero` because `KillCharacterAction.ApplyInternal` guards clan destruction on `victim.Clan != Clan.PlayerClan`. Read `KillCharacterAction.cs` and confirm. Then look for OTHER branches in that method that behave differently now that the victim is no longer `Hero.MainHero` but IS a clan leader whose clan is not the player clan. Does `DestroyClanAction.ApplyByClanLeaderDeath` on the throwaway clan do anything harmful, for example touching the kingdom, settlements, or firing events other systems listen to?

3. **The leftover character-creation party.** `ChangePlayerCharacterAction.Apply` transfers the old party to the new main hero via `LordPartyComponent.ChangePartyOwner` when the roster is non-empty, and that method does not move `MobileParty.ActualClan`. The claim is the party therefore stays registered to the throwaway clan and `DestroyClanAction` sweeps it on the takeover path. Verify. Then check the ADOPTION path, where the throwaway clan is the player own clan and survives: is `AbsorbOriginalParty` correct, and is it called at a point where the party still exists?

4. **Harmony binding by arity.** `Patch77_BodyGeneratorView_Constructor` uses `TargetMethods()` returning the single declared constructor plus a `Prepare()` guard, instead of a `Type[]` attribute. Confirm `BodyGeneratorView` declares exactly one public constructor in 1.4.8 and that this binding approach is sound. Also check the apply timing: the category is applied in `SubModule.cs` in the `OnGameInitializationFinished` batch. Does that run before a `BodyGeneratorView` can first be constructed during character creation? TAOM shipped bug #299 where a campaign-init batch was too late for a main-menu screen; check for the same class of error here.

5. **The preview suppression flag.** `BodyGeneratorPreviewSink` sets `IsPreviewActive`, and `Patch9_RaceFilter` early-returns on it. Find any path where that flag is left true after the preview ends. If it can stick, the culture race filter silently stops applying for the rest of character creation.

6. **The DryIoc dual registration.** `container.Register<IPlayerSwitchSession, PlayerSwitchSessionStore>(Reuse.Singleton)` followed by `container.RegisterMapping<IPlayerSwitchSessionWriter, IPlayerSwitchSession>()`. Verify a reader and a writer resolve to THE SAME instance. If they do not, the picker writes to one object and the handler reads another, and the whole feature is a silent no-op in game while every unit test still passes.

7. **Static state on the patch class.** `HostView`, `Movie`, `ViewModel`, `WeLoadedSpriteCategory` are static. What happens if `OnFinalize` never runs (exception, quit to main menu from the face generator, alt-F4)? Retained view graph? Sprite category never unloaded? TAOM has an open memory investigation (#385, CTD at 20.3 GB), so retention matters here.

8. **Sprite category unload safety.** `SpriteCategory` has an `IsLoaded` bool and no reference count. The teardown unloads `ui_clan` only if the picker loaded it. Construct an interleaving where that is still wrong.

9. **Reflection sites.** Two: `Campaign.PlayerDefaultFaction` (internal property, written) and `BodyGeneratorView._dressedEquipment` (private readonly field, mutated in place). Verify both exist in 1.4.8 with the assumed shape, and that failure handling is genuinely safe rather than leaving a half-swapped campaign.

10. **The eligibility rules.** `HeroPickerService` filters by culture, excludes the main hero, children, notables, placeholder names, and lore-locked heroes (Sauron and the Nazgul, opt-in). Look for a hero state that should be excluded and is not: prisoners, heroes with a `DeathMark`, disabled heroes, heroes already dead but still enumerated, heroes belonging to eliminated clans, heroes in a clan with no leader. Consider what happens if the player takes over a hero who is currently a prisoner.

## Also worth attacking

- Concurrency and co-op: TAOM has co-op interop. `PlayerPossessionBehavior` captures on `OnCharacterCreationIsOverEvent`, which fires after the 1100 handler. The claim is this closes a collision for free, by ordering. Verify.
- The kingdom-join offer gating: it must fire only after a successful adoption, never after a takeover, never after an ordinary creation.
- Anything in the tests that asserts nothing meaningful, or that would still pass if the code were broken.
- Null-reference paths: the TAOM rule is that computed TaleWorlds getters throw BEFORE a `!= null` check can run, so the inner object must be guarded (`party.MapFaction?.Culture`, not `party.Culture != null`). Look for violations.

## Output

For each finding: severity (P1 blocking / P2 should fix / P3 nice to have), file and line, what is wrong, why it matters in a real campaign, and the minimal fix. Separate CONFIRMED findings (you verified against engine source) from SUSPECTED (you could not verify). If a load-bearing claim above is correct, say so explicitly; a clean bill on a specific claim is useful signal. Do not invent findings to fill the report.
