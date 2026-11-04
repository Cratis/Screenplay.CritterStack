---
title: Adapter goals
description: The bounded Marten and Wolverine semantics the adapter recovers, and the evidence and diagnostic boundaries it enforces.
---

Use this reference when deciding what a Marten or Wolverine source shape can contribute to a Screenplay candidate. Screenplay.CritterStack is an optional, pre-release source adapter: it recovers bounded semantics from authorized .NET source, not a runnable replacement for the application. The [overview](index.md) explains its place in the generation pipeline and the host's responsibilities.

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
- Compound `Load*`, `Before*`, `After*`, `PostProcess*`, `Finally*`, and after-commit stages, with exact outgoing-message consequences retained on the owning entry point and explicit `WOLVERINE0020` loss.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Command-to-event `produces` links for unconditional `session.Events.StartStream(id, new TEvent(...))` / `session.Events.Append(id, new TEvent(...))` writes and for events an `[AggregateHandler]` returns or yields, with the command member that identifies the stream recorded on the link and marked as the command identifier. A conditional write, a stream identity that is not a command property, or an indirect payload keeps the handler reference and reports `WOLVERINE0025` naming the command, the event, and the reason. Produced events are placed with their command, and an HTTP endpoint that only forwards its command to a Wolverine handler defers to that handler's source file.
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
- Authored enums used by recovered artifacts emitted as enumeration concepts. Flags enums report `CRITTERSTACK0001` rather than pretending a combinable bit field is a closed set of choices. Enums with no values, or with values that collide after Screenplay naming, report `CRITTERSTACK0002`; an enum whose concept name is also required by another type (another enum, a Vogen concept or a Marten value type), or is printed for a different type without a concept (a generated, flags or package enum, a record, or a value object without its adapter), reports `CRITTERSTACK0003` and only the enum gives way. A field whose type has no emitted concept keeps its plain type name instead of dropping the artifact that uses it.
- Source placement that never aborts on a flat feature namespace: when one namespace or folder would hold artifacts of more than one slice kind, each command, aggregate, or read model gets its own slice and `CRITTERSTACK0004` explains how to choose the slices through folders, namespaces, or the skip-segments, feature-root, and module options.

### Output and review

- Deterministic output without starting the application or connecting to PostgreSQL.
- Explicit diagnostics whenever source behavior cannot be represented faithfully.

`CritterStackScreenplayGenerator.Generate` composes source contributions and passes them to Screenplay.Generation. Review the candidate and its diagnostics together: readable model source does not turn an unsupported lifecycle or unresolved source flow into represented behavior. To investigate a missing shape, follow the [adapter extension guide](guides/extend-source-adapter.md): choose the owning framework reader, add a focused authored-source scenario, and check the recovered relationships and loss diagnostics before updating a compatibility expectation. Compatibility expectations apply only to the package/sample versions they exercise; they are not a general support promise.

## Declared Wolverine slice patterns

The exact `Wolverine.Persistence.EventSourcing.SlicePatternAttribute` constructor accepts `SlicePattern`. Its mappings are explicit:

| Declaration | Screenplay slice kind |
| --- | --- |
| `Command` | `StateChange` |
| `View` | `StateView` |
| `Automation` | `Automation` |
| `Translation` | `Translate` |

Proven HTTP and `TimeoutMessage` scheduler triggers take precedence (HTTP GET and QUERY are read triggers, and other verbs are state-change triggers except HEAD and OPTIONS, which keep their full effect analysis but prove no slice kind), followed by the method declaration (including overridden-method metadata), the nearest handler-type declaration, and existing inference. Message types and unrelated enclosing types do not supply this metadata. Generated attributes and ignored or undiscovered handlers remain excluded. This does not add gRPC, recurring-schedule, or external-listener discovery.

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
