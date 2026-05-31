# RCA — Party-screen troop thumbnails stuck on loading spinner (stale GUI prefab clone, 2026-05-31)

**Symptom (user report, in-game):** On the Party screen, every troop row's character thumbnail showed only the loading spinner (the rotating circle of dots) forever — no troop portrait ever rendered. Reported on a Dale party; the cause is **global** (all cultures — Dale was just what was on screen).

**Top line:** TAOM ships full `<Prefab>` **clones** of several vanilla GUI prefabs. Bannerlord **1.4.5 renamed** the image-source binding on `ImageIdentifierWidget` and `MaskedTextureWidget` from `ImageTypeCode` → `TextureProviderName` (the backing `ImageIdentifierVM` now exposes `TextureProviderName`). TAOM's clones were stale from a pre-1.4.5 copy and still bound `ImageTypeCode="@ImageTypeCode"`, which in 1.4.5 resolves to nothing — so the widget never learned which texture provider renders the character `{Code}` and the `LoadingIconWidget` spun forever. Fixed 9 prefabs (12 `ImageTypeCode` occurrences → 0). A follow-up audit found this is the visible tip of broader prefab-clone drift (32 of 48 TAOM GUI prefabs are vanilla clones).

This is the **GUI-prefab instance of the stale-vs-vanilla failure class** that `.claude/rules/vanilla-data-comparison.md` already governs for data XML — now extended to cover prefab clones.

## Root cause

| Layer | Detail |
|---|---|
| Widget | `ImageIdentifierWidget` (troop/character thumbnails) and `MaskedTextureWidget` (banner/faction visuals) |
| Backing VM | `TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.ImageIdentifierVM` — exposes `TextureProviderName` in 1.4.5 (the old `ImageTypeCode` property is gone) |
| Stale binding | TAOM clones bound `ImageTypeCode="@ImageTypeCode"`; vanilla 1.4.5 binds `TextureProviderName="@TextureProviderName"` |
| Effect | No texture provider resolved → async character/banner render never starts → `LoadingIconWidget` (CircleLoadingWidget) shows indefinitely |
| Ground truth | Vanilla 1.4.5's own prefabs bind `TextureProviderName` and contain **0** `ImageTypeCode` — TaleWorlds would not ship a binding to a non-existent property, so the vanilla prefab is the authoritative spec |

## Fix — 9 prefabs (all XML; no C# change)

`grep ImageTypeCode= Main/_Module/GUI/` → 12 occurrences across 9 files, all eliminated:

- **Re-synced to vanilla 1.4.5** (clones of still-shipped vanilla prefabs, replaced verbatim — no TAOM-custom content existed in them; the re-sync also picked up other 1.4.5 redesigns the stale clones had missed): `Party/PartyTroopTuple.xml`, `Party/PartyTroopTupleLeft.xml`, `Party/PartyTroopManagerPopUp/PartyTroopRecruitItem.xml`, `Party/PartyTroopManagerPopUp/PartyTroopUpgradeItem.xml`, `CustomBattle/CompositionSlider.xml`, `CustomBattle/TroopTypeSelectionPopUp.xml`.
- **`Party/PartyScreen.xml`** re-synced too (it carried other stale bindings — missing `ScrollToCharacter`/`IsScrollTargetPrisoner`/`ScrollCharacterId`, `TextWidget`→`RichTextWidget`, obsolete `EaseIn`), **re-applying** TAOM's 2 intentional tweaks (`PartyNameLabel.Height=55`; wage-widget 80 / icon 40 / margin 40 for T10 wages). Verified it now diffs vanilla 1.4.5 by exactly those 4 lines.
- **Surgical binding rename** (TAOM-original prefabs, no vanilla counterpart; datasource is the vanilla `ImageIdentifierVM`, so XML-only): `MomentumView/MomentumView.xml` (×2), `MomentumView/KingdomIcon.xml`, `MomentumView/Relationship.xml`.

Static-verified: 0 `ImageTypeCode` remaining; all 10 touched files (9 + PartyScreen) well-formed XML; the 6 verbatim re-syncs byte-identical to vanilla 1.4.5. **`Not-tested:` in-game render — GUI prefab XML is not unit-testable; only the live game confirms it. Confirm troop thumbnails load on the Party screen, the Custom Battle troop-selection screen, and the MomentumView.**

## Investigation notes — two confident theories ruled out with evidence

Three parallel `Explore` agents returned three *different* root causes; the discipline that mattered was verifying each against the codebase before acting (`evidence-over-claims` §A):

- **"Missing `fighter_sturgia` body property"** (dead). `fighter_sturgia` is defined in vanilla `SandBoxCore/ModuleData/sandboxcore_bodyproperties.xml`; TAOM depends on SandBoxCore (`LoadBeforeThis`) and no XSLT strips it. Decisively, TAOM's own CHANGELOG documents that troops on *wrong* body templates "rendered as generic Empire men in-game" — a body-template mismatch renders a **wrong-looking** troop, never a hang.
- **"`CharacterTableau.SetRace` reflection"** (wrong widget). That patch drives the big 3D `CharacterTableauWidget` center preview, not the row thumbnails (which are `ImageIdentifierWidget`).
- The `AgentVisuals.Create` banner patch is benign (3 guards, then one `AddColorRandomness(false)` — cannot hang a render).

The decisive evidence was a `diff` of TAOM's `PartyTroopTuple.xml` against vanilla 1.4.5 at the exact stuck widget: vanilla `TextureProviderName`, TAOM `ImageTypeCode`.

## Blast-radius audit (workflow, adversarially verified)

A follow-up audit inventoried **every** TAOM GUI prefab against vanilla 1.4.5 (an independent verify agent corrected the first pass — occurrence count 12 not 24; one missed clone; a legend stat):

- **48** TAOM GUI prefab XML files; **32** are clones of vanilla 1.4.5 (by filename); **16** are TAOM-original. Only **4** clones are byte/whitespace-identical to vanilla.
- **Same bug class, FIXED as a follow-up** (commit `a5d7914`, after verifying each against vanilla):
  - `CustomBattle/SimpleDropdown.xml` — `DropdownWidget` bound `RichTextWidget="…\SelectedTextWidget"` but vanilla 1.4.5 uses `TextWidget=` (4/4 vanilla `DropdownWidget`s); fixed so the selected-faction text binds. *(Verified-broken.)*
  - `LayoutImp.LayoutMethod` → `StackLayout.LayoutMethod`: renamed across **6** prefabs (`CustomBattle/{ArmyComposition,CustomBattleScreen,SimpleDropdown}`, `FacGen/PreBuildCharacterSelection`, `MomentumView/{MomentumView,Relationship}`). Verified obsolete (0 vanilla `LayoutImp.LayoutMethod` vs 926 `StackLayout.LayoutMethod`); value-preserving. The valid `LayoutImp.Horizontal/VerticalLayoutMethod` attributes were left intact.
- **Two audit "follow-up" items were FALSE POSITIVES — verified against vanilla and NOT changed:**
  - `EaseIn="true"` is **not** obsolete: vanilla 1.4.5 uses `EaseIn` 18× (it coexists with `EaseType`). Changing it (esp. `EaseIn`→`EaseOut`, opposite directions) on working TAOM-original screens (`CareerScreen`) would have been wrong.
  - The `AutoScroll*Offset` ↔ `ScrollYOffset` rename was stated **backwards**: `ScrollYOffset` is the stale form (0 in vanilla), `AutoScrollTopOffset/BottomOffset` is current (137× in vanilla). The original stale TAOM clones had `ScrollYOffset`; the re-sync in `098ede9` already adopted vanilla's `AutoScroll*Offset`, so there is no remaining drift.
- **Intentional TAOM redesigns — LEFT ALONE (not rename casualties):** the Encyclopedia banner-widget swaps (`EncyclopediaClanListElement` replacing vanilla's `EncyclopediaClanSubPageElement` across Clan/Faction/Settlement/Hero pages), the `SettlementNameplateItem{Large,Medium,Small}` diamond-layout redesign, and the `CharacterCreationCultureStage`/`CharacterCreationNarrativeStage` theming. These diverge from vanilla by design; re-syncing them would erase TAOM customization. The CustomBattle clones were fixed **surgically** (not re-synced) for the same reason — their other divergences are vanilla *additions* TAOM lacks, not breaks.

## Why this shipped — the 1.4.5 migration boundary

The v1.3.15→v1.4.5 migration (landed 2026-05-22) scoped its audit to **C# API drift** (adapters, GameModels, Harmony patches) and **equipment XML migration** — see `docs/migration/TRACKING.md`. GUI prefab clones were **not in scope**, and `docs/migration/` contains no mention of auditing/re-syncing them. The clones themselves were bulk-added in **March 2026** (commit `c31570f` "Thyrell Updates"), well before the migration, and were never re-touched — so they sat outside the migration boundary and shipped with the deprecated `ImageTypeCode` binding.

This is why neither `/deep-review` nor `/review-codex` caught it: both are **static + C#-centric**, and prefab-render correctness is only observable in the live game. The bug was found by the user opening the Party screen — the same "only the live game confirms a prefab renders" limitation called out in `gui-ui.md` ("rendering ≠ live").

## Prevention codified

1. **`.claude/rules/vanilla-data-comparison.md`** extended: new "GUI prefab clones go stale across engine versions" section + GUI-prefab globs added to its `paths:` (so it auto-loads on prefab edits) + a **verified** 1.4.5 attribute-change table (`ImageTypeCode`→`TextureProviderName`; `LayoutImp.LayoutMethod`→`StackLayout.LayoutMethod`; `ScrollYOffset`→`AutoScrollTopOffset/BottomOffset`; `RichTextWidget=`→`TextWidget=` on `DropdownWidget`) plus a "verify each suspected rename against vanilla — do not trust a list" caution.
2. **`.claude/rules/gui-ui.md`** (auto-loads for `Main/_Module/GUI/**`) gains a "GUI prefab clones" caution pointing at the rule above.
3. **Memory** `feedback_gui_prefab_clones_stale_across_versions.md`.
4. **Recommended audit tool (offered, not yet built):** `tools/audit_gui_prefab_clones.py` — diff every TAOM `GUI/PreFabs/**` clone against its installed-vanilla counterpart and report drift (the manual method the audit workflow used: enumerate clones by filename, `diff -w --strip-trailing-cr`, classify rename-casualty vs intentional redesign). Belongs in the post-version-bump checklist.

## Process notes

- **Casing drift bit the investigation.** Git tracks these files as `Main/_Module/GUI/PreFabs/…` (capital-F), but the on-disk directory is lowercase `Prefabs`. Windows' case-insensitive FS hides this for `Read`/`Edit`/`grep`, but **git pathspecs are case-sensitive** — `git status -- Main/_Module/GUI/Prefabs/` returns falsely empty. Use the git-tracked casing (`PreFabs`) for git commands, or no pathspec.
- **Uncommitted GUI fixes are fragile.** During this session the fix was re-applied after an external `git reset`/`stash` (concurrent with two unrelated commits) silently discarded the first round of uncommitted prefab edits. **Commit prefab fixes promptly** rather than leaving them in the working tree across long-running background work.

## Related

- `.claude/rules/vanilla-data-comparison.md` (the data-side sibling rule, now covering prefabs)
- `.claude/rules/gui-ui.md` ("rendering ≠ live"; sprite-bake + PrefabExtension safety)
- `feedback_scene_name_refs_break_on_version_bump.md` (the scene-ref analogue of this same failure class)
