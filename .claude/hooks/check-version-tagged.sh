#!/bin/bash
# Stop hook: remind Claude when the module version in Main/_Module/SubModule.xml has no git tag.
# A reminder, not a gate: it cannot stop a commit.
#
# Mirrors check-verification-evidence.sh: one JSON {"decision":"block","reason":...} on
# stdout through _stop_reminder.sh (the Stop channel TAOM uses; until plan 011 this
# hook wrote to stderr and nothing arrived), silent when stop_hook_active is true (a tag made
# in that continuation still clears the marker), always
# exits 0, and mutes itself after one reminder per version.
#
# Why: the version in that file is what IdentityCollector stamps into every crash bundle as
# TaomVersion, and it is the only link between a player's report and our source. Five versions
# players actually ran — v2.0.11, v2.0.12, v2.0.14, v2.0.16, v2.0.17 — appear in no commit on
# any branch, so the two crash reports citing v2.0.12 can never be pinned to a commit. One
# condition catches both failure modes: a bump committed without a tag, and a version that
# never entered git at all. See docs/reference/release-process.md.
#
# Stop, not PreToolUse: the tag can only be created AFTER the release commit exists, so a
# commit-time gate would be structurally wrong.
#
# Muting: the marker records WHICH version was reminded about, so bumping to a new untagged
# version re-arms the reminder instead of staying silent behind a stale marker.

REMINDED=".claude/logs/.version-tag-reminded"
SUBMODULE="Main/_Module/SubModule.xml"

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

# Not a TAOM checkout (or the file moved): nothing to assert.
[[ -f "$SUBMODULE" ]] || exit 0

# The module version. Anchored on `<Version value=` so the DependedModuleMetadata
# `version="…"` attributes elsewhere in the file cannot match.
VERSION=$(grep -o '<Version value="[^"]*"' "$SUBMODULE" 2>/dev/null \
          | head -1 | sed 's/.*"\(.*\)"/\1/')

# Unreadable or unexpected shape: fail open.
[[ -n "$VERSION" ]] || exit 0

# Tag present: the release is anchored. Clear the marker so the next bump re-arms.
if git rev-parse -q --verify "refs/tags/$VERSION" >/dev/null 2>&1; then
  rm -f "$REMINDED" 2>/dev/null || true
  exit 0
fi

# Already reminded about THIS version: stay quiet.
if [[ -f "$REMINDED" ]] && [[ "$(cat "$REMINDED" 2>/dev/null)" == "$VERSION" ]]; then
  exit 0
fi

taom_stop_hook_active "$INPUT" && exit 0
taom_stop_block "check-version-tagged: module version $VERSION in $SUBMODULE has no git tag, so a player's crash report naming $VERSION cannot be traced to a commit (that is how v2.0.12 became unresolvable). Tagging and pushing need the user's go-ahead: ask whether to tag the release commit (git tag -a $VERSION -m 'Release $VERSION') and push the tag (git push origin $VERSION); /release runs the full sequence (docs/reference/release-process.md). If the bump is not committed yet, say so in one line. This fires once per version."

mkdir -p .claude/logs 2>/dev/null
echo "$VERSION" > "$REMINDED" 2>/dev/null || true

exit 0
