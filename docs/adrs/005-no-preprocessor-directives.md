# ADR-005: No Preprocessor Directives in Application Code

**Status**: Accepted
**Date**: 2025-11-02
**Context**: Build configuration management and testability

## Decision

Preprocessor directives (`#if`, `#ifdef`, `#ifndef`, `#else`, `#endif`) are prohibited in all application code. Environment-specific behavior MUST be handled through dependency injection with different service implementations. Preprocessor directives are ONLY allowed in `IoC.cs` for build-specific service registration.

## Rationale

### Problems with Preprocessor Directives

1. **Untestable Code Paths**: Code within `#if DEBUG` blocks cannot be unit tested in release builds
2. **Hidden Complexity**: Conditional compilation creates invisible code paths that vary by build configuration
3. **Maintenance Burden**: Developers must mentally track multiple code paths and combinations
4. **Poor Discoverability**: IDE features (Go To Definition, Find References) don't work across conditional blocks
5. **Debugging Difficulty**: Release bugs in debug-only code paths go undetected until production
6. **Violates DRY**: Often leads to duplicated logic with minor variations
7. **Breaks SRP**: Single method contains multiple implementations based on build flags

### Better Approach: Strategy Pattern + Dependency Injection

- **Single Implementation**: Each service has one implementation that is always compiled and testable
- **DI-Based Selection**: IoC container selects appropriate implementation at runtime based on build
- **Testable**: All code paths can be tested regardless of build configuration
- **Clean Separation**: Debug vs release behavior separated into distinct classes
- **Type Safety**: Compiler enforces interface contracts across all implementations

## Examples

### BAD: Preprocessor Directives in Application Code

```csharp
// Service with embedded preprocessor directives - WRONG!
public class MyService : IMyService
{
    public void ProcessData(string data)
    {
#if DEBUG
        Console.WriteLine($"[DEBUG] Processing: {data}");
        ValidateDataIntegrity(data);
        LogDetailedMetrics(data);
#endif

        // Common logic
        var result = Transform(data);

#if DEBUG
        Console.WriteLine($"[DEBUG] Result: {result}");
#else
        // Different behavior in release - WRONG!
        if (result.Length > 1000)
            result = result.Substring(0, 1000);
#endif

        Save(result);
    }
}
```

**Problems**:
- Debug validation logic untestable in release builds
- Release truncation logic untestable in debug builds
- Hidden behavioral differences between builds
- Impossible to unit test both paths in same test run

### GOOD: Strategy Pattern with DI

```csharp
// Clean interface
public interface IDataProcessor
{
    void ProcessData(string data);
}

// Debug implementation with all debug behavior
public class DebugDataProcessor : IDataProcessor
{
    private readonly ILogger _logger;

    public DebugDataProcessor(ILogger logger)
    {
        _logger = logger;
    }

    public void ProcessData(string data)
    {
        _logger.LogDebug($"Processing: {data}");
        ValidateDataIntegrity(data);
        LogDetailedMetrics(data);

        var result = Transform(data);
        _logger.LogDebug($"Result: {result}");

        Save(result);
    }

    private void ValidateDataIntegrity(string data) { /* ... */ }
    private void LogDetailedMetrics(string data) { /* ... */ }
}

// Release implementation optimized for production
public class ReleaseDataProcessor : IDataProcessor
{
    public void ProcessData(string data)
    {
        var result = Transform(data);

        // Production optimization
        if (result.Length > 1000)
            result = result.Substring(0, 1000);

        Save(result);
    }
}

// IoC.cs - ONLY place with preprocessor directives
public static class IoC
{
    public static void RegisterServices(IContainer container)
    {
#if DEBUG
        container.Register<IDataProcessor, DebugDataProcessor>(Reuse.Singleton);
#else
        container.Register<IDataProcessor, ReleaseDataProcessor>(Reuse.Singleton);
#endif
    }
}
```

**Benefits**:
- Both implementations fully testable in any build configuration
- Clear separation of debug vs release behavior
- No hidden code paths
- IDE tooling works across all implementations
- Easy to add new environment-specific implementations (e.g., Staging)

## Common Use Cases

### 1. Debug Logging

```csharp
// BAD
public void Calculate()
{
#if DEBUG
    Console.WriteLine("Calculating...");
#endif
    // logic
}

// GOOD
public interface IDebugService
{
    void LogOperation(string operation);
}

public class DebugService : IDebugService
{
    public void LogOperation(string operation) => Console.WriteLine(operation);
}

public class NoOpDebugService : IDebugService
{
    public void LogOperation(string operation) { /* no-op */ }
}

// In IoC.cs:
#if DEBUG
container.Register<IDebugService, DebugService>(Reuse.Singleton);
#else
container.Register<IDebugService, NoOpDebugService>(Reuse.Singleton);
#endif
```

### 2. Debug UI/Features

```csharp
// BAD
public class MyView
{
#if DEBUG
    private void ShowDebugPanel() { /* ... */ }
#endif
}

// GOOD
public interface IDebugUIService
{
    bool IsDebugUIEnabled { get; }
    void ShowDebugPanel();
}

public class DebugUIService : IDebugUIService
{
    public bool IsDebugUIEnabled => true;
    public void ShowDebugPanel() { /* show UI */ }
}

public class NoOpDebugUIService : IDebugUIService
{
    public bool IsDebugUIEnabled => false;
    public void ShowDebugPanel() { /* no-op */ }
}
```

## Exceptions

**ONLY Acceptable Location**: `IoC.cs` for build-specific service registration

```csharp
// ONLY acceptable use of #if DEBUG
public static class IoC
{
    public static void RegisterServices(IContainer container)
    {
#if DEBUG
        container.Register<IDebugService, DebugService>(Reuse.Singleton);
        container.Register<IValidationService, StrictValidationService>(Reuse.Singleton);
#else
        container.Register<IDebugService, NoOpDebugService>(Reuse.Singleton);
        container.Register<IValidationService, RelaxedValidationService>(Reuse.Singleton);
#endif
    }
}
```

**Rationale for Exception**:
- IoC.cs is configuration, not business logic
- Centralized in one location
- No business logic affected
- All implementations still compiled and testable

## Migration Strategy

When encountering `#if DEBUG` in application code:

1. **Identify Conditional Behavior**: Determine what differs between debug/release
2. **Extract Interface**: Create interface representing the behavior
3. **Create Implementations**: Separate debug and release implementations
4. **Update IoC**: Add conditional registration in `IoC.cs`
5. **Inject Service**: Replace preprocessor directive with service call
6. **Write Tests**: Test both implementations independently
7. **Remove Directives**: Delete all `#if DEBUG` blocks from application code

## Consequences

### Positive
- **Fully Testable**: All code paths testable regardless of build configuration
- **Better Design**: Forces proper separation of concerns via interfaces
- **Maintainable**: Clear, discoverable implementations
- **IDE Support**: Full IntelliSense and navigation across all code
- **Flexible**: Easy to add new environments (Staging, QA, etc.)
- **Type Safe**: Compiler enforces contracts

### Negative
- **More Classes**: Requires separate implementation classes per environment
- **Slightly More Verbose**: Interface + implementations vs inline directives
- **IoC Complexity**: Must manage registrations in `IoC.cs`

## Enforcement

1. **Code Review**: Reviewers REJECT any PR with `#if DEBUG` outside `IoC.cs`
2. **Static Analysis**: Consider analyzer rule to flag preprocessor directives
3. **PR Guidelines**: New environment-specific behavior requires DI approach
4. **Documentation**: This ADR linked in CLAUDE.md
5. **Architecture Tests**: Automated tests to detect preprocessor directive usage

## Testing Strategy

With this pattern, you can test both debug and release behaviors:

```csharp
[TestClass]
public class DataProcessorTests
{
    [TestMethod]
    public void DebugProcessor_ValidatesData_ThrowsOnInvalid()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var processor = new DebugDataProcessor(logger);

        // Act & Assert
        Assert.ThrowsException<ValidationException>(() =>
            processor.ProcessData("invalid"));
    }

    [TestMethod]
    public void ReleaseProcessor_TruncatesLongData_To1000Chars()
    {
        // Arrange
        var processor = new ReleaseDataProcessor();
        var longData = new string('x', 2000);

        // Act
        processor.ProcessData(longData);

        // Assert - verify truncation occurred
        // (both implementations testable in same test run!)
    }
}
```
