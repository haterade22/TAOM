# Input pack: WP 101 baseline-sync

Title (draft): Sync bannerlord-1.4.5 to origin, fold in the public #697 troop tree, record the baseline and decisions
Wave: 0   Lane: gate   Draft priority: P-   Groups: 0   Port commits: 0

## Orchestrator notes for this WP (binding)
- Fast-forward the MAIN checkout's bannerlord-1.4.5 to origin/bannerlord-1.4.5 (1b00d1acd), then merge origin/fix/public-armory-ids-145 (92a30ba42, 10a52fb3d, 8df90444c); the only expected conflict is docs/reviews/lessons/data-content-cultures.md (keep both sides).
- Expected after the merge: build OK, 9154 tests, 9151 passed, 3 skipped, 0 failed (measured by the orchestrator in a scratch export, 2026-11-04).
- Leave the uncommitted .serena/project.yml and any stash alone; never pull --rebase with an autostash.
- Maintainer step (environment): update the laptop's LOTRLOME_Armory install to the public 2026-09-29 Armory, then run python tools/validate_moduledata.py and record the BROKEN_ITEM_REF count as the laptop baseline (decision D20).
- This WP precedes plan 020, so it follows the 1.4.x hand-edited CHANGELOG rule for its own commit.
- Record in plans/README.md (the integrator's section) the measured baseline numbers that every later plan quotes.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/D4-patch88-and-binding-health.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- XML-DATA-02 [P2/high] Laptop data is half trustworthy: TAOM_Map complete (09-07 copy), Armory an old v2.0.26 copy, so item, mesh and body verdicts here do not speak for the public build | port:   (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- XML-DATA-03 [P1/high] origin/bannerlord-1.4.5 does not contain the public troop tree (#697); a release or port based on it regresses against the public Armory | port: 92a30ba42,10a52fb3d,8df90444c  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)
- RELEASE-INTEGRITY-04 [P1/high] Public build and 1.4.x trunk diverged both ways; a release cut from origin/bannerlord-1.4.5 alone regresses the troop tree against the channel's Armory | port:   (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- BHA0001 Patch88 analyzer warning (false positive per D4)

## Port groups owned by this WP (from the commit classification)
