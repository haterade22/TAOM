# Research Workflow

How to investigate and learn when facing uncertainty.

> **Complement to**: [problem-solving-guide.md](../problem-solving-guide.md) which covers TAOM-specific research (TaleWorlds types, decompiled sources, game engine behavior). This guide covers **general research practices**.

---

## The Core Principle

> **Never assume. Always verify.**

When you're not 100% certain about how something works, research it before writing code.

---

## When To Research

Research is needed when:

- You're unsure how a library/framework feature works
- You're modifying code you haven't fully read
- You're integrating with external systems or APIs
- You're encountering unfamiliar patterns in the codebase
- Something "should work" but doesn't
- You're about to add a new NuGet package

---

## Research Escalation Ladder

Start at step 1 and escalate as needed:

### 1. Check Existing Documentation

**Location**: `docs/`, `CLAUDE.md`, `docs/adrs/`, code comments

Questions to answer:
- Has this been documented already?
- Is there an ADR explaining why things are this way?
- Are there examples in the codebase?

```
# Check ADRs
docs/adrs/
```

Time budget: 5 minutes

### 2. Search the Codebase

**Tools**: Grep, Glob, IDE search

Look for:
- Similar implementations elsewhere
- Usage patterns of the library/function
- Tests that demonstrate expected behavior
- Configuration files that define behavior

Time budget: 10 minutes

### 3. Read the Source

**When**: Internal code that's unclear

Steps:
1. Find the entry point
2. Trace the execution path
3. Note any side effects or dependencies
4. Look at related tests for expected behavior

Time budget: 15-30 minutes

### 4. Check External Documentation

**Sources**: Official docs, GitHub repos, API references, NuGet pages

For .NET libraries:
- NuGet package page (readme, dependencies)
- GitHub repository (README, examples)
- Official documentation site
- GitHub issues (for known problems)

For game modding:
- See [taleworlds-research-guide.md](./taleworlds-research-guide.md)
- Community wikis and forums
- Other mod source code (Nexus, GitHub)

Time budget: 15 minutes

### 5. Experiment and Verify

**When**: Documentation is unclear or missing

Create a test to verify behavior:
```csharp
[TestMethod]
public void UnderstandLibraryBehavior()
{
    // Arrange
    var input = new SomeInput { Value = "test" };

    // Act
    var result = LibraryMethod(input);

    // Assert - See what actually happens
    Console.WriteLine($"Result type: {result.GetType()}");
    Console.WriteLine($"Result value: {result}");

    // Then add proper assertions
    Assert.IsNotNull(result);
}
```

Or use the REPL/scratch approach:
```csharp
// Quick experiment in a test or scratch file
var client = new SomeClient();
var response = await client.TestEndpoint();
Debug.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
```

Time budget: 15 minutes

### 6. Ask for Help

**When**: Research hasn't yielded answers

Before asking:
- Document what you've tried
- State what you expected vs. what happened
- Include relevant error messages/output

Format:
```
QUESTION: [Specific question]
CONTEXT: [What I'm trying to do]
RESEARCHED:
- Checked ADRs: [which ones]
- Searched codebase: [what patterns]
- Found: [relevant findings]
STILL UNCLEAR: [Specific gap]
```

---

## Document Your Findings

After researching, capture what you learned:

### For Significant Discoveries

Document in the appropriate location:

Format:
```markdown
# Discovery: How X Actually Works

## Context
What I was trying to do when I discovered this.

## Finding
What I learned about how X works.

## Evidence
- Code references (file:line)
- Documentation links
- Test results

## Implications
How this affects our codebase.
```

### For Architectural Decisions

Create an ADR using the template at `docs/adrs/000-template.md`.

### For Code Understanding

Add comments explaining "why" (not "what"):
```csharp
// PartySpeed uses a strategy pattern because modifiers are added dynamically
// at runtime based on character skills and equipment. See ADR-010.
public class PartySpeedService : IPartySpeedService
```

---

## Research Checklist

Before writing code that you researched:

- [ ] I understand what the code does
- [ ] I understand why it does it that way
- [ ] I've verified my understanding (test/experiment)
- [ ] I've documented significant findings
- [ ] I know the edge cases and failure modes

---

## Anti-Patterns

### Copy-Paste Without Understanding
- Don't copy code without understanding it
- Understand it first, then adapt to your context

### Assuming Documentation is Current
- Don't trust documentation blindly
- Verify behavior, especially for older docs or game engine behavior

### Skipping Research "To Save Time"
- Don't say "I'll figure it out as I go"
- Research upfront saves debugging time later

### Not Documenting Findings
- Don't research, implement, move on
- Research, document, implement

### Researching Too Long
- Don't go down endless rabbit holes
- Time-box, then escalate

---

## Time Boxes

Research should be time-boxed to avoid rabbit holes:

| Research Type | Max Time |
|--------------|----------|
| Quick lookup (docs, ADRs) | 10 min |
| Feature understanding | 30 min |
| Deep investigation | 1 hour |
| Unknown territory | 2 hours, then ask for help |

If you exceed the time box:
1. Document what you've learned so far
2. Document what's still unclear
3. Escalate (ask user, create GitHub issue, etc.)

---

## TAOM-Specific Research

For TaleWorlds-specific research (sealed types, game engine behavior, decompiled sources), see:

- [taleworlds-research-guide.md](./taleworlds-research-guide.md) - In-depth TaleWorlds investigation
- [../problem-solving-guide.md](../problem-solving-guide.md) - Comprehensive problem-solving with TaleWorlds focus

---

## Related Guides

- [iterative-problem-solving.md](./iterative-problem-solving.md) - Debugging through hypothesis testing
- [multi-approach-validation.md](./multi-approach-validation.md) - When multiple solutions are viable
- [tdd-enforcement.md](./tdd-enforcement.md) - Using tests to verify understanding
