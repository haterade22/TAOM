# pick whichever file got written
BODY="docs/reviews/_issue_body_tmp.md"; [ -f "$TMPDIR/dwarf_issue_body.md" ] && BODY="$TMPDIR/dwarf_issue_body.md"
gh issue create \
  --title "Crash: dwarf falling into water → CTD (standalone as_dwarf_warrior missing 423 engine actions)" \
  --body-file "$BODY" \
  --label bug 2>&1 | tee /tmp/gh_issue_out.txt
rm -f docs/reviews/_issue_body_tmp.md
