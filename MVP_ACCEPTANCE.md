<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Preview acceptance criteria (completed historical gate)

## Purpose

This document records the acceptance gate used for the original **0.1 preview**. It is historical evidence, not the current continuation plan, and never promised complete Marten/Wolverine behavioral parity.

The preview is acceptable when it safely produces a useful, compiling Screenplay from representative real applications, identifies every important unsupported semantic through diagnostics, and is available through the ordinary Cratis CLI distribution.

## Required before calling the preview usable

### Distribution

- [x] `Cratis.Screenplay.Generation.Contracts` 0.1.0 is available from nuget.org.
- [x] `Cratis.Screenplay.Generation` 0.1.0 is available from nuget.org.
- [x] `Cratis.Screenplay.Generation.DotNet` 0.1.0 is available from nuget.org.
- [x] `Cratis.CritterStack.Screenplay` 0.1.0 is available from nuget.org.
- [x] Temporary GitHub-release package restore bootstrap steps are removed from this repository.
- [x] CLI PR #84 passes CI, merges, and creates the `v2.11.0` release.
- [x] CLI release workflows restore the adapter from ordinary package sources; the temporary release-asset bootstrap was removed after nuget.org publication.
- [x] The Homebrew-installed `cratis` 2.11.0 tool generates and validates Arc, Marten, and Critter Stack Screenplays from `/tmp`, outside source repositories.

### Compatibility safety

- [x] Arc generation is upgraded to Screenplay 4 and remains covered by real Arc fixtures.
- [x] Generated documents compile and pass canonical print/compile/print verification.
- [x] MSBuildWorkspace missing framework references for net7/net9 projects are repaired before analysis.
- [x] A compilation with unresolved source errors fails generation instead of silently producing a smaller model (`CLI0008`).
- [x] A solution containing several deployable hosts is rejected as ambiguous unless a project is targeted explicitly (`CLI0009`).
- [x] Auto discovery rejects zero matches and unrelated multiple matches instead of guessing (`CLI0010`/`CLI0011`).
- [x] Package validation uses the public `Cratis.CritterStack.Screenplay` 0.1.0 baseline and runs during pull-request sentinel packing.

### Canonical behavior

- [x] BankAccountES verifies aggregate commands, event returns, read models, reducers, and queries.
- [x] Current IncidentService verifies route-bound aggregates, versioning, response wrappers, direct append/delete, updated aggregate, outgoing/delayed messages, and queries.
- [x] Legacy CritterStackHelpDesk verifies Marten 6/Wolverine 1 compatibility and valid module naming.
- [x] CqrsMinimalApi verifies ordinary Marten documents and CRUD/query relationships without inventing an event projection.
- [x] Reports verifies `IMartenOp` document persistence and custom identity source context.
- [x] MartenWithProjectAspire verifies generic/instance async projection registration, multi-stream grouping loss, and exact `EventProjection.Create` document storage without inventing a read model.
- [x] Canonical sample checks run green in CI at pinned commits.

### Trust and honesty

- [x] Analysis does not start the target host or connect to Chronicle/PostgreSQL.
- [x] Generated and framework-authored sources are distinguished to avoid duplicate artifacts.
- [x] Responses, persisted events, messages, document operations, and delayed consequences are represented separately.
- [x] Route, concurrency, validation, delayed delivery, HTTP, and ordinary-document losses produce stable diagnostics instead of silent omission.
- [x] Public strategy, independent-project disclaimer, provenance, upstream fixture licenses, implementation status, and fresh-session handover are committed.
- [x] User-facing CLI documentation labels the provider as preview and explains diagnostics and limitations.

## Not required before the 0.1 preview

These remain tracked compatibility work and should not block initial use when they produce honest diagnostics:

- `EventProjection` operations beyond exact authored `Create` returns and event-bound `IDocumentOperations.Store`/`Insert`/`Update`/`Delete`/`DeleteWhere` calls, plus arbitrary body/value reconstruction;
- arbitrary multi-stream groupers and event slicers;
- full compiled-query expression reconstruction;
- all tenancy/database topologies;
- every Wolverine custom discovery policy;
- complete saga visualization;
- all broker topology and delivery-option details;
- perfect FluentValidation/DataAnnotations translation;
- Screenplay syntax for every measured realization concern;
- automatic migration from Screenplay to behaviorally identical Cratis code without review.

## Criteria for a later 1.0

A stable 1.0 should additionally require:

- package API compatibility baselines and a documented support matrix;
- at least one released CLI version using normal NuGet packages;
- multi-project API/worker/contracts acceptance coverage;
- completed high-confidence Marten and Wolverine readers for the documented support matrix;
- stable source/provider diagnostics with migration guidance;
- a defined answer for the highest-value language gaps from `Cratis/Screenplay#128`;
- customer or external-user validation on real authorized applications;
- no unresolved security, source-exfiltration, or package-supply-chain concerns.

## Completion note

The preview gate was completed and superseded by later releases. Current status and continuation are owned by [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md); this checklist remains dated evidence only.
