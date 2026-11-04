# Testing Guide: shipped-data tests

The rest of the test rules (TDD, MSTest and NSubstitute, no-deploy commands) are in `.claude/rules/tests.md`, which loads when you open a test. Run the suite with `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.

## Shipped-Data Tests (required for content that can fail silently)

Neither of the two above covers TAOM's largest failure surface: **XML that ships to players and is
wrong without any code being wrong.** A missing culture attribute, a party template nothing binds, a
troop id pointing at a Calradian, an equipment roster with no civilian tag. No unit test touches it
(there is no unit), and no integration test either (the component graph is fine, the data is not).

These tests read the shipped `Main/_Module/ModuleData/**` files off disk and assert invariants over
them. They are fast, deterministic and need no game install, so they belong in the normal suite.
Existing examples: `TAOM.Tests/Core/CultureLordTemplateTests.cs` (every hero template belongs to the
culture that lists it), `TAOM.Tests/Core/CulturePartyTemplateTests.cs` (every party-template binding
resolves to a TAOM-authored id), `ShippedBannerBearerConfigTests`, `TavernMercenaryDataTests`,
`ShippedAlignmentConfigTests`.

Two conventions make them durable:

- **Derive the subject list from the data, never hand-write it.** A test that enumerates cultures in
  a C# array silently stops covering culture 25. `CultureDataFixture.ModuleDataPath()` walks up from
  the test working directory so the path survives a repo move.
- **When the data reaches the engine through a transform, assert on the transform's OUTPUT.** Reading
  the source markup cannot see an exclusion filter and cannot see an attribute that is not there. The
  sentinel-stub technique for this is written up in `.claude/rules/tests.md`.

Each of these tests exists because the defect it pins shipped to players first. That is the bar for
adding one: not "this data could be wrong" but "this class of wrongness has no other detector."

---

## Related Guides

- [architecture.md](./architecture.md) - Overview, client seam and folders
- [code-quality.md](./code-quality.md) - The nullable ratchet

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md)
- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/ai-includes/architecture.md](./architecture.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/recipe-add-a-culture.md](../modding/recipe-add-a-culture.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->