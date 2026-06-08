---
name: new-culture
description: Author or revamp a TAOM culture's armor set, troop tree, and recruitment wiring end-to-end. Use for new cultures or troop-tree revamps. Follows docs/ai-includes/new-culture-authoring.md.
argument-hint: [culture-id]
---

# New Culture — Armor + Troop Tree + Recruitment

End-to-end authoring of a culture's visible-in-game armor + troops + recruitment. **Read the authoritative guide first:** [docs/ai-includes/new-culture-authoring.md](../../../docs/ai-includes/new-culture-authoring.md). This skill is the entry point + checklist; the guide has the full per-phase detail (modeled on the Dale session, ~11 commits).

## When to invoke
- Solus delivered a new culture's `.tpac` armor pack to wire in, OR
- An existing culture needs a fresh troop tree (revamps like #99 / #211 / #212 / #224 / Dale).

If creating a **net-new `taom_spcultures.xml` Culture object**, read [docs/cultures.md](../../../docs/cultures.md) FIRST (the ~80 culture attributes + 16 NPC files). This flow picks up after the culture definition exists.

## Phases
0. **Prereqs** — confirm the 5 `.tpac` files exist; decide culture ID (custom vs XSLT-passthrough — check `kingdom-culture-mapping.md` memory); decide tier cap; pick 3–4 Tolkien lore citations.
1. **Armor** — harvest mesh IDs (`tools/tpac_skeleton_scan.py --all-types`) → clone `tools/generate_dale_armor.py` → emit XML → register the `<culture>/` folder in `LOTRLOME_Armory/SubModule.xml`.
2. **Troops** — lore + tier design on paper → generator → `troops_<culture>.xml` → register in `Main/_Module/SubModule.xml`.
3. **Wire** — `spcultures.xslt` (every CultureObject template/troop attr), `taom_partyTemplates.xml` (9 templates), `VolunteerRecruitmentService.cs` (culture + optional settlement/clan pools), add tests.
4. **Validate** — `python tools/validate_all_troop_refs.py` (underwear-bug gate), then `/ship`.
5. **Iterate** — expect 5–10 follow-up commits (renames, equipment swaps, balance, settlement-specific recruitment, colors). The first ship is a draft.

## Gotchas (cost rework if skipped)
- **Mesh typos are load-bearing** — the engine binds by exact mesh name; preserve Solus's typos verbatim (Dale's `chivlary`/`infrantry`).
- **Canonical Armory folder** — grep ALL `LOTRLOME_items/*/` for the item prefix before authoring; first folder with that prefix wins (AGENTS.md per-prefix table). Wrong folder = duplicate-ID shadowing.
- **Cover attributes** — leg items need `covers_legs="true"`, gloves `covers_hands="true"`, or the mesh doesn't render (bare legs/hands).
- **`<Flags UseTeamColor="true" />`** on every armor item for banner tint.
- Run `/xslt-check` after editing `spcultures.xslt`.
