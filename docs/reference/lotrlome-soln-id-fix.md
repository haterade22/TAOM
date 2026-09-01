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

It is reached two ways, and neither can ever produce a custom id:

- Eight hardcoded call sites in `Module.CreateProcessed*XMLForNative` (`soln_skins`,
  `soln_item_holsters`, `soln_action_sets`, `soln_action_types`, `soln_animations`,
  `soln_voice_definitions`, `soln_sound_event_data`, `soln_sound_parameter_data`).
- One `[MBCallback]`, `CreateProcessedModuleDataXMLForNative(string xmlType)`, which builds
  `"soln_" + xmlType` from a type name **native** passes in. Native only ever asks for its own
  standard types.

So a row with an id nothing requests is never iterated. No file is opened, no warning is logged, and
the row reads exactly like working registration. Vanilla's own `project.mbproj` defines the entire
legitimate vocabulary: 39 ids. Measured across the 12 installed modules, `LOTRLOME_Armory` was the
**only** one that had invented any; ADOD, ADOD_Beasts and Alliance.Wargs all use vanilla ids
exclusively.

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
general (three `soln_monsters` rows ship today), but the two cases are not equivalent, and the
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

`soln_monsters` has **no** XSD, so its three rows take the plain-append path. `soln_action_types.xsd`
**does** exist, so a second row would take the MergeElements path. One file, no merge, no risk.

### 2. `soln_spider_monster` (inert, and always was harmless)

`ModuleData/Monsters/LOTR/lotr_monster_spider.xml` was registered under a custom id, so that row
never did anything. The spider was never affected: the Monster is registered the **managed** way, in
`SubModule.xml` as `<XmlName id="Monsters" path="Monsters/LOTR/lotr_monster_spider"/>`, which is also
how the mumakil, the chariot and the war ram load. Four monsters ship through the SubModule path
alone and all four work in game, so the native `project.mbproj` registration is not required for a
Monster.

**Fix:** the row was removed and replaced with a comment recording the above.

**Do not "restore" it as `soln_monsters`.** That would start merging the spider into the native
monster table, a behaviour change to a shipping feature that nothing is asking for.

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

## Verification

```
python tools/audit_mbproj_registration.py           # TAOM's three modules; --all to sweep everything
```

Measured after the change:

- `action_types.xml`, `project.mbproj`, `action_sets.xml`, `monsters.xml` all parse.
- 247 declarations in `action_types.xml`, no duplicates.
- Unresolved actions in `action_sets.xml`: **27 before, 7 after.**
- Only vanilla `soln_*` ids remain (8 live rows), and every `<file name=>` resolves on disk.
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
