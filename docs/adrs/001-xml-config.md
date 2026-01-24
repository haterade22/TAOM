# ADR-001: Configuration File Format -> XML

**Status**: Accepted
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
