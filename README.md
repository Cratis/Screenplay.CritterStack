# Screenplay.CritterStack

Generate compiler-checked, reviewable [Cratis Screenplay](https://github.com/Cratis/Screenplay) candidates from authorized Marten, Wolverine, and independently composed .NET source semantics.

`Cratis.CritterStack.Screenplay` follows the same package architecture as `Cratis.Arc.Screenplay`: a host supplies Roslyn compilations, and the package analyzes framework conventions, builds one semantic application model, lowers it through the shared Screenplay generation SDK, prints canonical `.play` source, and verifies it with the Screenplay compiler.

This is an independent, optional pre-release Cratis compatibility project. It is not affiliated with or endorsed by JasperFx. Marten, Wolverine, JasperFx, and Critter Stack names belong to their respective owners. Generated models require human review wherever diagnostics report loss or ambiguity. The package is not an automatic migration authority, production runtime, compatibility promise, or support commitment.

## Why

If you have an existing event-sourced Marten + Wolverine codebase, Screenplay.CritterStack generates a reviewable, compiler-verified Screenplay model from that source — an optional, independent on-ramp to the Cratis model-first stack. The events, commands, projections, and handlers your code already expresses become a [Screenplay](https://github.com/Cratis/Screenplay) model you can inspect and evolve with [Stage](https://github.com/Cratis/Stage), [Chronicle](https://github.com/Cratis/Chronicle), and [Arc](https://github.com/Cratis/Arc). Wherever source semantics cannot be represented faithfully, explicit diagnostics mark the spot for human review instead of guessing.

## Goals

The adapter recovers bounded Marten and Wolverine semantics — event stores, documents, aggregates, projections, queries, HTTP and message handlers, sagas, tenancy and event-wire configuration, and Vogen concepts — deterministically, without starting the application or connecting to PostgreSQL, and reports explicit diagnostics whenever source behavior cannot be represented faithfully. The complete goal list, including per-diagnostic evidence boundaries, lives in [`Documentation/goals.md`](Documentation/goals.md).

## Architecture

```text
Roslyn compilations
  -> Vogen concept contribution (when exact authored evidence exists)
  -> Critter Stack Marten/Wolverine contribution (when framework evidence exists)
  -> subject-aware concept usage binding
  -> Cratis.Screenplay.Generation (all contributions, once)
  -> verified .play source
```

`CritterStackScreenplayAdapter` remains a low-level Marten/Wolverine adapter and matches those framework APIs by metadata name without runtime package references. The generator facade depends on the separate Vogen adapter package; neither production package depends on the Vogen source-generator/runtime package used by analyzed applications.

## Generator composition

For the generic code-to-Screenplay adapter contract, evidence rules, fact vocabulary, source placement, and specification checklist, see the canonical [`Screenplay.Generation/WRITING_SOURCE_ADAPTERS.md`](https://github.com/Cratis/Screenplay.Generation/blob/main/WRITING_SOURCE_ADAPTERS.md) guide. The documentation [Marten and Wolverine case study](Documentation/guides/extend-source-adapter.md) explains how this repository applies those rules to focused specs, diagnostics, and public compatibility evidence.

The parameterless facade composes Vogen and Critter Stack by default. Each adapter first identifies whether it can analyze the supplied projects, then contributes independently identified facts to one `ScreenplayDefinitionGenerator`:

```csharp
var result = new CritterStackScreenplayGenerator().Generate(
    projects,
    new CritterStackScreenplayOptions { Domain = "Ordering" });
```

Hosts can replace the default composition with one collection expression. Adapter order does not choose conflicts, and each contribution retains its own adapter identity and evidence:

```csharp
IDotNetScreenplayAdapter[] adapters =
[
    new VogenConceptScreenplayAdapter(),
    new CritterStackScreenplayAdapter(),
    externalAdapter
];

var generator = new CritterStackScreenplayGenerator(adapters);
var result = generator.Generate(projects, options);
```

The existing `(IDotNetScreenplayAdapter, ScreenplayDefinitionGenerator)` constructor remains available for hosts that supply one adapter and their own shared pipeline.

Shared generation infrastructure lives in [`Cratis/Screenplay.Generation`](https://github.com/Cratis/Screenplay.Generation). Cratis CLI owns `MSBuildWorkspace`, project/host selection, output, and the source context supplied for each selected project. The host must map its exact authored `Project.Documents` syntax trees and choose the stable project identity, display root, and case policy; adapters consume that context and do not infer source identity from physical Roslyn paths.

Project-aware generation derives placement from one fixed `DotNetSourceStructures` snapshot after Marten and Wolverine have established each artifact's semantic role. Configure an optional project-relative `FeatureRoot`, `Module`, and `NamespaceSegmentsToSkip` through `CritterStackScreenplayOptions`; folder and namespace placement must agree, and invalid, partial, ambiguous, or conflicting structures fail closed with `DOTNETSP####` diagnostics. Events shared by Marten and Wolverine use the strongest proven semantic role, while custom projections and Wolverine saga artifacts remain deliberately unplaced.

The `Generate(Compilation, ...)` facade and project calls without source context retain the legacy placement behavior. Supplying host-owned source context opts a project set into strict shared placement. This keeps existing compilation-only consumers compatible while allowing project-aware hosts to use stable shared placement and provenance.

The canonical runner uses source-path policy v1 with workspace-relative display paths and ordinal identity casing. Its stable project identity is the repository-relative project path without the `.csproj` extension, and each source identity uses the project-relative document path. This preserves the existing displayed paths while keeping physical checkout roots out of identities and policy reporting.

A compatibility-only heuristic can supply a legacy display range for a source-backed referenced-project symbol **after** Critter Stack's existing semantic discovery has admitted that symbol. It considers only declarations accepted by the shared authored-source heuristic, orders safe workspace-relative ranges deterministically, excludes generated names and headers, and never participates in artifact or fact admission. The range carries no `SourceFileIdentity`. This fallback requires a fully qualified `SourceRoot` and a declaration beneath it; otherwise `Source` is omitted rather than exposing an absolute path or basename. The strict location path never admits generated or out-of-context trees. The `Generate(Compilation, ...)` convenience overload has neither host-owned source context nor a safe source root, so its evidence omits source provenance. Hosts that need stable identity must call the project-aware overload with an explicit source context.

## Compatibility fixtures

The compatibility plan uses:

- Wolverine's current `src/Samples/IncidentService`;
- `JasperFx/CritterStackHelpDesk` for Marten 6/Wolverine 1 behavior;
- BankAccountES and other focused applications from the pinned public `JasperFx/CritterStackSamples` repository;
- MartenWithProjectAspire for instance-registered async, multi-stream, and event projections;
- the repository-owned `VogenConcepts` fixture pinned to Vogen 8.0.7, Marten 9.29.0, and Wolverine 6.29.2, including the canonical store-agnostic DCB and authored saga APIs.

These pinned samples and focused source-shape specs provide compatibility evidence for the versions and behaviors they exercise. They do not establish a broad support promise or close the requirement for a wholly Cratis-owned deterministic release fixture before broader Preview positioning.

See:

- [`Documentation/index.md`](Documentation/index.md) — documentation entry point and current boundaries
- [Stage: Build a renderer target](https://github.com/Cratis/Stage/blob/main/Documentation/guides/build-renderer-target.md) — canonical renderer-target guidance

## Build and test

```shell
dotnet test Screenplay.CritterStack.slnx --configuration Debug
dotnet build Screenplay.CritterStack.slnx --configuration Release
dotnet pack Screenplay.CritterStack.slnx --no-build --configuration Release -o Artifacts/NuGet
```

## The Cratis ecosystem

This project is part of [Cratis](https://www.cratis.io) — free, MIT-licensed tools for building event-sourced and CQRS applications.

- **[Chronicle](https://github.com/Cratis/Chronicle)** — event-sourcing database and runtime. Orleans-based kernel, pluggable storage (MongoDB default; PostgreSQL, SQL Server, SQLite, in-memory), language-agnostic gRPC contracts. [Docs](https://www.cratis.io/chronicle/)
- **Chronicle clients** — first-class [.NET SDK](https://github.com/Cratis/Chronicle), plus [TypeScript](https://github.com/Cratis/Chronicle.TypeScript), [Kotlin/Java](https://github.com/Cratis/Chronicle.Kotlin), and [Elixir](https://github.com/Cratis/Chronicle.Elixir); [Python](https://github.com/Cratis/Chronicle.Python) coming soon (pre-alpha). AI agents connect through the [Chronicle MCP server](https://github.com/Cratis/Chronicle.Mcp).
- **[Arc](https://github.com/Cratis/Arc)** — opinionated CQRS framework for ASP.NET Core with commands, queries, validation, authorization, and TypeScript proxy generation. Works without event sourcing. [Docs](https://www.cratis.io/arc/)
- **[Components](https://github.com/Cratis/Components)** — React components aligned with Arc patterns. [Docs](https://www.cratis.io/components/)
- **[CLI](https://github.com/Cratis/cli) + Workbench** — inspect and diagnose Chronicle from the terminal or the browser. [Docs](https://www.cratis.io/cli/)
- **Model-first layer (experimental)** — [Studio](https://github.com/Cratis/Studio), [Screenplay](https://github.com/Cratis/Screenplay), [Stage](https://github.com/Cratis/Stage), [Scene](https://github.com/Cratis/Scene), [Prologue](https://github.com/Cratis/Prologue)
- **Supporting** — [Fundamentals](https://github.com/Cratis/Fundamentals), [Specifications](https://github.com/Cratis/Specifications), [Synopsis](https://github.com/Cratis/Synopsis), [Lens](https://github.com/Cratis/Lens), [Narrator](https://github.com/Cratis/Narrator), and free [AI tooling](https://github.com/Cratis/AI) (preview); [Ensemble](https://github.com/Cratis/Ensemble) coming soon (pre-release)
- **[Samples](https://github.com/Cratis/Samples)** — runnable event sourcing and CQRS samples for the whole stack

Everything Cratis publishes today is MIT licensed and free to use.

## License

Screenplay.CritterStack is licensed under the [MIT license](LICENSE).
