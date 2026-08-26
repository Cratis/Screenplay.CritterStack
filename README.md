# Screenplay.CritterStack

Generate compiler-checked, reviewable [Cratis Screenplay](https://github.com/Cratis/Screenplay) candidates from authorized Marten, Wolverine, and independently composed .NET source semantics.

`Cratis.CritterStack.Screenplay` follows the same package architecture as `Cratis.Arc.Screenplay`: a host supplies Roslyn compilations, and the package analyzes framework conventions, builds one semantic application model, lowers it through the shared Screenplay generation SDK, prints canonical `.play` source, and verifies it with the Screenplay compiler.

This is an independent, optional pre-release Cratis compatibility project. It is not affiliated with or endorsed by JasperFx. Marten, Wolverine, JasperFx, and Critter Stack names belong to their respective owners. Generated models require human review wherever diagnostics report loss or ambiguity. The package is not an automatic migration authority, production runtime, compatibility promise, or support commitment.

## Goals

- Marten-only event stores, documents, aggregates, projections, and queries.
- Generic and instance-based Marten projection registrations, with exact authored projection name/version evidence and explicit diagnostics for unsupported async/live lifecycle semantics.
- Async daemon mode and first-class subscription registration/configuration evidence without inventing state views, automations, translations, events, messages, or document consequences from arbitrary processing code.
- Marten document identities from exact configuration, identity attributes, and conventions, without guessing unresolved expressions.
- Authored Marten event/document tenancy declarations, attributes, and global policies retained as located `MARTEN0013` diagnostic evidence without inferring effective state, runtime tenant resolution, or database topology.
- Authored Marten event aliases, schema-version helpers, naming style, and current upcast registrations retained as `MARTEN0011`/`MARTEN0012` diagnostic evidence without renaming or originating events or inferring upcast behavior.
- Marten compiled-query execution linked to proven Wolverine HTTP query entry points, including public plan parameters; unresolved nested executable flow reports `MARTEN0006` instead of guessing.
- Marten + Wolverine HTTP and message handlers, including signature-stable overloaded handler identities and batched `T[]` message delivery.
- Returned `IStorageAction<T>` / `UnitOfWork<T>` persistence, exact per-slot storage-factory refinement, and `[Entity]` / `[FirstOrDefault]` / `[Queryable]` bound reads.
- Presence diagnostics for Wolverine/Marten convention-alteration hooks, per-chain `Configure(HandlerChain)`, and Marten session listeners without interpreting policy or listener bodies.
- Compound `Load*`, `Before*`, `After*`, `PostProcess*`, `Finally*`, and after-commit stages, with exact outgoing-message consequences retained on the owning entry point and explicit `WOLVERINE0020` loss.
- Literal projection `PublishMessage(new TMessage(...))` side effects retained as Message/`Publishes` evidence, with `MARTEN0015` for unresolved payload flow.
- Event wire configuration (`UseBinarySerializer<T>`, append mode, stream identity) and `RegisterValueType` concept nomination retained without fabricating event or concept representations.
- Vogen concepts, primitive representations, authored validation hooks, nullable usages, and explicit loss diagnostics through the separately composed `Cratis.Screenplay.Generation.DotNet.Vogen` adapter.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Target-aware exact current and legacy `IEventStream<T>` appends across multiple handler parameters, including commandless HTTP and metadata-only loaded streams, with per-binding identities and explicit diagnostics instead of first-stream guesses.
- Bounded current and legacy Wolverine DCB evidence from authored `[DcbModel]` / `[BoundaryModel]` parameters, direct `EventTagQuery` fluent chains, exact boundary appends, and safe declarative returns, with `WOLVERINE0014`/`WOLVERINE0015` instead of invented stream topology.
- Bounded authored Wolverine saga discovery for public concrete closed `Wolverine.Saga` state, grouped by message with Wolverine-compatible `SagaChain` admission. It preserves admitted role spellings and `Async` twins, constructor/returned-state creation constraints, collision-safe handler identities, exact correlation precedence (including inherited public members), cascades, timeouts, direct bus calls, and exact `MarkCompleted()` evidence. Saga state is excluded at every final HTTP query, message, and event admission boundary. `WOLVERINE0016` is a report-only realization/provenance diagnostic: Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow. Screenplay uses ordinary Event Modeling building blocks; this is not a language-gap request, and generated `.play` bytes remain unchanged. `WOLVERINE0017` reports runtime-resolved correlation, while `WOLVERINE0018` reports rejected lifecycle shapes without inventing persistence or transport topology.
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
- [`STRATEGY.md`](STRATEGY.md) — current product position, shipped behavior, and claim boundary
- [`COMPATIBILITY.md`](COMPATIBILITY.md) — exact exercised package sets and compatibility evidence tiers
- [`CRITTER_STACK_PATTERN_DISCOVERY_RESEARCH.md`](CRITTER_STACK_PATTERN_DISCOVERY_RESEARCH.md) — how source behavior maps to State Change, State View, Automation, and Translation
- [`MVP_ACCEPTANCE.md`](MVP_ACCEPTANCE.md) — historical acceptance evidence for the original 0.1 preview
- [`CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`](CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md)
- [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md) — current release, working-tree coverage, residuals, and continuation
- [`CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md`](CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md) — historical implementation plan
- [Stage: Build a renderer target](https://github.com/Cratis/Stage/blob/main/Documentation/guides/build-renderer-target.md) — canonical renderer-target guidance
- [`WRITING_CRITTER_STACK_RENDERER.md`](WRITING_CRITTER_STACK_RENDERER.md) — unapproved design proposal; not canonical onboarding or an implementation commitment

## Build and test

```shell
dotnet test Screenplay.CritterStack.slnx --configuration Debug
dotnet build Screenplay.CritterStack.slnx --configuration Release
dotnet pack Screenplay.CritterStack.slnx --no-build --configuration Release -o Artifacts/NuGet
```

## License

Screenplay.CritterStack is licensed under the [MIT license](LICENSE).
