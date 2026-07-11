# MCL (Mission Control Language) — Claude Code Rules

## Project overview

MCL is a declarative pipeline language where `.mcl` files compose AI experts into
structured workflows. The CLI binary is `forge`. Runtime is .NET 10 Native AOT.

## Planning docs (hub/spoke)

`docs/plan.md` is the authoritative index. Every feature is a **phase**: a hub
`docs/phases/phase-N-<slug>.md` (vision, locked decisions, dependency-ordered spoke
list) plus spokes `docs/phases/phase-N.M-<slug>.md` (design → chronological tasks with
file paths, real APIs, and a "Done when", written so an agent can execute from the doc
alone). When we design a new feature: create the hub + spokes, then update `plan.md`'s
top pointer + phases index. Cross-cutting design lives in `docs/design/`. Match the
depth and format of the latest existing phase docs.

## AOT-first: standing rules for all new code

**Every change must remain Native AOT-safe.** The binary is published with
`<PublishAot>true</PublishAot>`. Violations will cause ILC (IL Compiler) warnings
or runtime crashes.

### JSON / STJ

- **Never** use `new JsonSerializerOptions { ... }` at runtime in AOT code.
  A bare `JsonSerializerOptions` without a `TypeInfoResolver` crashes under AOT.
- Use STJ source generation instead:
  ```csharp
  [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
  [JsonSerializable(typeof(MyType))]
  internal partial class MyTypeContext : JsonSerializerContext { }
  ```
  Then pass `MyTypeContext.Default.Options` wherever `JsonSerializerOptions` is needed.
- For `IChatClient.GetResponseAsync<T>`, always pass the source-gen options:
  ```csharp
  var response = await chatClient.GetResponseAsync<T>(messages, MyTypeContext.Default.Options, cancellationToken: ct);
  ```

### YAML (YamlDotNet)

YamlDotNet uses reflection internally. Preserve any POCO that flows through
`ISerializer`/`IDeserializer` with:
```csharp
[DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MyPoco))]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Type preserved via DynamicDependency")]
private static readonly IDeserializer Deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .IgnoreUnmatchedProperties()
    .Build();
```

### Reflection / dynamic dispatch

- Avoid `Type.GetType(string)`, `Activator.CreateInstance`, or `Assembly.GetTypes()`.
- Prefer `IChatClient` abstraction — it is AOT-safe by design (`Microsoft.Extensions.AI`).

### Warning suppression (already in Cli.csproj)

```xml
<IlcSuppressWarnings>IL3050</IlcSuppressWarnings>
<NoWarn>$(NoWarn);IL3050;IL2104;IL3053</NoWarn>
```

These cover YamlDotNet assembly-level warnings. Do **not** add new suppressions
without `[DynamicDependency]` or a concrete explanation.

## Build / install

```bash
make install      # native AOT publish → ~/.local/bin/forge  (osx-arm64)
make demo-naive   # end-to-end smoke test (forces a full rebuild)
```

On macOS the AOT linker needs Homebrew's OpenSSL and brotli on the library path;
`LinkerArg` entries in `Cli.csproj` handle this automatically.

## Release workflow

Releases are cut via GitHub Actions (`workflow_dispatch`):
1. Enter the version (e.g. `0.1.3`) in the Actions UI.
2. The workflow tags the commit, opens a draft release, and attaches
   `forge-osx-arm64`, `forge-linux-x64`, and `forge-win-arm64.exe`.
3. Review the draft on GitHub, then publish.

Semver: patch bump for bug fixes and backwards-compatible changes; minor for new
user-visible language features; major for breaking `.mcl` syntax changes.

## Supported providers

`ProviderClientBuilder` in `src/ForgeMission.Cli/ProviderClientBuilder.cs` maps
the `provider` field in `forge.toml` to an `IChatClient`. Adding a new provider
is a single switch case + one private method — no new packages needed for
OpenAI-compatible APIs.

| `provider` value | API | SDK used |
|---|---|---|
| `openai` / `azure` | OpenAI / Azure OpenAI | `OpenAI` NuGet |
| `anthropic` | Anthropic Claude | `Anthropic` NuGet |
| `ollama` | Ollama (local) | `OpenAI` NuGet (pointed at localhost) |
| `xai` | xAI Grok | `OpenAI` NuGet (pointed at api.x.ai/v1) |

**forge.toml examples:**

```toml
# OpenAI
[providers.default]
provider = "openai"
model    = env("MCL_MODEL", "gpt-4o-mini")
apiKey   = env("MCL_API_KEY")

# Anthropic
[providers.default]
provider = "anthropic"
model    = env("MCL_MODEL", "claude-haiku-4-5-20251001")
apiKey   = env("MCL_API_KEY")

# xAI (Grok)
[providers.default]
provider = "xai"
model    = env("MCL_MODEL", "grok-3-mini")
apiKey   = env("XAI_API_KEY")

# Ollama (local, no key needed)
[providers.default]
provider = "ollama"
model    = "llama3.2"
apiKey   = ""
```

**Adding a new OpenAI-compatible provider** (e.g. Groq, Together, Mistral):
1. Add a case to the switch in `ProviderClientBuilder.cs`
2. Add a private method pointing `OpenAIClientOptions.Endpoint` at the provider's base URL
3. No new NuGet packages required

## Code conventions

- Language files: `.mcl` extension, binary: `forge`.
- Expert markdown files live under `experts/<ExpertName>/expert.md`.
- Lock file: `mcl.lock` (relative paths, generated by `forge init`).
- Reserved context variables: `apiKey`, `model`, `provider`, `endpoint`.
- `IExpertRunner` is the only interface between the CLI and the AI provider.
  Keep it free of provider-specific types.
