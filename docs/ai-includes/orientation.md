# Orientation

Where things are in TAOM, and the trap index. [AGENTS.md](../../AGENTS.md) links here and
CLAUDE.md imports it ([ADR-011](../adrs/011-knowledge-delivery-tiers.md)).

## Where things are

- **Tools:** the catalog is [tools/README.md](../../tools/README.md). Blender retopology: the
  [quad toolkit](../features/cube-map-remesh.md#required-workflow-selection), never direct
  `xremesh.exe` (cube-map for eligible meshes, Exoside otherwise).
- **Maps:** [docs/INDEX.md](../INDEX.md) by topic, [feature-map.md](../reference/feature-map.md)
  from feature to code, [doc-lookup.md](../reference/doc-lookup.md) from task to doc.
- **Registries:** for crash triage, grep the failing type in
  [harmony-patch-registry.md](../reference/harmony-patch-registry.md); model overrides are in
  [gamemodel-registry.md](../reference/gamemodel-registry.md).
- **Three modules:** the data spans this repo plus the live, unversioned `TAOM_Map` and
  `LOTRLOME_Armory` installs ([module coverage](../features/moduledata-validation.md)).
- **Two machines:** every `E:\` path is the desktop. The laptop holds a partial install, so
  reference failures there are environment gaps, never repo defects
  ([development-machines.md](../reference/development-machines.md)).
- **Localization:** new player-facing text is `{=KEY}default`, then registered, translated and
  validated. 12 languages, all AI first drafts; `tools/translation_overrides/<lang>.json` wins
  ([TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md)).
- **Lessons:** the evidence archive per subsystem is `docs/reviews/lessons/<category>.md`
  ([index](../reviews/LESSONS-LEARNED.md)). Before touching a subsystem, list its headings with
  `grep -n '^### '`, read the entries that apply, and name them in the brief or your one-line plan.
  Append after every review or RCA.

## Trap index

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the history
and the gate. The first four rows are trap sheets: path rules that load when you open a matching file
in this checkout. Read a sheet by hand before you design in its area, and wherever no path rule
fires (a sibling worktree, a client without path rules). A new trap goes in its sheet; it goes here
only when no sheet's files reach the work.

| Trap | Rule | Doc |
|---|---|---|
| Sheet: campaign C# | Behaviors, ticks, menus, map events, capture, marriage, settings, console, `SubModule.cs` | [campaign](../../.claude/rules/traps/campaign.md) |
| Sheet: missions | Mission logic, agents, formations, siege ladders, moving platforms, native calls | [mission](../../.claude/rules/traps/mission.md) |
| Sheet: creatures, assets | Mounts, creatures, race rigs, skeletons, animation clips, Kit and UE imports | [creatures](../../.claude/rules/traps/creatures-assets.md) |
| Sheet: troop, culture data | Troop and character XML, equipment sets, start kits, culture files, ranged ladders | [data](../../.claude/rules/traps/data.md) |
| TAOM_Map settlements | Edit the live `TAOM_Map/ModuleData/settlements.xml`; the repo's copy is a stale shadow | [naming](../reference/taom-map-settlement-naming.md) |
| Prefab entity cap | The 131,072 queue is global across modules; `check_prefab_budget.py` counts TAOM_Map only | [module map](../modding/module-map.md) |
| Unversioned modules | A fix in the live Armory or TAOM_Map reverts on reinstall; land an in-repo gate with it | [coverage](../features/moduledata-validation.md) |
| Artist commit ports | Base: mirror revision nearest the file, not the commit parent: `merge_live_file.py --base-search` | [lesson](../reviews/lessons/data-content-cultures.md) |
| Vendored DLLs | `Main/_Module/bin/Win64_Shipping_Client/` ships no vendored binary (`TAOM.dll` is build output); never MCMv5 | [deps](../modding/module-dependencies.md) |
| Other mods | Other mods and tools reach TAOM by name; a rename passes the build and breaks them: grep the list | [couplings](../reference/external-mod-couplings.md) |
| Armory | `LOTRLOME_Armory`: a root `<action>` kills a server, a second `LOTRLOME_items/*/` merges into an id, loose `Assets/**` wins | [armory](../reference/armory-guide.md) |
| Co-op gating | Loaded, authority, dedicated server, human party; server X.Instance not null; `GameNetwork.IsSessionActive` false in co-op | [co-op](../features/coop-interop.md) |
| Server stop | A hard-killed launcher leaves its engine on UDP 4200: end both, or the next boot exits 3 | [server](../features/coop-dedicated-server.md) |
| Culture ids | `empire` is Dunland, `battania` Khand; troops may carry another culture: read both from the data | [ids](../modding/id-cheatsheet.md) |
| Armory art drops | A mesh rename strands XML refs and hangs preload; repoint refs, never restore a tpac | [ref audit](../features/armory-ref-audit.md) |
| `bo_` bodies | Use the mesh's own `bo_` twin, never a borrow; keep the misspelled `bo_capwm_isengard_shield_a02_clean` | [validation](../features/moduledata-validation.md) |
| Unsaved tpac | No `.rdc`: skipped (`check_rdc_entries.py`). A build reverts a Kit save, not its `.rdc`: repo it first | [banners](../reference/banner-icon-generation.md) |
| Loc keys | An English edit stales 12 languages; a copied `{=KEY}` shows the original: reset rows and cache, one key per id | [guide](../localization/TRANSLATOR_GUIDE.md) |
