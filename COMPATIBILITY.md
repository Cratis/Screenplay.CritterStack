<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Critter Stack compatibility reference

## What compatibility means

`Cratis.CritterStack.Screenplay` performs static source interpretation. Compatibility means that a pinned application compiles, the adapter recognizes the asserted source patterns, the generated Screenplay compiles and is print-stable, and every asserted semantic loss has a stable diagnostic.

It does **not** mean runtime behavioral equivalence, complete coverage of every Marten or Wolverine API, or support for every patch in a major line. The adapter never starts the application or connects to PostgreSQL while generating.

Maintaining an explicit compatibility reference is necessary rather than overkill. Marten and Wolverine have moved projection types, introduced source generators, renamed event-sourcing attributes, and changed method conventions across major versions. An unqualified “supports Critter Stack” statement would hide those differences.

## Canonically verified package sets

The canonical workflow pins source commits and verifies these exact package combinations:

| Fixture | Source pin | Marten | Wolverine | Coverage |
| --- | --- | ---: | ---: | --- |
| BankAccountES | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.0 | 6.23.1 | Aggregate handlers, snapshots, single-stream projection, commands, queries, validation loss, and exact `[Entity]` reads |
| CqrsMinimalApi | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.0 | 6.23.1 | Ordinary document CRUD, conventional identity, HTTP entry points, and exact `[Entity]` reads |
| Reports | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.1 | 6.23.1 | `IMartenOp`, document persistence, custom typed identity, `RegisterValueType` concept nomination, and explicit missing-representation diagnostics |
| MartenWithProjectAspire | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.1 | — | Generic and instance projection registration, async lifecycle, authored `Name` metadata, `DaemonMode.Solo`, exact multi-stream identity/member grouping, direct and `IEvent<T>` fan-out child evidence, and exact `EventProjection.Create` document storage with explicit value-flow loss |
| IncidentService | license-attributed fixture from `JasperFx/wolverine@af4807b5fb225ce7535c67785b74007fdad2dd9f` | 9.23.0 | 6.29.1 | Current HTTP aggregate workflow, response wrappers, direct append/delete, messages, delay, and query |
| VogenConcepts | Cratis-owned repository fixture | 9.29.0 | 6.29.2 | Vogen 8.0.7 source generation, concepts and nullable usages, authored validation, generated-source exclusion, exact Marten alias/upcast and logical tenancy diagnostics, target-aware streams, current store-agnostic `[DcbModel]` / `EventTagQuery`, and canonical `Saga`, `SagaIdentity`, `SagaIdentityFrom`, `TimeoutMessage`, and `MarkCompleted()` APIs |
| CritterStackHelpDesk | `JasperFx/CritterStackHelpDesk@b67659dd7ca6d8ff07e7b9dad20affc4a37b6062` | 6.3.0 | 1.11.1 | Legacy attributes/returns, API-worker contracts, event forwarding, generated-source exclusion, and compound `LoadAsync` presence |

Passing one row proves only the behaviors asserted by that fixture. It does not promote the entire Marten or Wolverine major line to verified status.

The current T1–T7 increment also uses focused authored-source specs for exact shapes not present in the pinned public applications: batched arrays, storage-action returns, `[FirstOrDefault]` / `[Queryable]` reads, convention-alteration hooks, continuation-bearing compound stages, projection message side effects, session listeners, and wire configuration. No new synthetic canonical fixture was added. Further package-level compatibility evidence must use pinned public Critter Stack samples or the public HelpDesk application.

Synthetic and canonical coverage verifies target-aware current and legacy `IEventStream<T>` bindings, exact receiver-bound `AppendOne`/`AppendMany` payloads, loaded streams without direct appends, commandless HTTP stream metadata, and unresolved-target diagnostics. It also verifies bounded DCB admission for exact/assignable `Wolverine.Persistence.EventSourcing.DcbModelAttribute` and exact legacy `Wolverine.Marten.BoundaryModelAttribute`, the actual `JasperFx.Events.Tags.IEventBoundary<T>` contract, sync/Task/ValueTask `EventTagQuery` companions, ordered OR conditions, safe direct returns/appends, and fail-closed query diagnostics.

Authored saga coverage admits only public concrete closed types whose authored base syntax resolves to the exact `Wolverine.Saga` symbol and whose type is active under the source-resolved Wolverine discovery/include/ignore policy. Lifecycle candidates are grouped by message exactly because Wolverine creates a `SagaChain` per message: instance roles, static `Start`/`StartAsync`, and static `NotFound` establish a chain; static `Starts`/`StartsAsync` and `NotFoundAsync` are retained only when another exact method for that message establishes the chain. Primitive-return methods are excluded as Wolverine discovery excludes them. Creation roles require a public parameterless constructor whenever instance or fallback creation is needed; an exact returned saga can supply creation for a static start-only chain. Existing-state-only chains do not require construction. A `NotFound`-only chain does: with no existing calls, Wolverine's starting-only generation path reaches `CreateNewSagaFrame`, which requires an accessible public parameterless constructor.

Coverage includes admitted role spellings and `Async` twins, signature-stable overloaded handlers, and Wolverine correlation precedence: `[SagaIdentity]`; the explicit `[SagaIdentityFrom]` name replacing the full saga-name tier; the `Saga`-stripped name; `SagaId`; then case-insensitive `Id`. Public inherited fields and properties participate as runtime reflection does, while ambiguous source matches fail closed rather than producing an unstable member guess. It also covers timeout/cascade/direct-bus behavior, explicit document operations, returned-state exclusion, and exact `MarkCompleted()` evidence. Saga state is excluded at every final HTTP Query/ReadModel/Returns, direct bus, `OutgoingMessages`, persistence-return, explicit event-stream append, and DCB message/event admission boundary; mixed payloads retain their ordinary sibling messages and events. Illegal, isolated-static, primitive-return, or constructor-incompatible lifecycle shapes report `WOLVERINE0018` without invented saga topology. It deliberately does not infer storage providers, saga inserts/updates/deletes, final completion state, outbox behavior, retries, resequencing, tenancy, subscriptions, forwarding, or transport topology. `WOLVERINE0016` is report-only realization/provenance: Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow. Screenplay uses ordinary Event Modeling building blocks. This is not a language-gap request and does not require Saga syntax.

The pre-release neutral handler-subject format now uses full .NET documentation method identities for both Marten and Wolverine facts. This intentionally migrates internal graph subjects to separate overloads and converge cross-adapter identity; it does not alter generated Screenplay `.play` bytes. The repository-owned Vogen fixture pins the current package APIs; focused current and Wolverine 1 source-compatible synthetic contexts preserve positive and negative cases.

The current local canonical run passes the public sample matrix plus the repository-owned baselines. The repository-owned Vogen fixture remains an additional exact-output gate; its source hash makes Vogen composition drift visible. BankAccountES and CqrsMinimalApi hashes were deliberately updated after reviewed `[Entity]` read additions. It intentionally did not suppress upstream warnings: Reports pins vulnerable `Microsoft.OpenApi` 2.0.0 (`NU1903`), MartenWithProjectAspire pins vulnerable `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.8.1 (`NU1902`), and legacy HelpDesk targets out-of-support net7.0 and has pre-existing nullability warnings. These applications are built and statically analyzed but never started. Their immutable pins make drift visible; they are compatibility evidence, not dependency recommendations.

## Research baselines

The source architecture was reviewed at:

| Product | Commit | Source version context |
| --- | --- | --- |
| JasperFx | `da9fd17d69df5ff41940800bfb34ad4d88a88391` | `V2.53.0-1` |
| Marten | `a483b09f881f1576152aa42a27b37cc17fab252f` | `V9.28.0-10` |
| Wolverine | `af4807b5fb225ce7535c67785b74007fdad2dd9f` | `V6.29.1` |

The projection-metadata and subscription interpretation was checked directly against the pinned Marten and JasperFx commits above and against Marten `v6.3.0` / `v7.33.2` for legacy shape changes. Saga interpretation was checked directly against `JasperFx/wolverine@af4807b5fb225ce7535c67785b74007fdad2dd9f` and the current authoritative source in `HandlerDiscovery`, `HandlerGraph`, `SagaChain`, `CreateNewSagaFrame`, and `CreateMissingSagaFrame`: discovery includes concrete closed `Saga` types and named role/`Async` methods while excluding direct primitive returns; chain admission is grouped by message and treats only instance methods, static `Start`/`StartAsync`, and static `NotFound` as independent saga-chain evidence; fallback creation requires a public no-argument constructor unless a static start-only chain returns the exact saga state, and a `NotFound`-only chain reaches `CreateNewSagaFrame` when no existing calls are present. `SagaChain` resolves `[SagaIdentity]`, the optional `[SagaIdentityFrom]` name in place of `<SagaTypeName>Id`, the `Saga`-stripped name, `SagaId`, then case-insensitive `Id` across inherited public fields and properties. `TimeoutMessage` is self-scheduled, and `MarkCompleted()` only marks conditional runtime state. DCB interpretation was checked against WolverineFx 6.29.2, WolverineFx.Marten 6.29.1, and JasperFx.Events 2.53.0: `DcbModelAttribute` admits public `Load`/`LoadAsync`/`Before`/`BeforeAsync` methods returning `EventTagQuery`, `Task<EventTagQuery>`, or `ValueTask<EventTagQuery>`; `BoundaryModelAttribute` derives from it in the current Marten integration; and `IEventBoundary<T>` exposes `AppendOne(object)` plus params and `IEnumerable<object>` `AppendMany` overloads. Event alias, schema-version, naming-style, and upcast recognition was checked against local Marten 9.29 source; it is diagnostic-only, preserves authored occurrences, and deliberately excludes runtime precedence, arbitrary upcaster implementations, and legacy `EventGraph.EventMappingFor<T>().EventTypeName`. Logical tenancy recognition was checked against current Marten/JasperFx APIs plus legacy `Marten.Storage.TenancyStyle` metadata: only authored `Single`/`Conjoined`, exact document fluent calls and attributes, and exact global policy calls are retained as located `MARTEN0013` evidence. It does not infer effective tenancy, tenant ids, runtime resolution, policy expansion, callbacks, database/shard/partition topology, daemon behavior, or projection consequences. Current Marten delegates projection `Name`/`Version`, subscription filtering and start policy, and daemon mode to JasperFx types; Marten 7 used `SubscriptionName`/`SubscriptionVersion`; Marten 6 used `ProjectionName`, had no projection version, and did not yet expose first-class event subscriptions. The adapter therefore matches exact metadata names and reports authored configuration as loss rather than treating one generation's property names as universal.

On 2026-08-21, the NuGet registry listed Marten 9.29.0 and WolverineFx/WolverineFx.Marten 6.29.2 as the newest stable versions. These registry observations are drift indicators, not automatic support claims:

- <https://api.nuget.org/v3-flatcontainer/marten/index.json>
- <https://api.nuget.org/v3-flatcontainer/wolverinefx/index.json>
- <https://api.nuget.org/v3-flatcontainer/wolverinefx.marten/index.json>

## Support tiers

Use these terms consistently:

1. **Canonical** — the bundled adapter version passes the exact pinned package set and asserted behaviors in CI.
2. **Source-reviewed** — framework source/docs were analyzed and metadata names are implemented, but no exact application fixture proves the behavior.
3. **Recognized with loss** — the construct is detected and produces a stable diagnostic because Screenplay or the adapter cannot preserve it.
4. **Unknown** — package/API generation is outside canonical evidence or uses unresolved customization; generation must not guess.
5. **Unsupported** — the adapter deliberately excludes the construct and explains why.

The adapter should fail closed for a newer major version until canonical evidence exists. A newer patch or minor within a source-reviewed major may be attempted, but the result must identify the detected version and remain human-reviewed.

## Package API compatibility

Package validation compares the candidate `Cratis.CritterStack.Screenplay` public surface with the latest released baseline, `0.21.0`. Compatible additions are allowed; public removals and incompatible signature changes fail the build. The clean candidate-package consumer separately exercises the current generator constructors, source context, dependency closure, and published diagnostic-code constants before publication.

## Version provenance

Roslyn exposes assembly identities, but assembly versions do not always equal NuGet package versions. Exact package provenance belongs at the workspace boundary owned by Cratis CLI, where `project.assets.json` and the selected target framework are available.

Source-path provenance is host-owned at the same boundary. The canonical policy is version 1, `Workspace` display root, and `Ordinal` case handling. Canonical project identity is the `/`-normalized repository-relative project path without `.csproj`; document identity is project-relative, while displayed paths remain repository-relative. The host maps the exact authored `Project.Documents` trees and fails rooted, outside-root, traversing, or missing mappings. Adapters use strict project-aware ranges: generated and out-of-context trees do not become source evidence.

For compatibility with source-backed project references, Critter Stack may attach a legacy display range to a symbol only after normal semantic admission. The heuristic is provenance decoration, not discovery: it cannot originate or admit an artifact, fact, or diagnostic. It considers only declarations admitted by the shared authored-source heuristic, orders their safe relative ranges deterministically, and excludes generated filenames and headers. It never creates `SourceFileIdentity`. A fully qualified `SourceRoot` and a declaration beneath that root are mandatory; absent or relative roots and outside-root declarations produce no `Source`, with no absolute-path or basename fallback. The `Generate(Compilation, ...)` convenience overload supplies no safe root or host-owned source context and therefore omits source provenance.

CLI `v2.12.0` implements the runtime provenance report. It records:

- bundled adapter package version;
- selected target framework;
- package ID and resolved NuGet version when available;
- referenced assembly identity/version as corroboration;
- API capability fingerprints;
- matched canonical/source-reviewed support tier;
- recognition status;
- semantic conformance;
- Screenplay lowering fidelity;
- diagnostics for unknown or unsupported framework generations and unresolved provider options.

The initial capability fingerprints cover projection arity/families, lifecycle namespaces, compiled queries, subscriptions, current/legacy Wolverine handler and aggregate metadata, event-capture wrappers, DCB models, and side effects. Extend them with `DeleteEvent<T>` forms, richer event-boundary capabilities, persisted-event side-effect wrappers, and forwarding APIs as those source-profile gaps land. Package versions, assembly capabilities, recognition, semantic conformance, and Screenplay lowering fidelity remain separate dimensions.

Canonical source commits stay in this file and the pinned workflow rather than being repeated in every generated result. This reference, the CLI provenance report, and the pinned canonical workflow together form the compatibility contract.
