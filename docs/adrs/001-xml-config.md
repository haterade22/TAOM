# ADR-001: Configuration File Format -> XML

**Status**: Accepted, amended 2026-11 (JSON feature configs, below)
**Date**: Initial project decision
**Context**: Mount & Blade II modding configuration

## Decision

All configuration files for the mod must use the XML format.

## Context

Configuration files are used to control various aspects of the mod, such as gameplay settings, race bonuses, and other customizable features. These files are often edited by scripters and modders with varying levels of technical expertise.

## Rationale

1. **Community Familiarity**: Mount & Blade modding community uses XML extensively
2. **Tool Support**: Wide availability of XML editors and validators
3. **Game Integration**: Native game engine XML parsing support
4. **Consistency**: Matches base game configuration format

## Consequences

### Positive
- Scripters and modders with experience in XML can easily edit and maintain configuration files
- Tooling and editors for XML are widely available and familiar to the modding community
- Future configuration changes will follow a consistent and well-understood structure
- Native game engine parsing support (no custom deserializers needed)

### Negative
- JSON would be more concise for complex nested structures
- XML more verbose than JSON or YAML
- No type safety in configuration files (handled at code level)

## Rules

### Required
- All game configuration in `/ModuleData/` uses XML
- XML files must be well-formed (validated against schema if available)
- MSBuild copies XML files to `_Module/ModuleData/` during build

### Prohibited
- No JSON files for game configuration
- No YAML files for game configuration
- No custom binary configuration formats

## Exceptions

**Non-Game Configuration** (NOT covered by this ADR):
- Build scripts and tooling configuration (any format)

## Examples

Historical examples from the initial decision; the files and paths below may no longer exist.

### XML Configuration Files

**Location**: `/ModuleData/`

**Examples**:
- `casualty_config.xml` - Post-battle death rates
- `autoresolve_cultures.xml` - Auto-resolve combat bonuses
- `race_bonuses.xml` - Culture-specific stat modifiers
- `starting_influence.xml` - Clan starting influence values

**MSBuild Integration** (`Main/TAOM.csproj`):
```xml
<ItemGroup>
  <None Include="..\ModuleData\**\*.xml">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>_Module\ModuleData\%(RecursiveDir)%(Filename)%(Extension)</Link>
  </None>
</ItemGroup>
```

### Sample XML Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<CultureModifiers>
    <Culture name="empire" multiplier="0.7" description="Organized military with medical corps" />
    <Culture name="vlandia" multiplier="0.65" description="Standard warfare practices" />
    <!-- ... more cultures -->
</CultureModifiers>
```

## Enforcement

1. **Code Review**: Reviewers verify no JSON/YAML in `/ModuleData/`
2. **Build Process**: MSBuild only copies `.xml` files to output
3. **Documentation**: Config format documented in development-guide.md

## Amendment: JSON feature configs (2026-11-06)

The "No JSON" rule above no longer matches the repo. Feature tuning files under
`Main/_Module/ModuleData/` are JSON by established practice (66 tracked `.json` files beside 338 `.xml`
files on 2026-11-06), loaded by a feature's config provider and validated per the "Config Providers MUST
Validate" rule in `.claude/rules/csharp-architecture.md`. They deploy because `CopyModule` copies `_Module`
recursively, not through the `*.xml` item group shown above.

**The rule now:**
- Data the engine reads (items, troops, cultures, settlements, XSLT patches, GUI prefabs, language files)
  stays XML, in the engine's own schema.
- A TAOM feature's own tuning numbers may be a small JSON file in its own `ModuleData/<feature>/` folder,
  read only by that feature's config provider, every value validated on load with a revert to the
  compiled default.
- A JSON list or dictionary with a non-empty compiled default is deserialized with
  `ObjectCreationHandling.Replace`, or the file's entries are added to the default instead of replacing it
  (`docs/reviews/lessons/state-lifecycle-save.md`).

The Decision's XML-only line, the No JSON prohibition and the Enforcement items above that ban JSON and copy only `.xml` are superseded.

### Amendment (2026-11-03): historical examples

The Examples section is marked historical: its config files are not at the `ModuleData` top level today, and `development-guide.md` is not at the repo root or under `docs/`. The amendment above now names every superseded line, including the Decision's XML-only sentence. The reference to "Config Providers MUST Validate" in `.claude/rules/csharp-architecture.md` still resolves.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/README.md](./README.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
