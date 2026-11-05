# Input pack: WP 145 late-followups

Title (draft): Triage and port the 1.5.x commits newer than the planning pin
Wave: 5   Lane: J   Draft priority: P2
Depends on (WP ids): set by the writer, one edge per WP whose code a ported commit touches

## Orchestrator notes for this WP (binding)
- The programme classified `origin/bannerlord-1.5.x` up to `e45f6df2c`. The branch then moved. This WP covers every non-merge commit in `e45f6df2c..4d1a9105a`, listed below. Use only that pinned range; commits newer than `4d1a9105a` are out of scope and the plan says so.
- Classify every commit with the vocabulary in `data/classified.jsonl` (PORT, PORT-PARTIAL, SKIP-FEATURE, SKIP-CONTENT, SKIP-BOOKKEEPING, SKIP-1.5-ONLY-FIX, SKIP-1.5-ENGINE, SKIP-SUPERSEDED, SKIP-ALREADY-ON-145) by reading its diff. Features, troops, kingdoms and co-op stay out. Never trust a subject line: a `feat` may carry a shared fix, and a `fix` may touch only 1.5.x code.
- A commit fixing code that a WP in this programme ports is PORT or PORT-PARTIAL: it corrects what would otherwise land with a known defect. Name the WP in its disposition row and add a Depends on edge. Plans 101 to 144 are not reopened: this plan runs after them.
- Reviewers already flagged these: `95a348a60`, `4d9ae0e8a`, `32aeb9e10` (career, WP129); `834e0703c` (WP126); `44d4dba46` (WP135); `8afe46d39`, `000bcf1d1`, `ff2114556`, `0385cc041` (gates and hooks, WP116, WP138, WP143); `931d59084` (RCA).
- Use `git log --no-merges e45f6df2c..4d1a9105a -- <path>` to find follow-ups to one file. Where several commits fix one file, port them together.
- Because the WPs this plan builds on are not merged, simulate conflicts against `origin/bannerlord-1.4.5` and state which result depends on an earlier WP; every step has a precheck and a STOP for a missing anchor.
- The Disposition table lists all 62 commits, skips included. If almost nothing qualifies, the plan is short; do not pad it.

## Commits

```
8a7725d94 2026-11-03 docs: v2.0.36 - build contracts for two commander features
a8aac6399 2026-11-03 docs: v2.0.36 - tidy two old records
f2ee9a16f 2026-11-04 feat(seat): v2.0.36 - a commander's view for any mission with a screen
1d5d0a561 2026-11-04 docs(commander): v2.0.36 - correct both build contracts
8b041f03e 2026-11-04 feat(seat): v2.0.36 - a field trial that plays one battle by itself
5f87810ed 2026-11-04 fix(tools): v2.0.36 - validator reuses its file reads and ref walk
8afe46d39 2026-11-04 fix(hooks): v2.0.36 - raw fallback stops over-blocking, scan follows
462d745ec 2026-11-04 fix(ui): v2.0.36 - gate brush sprites, delete an unused gold brush
b935b830a 2026-11-04 docs(ai): v2.0.36 - route the data gates and provenance checks
3b4a406f7 2026-11-04 feat(tools): v2.0.36 - BROKEN_ROSTER_REF, a misspelt EquipmentSet id
b7fefdae6 2026-11-04 fix(tools): v2.0.36 - map tools stop writing factions.json
c30c4e618 2026-11-04 fix(tools): v2.0.36 - realm tools refuse named weapons, dead scenes
c31a7ef69 2026-11-04 fix(tools): v2.0.36 - runners try a crash dump, print the right labels
44d4dba46 2026-11-04 fix(tools): v2.0.36 - loc tools read one source list, seed like the file
1011a4ef6 2026-11-04 fix(tools): v2.0.36 - four small tool follow-ups and their catalog rows
53febe1a1 2026-11-04 test(coop): v2.0.36 - gate co-op presence reads behind an allowlist
834e0703c 2026-11-04 test(core): v2.0.36 - race test reads cultures through the real loader
498d07e08 2026-11-04 fix(ui): v2.0.36 - remove dead visual definitions and gate them
258db5e73 2026-11-04 fix(action-bar): v2.0.36 - drop the dead DataSource on the movie root
c59a2b00c 2026-11-04 refactor(ui): v2.0.36 - one strict hex colour parser
f9a8bdebc 2026-11-04 test(faction-map): v2.0.36 - pin Blue Craig's and Lindon's game_faction
7983854e7 2026-11-04 refactor(race-age): v2.0.36 - drop an offspring rule vanilla repeats
8dbdfeb12 2026-11-04 refactor(banner-colors): v2.0.36 - remove a patch that changes nothing
69275212f 2026-11-04 fix(nameplate-fade): v2.0.36 - stop pinning MCM defaults at construction
32aeb9e10 2026-11-04 fix(armour-ladder): v2.0.36 - each ladder quest gets its own journal
79d90c08f 2026-11-04 refactor(race-age): v2.0.36 - pregnancy rules move into the service
4d9ae0e8a 2026-11-04 fix(careers): v2.0.36 - cloned careers show their source's ability icon
17b071c7a 2026-11-04 fix(warg): v2.0.36 - a rider who leaves a raging warg gets his formation
002f8ff7e 2026-11-04 feat(tools): v2.0.36 - one release check for the TAOM and TAOM_Map pair
14fcc5f33 2026-11-04 chore(data): v2.0.36 - remove 46 party templates and 22 rosters unused
333e74907 2026-11-04 docs(api-snapshot): v2.0.36 - regenerate after two deletions
35aa37439 2026-11-04 docs(mixed-formations): v2.0.36 - correct three stale claims
7a21239dc 2026-11-04 fix(warg): v2.0.36 - a rage always closes and gives the formation back
174a7e898 2026-11-04 fix(race-age): v2.0.36 - keep the pregnancy guards before the clan read
000bcf1d1 2026-11-04 fix(hooks): v2.0.36 - raw fallback refuses again, scan finds its memory
ad8f13d00 2026-11-04 fix(tools): v2.0.36 - map writers read the live row's scenes too
570fa0e85 2026-11-04 fix(tools): v2.0.36 - the translation rebuild writes only on --apply
91185bdc5 2026-11-04 fix(tools): v2.0.36 - a crash dump is judged by its file
b6dc611dd 2026-11-04 fix(tools): v2.0.36 - small tool fixes from the review
334c187e4 2026-11-04 fix(tools): v2.0.36 - read settlement refs in XSLT, skip comments
285c5a1e8 2026-11-04 fix(tools): v2.0.36 - the NuGet rule reads either order and split lines
ed3f911eb 2026-11-04 test(gates): v2.0.36 - close two holes in the new gates
17b738aa6 2026-11-04 chore(data): v2.0.36 - remove Lindon's and Blue Craig's unbound rosters
35044f583 2026-11-04 docs: v2.0.36 - correct the statements the review found false
1499aaa69 2026-11-04 docs(tools): v2.0.36 - catalog rows and notes follow the review's fixes
a4317fd8b 2026-11-04 feat(marshal): v2.0.36 - orders in turn and a line that keeps its shape
aaff9477a 2026-11-04 feat(seat): v2.0.36 - a cursor, a kept panel and a view that follows
4a7b7164d 2026-11-04 fix(warg): v2.0.36 - a new rider closes the rage, the player's returns
0385cc041 2026-11-04 fix(tools): v2.0.36 - last review fixes in the gates and the rebuild
931d59084 2026-11-04 docs(reviews): v2.0.36 - RCA for the follow-up fixes, 59 lines closed
5976636cf 2026-11-04 chore(snapshot): v2.0.36 - record Patch132 in the API snapshot
b7a3ac17b 2026-11-04 feat(fords-of-war): v2.0.36 - towers throw with no bowmen on top
95a348a60 2026-11-04 fix(career): v2.0.36 - Mike's RCA decisions: ladder, gate, HUD, income
316b09529 2026-11-04 docs(reviews): v2.0.36 - record Mike's 14 RCA decisions
38e5e6274 2026-11-04 fix(warg): v2.0.36 - rage timeout ends a chase, Mixed logs its gate
ff2114556 2026-11-04 fix(tools): v2.0.36 - what Codex found in the gates and hooks
3bb89aff5 2026-11-04 docs(reviews): v2.0.36 - record the Codex passes and Mike's answers
53cf195ac 2026-11-04 feat(dire-wolf): v2.0.36 - ride dire wolves in Gundabad and goblins
b42b3d1de 2026-11-04 feat(long-defeat): v2.0.36 - new defenders muster in the open field
ca1bc5f2e 2026-11-04 feat(tools): v2.0.36 - click through engine asserts without a stall
dba8f886e 2026-11-04 feat(long-defeat): v2.0.36 - speed controls at 1x, 2x, 4x and 10x
4d1a9105a 2026-11-04 docs(long-defeat): v2.0.36 - record the Phase 1 proof runs
```
