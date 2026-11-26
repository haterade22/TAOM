# Codex pre-review dispatch

Step 0 of `.claude/skills/deep-review/SKILL.md` sends you here once the pass is triggered, authorized and `codex login status` has passed. Do these in order, then return to Step 0 item 3. A later Codex pass of the same review (a focused pass from Step 2c, the fix-loop re-review) is dispatched the same way, at the effort its step names.

   - Write a focused prompt to `docs/reviews/codex-prereview-{feature}-{date}.prompt.md` (short version of the `/review-codex` prompt -- focus on Known Suspects + architectural risks; skip the heavy vanilla-decompile block).
   - Run via Bash:
     ```
     command: cd "<repo-root>" && mkdir -p docs/reviews/raw && codex exec -c model_reasoning_effort="<level>" -c project_doc_max_bytes=65536 - < "docs/reviews/codex-prereview-{feature}-{date}.prompt.md" > "docs/reviews/raw/codex-prereview-{feature}-{date}.md" 2>&1
     run_in_background: true
     timeout: 7200000  (an unattended run stops a background command at its timeout, 2 h at most)
     ```
   - `<level>` defaults to the one the Step 2 tier table gives; the session may lower it and says why in the dispatch line. The shorter prompt shortens the run, not the level.
   - See `.claude/skills/review-codex/SKILL.md` "Codex CLI invocation contract" for full dispatch semantics.
