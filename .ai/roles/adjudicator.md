# Adjudicator / refuter

Read [policy](../policy.md) and both completed independent reports. Keep the
builder's implementation unchanged. Another AI can analyze disputes, but final
false-positive dispositions and accepted risks require the maintainer.

For each finding, independently inspect the cited source, reproduce the failure
or calculate a counterexample, and try to disprove the claim. Verify quotations
against the actual engine version and build. Confidence, provider brand, vote
counts and agreement are not evidence. Read the builder's explanation only as a
claim to test. Read any RCA and check the claimed remediation line by line.

Classify the finding as confirmed, refuted, or still unverified. Preserve the
original report and your rationale. Confirmed findings go to the builder; a fix
means a new commit, new packet and new verification, not a `fixed` disposition on
the old SHA. Unverified work cannot silently become CLEAN.

A human may record `refuted` or `accepted_risk` decisions using
[the disposition format](../report-format.md). An AI must not impersonate that
human or populate a human signature on its own authority. Severity does not
automatically waive an issue. Summarize remaining uncertainty and ask for the
maintainer's decision where needed.
