# RCA: the KEYforce Armoury cleanup broke 275 references (2026-09-01)

Seven commits reorganising `LOTRLOME_Armory` synced into the live install. The sync was clean:
zero content drift on every tracked file. The reorganisation deleted art that XML still named and
item definitions that troops still equipped, and the breakage was discovered from blank icons in a
screenshot rather than from any gate.

## Findings

| # | Sev | Finding | Why it happened | Action |
|---|---|---|---|---|
| 1 | HIGH | 212 `BROKEN_ITEM_REF` across 159 consumers. `5cd6115a` deleted 3 shield item definitions while 212 references survived. Denethor, Forlong, Angbor, Golasgil, every Gondor character-creation preset, all four Gondor career starts. | The commit re-pointed three items onto the replacement art it added, and missed the rest. The Armory is untracked, so the commit hook that would have caught it never fires. | Re-pointed onto `sm_gd_shield_a*`, 212 refs in 9 files. |
| 2 | HIGH | The mesh gate reported 63 errors. The real total was 275. | `validate_mesh_refs.py` and `validate_moduledata.py` see disjoint halves and neither can see the other's. Nothing said to run both. | Both now PASS. Lesson recorded. |
| 3 | MED | 16 missing collision bodies, the confirmed #352 infinite-mission-load-hang class. | 8 were a case mismatch introduced by the same commit, 6 were pre-existing, 2 belonged to an unreferenced item. | All resolved. |
| 4 | MED | 22 elven arrow items broken, 385 references. `22f30795 "Fixes"` deleted the art without touching the XML naming it. | A mixed commit (50 files added, 110 deleted) whose title advertises neither half. | Re-pointed onto Mirkwood art, 22 values, all 385 refs preserved. |
| 5 | n/a | Two review agents attributed 24 missing Gondor sword meshes to the cleanup, in opposite directions. Both wrong. | Neither checked the base commit. | Proven pre-existing by scanning all 4,287 base-commit tpacs. |

## The finding that matters most: one gate is never the answer

The mesh gate had just been repaired earlier the same day, so its 63 errors read as the incident.
They were 23% of it. `validate_moduledata.py` independently held 212 more, and the two tools are
blind to each other by construction:

- `validate_mesh_refs.py` walks item XML and resolves mesh names against the asset tree. An item
  that has been **deleted entirely** is invisible to it, because there is no longer an item to walk.
- `validate_moduledata.py` resolves `Item.<id>` references against the registry. An item whose
  **art** vanished is invisible to it, because the item still resolves.

An art reorganisation does both at once, which is exactly the case where checking one gate and
declaring it clean is most tempting and most wrong.

## The near-miss: one symptom, three unrelated causes

Review produced three different explanations for the missing Gondor sword meshes, and the correct
answer was in none of the agent reports:

- one agent listed them as deleted by the cleanup
- another excluded them from its "gone" set without saying why
- the truth, from scanning all 4,287 tpacs at the base commit, is that the only Gondor sword art
  ever in the assets repo is a single tpac holding `a01, a02, a03, a07`. The cleanup moved that
  file and lost nothing. The XML has named ten variants while four shipped, for the life of the repo.

This changed the remedy completely. Art the cleanup deleted is recoverable with one
`git cat-file --filters` command. Art that never existed can only be re-authored or designed around.
The elven arrows were the first case; the swords were the second, and treating them alike would have
produced a restore that silently did nothing.

## What made the repair cheap

Measuring first turned three large-looking problems into small ones:

- **The arrows looked like a 385-reference edit.** Only the arrow mesh was deleted; all 16 elven
  quivers survive. It became 22 `mesh=` values with every reference untouched.
- **The swords looked like they needed re-authored art or 94 consumer edits.** 16 surviving parts
  give 256 combinations, so the six were rebuilt as distinct weapons by changing which pieces each
  item names. The collision body comes from the blade piece, so the #352 risk resolved as a side
  effect rather than needing its own fix.
- **8 of the 16 missing bodies were a case mismatch**, not deletions. 0 of 24,648 asset names contain
  uppercase, and those 8 were the only mixed-case refs in the Armory or in vanilla.

## Why the tooling could not have caught it

There was no baseline to diff against. Every inventory the repo held had rotted:

| Artifact | State |
|---|---|
| `tools/reports/mesh-audit/` | roughly 250 names out of date, and under a gitignored directory so it could never diff |
| `armory-guide.md` Gondor row | carried 5 of 17 regional tokens for months, by its own admission. Corrected 2026-08-04, so it was already right when the cleanup landed; it is here as an example of the rot, not a cause of this incident |
| `tools/dale_armor_meshes.txt` | written once in May, never revisited |

`docs/reference/armory-catalogue/` is the fix: generated, committed, and diffable, with `--diff`
classifying RENAME / MOVE / DELETE / NEW rather than emitting a line diff. The gate that stops it
rotting like its predecessors is that it exits 1 when a mesh resolves to an unknown culture or
category with no override row, so a new naming shape fails loudly.

**Deliberately not git-based.** `*.tpac` is LFS-tracked, so `-M --find-renames=40%` compares
~130-byte pointer files of near-identical boilerplate and fabricated 37 renames between wholly
unrelated cultures on these commits, including a Dol Guldur texture "renamed" to an Arnor steelbow.

## Corrections to what the audit itself produced

Worth recording, because these were confident and wrong:

- **"The troll art is undeployed, not deleted."** The repo-only tpac cited as proof is 405 bytes
  holding one item of unknown type named `LOME_troll_armor.fbx`, an FBX source stub with zero
  metameshes. `lotr_troll_armor` exists in neither tree. The existing allowlist entry was correct.
- **"`dale_armor_meshes.txt` is a rotting hand list, retire it."** It is the required default input
  (`MESHES_DEFAULT`) to `tools/generate_dale_armor.py`. Deleting it would have broken that generator.
- **"134 items have a `has_gender_variations` mismatch."** Built on the misreading that `_slim` is
  the female variant. It is the slim-BUILD suffix on the engine's non-female branch.

Each of those three, acted on unverified, would have caused a regression, and the check that
refuted it was one command in every case. A confident agent report is a hypothesis.

## Two defects in my own tooling, caught by its own guards

- The catalogue's rename classifier joined on the tpac path alone. A geo tpac holds many meshes, so
  a genuine deletion from a tpac that also gained a name read as a rename, and the report said
  nothing was lost. Caught by a simulation test written before trusting the output; fixed with a
  similarity check inside the candidate set.
- The override lookup keyed on the raw mesh name while the file stores escaped names. Identical for
  every normal name, and wrong for exactly the one asset most likely to need an override: the mesh
  carrying eight embedded NUL bytes.

Both are arguments for testing a new tool against a simulated change before believing its output on
a real one.

## What the second review round changed

The sword rebuild rested on one claim: that the collision body comes from the blade piece, so
choosing a surviving blade fixes the missing body for free. A review agent decompiled it and the
claim holds exactly. `InitCraftedItemObject` reads `UsedPieces[0].CraftingPiece.BladeData` for
`BodyName`, `UsedPieces[0]` is the blade slot, and `CollisionBodyName` is hard-set to the empty
string for every crafted item (`ItemObject.cs:355-368`). All six now resolve real bodies and the
mesh gate reports zero missing across 5,783 references.

What the round found, all of it things the work had not stated:

- **`weapon_descriptions.xslt` was never cleaned.** The 24 ids came out of
  `crafting_templates.xslt` and stayed in its sibling. Inert, because
  `WeaponDescription.Deserialize` skips a null lookup and vanilla already dangles 129 `mp_*`
  entries there, but the two files are a pair in the authoring workflow and half a change is the
  hazard. Removed from both trees.
- **The rebuild is a balance change, and nothing said so.** `a05` and `a10` were built on the two
  longest Gondor blades ever authored, 101.55 cm, and both are art that never shipped. They lose 31
  and 29 cm of reach against 53 and 18 references. Unavoidable, since the longest surviving blade is
  82.96 cm, but it belonged in the CHANGELOG the first time rather than the second.
- **One seam is unverifiable from XML.** Each guard's `next_piece_offset` was authored for its own
  blade, so mixing moves where the blade seats by up to 6 cm on `a08` and `a09`. The length
  arithmetic accounts for it, so reach is right; whether the join reads as a gap is a model-viewer
  question.

The same pattern recorded above about agent reports applies to my own output. Every claim that
survived scrutiny had a decompile behind it, and neither finding was a wrong fact: both were true
facts left unwritten, which is the failure mode a repair is most prone to once the errors are
gone.

A fourth finding is a decision rather than a defect, and it belongs to you. **The deleted arrow art
is recoverable.** `22f30795^:...wm_elven_arrows_geo.tpac` is 279,027 bytes of real content, not an
LFS pointer, and it holds all eight original meshes. The cull was treated as intended per your call,
so the substitution stands, but the option is one `git cat-file --filters` per file (the geo pack
plus four material and six texture packs) and it would restore the eight-way variety the three
surviving Mirkwood meshes cannot express.

What the round did change about the substitution is the grouping. The first mapping split on the
`_q` and `_v2` tokens in the item ids, and those encode which quiver an arrow is drawn from, not
which arrow it is. The original art followed one rule with no exceptions: odd quivers took the `v1`
family and even quivers took `v2`, 11 items each. The token split gave 6/4/12 and made
"Elven Arrow II" look unlike I, III and IV for no reason a player can read. Regrouped to the parity
rule, verified 11 and 11 with zero violations. The third Mirkwood mesh is deliberately unused: three
meshes cannot carry an eight-way split, and a third group would assert a distinction the surviving
art does not have.

One cosmetic consequence survives either way. The 22 keep their `[Noldor]` names on
`Culture.rivendell` while rendering Mirkwood art, and `wm_mirkwood_arrow_a01/a02/a03` ship as their
own items named `[Mirkwood] Elven Arrow I/II/III`, so a player can hold a Noldor and a Mirkwood
arrow side by side and see one mesh. Restoring the art is what removes that.

The engine-behaviour detail the round produced is now in
[weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md), including the `<UsablePiece>`
membership gate, which nothing in the repo documented and which is precisely the gate a piece
deletion sits on.

## Still owed

- In-game verification after a full restart: a Gondor field battle for the 159 restored shield
  consumers and the 6 rebuilt swords, and a new elf campaign for the arrows.
- `lotraom-assets` has 333 uncommitted `ModuleData` files and the live install matches that worktree
  rather than HEAD. A `git checkout` there destroys the merge result and this session's edits. The
  user owns that repo.
- Preview the guard-to-blade seam on `a08`, `a09`, `a04` and `a10` in `tools/BannerlordCraftingTool/`,
  which reproduces `CalculatePivotDistances`. Cheaper than a game launch and it targets the only
  claim in the sword rebuild that no offline check can settle.
- A decision on the `a05` and `a10` reach loss, 114 cm down to 83 and 85 across 71 references. It is
  forced by what art survives, not chosen, but if Gondor needs a long sword back the longest
  reachable build is a02's blade on a02's guard at 90.4 cm.
- A decision on whether to restore the elven arrow art after all. Recoverable in 11
  `git cat-file --filters` commands; the substitution is correct but carries two meshes where the
  author shipped eight.
- `tools/generate_armory_catalogue.py`, `tools/armory_catalogue_overrides.tsv` and
  `docs/reference/armory-catalogue/catalogue.tsv` are untracked, so the README's links to the first
  two publish dead. `lint_docs.py` reports them under "Link targets present but untracked".
- No GitHub issue exists for this incident.
