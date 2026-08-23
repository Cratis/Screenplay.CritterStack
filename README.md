# Screenplay.CritterStack

Generate verified [Cratis Screenplay](https://github.com/Cratis/Screenplay) definitions from Marten, Wolverine, and independently composed .NET source semantics.

`Cratis.CritterStack.Screenplay` follows the same package architecture as `Cratis.Arc.Screenplay`: a host supplies Roslyn compilations, and the package analyzes framework conventions, builds one semantic application model, lowers it through the shared Screenplay generation SDK, prints canonical `.play` source, and verifies it with the Screenplay compiler.

This is an independent Cratis compatibility project. It is not affiliated with or endorsed by JasperFx. Marten, Wolverine, JasperFx, and Critter Stack names belong to their respective owners. Generated models may require human review wherever diagnostics report semantic loss.

## Goals

- Marten-only event stores, documents, aggregates, projections, and queries.
- Generic and instance-based Marten projection registrations, with exact authored projection name/version evidence and explicit diagnostics for unsupported async/live lifecycle semantics.
- Async daemon mode and first-class subscription registration/configuration evidence without inventing state views, automations, translations, events, messages, or document consequences from arbitrary processing code.
- Marten document identities from exact configuration, identity attributes, and conventions, without guessing unresolved expressions.
- Authored Marten event/document tenancy declarations, attributes, and global policies retained as located `MARTEN0013` diagnostic evidence without inferring effective state, runtime tenant resolution, or database topology.
- Authored Marten event aliases, schema-version helpers, naming style, and current upcast registrations retained as `MARTEN0011`/`MARTEN0012` diagnostic evidence without renaming or originating events or inferring upcast behavior.
- Marten compiled-query execution linked to proven Wolverine HTTP query entry points, including public plan parameters; unresolved nested executable flow reports `MARTEN0006` instead of guessing.
- Marten + Wolverine HTTP and message handlers.
- Vogen concepts, primitive representations, authored validation hooks, nullable usages, and explicit loss diagnostics through the separately composed `Cratis.Screenplay.Generation.DotNet.Vogen` adapter.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Target-aware exact current and legacy `IEventStream<T>` appends across multiple handler parameters, including commandless HTTP and metadata-only loaded streams, with per-binding identities and explicit diagnostics instead of first-stream guesses.
- Markerless event/message discovery from actual framework usage.
- Deterministic output without starting the application or connecting to PostgreSQL.
- Explicit diagnostics whenever source behavior cannot be represented faithfully.

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

Shared generation infrastructure lives in [`Cratis/Screenplay.Generation`](https://github.com/Cratis/Screenplay.Generation). Cratis CLI owns `MSBuildWorkspace`, project/host selection, and output.

## Canonical fixtures

The compatibility plan uses:

- Wolverine's current `src/Samples/IncidentService`;
- `JasperFx/CritterStackHelpDesk` for Marten 6/Wolverine 1 behavior;
- BankAccountES and other focused applications from the local Critter Stack sample corpus;
- MartenWithProjectAspire for instance-registered async, multi-stream, and event projections;
- the repository-owned `VogenConcepts` fixture pinned to Vogen 8.0.7, Marten 9.29.0, and Wolverine 6.29.2.

See:

- [`STRATEGY.md`](STRATEGY.md) — why the adapter is public and how it supports visualization, interoperability, and migration
- [`COMPATIBILITY.md`](COMPATIBILITY.md) — exact canonical package sets, research baselines, and support tiers
- [`CRITTER_STACK_PATTERN_DISCOVERY_RESEARCH.md`](CRITTER_STACK_PATTERN_DISCOVERY_RESEARCH.md) — how source behavior maps to State Change, State View, Automation, and Translation
- [`MVP_ACCEPTANCE.md`](MVP_ACCEPTANCE.md) — the explicit stopping criteria for a credible preview and a later 1.0
- [`CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`](CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md)
- [`CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md`](CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md)
- [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md)

## Build and test

```shell
dotnet test Screenplay.CritterStack.slnx --configuration Debug
dotnet build Screenplay.CritterStack.slnx --configuration Release
dotnet pack Screenplay.CritterStack.slnx --no-build --configuration Release -o Artifacts/NuGet
```

## License

Screenplay.CritterStack is licensed under the [MIT license](LICENSE).
