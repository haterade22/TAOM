# Security Guardrails

Security is **non-negotiable**. These guardrails protect users, data, and systems from harm.

---

## 1. Secret Detection (CRITICAL)

### Patterns That Must NEVER Appear in Code

Claude must **NEVER** write these patterns to any file:

| Type | Pattern Examples |
|------|------------------|
| API Keys | `sk-...`, `pk_live_...`, `AKIA...`, `ghp_...`, `xox[baprs]-...` |
| Passwords | `password = "..."`, `pwd: "..."`, `secret: "..."` |
| Connection Strings | `Server=...;Password=...`, `mongodb://user:pass@...`, `postgres://...` |
| Private Keys | `-----BEGIN RSA PRIVATE KEY-----`, `-----BEGIN OPENSSH PRIVATE KEY-----` |
| Tokens | `Bearer eyJ...`, `token: "..."`, JWT strings |
| OAuth Secrets | `client_secret`, `ClientSecret`, `OAUTH_CLIENT_SECRET` hardcoded |
| Cloud Credentials | `aws_secret_access_key`, `AZURE_CLIENT_SECRET`, `GOOGLE_APPLICATION_CREDENTIALS` inline |

### When User Provides Secrets in Chat

If a user shares a secret in the conversation:

1. **Do NOT echo it back** in responses
2. **Do NOT include it** in any code output
3. **Acknowledge** that you've seen it but won't use it directly
4. **Suggest** the secure alternative immediately

**Example response:**
> "I notice you've shared an API key. I won't include that in the code. Instead, let me set this up using environment variables or user secrets so your secret stays secure."

### The Secure Pattern

```
# When Claude detects a hardcoded secret, respond:
"I notice this contains [type of secret]. Let me refactor to use configuration/environment variables instead."
```

**Always use configuration or secret managers:**

```csharp
// WRONG - Hardcoded secret
public class AuthService
{
    private const string ClientSecret = "actual-secret-here"; // NEVER DO THIS
}

// CORRECT - Use IConfiguration
public class AuthService
{
    private readonly string _clientSecret;

    public AuthService(IConfiguration configuration)
    {
        _clientSecret = configuration["OAuth:ClientSecret"]
            ?? throw new InvalidOperationException("OAuth:ClientSecret not configured");
    }
}

// CORRECT - Use Options pattern
public class OAuthSettings
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
}

// In DI setup
services.Configure<OAuthSettings>(configuration.GetSection("OAuth"));
```

### Configuration Sources (Precedence)

```csharp
// In Program.cs or host builder
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true); // Development only
```

### Required: appsettings.example.json Template

When creating projects with secrets, always include:

```json
{
  "OAuth": {
    "ClientId": "your-client-id-here",
    "ClientSecret": "your-client-secret-here",
    "Authority": "https://your-auth-server"
  },
  "Database": {
    "ConnectionString": "Server=localhost;Database=mydb;User Id=user;Password=your-password;"
  }
}
```

And ensure `.gitignore` includes:
```
appsettings.Development.json
appsettings.Local.json
*.pfx
*.pem
*.key
credentials.json
```

---

## 2. Input Validation Requirements

**ALL external input is untrusted.** Validate before use.

### SQL Injection Prevention (Entity Framework)

```csharp
// WRONG - SQL Injection vulnerable
var query = $"SELECT * FROM Users WHERE Name = '{userName}'";
await context.Database.ExecuteSqlRawAsync(query);

// WRONG - String concatenation
var users = await context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Name = '" + userName + "'")
    .ToListAsync();

// CORRECT - EF Core handles parameterization automatically
var user = await context.Users
    .FirstOrDefaultAsync(u => u.Name == userName);

// CORRECT - Parameterized raw SQL when needed
var users = await context.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Name = {userName}")
    .ToListAsync();

// CORRECT - Explicit parameters
var users = await context.Users
    .FromSqlRaw("SELECT * FROM Users WHERE Name = {0}", userName)
    .ToListAsync();
```

### Path Traversal Prevention

```csharp
// WRONG - Path traversal vulnerable
public async Task<byte[]> GetModFile(string fileName)
{
    var path = Path.Combine(_modDirectory, fileName);
    return await File.ReadAllBytesAsync(path); // User could pass "../../../Windows/System32/config/SAM"
}

// CORRECT - Validate resolved path
public async Task<byte[]> GetModFile(string fileName)
{
    var baseDir = Path.GetFullPath(_modDirectory);
    var requestedPath = Path.GetFullPath(Path.Combine(baseDir, fileName));

    if (!requestedPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Invalid file path", nameof(fileName));
    }

    return await File.ReadAllBytesAsync(requestedPath);
}
```

### Command Injection Prevention (Harmony/Process)

```csharp
// WRONG - Command injection vulnerable
public void RunModTool(string modName)
{
    Process.Start("cmd.exe", $"/c modtool.exe {modName}"); // User could inject: "; del /f /q *"
}

// CORRECT - Use ProcessStartInfo with arguments array
public void RunModTool(string modName)
{
    // Validate input first
    if (!IsValidModName(modName))
    {
        throw new ArgumentException("Invalid mod name", nameof(modName));
    }

    var psi = new ProcessStartInfo
    {
        FileName = "modtool.exe",
        Arguments = modName, // Single validated argument
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi);
    process?.WaitForExit();
}

private bool IsValidModName(string name)
{
    // Allowlist: alphanumeric, underscores, hyphens only
    return Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$");
}
```

### Input Validation Checklist

Before using any external input:

- [ ] Type validation (use strongly-typed models)
- [ ] Length/size limits enforced
- [ ] Format validation (DataAnnotations, FluentValidation)
- [ ] Range validation for numbers
- [ ] Allowlist validation for enumerated values
- [ ] Encoding handled correctly (UTF-8)
- [ ] Null/empty handled

### Validation Example with DataAnnotations

```csharp
public class UpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ModVersion { get; set; } = null!;

    [Required]
    [RegularExpression(@"^[a-zA-Z0-9.-]+$")]
    public string FileName { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int FileSize { get; set; }
}

// In controller
[HttpPost]
public async Task<IActionResult> RequestUpdate([FromBody] UpdateRequest request)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }

    // Proceed with validated input
}
```

---

## 3. Dependency Security

### Before Adding Any Package

1. **Check for known vulnerabilities**
   ```powershell
   # .NET vulnerability check
   dotnet list package --vulnerable

   # Or use audit command
   dotnet restore
   dotnet list package --vulnerable --include-transitive
   ```

2. **Evaluate the package**
   - When was it last updated?
   - How many maintainers?
   - Is it from a trusted source (Microsoft, established OSS)?
   - Are issues being addressed?

3. **Check the license** - is it compatible with mod distribution?

### Package Selection Criteria

| Prefer | Avoid |
|--------|-------|
| Active maintenance (updated within 6 months) | Abandoned projects (no updates in 2+ years) |
| Multiple maintainers or corporate backing | Single maintainer with no activity |
| NuGet.org verified/prefixed packages | Unknown sources |
| Good security track record | History of vulnerabilities |
| Clear documentation | Unclear or missing docs |

### Version Pinning

```xml
<!-- .csproj - Pin exact versions for stability -->
<PackageReference Include="DryIoc.dll" Version="5.4.3" />
<PackageReference Include="Lib.Harmony" Version="2.3.3" />

<!-- Avoid floating versions in production -->
<!-- <PackageReference Include="SomePackage" Version="*" /> AVOID -->
```

### Mod-Specific Concerns

**Harmony Patches**: Be cautious when patches interact with security-sensitive code:

```csharp
// CAREFUL - Don't expose internal game data through patches
[HarmonyPatch(typeof(SomeGameClass), "SomeMethod")]
public static class SomeGameClassPatch
{
    // WRONG - Logging sensitive data
    public static void Postfix(string playerPassword)
    {
        Log.Info($"Player password: {playerPassword}"); // NEVER LOG SECRETS
    }

    // CORRECT - Log only non-sensitive data
    public static void Postfix(string playerId)
    {
        Log.Info($"Processing player: {playerId}");
    }
}
```

---

## 4. Enforcement Protocol

### Proactive Security Flagging

Claude must **proactively identify and flag** security issues:

```
When security concern detected, use this format:

"**Security Concern**: I've identified [issue type] in [location].

**Risk**: [Explain the potential impact]

**Recommendation**: [Specific fix]

I'll proceed with the secure implementation unless you have specific requirements that prevent this."
```

### Severity Levels and Responses

| Severity | Examples | Claude Response |
|----------|----------|-----------------|
| CRITICAL | Hardcoded secrets, SQL injection | Stop and fix before proceeding |
| HIGH | Missing auth checks, XSS in API | Flag and fix immediately |
| MEDIUM | Weak token storage, missing rate limiting | Flag and recommend fix |
| LOW | Missing security headers, verbose errors | Note for future improvement |

### When to Refuse

Claude must **refuse to proceed** without a security fix for:

1. **Hardcoded secrets** in any form
2. **Direct SQL string concatenation** with user input
3. **Unvalidated file paths** with user input (critical for mod file handling)
4. **Shell commands** with unvalidated user input
5. **Custom cryptography** implementation
6. **Disabled security features** without justification
7. **Logging sensitive data** (tokens, passwords, personal info)

**Refusal language:**
> "I can't implement this as written because it contains [security issue]. This could lead to [consequence]. Let me show you the secure way to do this instead."

### Security Review Checklist

Before considering any code complete:

- [ ] No hardcoded secrets
- [ ] All user input validated
- [ ] Database queries parameterized (EF Core or explicit params)
- [ ] Output properly encoded/escaped
- [ ] Dependencies scanned for vulnerabilities
- [ ] Error messages don't leak sensitive info
- [ ] Logging doesn't include secrets or tokens
- [ ] File paths validated (no traversal)

---

## Quick Reference Card

### The Golden Rules

1. **Never trust user input** - validate everything
2. **Never hardcode secrets** - use IConfiguration, environment variables, user secrets
3. **Never roll custom crypto** - use Data Protection API, established libraries
4. **Always use parameterized queries** - let EF Core handle it, or use FromSqlInterpolated
5. **Always escape output** - especially in API responses
6. **Always validate paths** - prevent traversal attacks in mod file handling
7. **Always check dependencies** - `dotnet list package --vulnerable`

### Response Templates

**Secret detected:**
> "I notice this contains [type]. Let me refactor to use IConfiguration/user secrets instead."

**Injection vulnerability:**
> "This code is vulnerable to [type] injection. I'll use parameterized queries/proper validation instead."

**Risky dependency:**
> "Before adding [package], I should note it has [concern]. Consider [alternative] instead, or we can proceed with awareness of the risk."

---

## Related Guides

- [code-quality.md](./code-quality.md) - Clean code principles (complements security)
- [testing-guide.md](./testing-guide.md) - Security testing patterns
- [architecture.md](./architecture.md) - Secure architecture principles
- [patterns.md](./patterns.md) - Adapter pattern for safe abstractions
