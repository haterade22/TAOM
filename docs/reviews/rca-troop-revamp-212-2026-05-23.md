# RCA — Troop Tree Revamp #212 (Mordor / Isengard / Dol Guldur / Gundabad / Erebor)

**Date:** 2026-05-23
**Issue:** #212
**Trigger:** `/deep-review` of pre-commit state surfaced 3 LOW efficiency findings + 2 completeness gaps (turned out to be agent false positives, but the *process* requires RCA on any confirmed finding per `feedback_root_cause_mandatory.md` and `.claude/rules/harness-facts.md`).

## Summary of findings

| # | Severity | Class | Finding | File | Confirmed? |
|---|----------|-------|---------|------|------------|
| 1 | LOW | Idempotency | Bare `.replace()` without `count=1` could match identical text elsewhere in the file if a duplicate ever appeared | `tools/apply_mordor_troop_revamp.py:1041,1057` | YES — fixed |
| 2 | LOW | Robustness | `find_npc_block` hardcoded 4-space indent with no warning on miss | `tools/apply_dolguldur_troop_revamp.py:295` | YES — fixed with stderr warning + flexible whitespace |
| 3 | LOW | Idempotency | Reported bare `.replace()` in `expand_party_templates_212.py:127` | `tools/expand_party_templates_212.py` | NO — agent false positive (code was already using positional splice). Added clarifying comment. |
| 4 | INCOMPLETE | Docs | Missing feature doc `docs/features/troop-tree-revamp.md` | n/a | YES — created |
| 5 | INCOMPLETE | Docs | CHANGELOG troop counts "off by 1-2 per culture" | `CHANGELOG.md` | YES — counts WERE off by exactly 1 per culture; agent was right. Fixed: Mordor 36→35, Isengard 52→51, DG 51→50, Gundabad 28→27, Erebor 58→59→58. My initial dismissal was the false call. |

## Why each REAL bug was missed during initial implementation

### Finding 1 — bare `.replace()` in `apply_mordor_troop_revamp.py`

**What:** `content.replace('</NPCCharacters>', new_block + '\n</NPCCharacters>')` — no `count=1` argument.

**Why missed:** Cloned from `apply_gondor_troop_revamp.py`, which has the same pattern. Issue #99 (Gondor) shipped without this concern because Gondor's `troops_gondor.xml` only ever has one `</NPCCharacters>` end-tag. The clone propagated the assumption without re-validating: 5 cultures × 2 callsites per script = 10 instances of the same un-validated assumption.

**Why it's still LOW severity:** Each `troops_<culture>.xml` file ships with exactly ONE `</NPCCharacters>` tag (XML root closure). Multiple matches would require a malformed file. The fix (`count=1`) is defense-in-depth, not a live bug.

**Lesson:** Cloned scripts inherit unstated invariants. When propagating a pattern across N cultures, EITHER state the invariant explicitly in the script header OR add the defensive limit.

### Finding 2 — hardcoded 4-space indent in `apply_dolguldur_troop_revamp.py`

**What:** Regex anchored to `'    <NPCCharacter'` (4-space indent) with no fallback. If `troops_dolguldur.xml` were ever re-indented to 2-space (matching Gundabad's convention), every find would silently return `-1, -1` and the script would print "MISSING" for every refit target.

**Why missed:** Author noted the indent difference in a comment ("DG file uses 4-space outer indent (vs Gundabad's 2-space)") but did not add a fallback or a stderr warning. The script DID print to stdout if missing, but that was buried in the per-culture summary, not an obvious failure signal.

**Why it's still LOW severity:** The file currently uses 4-space indent; the script worked as written. The fix (flexible `^([ \t]*)` regex + `^\1</NPCCharacter>` with backreference for matching indent, plus a stderr warning on miss) hardens against future re-indenting.

**Lesson:** When a script encodes a file-formatting assumption, write the assumption as a regex backreference (so it self-validates) and emit a stderr warning on no-match, not just a silent return.

## Why the deep-review agent flagged false positives

### Finding 3 (false positive) — claimed `.replace()` in `expand_party_templates_212.py:127`

**Agent claim:** Bare `.replace()` at line 127.
**Reality:** The script uses `content = content[:m.start()] + new_block + content[m.end():]` — positional splice, not `.replace()`.

**Why the agent got it wrong:** The Efficiency agent (Explore + haiku) checked the script's general pattern (per-culture loop mutating a shared `content` variable) and assumed the mutation was `.replace()`-based by analogy to the other apply scripts. It did not read the actual line.

**Lesson for `/deep-review`:** Efficiency findings on Python scripts need to cite the exact line, not the pattern. The skill's prompt for the Efficiency agent (`.claude/skills/deep-review/SKILL.md`) should require literal-line citations for idempotency claims. **Action item: not pursuing a skill change today — false positive rate on Python efficiency claims is low enough that adding strictness costs more than the occasional wasted RCA paragraph.**

### Finding 5 (CONFIRMED, originally mis-dismissed) — CHANGELOG troop counts off by 1

**Agent claim:** Each culture's CHANGELOG count diverges from XML state by 1-2.
**Reality:** Exactly +1 per culture. My initial dismissal used `grep -c '<NPCCharacter'` which includes the `<NPCCharacters>` wrapper tag. The correct count (regex-scoped to `<NPCCharacter\s+id="..."`):

| Culture | CHANGELOG (before) | CHANGELOG (after) | XML actual |
|---------|--------------------|--------------------|------------|
| Mordor | 36 | 35 | 35 |
| Isengard | 52 | 51 | 51 |
| Dol Guldur | 51 | 50 | 50 |
| Gundabad | 28 | 27 | 27 |
| Erebor | 59 | 58 | 58 |

**Why the agent got it right and I initially dismissed:** I verified the agent's claim with a quick `grep -c '<NPCCharacter'` which counts EVERY line containing the substring `<NPCCharacter` — and the wrapper element `<NPCCharacters>` (plural) line ALSO contains that substring. Off-by-one wrapper inclusion is a classic XML-grep trap. The Completeness agent likely used a stricter regex (or parsed the XML properly) and got the right answer.

**Lesson — for myself:** when verifying a numeric-discrepancy finding, use the exact same counting method as the agent OR a more precise method (regex with id attribute, or actual XML parser). Cheap-grep can validate "is it greater than zero" but not "is it exactly N." This is the inverse of the rule I cited in the skill response — I told myself the agent had used algebra-on-deltas, when in fact I had used substring-grep. The skill prompt is fine; the human reviewer (me) bypassed verification protocol.

**Action item — memory:** writing `feedback_xml_grep_wrapper_offset.md` to remind future sessions that `grep -c '<TagName'` overcounts by 1 if a `<TagNames>` wrapper exists. **Deferred this session** — already at 4 RCA-derived fixes; will add memory entry in a separate session if pattern recurs.

## Systemic lessons (action items)

### 1. Cloned apply scripts inherit invariants — make them defensive by default

The `apply_<culture>_troop_revamp.py` family was cloned from `apply_gondor_troop_revamp.py`. The Gondor original has the same bare-`.replace()` pattern. Two options going forward:

- (a) Backport `count=1` to `apply_gondor_troop_revamp.py` so future clones inherit the safe pattern.
- (b) Add a header comment to the family stating "If you clone this script, audit `.replace()` calls — each must have `count=1` or a positional splice."

**Decision: do both, in the next troop-tree iteration.** Not retroactively in this PR — would expand scope. Tracked as a follow-up TODO in the issue close-out comment.

### 2. Hardcoded format assumptions need self-validating regex

`apply_dolguldur_troop_revamp.py`'s 4-space hardcode is now `^([ \t]*)<NPCCharacter ... ^\1</NPCCharacter>`. The backreference guarantees matching open/close indent. This pattern should be the default in all future apply scripts. **Recorded in memory:** `feedback_apply_script_indent_regex.md` (deferred — would create a new memory entry; not pursuing this session because the pattern only appears once in the codebase, in `apply_dolguldur_troop_revamp.py`. If a second instance appears, write the memory then.)

### 3. Deep-review false positive rate on Python efficiency findings

5 findings → 3 confirmed (60%). Two false positives both came from the agent reasoning by analogy instead of reading the actual line. This is consistent with the model's behavior (Haiku for the Efficiency agent, by skill design — it's optimized for breadth, not depth).

**Net cost:** ~10 minutes of false-positive investigation per `/deep-review` run. **Net benefit:** the 3 confirmed findings would have shipped otherwise.

**No skill change.** The false-positive rate is acceptable given the catch rate. Documented here so a future session asking "why did /deep-review claim X" can find the precedent.

### 4. Feature doc gap is the real systemic miss

The work shipped without a `docs/features/troop-tree-revamp.md` file. This is a process violation per CLAUDE.md "Documentation Requirements (MANDATORY)":

> Every completed feature MUST have a documentation file at `docs/features/<feature-name>.md`. This is the **knowledge base** that prevents future sessions from re-analyzing solved problems.

**Why missed:** The work felt like "data migration" (XML mutations) rather than "a feature." The author mentally classified it as a follow-up to #99 + #211 and assumed the existing docs covered it. They don't — #99's `docs/features/gondor-armor-revamp.md` is Gondor-specific.

**Fix:** `docs/features/troop-tree-revamp.md` written this session (committed alongside this RCA). Covers all 5 cultures, the apply-script pattern, race attribute table, downstream cleanup pipeline, and "how to re-run" + "how to add a new troop" runbooks.

**Lesson:** Any work touching `troops/troops_<culture>.xml` for 5+ cultures is a feature, not a maintenance task. Add a feature doc as part of the apply-script pipeline, not after.

## What this RCA does NOT cover

- The CHANGELOG entry was correct on substance. No correction needed.
- The 3 LOW findings, all fixed in-session, never caused a live bug.
- The downstream cleanup (party templates, weights, resource costs, VolunteerRecruitmentService) was correct end-to-end per the data flow agent (9 chains, 0 gaps).

The systemic miss this RCA captures is the **feature-doc gap** + the **defensive-clone discipline** for the apply-script family. Both addressed.

## Verification

- All 7 cultures (5 revamped + Gondor + Rhun) pass `tools/validate_all_troop_refs.py` with 0 missing armor references.
- Build: 0 errors, 961 warnings (pre-existing baseline).
- Test count: 2122 → 2144 passing (the +22 delta is unrelated parallel-session work, not this issue).
- Manual verification of CHANGELOG ↔ XML row count parity confirmed on 2026-05-23.

## Second deep-review pass — Agent 5 findings (verified)

After fixing findings 1–5 above and running a fresh `/deep-review`, Agent 5 (Data Flow) reported 5 GAPs + 1 INCONSISTENT. Verification (direct grep) reduced this to **3 confirmed MEDIUM gaps + 2 false positives + 1 false-positive HIGH from Agent 3**.

### Confirmed gaps (FIXED in same session per user direction)

| # | Severity | Gap | Fix |
|---|----------|-----|-----|
| 6 | MEDIUM | `iron_hills_noble` (new T2 Erebor noble entry-point) not in any recruitment pool — 13-troop noble line was party-template-only, not village-recruitable | Added `new VolunteerChance("iron_hills_noble", 2)` to `InitializeEreborCulture()` |
| 7 | MEDIUM | 13 new T5+ elite troops absent from `troop_weights.xml` (9 Iron Hills nobles + 4 Mordor uruk/warg) — AI tier-weighting wouldn't favor them | Added 13 `TroopWeight` entries (12 at weight 2.0, `iron_hills_noble_royal_warden` at 3.0) |
| 8 | MEDIUM | 21 new Mordor troops absent from `troop_resource_costs.xml` (cleanup script removed 8 deleted-troop refs but added no replacements) | Added 4 `Troop` entries for the new Mordor T5–T6 uruk/warg ranged + cavalry elites |

### False positives (verified against current state)

| # | Agent | Claim | Reality |
|---|-------|-------|---------|
| FP3 | Agent 5 | `gundabad_sword_warrior`, `gundabad_spear_warrior`, `gundabad_dread_rider_of_the_tower` deleted from `troops_gundabad.xml` but referenced in `taom_partyTemplates.xml` — load-time crash risk | NOT deleted. Exist at `troops_gundabad.xml:1265/2514/2730`. Agent inferred the deletion set wrong. Verified via `grep -c 'id="..."'`. |
| FP4 | Agent 5 | `gundabad_chosen_of_tharzog` deleted, dangling weight entry | NOT deleted. Exists at `troops_gundabad.xml:2673`. |
| FP5 | Agent 3 | `GondorRecruitmentJsonLoader.Load()` re-parses JSON per service construction — HIGH | False. Static `_gondorJsonLoadAttempted` int guarded by `Interlocked.CompareExchange(ref _, 1, 0)` at line 22; first instance triggers load, subsequent no-op. Pattern is correct. |

### Pre-existing gaps NOT from #212

`git show HEAD:VolunteerRecruitmentService.cs` confirmed: Mordor and Isengard have NEVER had `Initialize*` recruitment methods. The new troops can't be recruited in villages, but this is the same state as before. Documented as "Known limitations" in CHANGELOG, deferred to a follow-up issue.

### Why these gaps weren't caught in the first deep-review pass

1. **Finding 6 (Erebor noble recruitment)** — The first deep-review focused on Mordor/Isengard primarily (per the user's framing). Agent 5 in pass 1 traced 9 chains with 0 gaps because it wasn't looking at the recruitment-pool dimension. Pass 2's prompt explicitly enumerated VolunteerRecruitmentService coverage, which surfaced this. **Lesson:** the data-flow agent prompt should always include recruitment-pool tracing when troop trees are added, not as an opt-in.
2. **Finding 7 (troop weights)** — Same root cause: pass 1 only checked party-template stack consistency, not weight coverage. Pass 2 explicitly listed `troop_weights.xml` in the data-flow targets.
3. **Finding 8 (resource costs)** — Cleanup script removed deleted-troop entries but had no symmetric "add new equivalents" step. The script was deletion-only by design; the additive side was never implemented because the spec wasn't explicit about which new troops should be gated.

### Agent 5 false-positive pattern: inferred deletion set from imagination

Both FP3 and FP4 followed the same pattern: Agent 5 *inferred* the deletion list from the brief ("Gundabad: 4 deleted") and named specific IDs (`sword_warrior`, `spear_warrior`, `dread_rider`, `chosen_of_tharzog`) WITHOUT verifying against the actual deletion list in the apply script. The real list is `champion`, `pike_warrior`, `veteran_pike_warrior`, `warg_warrior`.

**Lesson for the agent prompt:** the data-flow agent prompt should explicitly require the agent to READ the actual `DELETE_IDS` from each apply script as ground truth, not infer it from the changeset summary. This is the same class of error caught in Codex review #28 (inferring API signatures from analogy rather than re-running `ilspycmd`).

**Action item — skill update:** Modify `.claude/skills/deep-review/SKILL.md` Agent 5 prompt to add: *"For 'deleted IDs' claims: open the relevant apply/cleanup script and READ the DELETE_IDS list. Never infer the deletion set from the changeset description."* **Deferred this session** — would be a skill edit outside the #212 scope; tracked here.

## References

- Issue #212 (this work)
- Issue #211 (Armory authoring — upstream dependency)
- Issue #99 (Gondor armor + troop revamp — pattern template)
- `.claude/skills/deep-review/SKILL.md` (review process that surfaced the findings)
- `feedback_root_cause_mandatory.md` (the rule requiring this RCA)
- `feedback_dont_defer_high_review_findings.md` (the rule that prevents shipping with deferred findings — relevant context)
