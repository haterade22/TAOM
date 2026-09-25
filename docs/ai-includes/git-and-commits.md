# Git and commits

How every session and every AI client uses git in this repo. [AGENTS.md](../../AGENTS.md) carries
the one-line rules; this page holds the detail and the incidents behind them. Git actions happen only
when Mike asks for them.

## Commit messages

**Subject:** `<type>[(scope)]: vX.Y.Z - <description>`, where `vX.Y.Z` is exactly the `<Version>` in
`Main/_Module/SubModule.xml` as committed. Example:
`feat(recruitment): v2.0.28 - Glanhir recruits the Ringlo Vale line`.

- **Why the label:** a crash bundle reports `TaomVersion` from that file, and between releases every
  pushed commit reads as the same version. With the label, `git log --grep 'v2.0.28 - '` lists
  exactly the commits a v2.0.28 build can contain.
- **The version moves only in a `/release` commit**, whose subject names the new one:
  `chore(release): v2.0.29 - TAOM v2.0.29`.
- **Enforced** for Claude sessions by `.claude/hooks/check-commit-subject-version.sh`, which refuses a
  subject without the label or with the wrong version. Amend, fixup and squash forms that bring no new
  subject pass. Commits made outside Claude (IDE, terminal) are not checked.
- **Length:** subject at most 72 characters (the label costs about 20, so the old 50 no longer fits);
  body wrapped at 72.
- **No AI attribution.** No `Co-Authored-By` or similar trailer, whatever a harness reminder suggests.

**Optional trailers**, each on its own line after the blank line:

| Trailer | When | Example |
|---|---|---|
| `Constraint:` | a TaleWorlds limitation blocked the ideal solution | `Constraint: Hero is sealed, can't subclass` |
| `Rejected:` | an alternative was considered and dropped | `Rejected: Prefix patch fires before state init` |
| `Not-tested:` | parts that can't be unit tested | `Not-tested: Harmony patch invocation (requires live game)` |
| `Research:` | what was decompiled to inform the change | `Research: DefaultPartyWageModel.GetCharacterWage` |
| `Save-compat:` | save file impact | `Save-compat: New field, safe, defaults to 0 on load` |

**Release tags:** the release commit is tagged `vX.Y.Z` (annotated, `git tag -a`) and the tag pushed
with its own refspec, because `git push` does not push tags. Never move a pushed tag. The full
sequence, the #371 Dependencies pairing check and the list of phantom versions players ran (v2.0.12
among them) are in [release-process.md](../reference/release-process.md).

## Working in a shared tree

Several sessions (Claude, Codex, the IDE) often share one working tree, and anything uncommitted is
exposed to the next git operation any of them runs. On 2026-08-07 a session ran `git pull --rebase`,
which auto-stashed the whole tree including another session's in-progress files, committed over the
top, and left the stash unpopped. The owning session next found its own source reverted to the
pre-fix version and nearly committed the original bug over its fix, with a message describing a fix
that was no longer there.

| Rule | Why | Enforced by |
|---|---|---|
| **Commit, or stash deliberately, before any `git pull --rebase`** | Anything uncommitted is fair game for the auto-stash, including files you don't own. | nothing |
| **Run `git stash list` after any rebase and restore what it took** | An unpopped auto-stash is invisible data loss: no error, no conflict, just reverted files. The 2026-08-07 stash sat for two days before anyone looked. | `session-start.sh` prints the stash count |
| **Recover with `git stash apply`, never `pop`** | `apply` keeps the stash as a safety net until the recovery is verified green. | nothing (`block-dangerous-git.sh` asks only on `stash drop`/`clear`) |
| **Re-read your own changes from disk before any completion claim** | Another session restoring a path reverts your uncommitted edits with no error and a clean `git status`. The tree compiling is not proof your change is still in it. | nothing |
| **Stage explicit paths; never `git add -A` or `git commit -a`** | A shared file, CHANGELOG.md above all, routinely holds two sessions' edits. Commit only your own hunks. | `block-broad-git-add.sh` asks and lists what the sweep would take |
| **Run `git status --porcelain` before editing a shared file** | An unpopped auto-stash can leave a path `UU` with everything already staged. On 2026-08-08 a session appended a CHANGELOG entry into a file carrying live `<<<<<<<` markers and never saw them. | nothing |
| **Prove a stash redundant before dropping it, with line endings normalised** | `git show stash@{0}:<f>` emits LF against a CRLF tree, so a raw `diff` marks every line changed. `diff --strip-trailing-cr` turned "35 files diverged" into "the stash is a strict subset". | `session-start.sh` prints the reminder when a stash exists |
| **Re-read `HEAD` immediately before `--amend`** | If another session commits in between, your amend rewrites their commit (2026-08-08). Recovery: `git reset --mixed <their-sha>`, confirm `git diff <their-sha> HEAD` is empty, then commit yours separately. | nothing |
| **Revert per file, never per directory** | `git checkout -- <dir>` reverts every uncommitted file under it. On 2026-08-28 repairing eight files that way also wiped another session's new troops in `troops_erebor.xml`. | `block-dangerous-git.sh` asks on any `checkout --` or `restore` |
| **Never write a reconstructed shared file to disk** | Rebuilding CHANGELOG.md from `HEAD` plus your own hunk and writing it to the worktree silently deletes other sessions' uncommitted entries and still looks well-formed (2026-08-28: two entries lost). Reconstruct into the index instead: `git hash-object -w` then `git update-index --cacheinfo`, which never touches the worktree. The tell: an append should be inserts only. | nothing |
| **Stage an edit in the same command that makes it** | An unstaged edit in a shared tree is fair game for the next git operation. On 2026-08-08 a nine-line CHANGELOG paragraph survived a reset, then vanished to a concurrent operation before it could be staged. | nothing |

If the tree holds another session's edits, leave them unstaged and say so: don't commit, revert or
tidy them. If Mike asks for both sessions' work to land, commit theirs as its own commit, attributed
in the message and verified green first, never folded into yours.

Watch for the inverse tell: a feature doc or commit body in `HEAD` describing code the commit lacks means the documentation
half of a change was committed and the code half was not.

More incidents and the reasoning behind these rules:
[lessons/build-tooling-workflow.md](../reviews/lessons/build-tooling-workflow.md).
