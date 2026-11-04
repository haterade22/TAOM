# LOTRLOME_Armory: retiring two dead `project.mbproj` registrations (2026-08-28)

Two `<file>` rows in the external `LOTRLOME_Armory` module
(`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\`) used **invented
`soln_*` ids**, so the engine never read the files they pointed at. One was harmless. The other meant
20 action declarations had never once reached the engine while `action_sets.xml` bound them 221
times.

The module is not tracked by this repo, so a reinstall silently reverts everything below. The
in-repo gate is `tools/audit_mbproj_registration.py`.

## Why an invented id is inert, and why nothing reports it

`MBObjectManager.GetMergedXmlForNative(id)` walks `XmlResource.MbprojXmls` and keeps only the entries
whose `Id` matches **exactly**:

```csharp
foreach (MbObjectXmlInformation mbprojXml in XmlResource.MbprojXmls)
{
    if (mbprojXml.Id == id)   // exact string equality, nothing fuzzy
    { ... }
}
```

It is reached from a fixed set of requesters, and none of them can ever ask for a custom id:

- Six hardcoded call sites in `Module.CreateProcessed*XMLForNative` that something calls
  (`soln_skins`, `soln_item_holsters`, `soln_action_sets`, `soln_action_types`,
  `soln_voice_definitions`, `soln_sound_parameter_data`). The Animations and SoundEventData
  callbacks (`soln_animations`, `soln_sound_event_data`) have no caller in v1.5.5: native callback
  slots 0x2b0 and 0x2d0 are unused, in the shipping and the editor build.
- `MapColorGradeManager.ReadColorGradesXml` (`soln_worldmap_color_grades`), and `MBMusicManager`,
  which reads `XmlResource.MbprojXmls` itself for `soln_soundtrack` rather than going through
  `GetMergedXmlForNative` (the row's name is ignored and the music loads from
  `<module>/Music/soundtrack.xml`).
- One `[MBCallback]`, `CreateProcessedModuleDataXMLForNative(string xmlType)`, which builds
  `"soln_" + xmlType` from a type name **native** passes in: `soln_movement_sets`,
  `soln_full_movement_sets`, `soln_bone_body_types`, `soln_monster_usage_sets` and
  `soln_item_usage_sets` among them. Native only ever asks for its own standard types.

So a row with an id nothing requests is never iterated by that route. No file is opened, no warning
is logged, and the row reads exactly like working registration. The audit's table `REQUESTED_IDS`
holds exactly these thirteen ids, each with its source. It does not take vanilla's own
`project.mbproj` as the vocabulary: Native ships rows nothing requests (`soln_monsters`,
`soln_strings`, `soln_particle_systems` and more), so that file was a superset of the exact list
until #987.

**The second loader reads `type`, not `id`.** Native code parses `project.mbproj` itself and asks
for thirteen kinds of file by `type` (the callers of `FUN_180719810`, the table `NATIVE_BY_TYPE` in
`tools/audit_mbproj_registration.py`), reading each matching row's `name` and never its `id`. A row
of one of those types loads whatever its id is: `soln_module_sound` is one. A `type` outside the
thirteen does nothing, and that includes `action_set`, `action_type`, `monster` and
`monster_usage_set`, the types the dead rows below carried. An earlier version of the gate
accepted any type the vanilla manifest ships, which cleared all six of those rows. Two tests pin the
fix: `NativeTypeTable.test_spider_era_rows_are_dead` copies the six rows verbatim and expects each to
be a DEAD-ID, `MainEntry.test_the_spider_era_rows_stay_dead` runs `main()` on spider-era rows and
expects exit 1 with both dead, and `MainEntry.test_native_project_mbproj_is_not_read_at_all` puts the
same id in Native's own manifest and expects the module's row to stay dead. Measured across the 12
installed modules, `LOTRLOME_Armory` was the **only** one that had invented any; the three
third-party modules all use vanilla ids exclusively.

This class already cost TAOM one crash. In 2026-06 the giant spider shipped `soln_spider_*` ids, its
`action_sets` and `monster_usage_set` never loaded, `GetMonsterUsageIndex("spider")` returned -1 and
native `CreateAgent` divided by zero on spawn. The lesson was written into a comment at the top of
`project.mbproj` and is still there. Two rows survived that cleanup anyway, one of them sitting
directly underneath the comment explaining why it could not work.

## What changed

### 1. `soln_lotr_misc_action_types` (the one with a consequence)

`ModuleData/Animations/action_types_lotr_misc.xml` declared 20 action types. Its own header comment
states its purpose: to stop the engine logging "Trying to use undefined action with name in
action_sets.xml". It had never done that, because it had never loaded.

The 20 names are absent from vanilla `action_types.xml` and from the Armory's own registered
`action_types.xml`, and `action_sets.xml` binds them **221 times** across 34,452 bindings, so all 221
resolved to `act_none`:

| Group | Count | Bindings | Referenced by |
|---|---|---|---|
| `act_character_creation_male_default_0..6` | 7 | 13 each | face-gen sets (`as_uruk_facegen`, `as_uruk_hai_facegen`, ...) |
| `act_character_creation_female_default_0..6` | 7 | 13 each | the same face-gen sets |
| `act_drunk_trio_{left,middle,right}_2` | 3 | 12 each | villager / tavern sets |
| `act_lancer_ride_{0,1}` | 2 | 1 each | cavalry sets |
| `act_polearm_brace_ready` | 1 | 1 | polearm sets |

**Fix:** the 20 declarations were folded into `ModuleData/action_types.xml`, the module's single
registered `soln_action_types` file, as an `LOTR MISC` block at the end.
`Animations/action_types_lotr_misc.xml` was renamed to `.bak-superseded-20260828` so nothing globs it
and nobody edits a dead file.

**A second `soln_action_types` row was rejected deliberately.** Duplicating an id is legitimate in
general (four `soln_monsters` rows ship today, read by nothing), but the two cases are not equivalent, and the
difference is exactly whether an XSD exists:

```csharp
// GetMergedXmlForNative
string text = ModuleHelper.GetXsdPath(id) ?? string.Empty;   // <game root>/XmlSchemas/<id>.xsd
if (!File.Exists(text)) { text = ""; }
...
// MergeTwoXmls
if (keepDuplicates || xsdPath == "")
    xDocument.Root.Add(xDocument2.Root.Elements());          // plain append, safe
else
    MergeElements(xDocument.Root, xDocument2.Root, xsdPath); // schema-driven, can throw
```

`MergeElements` indexes a dictionary built from the schema with a raw `[...]` lookup
(`elementSchema[XmlResource.GetFullXPathOfElement(...)]`), which throws `KeyNotFoundException` on any
element XPath the schema does not carry. That is the elephant "Crash #3" already recorded in
`project.mbproj`.

`soln_monsters` has **no** XSD, so a repeat would take the plain-append path (and nothing requests that id,
so its rows are read by nothing either). `soln_action_types.xsd`
**does** exist, so a second row would take the MergeElements path. One file, no merge, no risk.

### 2. `soln_spider_monster` (inert, and always was harmless)

`ModuleData/Monsters/LOTR/lotr_monster_spider.xml` was registered under a custom id, so that row
never did anything. The spider was never affected: the Monster is registered the **managed** way, in
`SubModule.xml` as `<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_spider"/>`, which is also
how the mumakil, the chariot and the war ram load. Four monsters ship through the SubModule path
alone and all four work in game, so the native `project.mbproj` registration is not required for a
Monster.

**Fix:** the row was removed and replaced with a comment recording the above.

**Do not "restore" it as `soln_monsters`.** That row would be read by nothing either: nothing requests
the id `soln_monsters` and `monster` is not a type native loads, so the audit reports it `INERT-ROW`
beside the `<XmlName id="Monsters">` registration and `DEAD-ID` without it. It would only read as
registration. (The Armory's own comment gives merging into the native monster table as the reason;
that stated reason is wrong, no merge happens.)

## Files touched (all external, all backed up)

| File | Change | Backup |
|---|---|---|
| `ModuleData/action_types.xml` | +20 declarations in a new `LOTR MISC` block (227 -> 247) | `.bak-solnfix-20260828` |
| `ModuleData/project.mbproj` | both dead `<file>` rows replaced with explanatory comments | `.bak-solnfix-20260828` |
| `ModuleData/Animations/action_types_lotr_misc.xml` | retired, renamed | `.bak-superseded-20260828` |

Backups use non-`.xml` extensions on purpose: these directories are globbed with `GetFiles("*.xml")`,
so an `.xml` backup injects duplicate ids.

> **Moved 2026-09-01.** All three are now under
> `E:\Bannerlord_Backups\module_bak_sweep_2026-09-01\LOTRLOME_Armory\` at the same relative paths,
> because `.bak` breaks the Cloudflare distribution. That includes the retired
> `Animations/action_types_lotr_misc.xml.bak-superseded-20260828`, which is no longer in the module
> at all: it had no live sibling, so the sweep classified it as a sole copy rather than a backup.
> The "nothing globs it" outcome above is unchanged and now holds for a stronger reason. See
> [module-backup-sweep](module-backup-sweep.md).
>
> **Pruned by 2026-11-03.** Of these three, only `Animations/action_types_lotr_misc.xml.bak-superseded-20260828`
> is still in that folder; `ModuleData/action_types.xml.bak-solnfix-20260828` and
> `ModuleData/project.mbproj.bak-solnfix-20260828` are not (the manifest keeps their SHA256).

## Verification

```
python tools/audit_mbproj_registration.py           # TAOM's three modules; --all adds the third-party mods (exits 1 on their rows; official modules are skipped)
```

Replay of the pre-fix state: the backup `project.mbproj.bak-solnfix-20260828` (under
`E:\Bannerlord_Backups\module_bak_sweep_2026-09-01\LOTRLOME_Armory\ModuleData\`) was copied into a
scratch install next to vanilla's Native manifest and audited with `--game-dir` and `--module
LOTRLOME_Armory` on 2026-10-23: **2 errors, exit 1**, `DEAD-ID soln_spider_monster` and
`DEAD-ID soln_lotr_misc_action_types`. The four spider-era rows of lotraom-assets `7b45ffb97` are
dead the same way, and the audit's `NativeTypeTable.test_spider_era_rows_are_dead` pins all six.

Replayed again on 2026-10-24 with the exact id list (#987). The backup alone, which holds no
SubModule.xml: **5 errors, exit 1**, the three `soln_monsters` rows (nothing requests that id) as well
as the two above. With the live Armory SubModule.xml beside it, the four `soln_monsters`-class rows,
`soln_spider_monster` among them (SubModule.xml registers `Monsters/LOTR/lotr_monster_spider`), are
`INERT-ROW` warnings, and only `soln_lotr_misc_action_types` stays a `DEAD-ID`: an `action_type` row has no
managed twin (`MANAGED_TWIN`), so it stays DEAD-ID whatever registers its file.

Measured after the change:

- `action_types.xml`, `project.mbproj`, `action_sets.xml`, `monsters.xml` all parse.
- 247 declarations in `action_types.xml`, no duplicates.
- Unresolved actions in `action_sets.xml`: **27 before, 7 after.**
- Only vanilla `soln_*` ids remain (8 live rows), and every `<file name=>` resolves on disk. Since #987 the
  audit lists the four `soln_monsters` rows among them as `INERT-ROW` warnings: nothing reads them and
  SubModule.xml registers their files.
- The gate was mutation-tested against a reconstruction of the pre-fix state built from the backups:
  it exits 1 and reports both dead ids plus all 20 undeclared actions, and exits 0 against the fixed
  install.

**Still owed: an in-game check.** Every claim here is static analysis. The visible prediction is that
face-gen and tavern animations that previously fell back now play their intended clips; that has not
been observed.

## The 7 remaining unresolved actions are a different defect, left alone

```
act_ghurab_captain_idle
act_idle_javelin_{with,without}_shield_{1,2,3}_left_stance
```

These are declared in **no** `action_types.xml` anywhere, so there is no orphaned file to un-orphan;
they are a separate, pre-existing gap. Declaring a name does not make its animation exist, so
"fixing" them needs an animation-inventory check first (the `_left_stance` names look like vanilla
names that drifted across an engine version). They are recorded in `UNDECLARED_BASELINE` in the audit
tool so the gate stays green on the known set and fails on anything new.

## Related

- [lotrlome-war-ram-changes.md](lotrlome-war-ram-changes.md), [lotrlome-warg-changes.md](lotrlome-warg-changes.md): sibling external-module ledgers.
- [armory-guide.md](armory-guide.md): `action_sets` structure and the dedicated-server root-`<action>` trap.
- [../features/spider.md](../features/spider.md): the 2026-06 `soln_spider_*` DivideByZero this class first produced.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/submodule-and-registration.md](../modding/submodule-and-registration.md)
- [docs/reference/lotrlome-warg-changes.md](./lotrlome-warg-changes.md)
- [docs/reference/module-backup-sweep.md](./module-backup-sweep.md)

<!-- backlinks-end -->
