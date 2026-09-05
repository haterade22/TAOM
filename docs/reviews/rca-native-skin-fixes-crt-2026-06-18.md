# RCA — NativeSkinFixes CRT load-failure fix (Codex review, 2026-06-18)

**Summary.** The CRT load-failure fix (Debug `/MTd` static + three guards) passed a 4-agent `/deep-review` with zero bugs. Codex (`gpt-5.5`, xhigh) then found **1 HIGH + 2 LOW**, all confirmed against the repo and all fixed in-session. None were defects in the *runtime* fix (the static DLL is correct); all three were gaps in the *guard tooling* and *docs* around it. The HIGH is the highest-value catch: the commit-gate hook validated the on-disk working-tree DLL, not the staged blob that actually gets committed — so it could not catch the exact mismatch this change creates (a static DLL on disk while a stale dynamic blob is staged / the rebuilt DLL left unstaged).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `check-native-dll-crt.sh` ran `pe_inspect` on the **on-disk** `$DLL`, not the **staged blob**. The committed/HEAD DLL is still the dynamic build; the static rebuild is unstaged. A commit that stages a dynamic blob while a static file sits on disk (or vice versa) would be mis-validated. | Verification gap (wrong artifact) | Modeled the hook on `check-moduledata-validation.sh`, which validates the *working-tree* XML — correct for that hook (the validator reads files). For a **binary artifact** the staged blob is what's committed and can differ from disk; I didn't re-evaluate that when copying the pattern. | Hook now extracts `git show ":$DLL"` to a temp file and inspects **that**. New rule: a commit-gate hook that validates a file's **content** must check the staged blob, not the on-disk file. CI already checks the committed blob (post-checkout), so it was the backstop — but the local hook should be correct on its own. |
| 2 | LOW | Two-stage git matcher treats `git -C . commit-tree` / `git -c k=v commit-graph` as commits (the reject only caught contiguous `git commit-`). | Convention gap (canonical pattern) | Copied the canonical two-stage matcher from `.claude/rules/harness-facts.md` **verbatim** — the gap is in the canonical pattern itself, which `*"git -"*" commit"*` accepts but only rejects `*"git commit-"*`. The deep-review hook agent validated *conformance to the canonical pattern*, not the pattern's own correctness. | Fixed in this hook: reject `*"git commit-"* \| *"git -"*" commit-"*`. **Systemic follow-up (separate PR):** the same gap exists in the canonical pattern + 4 sibling hooks (`check-moduledata-validation`, `check-changelog-changed`, `check-claude-files-tracked`, `suggest-compact`). Update `harness-facts.md` + sweep siblings. |
| 3 | LOW | Doc + hook comment claimed a static build imports "only `MinHook.x64.dll` + `KERNEL32.dll`"; the actual `/MTd` build also imports OS DLLs `SHELL32.dll` / `ole32.dll`. | Doc drift (wrote before observing) | Wrote the doc/comment from *expectation* before the final static DLL was rebuilt and inspected. The actual imports (`SHELL32`/`ole32`, pulled in by the static CRT) only appeared in `pe_inspect` after the re-vendor. The CHANGELOG — written after the re-vendor — was correct. | Doc + comment now list the OS DLLs. Reinforces evidence-over-claims §C: state import lists / counts from the actual `pe_inspect` output, not from expectation. |

## Root-cause pattern

Two of the three (1 and 3) share one root: **I wrote the guard/doc from a mental model of the artifact, then didn't reconcile it against the artifact's real bytes.** Finding 1 = "validate the artifact that actually ships (staged blob)"; finding 3 = "describe the artifact from its real imports." Both are the evidence-over-claims §C reflex applied to *tooling and docs*, not just prose: read the proving output (`git show :path | pe_inspect`, the real import list) before asserting what the artifact is.

Finding 2 is a separate, pre-existing systemic gap in a shared convention — faithfully inherited, which is why a conformance-checking review passed it.

## Why each `/deep-review` agent missed these

- **Hook-conventions agent** — validated the hook against the canonical harness-facts pattern and the sibling template, and reported PASS. It checked *conformance*, so it could not catch (1) the staged-vs-on-disk distinction (the sibling it compared against legitimately checks the working tree) or (2) a latent gap in the canonical pattern itself. Conformance ≠ correctness when the reference is also flawed.
- **PowerShell/regex agent** — scoped to Build.ps1 + the regex; the staged-blob question lives in the bash hook, out of its lens.
- **C#/vcxproj agent** — scoped to NativeHookLoader.cs + vcxproj; not the hook or docs.
- **Completeness/consistency agent** — confirmed doc ↔ CHANGELOG ↔ CLAUDE.md told the *same story*, but didn't cross-check the doc's import-list claim against actual `pe_inspect` output (it took the narrative as the source of truth). Codex cross-checked the bytes and caught the `SHELL32`/`ole32` omission.

**What Codex did that the agents didn't:** it went to the git **object store** — parsed the index (`:path`) and HEAD blobs in-memory and compared them to the working tree. That is what surfaced the HIGH. The deep-review agents reviewed source logic; Codex reviewed the *artifacts the commit would produce*.

## Feedback memory to codify

One genuine, generalizable lesson worth a memory: **a commit-gate hook that validates a file's content must inspect the staged blob (`git show :path`), not the on-disk working-tree file — they can differ, and the staged blob is what ships.** This is distinct from presence-checking hooks (which legitimately use `git diff --cached --name-only`). Candidate: `feedback_commit_hook_validate_staged_blob_not_worktree.md`.

The matcher gap (finding 2) is already documented in `harness-facts.md`'s git-invocation table; the fix is to *extend* that canonical pattern, not to write a new memory.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
