---
title: Adapter goals
description: The bounded Marten and Wolverine semantics the adapter recovers, and the evidence and diagnostic boundaries it enforces.
---

The adapter recovers bounded Marten and Wolverine semantics from authorized source and reports explicit diagnostics whenever source behavior cannot be represented faithfully. This page lists the current goals in full; the [overview](index.md) summarizes the adapter and its boundaries.

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
