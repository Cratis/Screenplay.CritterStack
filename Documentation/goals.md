---
title: Adapter goals
description: The bounded Marten and Wolverine semantics the adapter recovers, and the evidence and diagnostic boundaries it enforces.
---

Use this reference when deciding what a Marten or Wolverine source shape can contribute to a Screenplay candidate. Screenplay.CritterStack is an optional source adapter: it recovers bounded semantics from authorized .NET source, not a runnable replacement for the application. The [overview](index.md) explains its place in the generation pipeline and the host's responsibilities.

Recovery has more than one outcome. An admitted source shape can contribute model facts, retain configuration or provenance as diagnostic evidence, or require a separately composed adapter. Keep those outcomes distinct when reviewing the candidate; a compiler-checked definition is not proof that the application's business behavior has been reproduced.

## Goals

### Marten facts and configuration

Start with the store, documents and projections. Then check whether a configuration entry becomes a model fact or is retained only as diagnostic evidence. A tenancy declaration can be located without establishing the effective tenant at runtime; the entries below keep that distinction beside the relevant source shape.

- Marten-only event stores, documents, aggregates, projections, and queries.
- Generic and instance-based Marten projection registrations, including `Snapshot<T,TId>` and `LiveStreamAggregation<T,TId>` with lifecycle/configuration arguments. The first type argument is the model; the identity type is not a second model. Authored projection name/version evidence is retained, with explicit diagnostics for unsupported async/live lifecycle semantics.
- Async daemon mode and first-class subscription registration/configuration evidence without inventing state views, automations, translations, events, messages, or document consequences from arbitrary processing code.
- Marten document identities from exact configuration, identity attributes, and conventions, without guessing unresolved expressions.
- Authored Marten event/document tenancy declarations, attributes, and global policies retained as located `MARTEN0013` diagnostic evidence without inferring effective state, runtime tenant resolution, or database topology.
- Authored Marten event aliases, schema-version helpers, naming style, and current upcast registrations retained as `MARTEN0011`/`MARTEN0012` diagnostic evidence without renaming or originating events or inferring upcast behavior.
- Marten compiled-query execution linked to proven Wolverine HTTP query entry points, including public plan parameters; unresolved nested executable flow reports `MARTEN0006` instead of guessing.
- Literal projection `PublishMessage(new TMessage(...))` side effects retained as Message/`Publishes` evidence. `MARTEN0015` reports unresolved payload flow (`Unknown`) or exact `Wolverine.ISendMyself` payloads (`Unsupported`): custom `ApplyAsync` sending is not interpreted, and delivery wrappers are not invented as published domain messages. Built-in `TimeoutMessage` descendants retain their existing message evidence.
- Event wire configuration (`UseBinarySerializer<T>`, append mode, stream identity) and `RegisterValueType` concept nomination retained without fabricating event or concept representations.

### Wolverine handlers and consequences

Follow an admitted endpoint or handler to the consequences its source proves. A stored document, an appended event and an outgoing message are different results; do not read a handler's slice classification as proof that every consequence is a persisted event. Configuration hooks and unresolved flow keep their stated diagnostic boundaries.

- Marten + Wolverine HTTP and message handlers, including signature-stable overloaded handler identities and batched `T[]` message delivery.
- Returned `IStorageAction<T>` / `UnitOfWork<T>` persistence, exact per-slot storage-factory refinement, and `[Entity]` / `[FirstOrDefault]` / `[Queryable]` bound reads.
- Presence diagnostics for Wolverine/Marten convention-alteration hooks, per-chain `Configure(HandlerChain)`, and Marten session listeners without interpreting policy or listener bodies.
- Compound `Load*`, `Before*`, `After*`, `PostProcess*`, `Finally*`, and after-commit stages, with exact outgoing-message consequences retained on the owning entry point and explicit `WOLVERINE0020` loss. `OnException`/`OnExceptionAsync` hooks that take the handled message and an exception are failure stages. `WOLVERINE0026` names the owning entry point, exception type and proven failure-only messages. Those messages do not create Cascades relationships or failure-only Message artifacts: the generation model has no failure branch, so lowering them would imply success-path behavior. Failure-only storage, appends and production are also excluded from success facts.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Command-to-event `produces` links for unconditional `session.Events.StartStream(id, new TEvent(...))` / `session.Events.Append(id, new TEvent(...))` writes and for events an `[AggregateHandler]` returns or yields, with the command member that identifies the stream recorded on the link and marked as the command identifier. A conditional write, a stream identity that is not a command property, or an indirect payload keeps the handler reference and reports `WOLVERINE0025` naming the command, the event, and the reason. `WOLVERINE0025` is a Warning when production evidence is omitted or unresolved (conditional or compound-validated writes, unresolved stream identity or payload, several streams, event collections), and Information when the handler reference accurately represents a known structural pattern (`IEventStream<T>` parameter appends and imperative DCB boundary appends). Produced events are placed with their command, and an HTTP endpoint that only forwards its command to a Wolverine handler defers to that handler's source file.
- Target-aware exact current and legacy `IEventStream<T>` appends across multiple handler parameters, including commandless HTTP and metadata-only loaded streams, with per-binding identities and explicit diagnostics instead of first-stream guesses.
- Bounded current and legacy Wolverine DCB evidence from authored `[DcbModel]` / `[BoundaryModel]` parameters, direct `EventTagQuery` fluent chains, exact boundary appends, and safe declarative returns, with `WOLVERINE0014`/`WOLVERINE0015` instead of invented stream topology.
- Markerless event/message discovery from actual framework usage.

#### Saga discovery and diagnostic-only lifecycle

Saga recovery starts with public, concrete, closed `Wolverine.Saga` state. The adapter groups admitted handlers by message using Wolverine-compatible `SagaChain` admission. It preserves admitted role spellings and `Async` twins, constructor/returned-state creation constraints, collision-safe handler identities, exact correlation precedence (including inherited public members), cascades, timeouts, direct bus calls, and exact `MarkCompleted()` evidence. Saga state is excluded at every final HTTP query, message, and event admission boundary.

`WOLVERINE0016` is a report-only realization/provenance diagnostic: Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow.

`WOLVERINE0017` reports runtime-resolved correlation, while `WOLVERINE0018` reports rejected lifecycle shapes without inventing persistence or transport topology.

Screenplay uses ordinary Event Modeling building blocks; this is not a language-gap request, and generated `.play` bytes remain unchanged.

### Shared types and source placement

Some recovered types come from adapter composition, while source placement decides where admitted artifacts appear. Review those choices separately from the Marten and Wolverine behavior above; a concept or a folder choice is not additional evidence of runtime behavior.

- Vogen concepts, primitive representations, authored validation hooks, nullable usages, and explicit loss diagnostics through the separately composed `Cratis.Screenplay.Generation.DotNet.Vogen` adapter.
- Authored enums used by recovered artifacts emitted as enumeration concepts. Member numbers are preserved: implicit or explicit `0, 1, 2, …` numbering stays positional and prints names only, while any other distinct numbering that fits a 32-bit signed integer (sparse, reordered, negative, or following an explicit assignment) prints `name = number` for every member in declaration order. Numbering that cannot be represented (two members sharing a number, or a number outside the 32-bit signed range) reports `CRITTERSTACK0005` (Warning/Unsupported) at the enum, which is then not declared as a concept; its uses keep the plain type name. Flags enums report `CRITTERSTACK0001` rather than pretending a combinable bit field is a closed set of choices. Enums with no values, or with values that collide after Screenplay naming, report `CRITTERSTACK0002`; an enum whose concept name is also required by another type (another enum, a Vogen concept or a Marten value type), or is printed for a different type without a concept (a generated, flags or package enum, a record, or a value object without its adapter), reports `CRITTERSTACK0003` and only the enum gives way. A field whose type has no emitted concept keeps its plain type name instead of dropping the artifact that uses it.
- Source placement that never aborts on a flat feature namespace: when one namespace or folder would hold artifacts of more than one slice kind, each command, aggregate, or read model gets its own slice and `CRITTERSTACK0004` explains how to choose the slices through folders, namespaces, or the skip-segments, feature-root, and module options.

### Output and review

- Deterministic output without starting the application or connecting to PostgreSQL.
- Explicit diagnostics whenever source behavior cannot be represented faithfully.

`CritterStackScreenplayGenerator.Generate` composes source contributions and passes them to Screenplay.Generation. Review the candidate and its diagnostics together: readable model source does not turn an unsupported lifecycle or unresolved source flow into represented behavior. To investigate a missing shape, follow the [adapter extension guide](guides/extend-source-adapter.md): choose the owning framework reader, add a focused authored-source scenario, and check the recovered relationships and loss diagnostics before updating a compatibility expectation. Compatibility expectations apply only to the package/sample versions they exercise; they are not a general support promise.

## Conventions and diagnostics

The conventions below are recovered from authored source and symbol-bound framework APIs. Matching a method or attribute name alone does not establish framework behavior. The repository pins Marten 9.62.0 and Wolverine 6.64.2; the compatibility fixtures also exercise selected older APIs. These pins do not promise support for every version or configuration.

### Marten projection and store boundaries

Conventional `Apply`, `Create` and `ShouldDelete` discovery is supplemented by parameterless `DeleteEvent<TEvent>()` registrations in authored projection constructors. Direct, `this`/`base` and supported fluent registrations contribute event/Consumes facts. Calls on another instance, lambda-taking overloads, deferred or local-function calls, and calls outside constructors do not qualify. A recognized registration does not prove that it executes unconditionally.

Genuine overrides of `Evolve`, `EvolveAsync`, `DetermineAction` and `DetermineActionAsync` are checked through the override chain to `JasperFxAggregationProjectionBase<TDoc,TId,TOperations,TQuerySession>`. At the pinned API these dispatch methods consume untyped events, so they produce located `MARTEN0010` evidence instead of inferred event types. A typed `Evolve(IEvent<T>)` overload is not a dispatch override; neither are same-name helpers or hiding members.

Composite and flat-table registrations remain opaque. `MARTEN0018` and `MARTEN0019` report them at the registration site, with composite constituent types when directly resolvable. The registration itself contributes no artifacts, relationships or subprojection ordering. Independently discovered conventional projections retain their facts.

Proven Marten backend ownership distinguishes store-owned artifact occurrences and relationship endpoints without changing their .NET type or method subjects. A store is identified by its registration's mapped project, file and source span—not its marker interface or event-model name. Distinct registrations remain distinct even when their names are equal or null. A single unnamed default `AddMarten` retains legacy unscoped output. Ancillary, named or multiple-store configurations use explicit backend keys.

Projection registrations bind to their exact `StoreOptions` receiver. `EventModelName` accepts null or a nonblank, control-free constant string, including constant concatenation and `nameof`; dynamic, conditional or unbound assignments do not establish ownership. Cross-project marker references resolve to the defining project. Method-level Wolverine `[MartenStore]` or `[Storage]` selection takes precedence over inherited type-level selection. Supported namespace and assembly ancillary-storage policies use source order only inside one proven configuration context. Conflicting or unproved ownership produces Generation SDK backend-ownership diagnostics and omits the affected store-owned occurrence rather than emitting an unscoped substitute.

An explicit ancillary session scopes operations on that session, not the handler's transaction chain. Persistence-backed handlers, commands, events, streams, documents and read targets carry proven ownership. HTTP Query identity, reusable payload concepts and generic outgoing messages stay neutral; their backend-bound relationship targets remain scoped. Native Wolverine sagas remain neutral unless attributes or routing prove a Marten backend. Backend keys are not rendered model or module names, runtime store selection, tenancy or a store-to-module mapping. The legacy `MARTEN0020` code is reserved and is no longer emitted for represented ownership.

### Wolverine reads, consistency and returned operations

`Wolverine.Persistence.EventSourcing.ReadModelAttribute`, legacy `Wolverine.Marten.ReadAggregateAttribute` and admitted derived attributes contribute Aggregate and Reads facts at the parameter attribute. Two parameters reading the same model keep distinct read evidence. Read parameters are excluded from request and writable-aggregate inference: a read-only handler follows the non-persisting Reaction path, even when its type name ends in `AggregateHandler`. Reading alone never proves returned-event persistence. A mixed read/write handler keeps its writable path and separate reference reads.

Current and legacy consistency inheritance is retained, including HTTP and derived `ConsistentAggregate` forms. Always-enforced consistency defaults are recognized. `WOLVERINE0003` reports the unrepresented consistency semantics; explicit `VersionSource` evidence remains located at the command member. Plain `ConsistentAggregate` parameters and class/method `ConsistentAggregateHandler` declarations receive Information/Unsupported diagnostics for their consistency-only behavior. An explicit `AlwaysEnforceConsistency = false` suppresses that consistency-only diagnostic, not independently applicable diagnostics.

An authored `JasperFx.Events.Aggregation.BoundaryAggregateAttribute` on the type of an otherwise unrecognized handler parameter produces `WOLVERINE0014` (Information/Unsupported), located at the marker and deduplicated by that source location. The marker alone creates no Aggregate, Reads, Appends or Produces facts and establishes no stream identity. Recognized DCB, read-model and writable-aggregate workflows retain their existing handling; same-name unrelated attributes and generated markers do not qualify.

Each authored property-level `JasperFx.Events.Aggregation.NaturalKeyAttribute` on an already recognized aggregate produces one `WOLVERINE0030` diagnostic, deduplicated across handlers. Existing surrogate-id handling stays intact; a natural key does not establish a stream identity.

Returned, symbol-bound `Wolverine.Marten.MartenOps` factories refine generic `IMartenOp` side effects as follows:

| Factory | Recovered consequence |
| --- | --- |
| `Store`, `Insert` | Stores; there is no distinct insert relationship. |
| `Update` | Updates. |
| `Delete` | Deletes. |
| `StoreMany` | Stores with collection evidence. |
| `StartStream` | Aggregate and StartsStream evidence; existing event discovery handles inline payloads. |

Classification is per return slot, including task/value-task unwrapping, direct tuples, `Task.FromResult` tuples and unchanged local factory results. Every return path for a slot must resolve to the same factory and model type. Tenant overloads use the generic model type, not the tenant argument, and do not introduce tenant scope. `StoreObjects`, `DeleteWhere`, custom factories, mutated or unresolved values, and interprocedural factory following are not refined. Operation wrappers never become cascading domain messages. StartsStream is a Generation relationship, not new executable Screenplay syntax.

### Opt-in authored persistence abstractions

Set `CritterStackScreenplayOptions.FollowAuthoredAbstractions` to `true` when the host supplies the selected projects' compilations and authored source locations and needs to follow persistence behind repository or helper calls. The option defaults to `false`. Leaving it off preserves direct framework recognition without following those abstractions or issuing `WOLVERINE0029` for them. This is selected-source analysis, not dependency-injection container execution or runtime plugin discovery.

With the option enabled, authored helpers and interface, abstract or virtual dispatch can contribute persistence evidence only when the complete visible implementation set is available and every implementation agrees on the event, stream and conditional consequences. Exact closed invariant generic repository dispatch is supported, including unconstrained generic implementations constructed from the contract arguments. Same-name unrelated interfaces and overloads are not combined. Call-site, implementation-declaration, nested-call and append locations remain attached to successful production evidence.

`WOLVERINE0029` (`AuthoredAbstractionUnresolved`, Warning/Unknown) names and locates an unresolved call. Missing, generated or external bodies, unselected implementation scopes, unavailable call or implementation locations, conflicting alternatives, unsupported argument binding, recursion and traversal limits prevent inference. An implementation that does not persist cannot be ignored when another implementation does. Traversal is limited to eight authored calls in depth and a shared budget of 128 implementation expansions per handler. A failed traversal discards all partial authored production for that handler; direct framework evidence is unchanged.

Generic methods, open/variant/constrained generic dispatch, reduced extension methods, ref/out arguments and reassigned forwarded values are not guessed. Local aliases do not establish stream identity. An external dependency referencing a selected contract makes dispatch incomplete even if a selected implementation is visible. Conditional calls or unproven stream identity retain the existing `WOLVERINE0025` and imperative handler-reference behavior rather than becoming unconditional declarative production. The pinned sample coverage does not establish general support for application-specific repository abstractions; enable the option only with review of the resulting candidate and diagnostics.

### Middleware, forwarding and response policies

Public methods marked `[WolverineBefore]`, `[WolverineAfter]` or `[WolverineFinally]` can use arbitrary names. Finally retains after-stage treatment. A method matching both a name convention and an attribute is included once.

Authored `[Middleware(typeof(...))]` on handler/endpoint classes or methods, direct unfiltered `WolverineOptions.Policies.AddMiddleware` registrations, and `WolverineHttpOptions.AddMiddleware` registrations can resolve authored middleware types. `MiddlewareScoping.All`, `Handlers` and `HttpEndpoints` keep the two chain kinds separate. Resolved stages retain `WOLVERINE0020` at the applying attribute or registration: ordering, short-circuit and transaction behavior remain unrepresented. A before/load stage returning `HandlerContinuation`, `RequirementResult` or an HTTP response prevents an unconditional produces assertion.

Runtime filters, conditional registrations, runtime-selected types, and null or partly resolved attribute type lists are not applied. They report `WOLVERINE0007` rather than guessed consequences. Unrelated `AddMiddleware` methods and name-only lookalike attributes are ignored.

The adapter detects `PublishEventsToWolverine`, `ProcessEventsWithWolverineHandlersInStrictOrder`, `SubscribeToEvents`, `SubscribeToEvent<T>().TransformedTo<TMessage>()` and `UseFastEventForwarding` on recognized Wolverine/Marten owners. `WOLVERINE0027` reports the source location and resolvable types, but creates no Reaction, Consumer, Consumes or Cascades facts for forwarding. A bare `SubscribeToEvent<T>()` reports an incomplete transformation without claiming forwarding is enabled. Literal `UseFastEventForwarding = false` is silent. The HelpDesk 1.x spelling `EventForwardingToWolverine` is not recognized; the presence of that sample does not establish forwarding coverage.

`[DeduplicatedWithResponse]`, `[AlwaysPublishResponse]` and `[Topic]` each produce `WOLVERINE0028` at the attribute. They add no response replay, deduplication, publication or routing execution facts.

### Diagnostic reference

These entries describe the cases above, not every use of a diagnostic code. Review the source location and message as well as the code. In particular, detecting an omitted policy or store boundary does not mean it has been recovered as executable behavior.

| Code | Name | Severity / outcome for these cases |
| --- | --- | --- |
| `MARTEN0010` | `CustomProcessingOmitted` | Information / Unsupported for genuine untyped evolution/dispatch overrides. |
| `MARTEN0018` | `CompositeProjectionOmitted` | Warning / Unsupported. |
| `MARTEN0019` | `FlatTableProjectionOmitted` | Warning / Unsupported. |
| `MARTEN0020` | `StoreScopeNotRepresented` | Reserved legacy code; represented ownership uses backend keys, and unknown or conflicting ownership uses Generation SDK diagnostics. |
| `WOLVERINE0003` | `StreamVersionOmitted` | Information / Unsupported for consistency-only declarations. |
| `WOLVERINE0007` | `HandlerDiscoveryConfigurationUnresolved` | Warning / Unknown for unresolved middleware application. |
| `WOLVERINE0014` | `DcbBoundaryOmitted` | Information / Unsupported for a standalone BoundaryAggregate marker; the marker alone contributes no aggregate workflow facts. |
| `WOLVERINE0020` | `CompoundStageOmitted` | Information / Unsupported for resolved compound stages. |
| `WOLVERINE0026` | `FailureStageConditionOmitted` | Warning / Unsupported; failure-only messages remain diagnostic-only. |
| `WOLVERINE0027` | `EventForwardingOmitted` | Warning / Unsupported. |
| `WOLVERINE0028` | `EndpointIdempotencyOmitted` | Warning / Unsupported. |
| `WOLVERINE0029` | `AuthoredAbstractionUnresolved` | Warning / Unknown for unresolved authored traversal when explicitly enabled. |
| `WOLVERINE0030` | `NaturalKeyIdentityOmitted` | Information / Unsupported, located at the property attribute. |
| `CRITTERSTACK0005` | `EnumNumbersNotPreserved` | Warning / Unsupported; the enum has aliased or out-of-int32 numbering, is not declared as a concept, and its uses keep the plain type name. |

## Declared Wolverine slice patterns

The exact `Wolverine.Persistence.EventSourcing.SlicePatternAttribute` constructor accepts `SlicePattern`. Its mappings are explicit:

| Declaration | Screenplay slice kind |
| --- | --- |
| `Command` | `StateChange` |
| `View` | `StateView` |
| `Automation` | `Automation` |
| `Translation` | `Translate` |

Proven HTTP and `TimeoutMessage` scheduler triggers take precedence (HTTP GET and QUERY are read triggers, and other verbs are state-change triggers except HEAD and OPTIONS, which keep their full effect analysis but prove no slice kind), followed by the method declaration (including overridden-method metadata), the nearest handler-type declaration, and existing inference. An admitted non-saga `TimeoutMessage` handler without any declaration is therefore Automation, together with the events it produces. Message types and unrelated enclosing types do not supply this metadata. Generated attributes and ignored or undiscovered handlers remain excluded. This does not add gRPC, recurring-schedule, or external-listener discovery.

Classification changes placement only. An automation-classified writer remains a Command with its Reads, Produces, and Appends relationships; a command-classified cascade does not become a persisted event. Produced events receive the same slice kind. Placement explanations preserve exact classification evidence separately from heuristic folder placement. Reaction classification does not add reaction lowering.

| Diagnostic | Boundary |
| --- | --- |
| `WOLVERINE0022` | Unresolved constants (`Unknown`) or undefined enum values (`Unsupported`); independent inference survives. |
| `WOLVERINE0023` | Declaration conflicts with a proven trigger (`Conflict`); the trigger wins. Method-over-type precedence is not a conflict. |
| `WOLVERINE0024` | An admitted declaration cannot obtain a supported artifact placement (`Unsupported`); no handler behavior is invented. |

Conflicting classifications from different handlers on shared artifacts still fail closed through the existing `DOTNETSP0013`, `GEN0008`, and `GEN0007` placement/lowering diagnostics.

## Batch-fetched event streams

The adapter recognizes the Guid and string overloads of `IEventStoreOperations.FetchManyForWriting<T>`, including calls through the inheriting `Marten.Events.IEventStoreOperations`. It requires the exact `Task<IReadOnlyList<IEventStream<T>>>` return shape and a local initialized by awaiting the call, optionally through `Task<T>.ConfigureAwait(bool)`. Optional and named cancellation arguments are supported.

- Inline arrays and collection expressions produce one Reads binding per fixed slot. Constant result indexing, a local initialized directly from an indexed result, and direct appends inside `foreach` retain per-slot identities and distinct Appends discriminators.
- Runtime ID collections iterated with `foreach` produce quantified Reads/Appends families (`IsCollection=true`), not an invented stream count. A direct request-member identity is retained as `SourceMember`.
- Produces is emitted once per command/event type even when several slots append that type. Fetches without appends retain reads. Batch IDs are never marked as global command identifiers.
- Statically empty arrays, collection expressions, and `Array.Empty<T>()` establish no stream instances. Repeated authored identity expressions remain distinct read evidence but are not proven distinct; append targets are not inferred. The diagnostic states the framework's duplicate-ID rejection rule without claiming to execute it. Missing streams are not simulated or validated by source analysis.
- Reassigned or ref-aliased locals, locals escaping into arbitrary aliases, helper arguments, extension-method receivers, or captured callbacks, fields, helper-returned streams, LINQ pipelines, dynamic indices, runtime-family indexing, and non-append member access are outside the supported target shapes. Unresolved targets or opaque payloads keep independently proven reads but omit unproven append/event consequences.

`WOLVERINE0012` locates per-binding loading/version loss at the fetch site and explains unknown runtime cardinality and quantified identity correspondence where applicable. Batch optimistic checks are not one global concurrency rule. `WOLVERINE0013` locates each unsupported exact append. `WOLVERINE0003` remains reserved for observed version/consistency metadata; this API has no expected-version argument.

Fetched bindings do not alter aggregate-return event classification or an independently established aggregate identifier. A single stream parameter keeps its own identifier, return conventions, and primary state-feature placement even alongside fetched streams: only the fetches receive batch-loading `WOLVERINE0012` diagnostics, not a spurious multiple-parameter warning. Multiple stream parameters still receive the parameter-specific loading diagnostic. Direct `FetchForWriting` invocation analysis and speculative collection-parameter handler injection are not supported.
