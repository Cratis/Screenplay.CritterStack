<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Source-to-Screenplay adapters: Critter Stack research and architecture

## Executive conclusion

Generating a Screenplay `.play` definition from Marten and Marten + Wolverine source is feasible and should follow the same fundamental pattern as the existing Arc generator:

```text
source workspace
  -> language/compiler model
  -> framework-specific semantic evidence
  -> resolved Screenplay-semantic graph
  -> lowerable Screenplay model
  -> Cratis.Screenplay syntax tree
  -> canonical printer
  -> Screenplay compiler verification
  -> .play
```

The generator must be **source-first, framework-aware, deterministic, and honest about loss**. It must not start the application or connect to PostgreSQL by default.

The most important architectural decision is that adapters contribute typed semantic facts and relationships. They do **not** emit `.play` strings and do **not** construct `ApplicationSyntax` directly. A central generation layer owns conflict resolution, naming, lowering, printing, and verification.

For the first implementation, official .NET adapters should run in-process over one Roslyn/MSBuild workspace. The semantic contracts must be serialization-friendly so an out-of-process protocol and adapters for other languages can be added later, but a plugin protocol and adapter-host executable should not delay the first Critter Stack vertical slice.

## Research baseline

The research was performed read-only against these local checkouts:

| Repository | Baseline |
| --- | --- |
| Screenplay | `main`, `a47cb2dd5f664f2aae351cd0986b8674475326e4`, `v4.2.1-5-ga47cb2d` |
| Arc | `main`, `1e4750ff5784d77a15330e21ed2b0d49e188116a` |
| Cratis CLI | `7b287b8...`; shipped Arc Screenplay package 21.14.2 was also inspected |
| JasperFx | `main`, `da9fd17d69df5ff41940800bfb34ad4d88a88391`, package 2.53.0 |
| Marten | `master`, `a483b09f881f1576152aa42a27b37cc17fab252f`, package 9.28.0 |
| Wolverine | `main`, `af4807b5fb225ce7535c67785b74007fdad2dd9f`, package 6.29.1 |
| CritterStackSamples | local clone at `~/CritterStackSamples` |
| CritterStackHelpDesk | `main`, `b67659dd7ca6d8ff07e7b9dad20affc4a37b6062`, Marten 6.3/Wolverine 1.11 |
| Wolverine IncidentService | canonical sample under `Wolverine/src/Samples/IncidentService`, Marten 9.23/Wolverine 6.29.1 |

Official documentation reviewed included:

- <https://martendb.io/events/projections/>
- <https://martendb.io/events/projections/single-stream-projections>
- <https://martendb.io/events/projections/multi-stream-projections>
- <https://martendb.io/events/projections/event-projections.html>
- <https://wolverinefx.net/guide/handlers/>
- <https://wolverinefx.net/guide/handlers/discovery.html>
- <https://wolverinefx.net/guide/handlers/return-values>
- <https://wolverinefx.net/guide/http/integration>
- <https://wolverinefx.net/guide/http/marten>
- <https://wolverinefx.net/guide/durability/marten/event-sourcing>
- <https://wolverinefx.net/guide/durability/marten/transactional-middleware>

## Existing Arc-to-Screenplay pipeline

The Arc generator is an ordinary library, not a Roslyn source generator or MSBuild task.

Primary entry points:

- `Arc/Source/DotNET/Screenplay/IScreenplayGenerator.cs`
- `Arc/Source/DotNET/Screenplay/ScreenplayGenerator.cs`
- `Arc/Source/DotNET/Screenplay/Analysis/ApplicationModelAnalyzer.cs`
- `Arc/Source/DotNET/Screenplay/Emission/ScreenplayEmitter.cs`
- `Arc/Source/DotNET/Screenplay/Verification/ScreenplayVerifier.cs`

Its flow is:

1. Receive one or more Roslyn `Compilation` objects.
2. Catalog symbols across projects.
3. Analyze Arc/Chronicle artifacts into `Cratis.Arc.Screenplay.Model.ApplicationModel`.
4. Build `Cratis.Screenplay.Syntax.ApplicationSyntax`.
5. Print through `ScreenplayPrinter`.
6. Compile the generated text through `ScreenplayCompiler`.
7. Return source, model, and stable `SPxxxx` diagnostics.

### Reusable commonalities

These should become generation-platform behavior shared by every adapter:

- compilation/workspace ownership is outside the framework analyzer;
- source locations and repository-relative file references;
- deterministic project, artifact, and diagnostic ordering;
- generated-source detection;
- cross-project type and semantic-model routing;
- type-shape extraction and primitive conversion;
- FluentValidation translation;
- ASP.NET authorization translation;
- policy, validation, and naming safety;
- explicit diagnostics for information that cannot be preserved;
- one central AST emitter and printer;
- mandatory compile and print/compile/print verification.

### Arc-specific behavior

These remain in the Arc adapter:

- namespace equals vertical-slice boundary;
- `[Command]` records and instance `Handle()`;
- `[EventType]` event recognition;
- `[ReadModel]` and static query methods;
- Chronicle projection attributes and `IProjectionFor<T>`;
- Chronicle reducers, reactors, constraints, aggregate roots, concurrency, and event sequences;
- Cratis scenario specifications;
- Arc-generated TypeScript proxy imports and screen discovery.

The current Arc public API is broad. Its public model records, readers, emitters, diagnostics, injected collaborators, constructors, and `ScreenplayGenerationResult.Model` cannot simply be moved to another assembly without a breaking release.

## Findings from JasperFx

`JasperFx.Events.SourceGenerator` generates projection execution dispatchers, not a complete application/event-model manifest.

Relevant source:

- `JasperFx/src/JasperFx.Events.SourceGenerator/AggregateEvolverGenerator.cs`
- `AggregateAnalyzer.cs`
- `EvolverCodeEmitter.cs`
- `JasperFx.Events/Aggregation/GeneratedEvolverAttribute.cs`

It can corroborate:

- aggregate/projection binding;
- inferred aggregate identity;
- event types consumed by generated evolvers;
- documents published by `EventProjection` methods.

It does not supply:

- commands or messages;
- handler relationships;
- HTTP origins;
- stream ownership;
- projection lifecycle or grouping;
- source locations;
- complete payload shapes;
- slice/module placement.

`AggregateDescriptor`, `HandlerRelationshipDescriptor`, `PublisherOrigin`, and `EventModelDefinition` exist as public modeling contracts under `JasperFx.Events/EventModeling`, but the inspected JasperFx and Wolverine versions do not generate a complete manifest from them.

Runtime `EventStoreUsage` and `DocumentStoreUsage` are rich diagnostic snapshots, but constructing them executes application configuration and may contact PostgreSQL. They are optional enrichment, not the default source-generation foundation.

## Marten semantic model

### Documents

Document evidence can come from:

- `Schema.For<T>()`;
- `RegisterDocumentType<T>()` and explicit registration;
- `[DocumentAlias]` and related Marten attributes;
- `Store`, `Insert`, `Update`, and `Delete`;
- `Query<T>`, `LoadAsync<T>`, and compiled queries;
- projection-published types.

Identity resolution should mirror Marten/JasperFx:

1. `[Identity]` member;
2. case-insensitive `Id` property or field;
3. explicit `.Identity(...)` configuration;
4. recognized strongly typed/F# identity handling.

An ordinary Marten document is not automatically an event-built Screenplay read model. The semantic graph needs distinct roles for:

- ordinary persisted document;
- projected document/read model;
- live event-sourced aggregate;
- materialized aggregate snapshot;
- decision state loaded by Wolverine.

### Events

Marten events are usually markerless. Strong evidence includes:

- explicit event registration or alias mapping;
- `StartStream` and append values;
- aggregate `Create`, `Apply`, `ShouldDelete`, and `Evolve` methods;
- projection handler parameters;
- event queries and filters;
- generated evolver metadata as corroboration.

Persisted aliases, old aliases, upcasts, binary serialization, and schema generations are not represented by current `EventSyntax`. The initial adapter should use the CLR type name and emit a loss diagnostic for contract metadata it cannot preserve.

### Streams and concurrency

Analyze symbol-bound calls to:

- `StartStream<TAggregate>`;
- `Append`, `AppendOptimistic`, and `AppendExclusive`;
- `FetchForWriting<T>` and `FetchForExclusiveWriting<T>`;
- `WriteToAggregate<T>`;
- `IEventStream<T>.AppendOne/AppendMany`;
- `MartenOps.StartStream<T>` and `IStartStream`.

Generic `StartStream<TAggregate>` is strong aggregate and identity evidence. Untyped appends may not reveal the aggregate. Expected versions and exclusive locking are not identical to Chronicle-oriented Screenplay concurrency dimensions and must not be silently flattened.

### Projections

Self-aggregating and single-stream projections can often become:

- a PDL projection when the source contains simple, explicit mappings; or
- a reducer with file references when behavior is current-state-plus-event.

Multi-stream projection mapping is exact only for simple identity functions such as:

```csharp
Identity<SomethingHappened>(e => e.CustomerId)
```

One-to-many identities, custom groupers, event slicers, tenant rollups, and complex fan-out are not currently representable.

`EventProjection` can publish one or more document types. Simple `Create`/`Transform` object construction may lower to PDL. Arbitrary operations or multiple coupled targets require file fallbacks and loss diagnostics.

### Marten-only commands

Marten is a storage framework, not a command framework. An append might belong to an endpoint, controller, worker, scheduled job, test, or projection side effect.

A Marten-only adapter should confidently generate events, documents, aggregates, projections, reducers, and well-evidenced queries. It should emit commands only where an application entry point is statically visible or the application supplies an explicit event model.

## Wolverine semantic model

### Handler discovery

Mirror `Wolverine/Configuration/HandlerDiscovery.cs` rather than relying on names alone.

Handler types are public, concrete, closed types selected through:

- `Handler` or `Consumer` suffix;
- `IWolverineHandler`;
- `[WolverineHandler]`;
- saga inheritance;
- explicit type/assembly inclusion.

Handler methods include the `Handle`, `Consume`, and saga method families or methods marked `[WolverineHandler]`. Honor `[WolverineIgnore]`, disabled conventional discovery, modules, and statically resolvable custom discovery.

Lifecycle methods such as `Before`, `Load`, `Validate`, `After`, and `Finally` are middleware for a handler; they are not independent handlers.

### Return-value semantics

Return interpretation is contextual and is the highest-risk area.

Ordinary handlers generally treat return values as cascading messages. Tuple elements are decomposed independently. `OutgoingMessages` cascades contents. `ISideEffect` executes without cascading. Saga state is persisted as saga state.

HTTP endpoints generally treat the first valid created value as the HTTP response and later values as cascades or side effects, but wrappers override the naive rule:

- `CreationResponse<T>` controls 201/location;
- `[EmptyResponse]` forces an empty response;
- `UpdatedAggregate` requests a re-fetched aggregate response;
- `Events`, `OutgoingMessages`, and other `IWolverineReturnType` values are not response bodies.

Marten aggregate/decider workflows reinterpret returns as stream events. Recognize both current store-agnostic APIs and legacy aliases:

- `[DeciderFunction]`, `[WriteModel]`, `[ReadModel]`, `EventsToAppend`;
- `[AggregateHandler]`, `[WriteAggregate]`, `[ReadAggregate]`, `[Aggregate]`, `Wolverine.Marten.Events`.

When a handler accepts `IEventStream<T>`, the handler is expected to append directly; other returns retain ordinary cascade semantics. Without this distinction, an adapter will incorrectly turn messages into persisted events.

### HTTP and binding

Recognize Wolverine HTTP attributes and startup mapping calls. Preserve route and binding evidence separately from domain input shape.

Parameter binding precedence includes explicit form/query/service attributes, `AsParameters`, files, custom parameter attributes, services/context, route, headers, query convention, and JSON body.

Validation and authorization are active only when their policies are enabled. A validator package reference is not proof that validation executes. Domain properties named `Role` are not authorization.

### Messages, side effects, and scheduling

Persisted events, local cascades, explicit publishes, transport routing, side effects, and delayed delivery are separate relationships.

Never map a broker publication or local cascade to Screenplay `produces`. `produces` means an appended fact.

## Canonical sample requirements

### Current IncidentService

Canonical path: `Wolverine/src/Samples/IncidentService`.

It requires support for:

- generated stream identity plus `CreationResponse<Guid>`;
- route-only aggregate identities absent from command DTOs;
- optimistic version from a command property;
- state-dependent compound-handler validation;
- `UpdatedAggregate`, `Events`, and `OutgoingMessages` in one tuple;
- delayed one-shot `ArchiveIncident` dispatch;
- direct session append and snapshot deletion;
- explicit inline `Snapshot<Incident>` registration;
- reducer events only where `Apply`/`ShouldDelete` methods actually exist;
- GET query using `FetchLatest`;
- excluding internal/inactive decider-like methods and comments.

### CritterStackHelpDesk

Canonical path: `/Volumes/sourcecode/repos/CritterStackHelpDesk`.

It adds compatibility and architecture coverage for:

- Marten 6/Wolverine 1 APIs;
- API + worker + test + demonstration project roles;
- markerless contracts in referenced projects;
- one command/message with both HTTP and message handlers;
- `IStartStream`, `Events`, `OutgoingMessages`, and nullable aggregate-event returns;
- event forwarding to Wolverine;
- custom before middleware and user detection;
- a stream type (`Incident`) different from projected/decision state (`IncidentDetails`);
- inline projection plus live aggregation;
- Rabbit exchange publisher and separate worker queue consumer;
- checked-in generated-code exclusion and optional corroboration;
- negative evidence preventing fabricated authorization, tenancy, sagas, schedules, aliases, or upcasts.

### CritterStackSamples

Representative fixtures include:

- `BankAccountES` for event-sourced aggregate handlers;
- `MartenWithProjectAspire` for Marten-only async projections;
- `CqrsMinimalApi` for direct document CRUD;
- `OutboxDemo` for saga/outbox;
- `BookingMonolith` for event sourcing and multiple entity loads;
- `Reports` for `IMartenOp` and custom identity;
- one Fleet service for transport, delay, and projection side effects;
- `ProjectManagement` to prove not every endpoint is Wolverine.

## Shared semantic architecture

Use four explicit layers:

```text
adapter facts
  -> resolved application graph
  -> lowerable Screenplay model
  -> Screenplay AST
```

### Fact requirements

Every assertion should carry:

- stable fact ID;
- stable subject ID;
- typed fact kind;
- provider ID and version;
- source URI and range;
- evidence strength: `Exact`, `Configured`, `Conventional`, or `Heuristic`;
- explanation;
- related fact IDs.

For .NET, subject identity should include assembly/project identity and fully qualified metadata name. Simple names are display values, never merge keys.

Facts should cover at least:

- type declarations and shapes;
- application/project hosts;
- artifact roles;
- handler and endpoint entry points;
- persistence operations;
- projection/aggregate relationships;
- reads and query returns;
- response/cascade/publish/side-effect relationships;
- module/feature/slice placement evidence;
- validation and authorization;
- unresolved/lost semantics.

### Resolution rules

Resolution must be deterministic and order-independent:

1. merge exact stable identities;
2. apply explicit equivalence facts;
3. collapse identical facts;
4. union set-valued facts;
5. preserve conflicting scalar evidence;
6. report unresolved conflicts rather than using adapter order or last-wins behavior;
7. keep provenance through lowering and optionally write a sidecar manifest.

Marten and Wolverine should compose in phases:

1. discover source and framework facts;
2. link registrations and handler relationships;
3. apply HTTP response/binding semantics;
4. apply Marten+Wolverine aggregate event-capture semantics;
5. classify consequences;
6. lower to Screenplay.

The integration layer augments evidence. It must not destructively replace Marten or Wolverine facts.

## Package architecture

### Implement for the Critter Stack vertical slice

```text
Cratis.Screenplay
  compiler, AST, printer, validator

Cratis.Screenplay.Generation.Contracts
  typed facts, evidence, source locations, diagnostics
  no Roslyn or framework references

Cratis.Screenplay.Generation
  resolver, merger, lowerer, AST emitter, printer, verification

Cratis.Screenplay.Generation.DotNet
  Compilation-facing context and reusable Roslyn/source utilities
  no MSBuildWorkspace ownership

Cratis.CritterStack.Screenplay
  Marten readers
  Wolverine readers
  Marten+Wolverine augmentation
```

These can initially live in the Screenplay repository. Keep `Cratis.Arc.Screenplay` in Arc unchanged during the MVP.

The Cratis CLI continues owning `MSBuildWorkspace` initially so its existing global-tool/MSBuild packaging behavior remains stable and one workspace can feed official .NET adapters.

### Future adapter seam

The contracts must remain serialization-friendly and the orchestration boundary must be:

```text
analysis request -> adapter facts -> deterministic generation result
```

When a non-.NET adapter exists, introduce a versioned out-of-process protocol and adapter host. Do not ship plugin discovery, JSON-RPC, or a helper executable before two independent hosts can prove the protocol.

Future adapter protocol requirements include:

- protocol and IR schema negotiation;
- framed protocol-only stdout and bounded stderr logs;
- cancellation, timeout, and process cleanup;
- unknown fact/field handling;
- explicit allowlisting and hashes for external adapters;
- no automatic execution of workspace-provided binaries;
- cross-platform packaging tests.

An out-of-process host isolates dependencies and crashes but is not a security sandbox. MSBuild evaluation itself executes repository-controlled logic.

## CLI behavior

The current CLI hard-codes Arc in both provider construction and project filtering.

For the MVP:

- add `--provider arc|marten|critter-stack|auto`;
- keep Arc on its existing generator path;
- send Marten/Critter Stack through the new facts pipeline;
- load one workspace;
- target a `.csproj` as the application root and include its transitive project-reference closure;
- for a solution with one detected host, use that host and closure;
- for multiple detected hosts, fail with a diagnostic and require a project target;
- do not silently merge deployable API and worker hosts into one application unless explicitly requested;
- report selected/skipped target frameworks until an explicit `--framework` policy is implemented;
- map diagnostic severities explicitly rather than integer-casting enums.

## Screenplay language gaps

A valid initial `.play` can use existing file-reference fallbacks, but full Critter Stack fidelity needs language design work.

High-priority gaps:

1. direct document state and `store`/`update`/`delete` operations;
2. message publication/cascade distinct from event append;
3. command-level outgoing messages and delayed one-shot delivery;
4. HTTP exposure: verb, route, binding, response, status, missing behavior;
5. projection lifecycle/name/version;
6. stream/aggregate identity, start/append, and expected/exclusive version semantics;
7. multi-stream grouping and one-to-many identities;
8. saga state and correlation;
9. Marten event aliases/upcasts;
10. tenancy, subscriptions, daemon/shard metadata.

Do not expand the grammar before the source-generation MVP proves which losses most damage the resulting model. Emit stable diagnostics and file references in the meantime.

## Determinism and safety

Required output contract:

- ordinal sorting of projects, facts, artifacts, relationships, and diagnostics;
- UTF-8 without BOM and LF endings;
- invariant formatting;
- repository-relative `/` paths;
- no timestamps, random IDs, machine paths, or culture-dependent data;
- canonical fact serialization for snapshots/hashes;
- one central AST lowerer/printer;
- mandatory Screenplay compilation;
- print/compile/print stability.

Security/trust constraints:

- MSBuild evaluation is active execution of repository-controlled files;
- no runtime application bootstrapping by default;
- no PostgreSQL access by default;
- generated source may corroborate but not replace authored-source analysis;
- external adapters must eventually be explicitly trusted;
- output paths must be constrained and path traversal rejected.

## Architectural decisions

### Go

- Roslyn-first source analysis for .NET.
- Typed semantic facts with provenance.
- One shared workspace.
- In-process official .NET adapters for the MVP.
- Framework-context return classification.
- Deterministic conflict handling.
- Useful partial `.play` plus explicit diagnostics.
- Existing Arc behavior preserved.

### No-go

- No adapters emitting `.play` or AST directly.
- No concatenation of independently generated documents.
- No Arc model as the neutral IR.
- No wholesale Arc public API move/type-forwarding in the MVP.
- No runtime-only discovery.
- No claims that static analysis is exact when configuration is unresolved.
- No mapping broker messages to persisted events.
- No automatic solution-wide merge of multiple hosts.
- No plugin protocol before a non-.NET adapter exists.
- No grammar expansion before source-generation losses are measured.

## Recommended first proof

The first two vertical proofs should be:

1. `CritterStackSamples/BankAccountES` — Marten + Wolverine event sourcing, aggregate return classification, validation, snapshots.
2. `Wolverine/src/Samples/IncidentService` — current canonical HTTP/aggregate response wrappers, route identity, direct operations, delay, query, snapshot reducer.

Then add:

1. `MartenWithProjectAspire` — Marten-only async projection daemon.
2. `CritterStackHelpDesk` — older API generation and multi-project API/worker/contracts behavior.

A complete implementation plan and fresh-session prompt are in the companion handover files.
