# Independent adversarial review: TAOM Bannerlord v1.4.8 -> v1.5.0 engine bump

You are an independent reviewer. Be adversarial. Your job is to find bugs the author missed, not to
agree. Assume the author is competent and has already run a 5-agent internal review that came back
clean on standards and API compatibility, so easy findings are gone. Look for what a careful reviewer
would still miss.

## Repo and branch
`e:\repos\TAOM`, branch `bannerlord-1.5.x`. All work is UNCOMMITTED. Use `git status --porcelain` and
`git diff` to see the changeset. Read `CLAUDE.md` and `.claude/rules/*.md` for project standards.

## Verification resources
- Installed game is **v1.5.0** at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`.
- Fresh v1.5.0 decompile: `E:\Decompiled_Bannerlord\_shipping_build` (base bin) and `_modules_build`
  (module satellites, `<Module>__<Dll>.cs`).
- **Preserved v1.4.8 baseline** for diffing: `_shipping_build_v1.4.8`, `_modules_build_v1.4.8`.
- Signature checks: `pwsh tools/taom-src.ps1 path <FullyQualifiedType>`.
- The update's exact change-set (every file v1.5.0 rewrote, by mtime): `docs/migration/v1.5.0-changeset.txt`.
- Full context of what was done and why: `docs/migration/v1.5.0-impact.md`.
- Do NOT run `./build.ps1`. If you must build: `dotnet build -p:DisableModuleCopy=true -p:ModuleId=`.

## What changed (summary; verify against the diff, do not trust this list)
Eight compile-level engine adaptations: `MissionBehavior.OnTeamDeployed(Team)` deleted so
`BannerBearerAssignmentMissionLogic` moved to `OnBattleSideSpawned(BattleSideEnum)` iterating
`Mission.GetTeamsOfSide(side)`; `GetPrisonerRecruitmentMoraleEffect` int->float;
`SettlementLoyaltyModel`'s governor-culture members removed (override plus its config field,
validator and tests deleted); `TraitLevelingHelper.OnLordExecuted` retargeted to
`OnBloodFeudStarted(Hero)`; `ExecutionRelationModel` deleted engine-wide so `TaomExecutionRelationModel`,
`ExecutionContext` and the `KillCharacterAction` prefix were removed; `MobileParty.HasPerk` needs
`out Hero`; `OnCharacterCreationIsOverEvent` became `MbEvent<int>` firing 10 times as a phase index
(TAOM handlers guarded at phase 9); `MobilePartyVisual.AddCharacterToPartyIcon` 11 params -> 7.

Behavioural fixes: `CultureObject.Executioner` added to 24 cultures plus 6 XSLT-renamed ones;
`PartyNameplateItem.xml` gained `BloodFeudIconWidget` (engine dereferences it unguarded per frame);
`Patch53_PartyIconScale` split into two transpilers because one site relocated and a third was newly
hardcoded; banner colours re-seamed from a postfix to a transpiler; `SpecialResources` XPath
repointed; `comment_strings.xslt` 23 templates restored to copy `<tags>` children; Civil Unrest
thresholds honoured; 88 dwarf action sets given `act_ghurab_captain_idle`; startup gold re-applied at
phase 9 because v1.5.0 hard-assigns `Hero.MainHero.Gold = 1000`; 16 female notable templates added
because ASO's Trader start crashes on an empty gender-filtered list; NavalDLC declared incompatible.

Five new gates: `TranspilerSiteBindingTests`, `PrefabExtensionBindingTests`, `CommentStringTagsTests`,
`NotableTemplateGenderTests`, plus rewritten `PartyIconScaleTranspilerTests`.

## Where to hunt hardest

1. **The two transpilers now patching the SAME method** (`SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper.GetHumanAgentPartyVisual`):
   `BannerColorTranspiler` (applied manually via `ManualPatchApplicator`) inserts `ldarg.2 + call`
   after each `IFaction::get_Color` / `get_Color2`; `PartyIconScaleTranspiler.RewriteHumanVisualSite`
   (applied by Harmony category) swaps an `ldc.r4 0.3`. Verify order-independence, stack balance,
   label/exception-block preservation, and that neither matcher can match the other's INJECTED
   instructions on a re-apply. Check what happens if the category is applied twice.
2. **`ldarg.2`.** `GetHumanAgentPartyVisual` is static with `party` as the third parameter. Confirm
   the index is right and that the resolver signature `(uint, PartyBase)` leaves the stack balanced.
3. **`PartyNameplateItem.xml`.** The root widget references children by backslash path. One path was
   repointed and two attributes added. Verify EVERY path on that root still resolves to a real `Id`,
   and check sibling nameplate prefabs for the same widget type. A broken path is the same class of
   unguarded NRE the change was fixing.
4. **The `executioner` attribute.** Enumerate every attribute `CultureObject.Deserialize` reads in
   v1.5.0 and check the 6 XSLT-renamed culture blocks emit or deliberately inherit each one. The
   stylesheet's own comment warns that `xsl:apply-templates select="@*"` INHERITS unnamed attributes,
   so report any OTHER newly-added engine-read attribute that was missed, not just `executioner`.
   Also: do the live `TAOM_Map` / `LOTRLOME_Armory` modules define or override any culture?
5. **Phase 9.** Three TAOM handlers now guard `index != 9`. Independently enumerate what every
   subscriber across CampaignSystem, SandBox, StoryMode does at each index 0..9 and confirm 9 is
   correct for all three, including that nothing relevant runs after it.
6. **The startup-gold re-apply.** It is gated on `GetStartType()` being empty or "default". Check the
   null path on an old save, whether the gate string is right, and whether re-granting at phase 9 can
   double-apply if character creation is somehow re-entered.
7. **The 16 new female notable templates.** They clone a male donor and flip `is_female`. Check the
   equipment rosters are valid for a female character, that no duplicate ids were introduced, and
   that `skill_template` / `occupation` / `voice` are still sane. Look for anything that makes them
   crash or render broken rather than merely look approximate.
8. **Deleted code fallout.** `IExecutionRelationService` is registered in `ExecutionIoC` but its only
   consumer was deleted. Is `ExecutionActionHook.GetRelationModifier` now dead too? Is the
   `Patch14_Execution` category still live and registered?
9. **The new gates themselves.** Can any of them pass while the thing they guard is broken? In
   particular `PrefabExtensionBindingTests` resolves prefabs from the DEPLOYED game module folder,
   which can be stale relative to the repo.

## Output
Findings ranked P1 (must fix before merge) / P2 (should fix) / P3 (nit), each with file:line, the
concrete failure scenario, and the minimal fix. Explicitly state anything you checked and found
CLEAN. If you disagree with a decision the author made, say so and why. Do not pad; a short list of
real findings beats a long list of speculation.
