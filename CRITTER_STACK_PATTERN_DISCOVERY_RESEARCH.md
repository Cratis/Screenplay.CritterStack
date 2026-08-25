<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Discovering Screenplay behavior in Marten and Wolverine source

## Purpose

Marten and Wolverine do not use the Screenplay terms **State Change**, **State View**, **Automation**, and **Translation**. The adapter must derive those roles from entry-point, persistence, projection, and consequence evidence. Names alone are insufficient: the same CLR event-looking type can be persisted by Marten, cascaded locally by Wolverine, published to a broker, returned as HTTP content, or used as a projection input.

This research extends the source-first architecture in [`CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`](CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md). Version claims and exact fixture pins are kept separately in [`COMPATIBILITY.md`](COMPATIBILITY.md).

## Core conclusion

Classification must happen **after** source discovery and contextual return interpretation:

```text
entry-point + type + operation + return facts
  -> handler/projection/subscription context
  -> response/event/message/side-effect separation
  -> source and target state/stream resolution
  -> Screenplay slice candidate
  -> conflict/loss diagnostics
```

A low-level reader should never emit a Screenplay slice merely because it sees `Handle`, `Apply`, `Create`, `Store`, or an event-shaped record. It should emit facts with provenance. The resolver decides the role only after all Marten, Wolverine, HTTP, and host facts are available.

## Classification matrix

| Source behavior | Required evidence | Screenplay candidate | Do not confuse with |
| --- | --- | --- | --- |
| HTTP/message entry point appends events to its aggregate stream | Valid entry point plus aggregate-write context plus persisted event consequence | State Change | HTTP response, ordinary cascade, broker publish |
| Entry point starts a new stream | Valid entry point plus `StartStream`/`IStartStream`/`StartStream` side effect and target stream | State Change | Generated identity response |
| Entry point stores/updates/deletes an ordinary document | Valid entry point plus bound Marten document operation | State Change with document-language loss | Event-built read model |
| Snapshot/single-stream/multi-stream/EventProjection builds queryable state | Valid projection registration/base plus implemented event methods/operations | State View | Command or event producer |
| HTTP/message entry point reads and returns state | Valid entry point plus bound read and return shape | State View query | Projection registration by itself |
| Event/message/subscription triggers external work, message delivery, or side effect | Valid trigger plus side-effect/cascade/publish/send/schedule evidence and no event append target | Automation | Persisted event translation |
| Event-triggered behavior appends facts to a different stream | Valid trigger plus explicit target stream identity/type distinct from the consumed stream | Translation | Same-stream aggregate command, cascade, broker event publication |
| Saga coordinates messages over persisted process state | Saga inheritance/lifecycle/correlation evidence | Workflow loss; preserve saga facts | Event-sourced aggregate or ordinary automation |

A handler can have several consequences. Do not force a mixed handler into one role by dropping evidence. Preserve every relationship, select a role only when one interpretation dominates, and diagnose mixed or unrepresentable behavior.

## Neutral contract implications

The existing neutral contracts already separate `Command`, `Query`, `Projection`, `Reducer`, `Reaction`, `Message`, `Handler`, `Endpoint`, `Response`, and `Saga`, and distinguish `Produces`, `Consumes`, `Builds`, `Cascades`, `Publishes`, `SideEffect`, stream operations, and document operations. Keep those distinctions through resolution.

Measured gaps remain: delayed/scheduled delivery has no dedicated relationship; projection/subscription lifecycle, version, filter, and tenancy remain diagnostic-only; arbitrary non-literal side-effect flow is unresolved; and mixed behavior has no explicit classification result. Target-aware stream identities and literal projection message publication are now retained. Extend facts additively only after canonical fixtures prove the required shape. Do not work around these gaps by overloading `Produces` or inventing Screenplay syntax.

## State Change patterns

### Wolverine aggregate workflows

Strong evidence is a discovered Wolverine handler or HTTP endpoint combined with current store-agnostic attributes (`DeciderFunctionAttribute`, `WriteModelAttribute`) or legacy Marten attributes (`AggregateHandlerAttribute`, `WriteAggregateAttribute`, HTTP `AggregateAttribute`). Resolve:

- command/message and endpoint entry point;
- aggregate parameter and identity source (`<Aggregate>Id`, `[Identity]`, route/query/body binding);
- expected version and required/optional aggregate semantics;
- return decomposition after HTTP wrappers are removed;
- same-stream persisted events versus `OutgoingMessages`, `ISideEffect`, response values, and saga state.

`IEventStream<T>` changes only direct append semantics. Appends through it are persisted events; unrelated returns keep ordinary Wolverine cascade/response behavior. Current `EventsToAppend`, legacy `Wolverine.Marten.Events`, arrays/enumerables, tuples, and single event values become persisted events only inside proven aggregate-write context.

Current DCB APIs (`DcbModelAttribute`, `EventTagQuery`, `IEventBoundary<T>`, `FetchForWritingByTags<T>`, and boundary `AppendOne`/`AppendMany`) establish a tag-based consistency boundary, not an ordinary single aggregate. Preserve tags, grouping, and concurrency as distinct facts and diagnose unsupported lowering.

### Direct event-store operations

Recognize symbol-bound calls and returned storage actions:

- Marten `StartStream<T>`, `Append`, `AppendOptimistic`, `AppendExclusive`, `FetchForWriting<T>`, `WriteToAggregate<T>`;
- Wolverine legacy `IStartStream`/`IMartenOp`;
- current Wolverine `StartStream` and `AppendEvents` side effects;
- direct `IEventStream<T>.AppendOne/AppendMany`;
- direct session event operations inside an authored entry point.

An operation outside a proven entry point establishes event/stream evidence, not a command. Test/demo/generated calls are excluded or explicitly diagnosed.

### Ordinary documents and saga state

Bound Marten `Store`, `Insert`, `Update`, `Delete`, `DeleteWhere`, and Wolverine `IStorageAction<T>`/`UnitOfWork<T>` establish state mutation. They do not establish event sourcing. Saga start/update/complete methods persist process state and require saga-specific facts; do not turn the saga document into an aggregate or read model.

## State View patterns

### Aggregate and projection recipes

Marten has three lifecycles with materially different semantics:

- `Inline` — persisted in the event-capture transaction;
- `Async` — persisted later by the daemon;
- `Live` — rebuilt on demand and not persisted.

Recognize generic and instance registration through Marten `ProjectionOptions`, including inherited JasperFx `ProjectionGraph.Add(...)`. Retain `ProjectionLifecycle`, `SnapshotLifecycle`, `LiveStreamAggregation`, name, version, teardown, filters, tenancy, daemon, and subscription evidence even when Screenplay cannot express it.

Projection families:

1. **Self-aggregating snapshot** — `Snapshot<T>` or live aggregate plus implemented `Create`, `Apply`, `Evolve`, and `ShouldDelete` methods on `T`.
2. **Single stream** — a type closing current `Marten.Events.Aggregation.SingleStreamProjection<T,TId>` or legacy projection namespace variants. Consume only event types proven by implemented conventions/overrides.
3. **Multi stream** — `MultiStreamProjection<T,TId>` plus `Identity<T>`, `Identities<T>`, `FanOut<T,TChild>`, custom `IAggregateGrouper`, or replacing `IEventSlicer`. Simple identities may eventually lower; arbitrary grouping must remain a diagnostic/file reference.
4. **EventProjection** — `Create` returns a document; `Project`/`ApplyAsync` uses `IDocumentOperations`; one event can create, store, update, or delete several document types. Return documents are not events. Constructor teardown declarations corroborate output types but arbitrary method bodies still require bounded value flow.
5. **Custom projection/subscription** — raw `IProjection` or `ISubscription` processing is code-defined. Emit observer facts and exact operations; do not infer mappings absent from source.

Marten 9 removed predicate overloads of `DeleteEvent<T>(...)`, but retains unconditional constructor `DeleteEvent<T>()`; conditional deletion uses `ShouldDelete`. Analyze both current forms and keep legacy predicate forms as version-specific evidence.

### Query entry points

A Marten read (`Load`, `LoadAsync`, `Query<T>`, `FetchLatest<T>`, event query) establishes a `Reads` relationship. It becomes a Screenplay query only when a Wolverine HTTP endpoint, message request/reply handler, controller/minimal endpoint, or explicit application API exposes it.

Compiled query types implement `ICompiledQuery<TDoc,TOut>` or convenience interfaces and expose `QueryIs()`. Their public readable fields/properties are parameters unless `[MartenIgnore]`. The compiled query type is a reusable query plan, not automatically an application entry point. Link it to `QueryAsync`, batch `Query`, or `QueryByPlan` call sites, then classify the containing entry point and return shape. Generated Marten 9 handlers are corroboration only.

## Automation patterns

### Wolverine reactions

A discovered Wolverine handler becomes an automation candidate when its input is a proven event/message trigger and its consequences are messaging or side effects rather than event appends to a target stream. Preserve these as distinct relationships:

- ordinary return/tuple/collection and `OutgoingMessages` — cascade;
- `IMessageBus.SendAsync` — required-subscriber send;
- `PublishAsync` — zero-to-many publish;
- `InvokeAsync<T>` — request/reply, not cascade unless explicitly republished;
- `ScheduleAsync`, `DelayedFor`, `ScheduledAt`, `TimeoutMessage` — delayed one-shot delivery;
- concrete `ISideEffect.Execute/ExecuteAsync` — inline side effect;
- broker endpoint/raw send — transport delivery, never persisted event append.

Compound `Before`, `Load`, `Validate`, `After`, `PostProcess`, and `Finally` methods are middleware around one behavior, not independent automations.

### Marten subscriptions

`ISubscription`/`SubscriptionBase` plus `Events.Subscribe(...)` or `AddSubscriptionWithServices<T>` is strong automation evidence. `ProcessEventsAsync(EventRange, ISubscriptionController, IDocumentOperations, CancellationToken)` consumes ordered event pages in the async daemon. Capture filters, start position, name/version, archived-event policy, daemon mode, document operations, and change listeners.

A subscription that only materializes documents may be a State View candidate; one that calls external systems or sends messages is Automation. If it appends events to a different stream, it is Translation. Arbitrary subscription code without resolved consequences remains an observer with a loss diagnostic.

Aggregation projection side effects need the same split. `IEventSlice<T>.AppendEvent(...)` persists a follow-up event; `PublishMessage(...)` emits a Wolverine message. The projection remains a State View, while its append/message consequence becomes a separate Translation/Automation relationship.

## Translation patterns

Translation requires all of the following:

1. a consumed event or event-derived message;
2. an explicit persisted event append/start consequence;
3. a resolved target stream type/identity;
4. evidence that the target differs from the consumed event's source stream, or an explicit cross-stream declaration.

Strong patterns include a Wolverine/Marten event handler returning current event-store `StartStream`/`AppendEvents`, legacy `IStartStream`, direct session `StartStream<T>`/`Append`, or a Marten subscription appending through its scoped operations. Configured Marten-to-Wolverine forwarding with `SubscribeToEvent<T>().TransformedTo<TDestination>(...)` is exact persisted-event-to-message Translation, but the destination is not persisted unless another exact append proves it. A same-stream aggregate handler is a State Change, not Translation.

Never classify these as Translation:

- Wolverine cascade, send, publish, delayed message, or broker route;
- `CreationResponse`, `EmptyResponse`, `UpdatedAggregate`, HTTP body/status;
- an event-looking message contract with no Marten append;
- an `EventProjection.Create` document return;
- indirect flow where handler A cascades command B and handler B later appends an event. Preserve the two linked behaviors instead.

## Discovery and resolution order

1. Select one deployable host and its project-reference closure; reject ambiguous hosts.
2. Catalog authored symbols and generated-source corroboration separately.
3. Discover framework registrations, type roles, entry points, projections, subscriptions, and sagas.
4. Discover bound reads, writes, appends, starts, messaging calls, responses, and side effects.
5. Apply Wolverine handler discovery/ignore/customization rules.
6. Apply HTTP binding and response-wrapper semantics.
7. Apply Marten/Wolverine aggregate capture semantics.
8. Resolve stream/document identities and target streams.
9. Classify slice candidates from complete consequences.
10. Preserve conflicts and emit stable diagnostics; never use reader order as a tie-breaker.

## Evidence strength and negative rules

- **Exact** — bound invocation/attribute/interface/override and exact source range.
- **Configured** — registration, discovery policy, route, lifecycle, identity/grouping, daemon, or transport configuration.
- **Conventional** — documented framework method/type/member convention after exact handler/projection context is proven.
- **Heuristic** — placement/display naming only; never event persistence or stream ownership.

Negative evidence matters: `[WolverineIgnore]`, disabled conventional discovery, generated code, test/demo projects, internal inactive methods, comments, response wrappers, `ISideEffect`, saga inheritance, unresolved custom groupers, and absent validation/authorization enablement all prevent fabricated artifacts.

## Version implications

Version tracking is necessary because semantic APIs moved:

- Marten 6-era projection/lifecycle namespaces differ from Marten 9's JasperFx.Events split.
- Marten 7.7 introduced the lean subscription model documented today.
- Marten 9 removed predicate `DeleteEvent<T>(...)` overloads while retaining unconditional `DeleteEvent<T>()`, added compiled-query source generation, and changed custom identity extension internals.
- Wolverine 1 uses Marten-specific aggregate attributes and return wrappers.
- Wolverine 6.27+ prefers store-agnostic `WriteModel`/`ReadModel`/`DeciderFunction` while retaining legacy aliases.

Use exact canonical package sets and support tiers from [`COMPATIBILITY.md`](COMPATIBILITY.md). Package provenance should ultimately come from CLI workspace assets; assembly versions alone are insufficient.

## Current residual priorities

1. Keep compatibility/version provenance separate from semantic conformance and fail closed outside reviewed framework generations.
2. Keep arbitrary policy, listener, subscription, and non-literal projection bodies as explicit loss rather than traversing unbounded code.
3. Defer persistence-indirection diagnostics until a solution-wide authored implementation manifest can join interface members across project boundaries without guessing.
4. Resolve Translation only after source and target boundaries are proven.
5. Measure remaining target-language loss before adding Screenplay grammar.

Additional package-level compatibility evidence must use pinned public Critter Stack samples or the public HelpDesk application. Focused source-shape specs remain appropriate for bounded positive/negative cases; do not add a new synthetic canonical fixture for this increment. Every canonical check must retain deterministic bytes, compiler/print stability, and proof that no host or database starts.

## Authoritative documentation reviewed

- <https://martendb.io/events/projections/>
- <https://martendb.io/events/projections/single-stream-projections>
- <https://martendb.io/events/projections/multi-stream-projections>
- <https://martendb.io/events/projections/event-projections.html>
- <https://martendb.io/events/projections/async-daemon.html>
- <https://martendb.io/events/subscriptions>
- <https://martendb.io/documents/querying/compiled-queries>
- <https://martendb.io/documents/identity.html>
- <https://martendb.io/events/versioning>
- <https://wolverinefx.net/guide/handlers/>
- <https://wolverinefx.net/guide/handlers/discovery.html>
- <https://wolverinefx.net/guide/handlers/return-values.html>
- <https://wolverinefx.net/guide/handlers/side-effects.html>
- <https://wolverinefx.net/guide/durability/marten/event-sourcing.html>
- <https://wolverinefx.net/guide/http/marten.html>
- <https://wolverinefx.net/guide/durability/sagas>
- <https://wolverinefx.net/guide/messaging/message-bus.html>

Key source baselines include `Marten/Events/Projections/ProjectionOptions.cs`, `Marten/Events/Projections/EventProjection.cs`, `Marten/Subscriptions/ISubscription.cs`, `Marten/Linq/ICompiledQuery.cs`, `JasperFx.Events/Aggregation/JasperFxMultiStreamProjectionBase.cs`, `JasperFx.Events/Projections/ProjectionGraph.cs`, `Wolverine/Configuration/HandlerDiscovery.cs`, `Wolverine/Persistence/EventSourcing/*`, `Wolverine.Marten` forwarding/subscription code, and the Marten-specific Wolverine code-generation frames. Current Wolverine authored documentation is under `Wolverine/docs`; `Wolverine/documentation` is not present in the reviewed checkout.
