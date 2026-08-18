# Code Style & Conventions

## Formatting
- C#: 4-space indentation
- XML: 2-space indentation
- Line endings: CRLF
- Configured in `.editorconfig`

## Naming
- Standard C# conventions (PascalCase for public, camelCase for private)
- Interfaces prefixed with `I` (e.g., `ICareerHeroAdapter`)
- Adapters: `{TypeName}Adapter` implementing `I{TypeName}Adapter`

## Design Patterns
- **Adapter Pattern**: All TaleWorlds sealed types accessed via adapter interfaces
- **IoC**: Services registered in `Main/IoC.cs`
- **Feature Modules**: Self-contained features in `Main/Features/{FeatureName}/`
- **Thin Entry Points**: < 150 lines, delegate to services

## Prohibited
- No `#region` blocks — use class decomposition
- No `[Obsolete]` attributes — migrate all usage in same PR
- No `#if DEBUG` — except in IoC.cs registration

## Commit Messages
- 50/72 rule (50-char subject, 72-char body wrap)
- No AI attribution
- Example: `feat: add garrison patrol calculation`

## XSLT Rules
- Always pass through ALL vanilla attributes with `<xsl:apply-templates select="@*"/>`
- Use SandBoxCore (not SandBox) as vanilla XML reference