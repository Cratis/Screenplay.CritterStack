---
title: Adapter goals
description: The bounded Marten and Wolverine semantics the adapter recovers, and the evidence and diagnostic boundaries it enforces.
---

The adapter recovers bounded Marten and Wolverine semantics from authorized source and reports explicit diagnostics whenever source behavior cannot be represented faithfully. This page lists the current goals in full; the [overview](index.md) summarizes the adapter and its boundaries.

## Goals

- Marten-only event stores, documents, aggregates, projections, and queries.
- Generic and instance-based Marten projection registrations, including `Snapshot<T,TId>` and `LiveStreamAggregation<T,TId>` with lifecycle/configuration arguments. The first type argument is the model; the identity type is not a second model. Authored projection name/version evidence is retained, with explicit diagnostics for unsupported async/live lifecycle semantics.
- Async daemon mode and first-class subscription registration/configuration evidence without inventing state views, automations, translations, events, messages, or document consequences from arbitrary processing code.
- Marten document identities from exact configuration, identity attributes, and conventions, without guessing unresolved expressions.
- Authored Marten event/document tenancy declarations, attributes, and global policies retained as located `MARTEN0013` diagnostic evidence without inferring effective state, runtime tenant resolution, or database topology.
- Authored Marten event aliases, schema-version helpers, naming style, and current upcast registrations retained as `MARTEN0011`/`MARTEN0012` diagnostic evidence without renaming or originating events or inferring upcast behavior.
- Marten compiled-query execution linked to proven Wolverine HTTP query entry points, including public plan parameters; unresolved nested executable flow reports `MARTEN0006` instead of guessing.
- Marten + Wolverine HTTP and message handlers, including signature-stable overloaded handler identities and batched `T[]` message delivery.
- Returned `IStorageAction<T>` / `UnitOfWork<T>` persistence, exact per-slot storage-factory refinement, and `[Entity]` / `[FirstOrDefault]` / `[Queryable]` bound reads.
- Presence diagnostics for Wolverine/Marten convention-alteration hooks, per-chain `Configure(HandlerChain)`, and Marten session listeners without interpreting policy or listener bodies.
- Compound `Load*`, `Before*`, `After*`, `PostProcess*`, `Finally*`, and after-commit stages, with exact outgoing-message consequences retained on the owning entry point and explicit `WOLVERINE0020` loss.
- Literal projection `PublishMessage(new TMessage(...))` side effects retained as Message/`Publishes` evidence. `MARTEN0015` reports unresolved payload flow (`Unknown`) or exact `Wolverine.ISendMyself` payloads (`Unsupported`): custom `ApplyAsync` sending is not interpreted, and delivery wrappers are not invented as published domain messages.
- Event wire configuration (`UseBinarySerializer<T>`, append mode, stream identity) and `RegisterValueType` concept nomination retained without fabricating event or concept representations.
- Vogen concepts, primitive representations, authored validation hooks, nullable usages, and explicit loss diagnostics through the separately composed `Cratis.Screenplay.Generation.DotNet.Vogen` adapter.
- Authored enums used by recovered artifacts emitted as enumeration concepts. Flags enums report `CRITTERSTACK0001` rather than pretending a combinable bit field is a closed set of choices.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Target-aware exact current and legacy `IEventStream<T>` appends across multiple handler parameters, including commandless HTTP and metadata-only loaded streams, with per-binding identities and explicit diagnostics instead of first-stream guesses.
- Bounded current and legacy Wolverine DCB evidence from authored `[DcbModel]` / `[BoundaryModel]` parameters, direct `EventTagQuery` fluent chains, exact boundary appends, and safe declarative returns, with `WOLVERINE0014`/`WOLVERINE0015` instead of invented stream topology.
- Bounded authored Wolverine saga discovery for public concrete closed `Wolverine.Saga` state, grouped by message with Wolverine-compatible `SagaChain` admission. It preserves admitted role spellings and `Async` twins, constructor/returned-state creation constraints, collision-safe handler identities, exact correlation precedence (including inherited public members), cascades, timeouts, direct bus calls, and exact `MarkCompleted()` evidence. Saga state is excluded at every final HTTP query, message, and event admission boundary. `WOLVERINE0016` is a report-only realization/provenance diagnostic: Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow. Screenplay uses ordinary Event Modeling building blocks; this is not a language-gap request, and generated `.play` bytes remain unchanged. `WOLVERINE0017` reports runtime-resolved correlation, while `WOLVERINE0018` reports rejected lifecycle shapes without inventing persistence or transport topology.
- Markerless event/message discovery from actual framework usage.
- Deterministic output without starting the application or connecting to PostgreSQL.
- Explicit diagnostics whenever source behavior cannot be represented faithfully.

## Declared Wolverine slice patterns

The exact `Wolverine.Persistence.EventSourcing.SlicePatternAttribute` constructor accepts `JasperFx.Events.EventModeling.SlicePattern`. Its mappings are explicit:

| Declaration | Screenplay slice kind |
| --- | --- |
| `Command` | `StateChange` |
| `View` | `StateView` |
| `Automation` | `Automation` |
| `Translation` | `Translate` |

Proven HTTP and `TimeoutMessage` scheduler triggers take precedence, followed by the method declaration (including overridden-method metadata), the nearest handler-type declaration, and existing inference. Message types and unrelated enclosing types do not supply this metadata. Generated attributes and ignored or undiscovered handlers remain excluded. This does not add gRPC, recurring-schedule, or external-listener discovery.

Classification changes placement only. An automation-classified writer remains a Command with its Reads, Produces, and Appends relationships; a command-classified cascade does not become a persisted event. Produced events receive the same slice kind. Placement explanations preserve exact classification evidence separately from heuristic folder placement. Reaction classification does not add reaction lowering.

| Diagnostic | Boundary |
| --- | --- |
| `WOLVERINE0022` | Unresolved constants (`Unknown`) or undefined enum values (`Unsupported`); independent inference survives. |
| `WOLVERINE0023` | Declaration conflicts with a proven trigger (`Conflict`); the trigger wins. Method-over-type precedence is not a conflict. |
| `WOLVERINE0024` | An admitted declaration cannot obtain a supported artifact placement (`Unsupported`); no handler behavior is invented. |

Conflicting classifications from different handlers on shared artifacts still fail closed through the existing `DOTNETSP0013`, `GEN0008`, and `GEN0007` placement/lowering diagnostics.

## Batch-fetched event streams

The adapter recognizes the Guid and string overloads of `JasperFx.Events.IEventStoreOperations.FetchManyForWriting<T>`, including calls through the inheriting `Marten.Events.IEventStoreOperations`. It requires the exact `Task<IReadOnlyList<IEventStream<T>>>` return shape and a local initialized by awaiting the call, optionally through `Task<T>.ConfigureAwait(bool)`. Optional and named cancellation arguments are supported.

- Inline arrays and collection expressions produce one Reads binding per fixed slot. Constant result indexing, a local initialized directly from an indexed result, and direct appends inside `foreach` retain per-slot identities and distinct Appends discriminators.
- Runtime ID collections iterated with `foreach` produce quantified Reads/Appends families (`IsCollection=true`), not an invented stream count. A direct request-member identity is retained as `SourceMember`.
- Produces is emitted once per command/event type even when several slots append that type. Fetches without appends retain reads. Batch IDs are never marked as global command identifiers.
- Statically empty arrays, collection expressions, and `Array.Empty<T>()` establish no stream instances. Repeated authored identity expressions remain distinct read evidence but are not proven distinct; append targets are not inferred. The diagnostic states the framework's duplicate-ID rejection rule without claiming to execute it. Missing streams are not simulated or validated by source analysis.
- Reassigned or ref-aliased locals, locals escaping directly into arbitrary aliases or helper arguments, fields, helper-returned streams, LINQ pipelines, dynamic indices, runtime-family indexing, and nested callbacks are outside the supported target shapes. Unresolved targets or opaque payloads keep independently proven reads but omit unproven append/event consequences.

`WOLVERINE0012` locates per-binding loading/version loss at the fetch site and explains unknown runtime cardinality and quantified identity correspondence where applicable. Batch optimistic checks are not one global concurrency rule. `WOLVERINE0013` locates each unsupported exact append. `WOLVERINE0003` remains reserved for observed version/consistency metadata; this API has no expected-version argument.

Singular stream handler parameters keep their existing behavior. Direct `FetchForWriting` invocation analysis and speculative collection-parameter handler injection are not supported.
