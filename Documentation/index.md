---
title: Screenplay.CritterStack
description: Understand the optional Marten and Wolverine source adapter and its evidence boundaries.
---

Screenplay.CritterStack is an optional, pre-release compatibility adapter. Given an authorized .NET source context, it identifies bounded Marten and Wolverine semantics and contributes neutral facts, evidence, and diagnostics to Screenplay.Generation. The result is a reviewable Screenplay candidate; it is not an automatic migration or a claim of behavioral equivalence.

The adapter library does not start the analyzed application or connect to its database. The host still owns workspace loading, source authorization, project and target-framework selection, and the trust boundary around MSBuild evaluation.

## Extend the adapter

Start with the canonical [Generation source-adapter guide](/screenplay/generation/guides/build-source-adapter/). It owns the generic adapter contract, fact vocabulary, evidence model, source identity, diagnostics, composition, and verification rules.

Then use the [Marten and Wolverine case study](guides/extend-source-adapter.md) to see how this repository applies those rules to framework metadata, handler consequences, focused specifications, and compatibility samples.

## Current boundaries

- Source recovery produces a candidate for human review wherever diagnostics report loss or ambiguity.
- Compilation errors, ambiguous hosts, unsupported framework shapes, and unresolved semantic flow must fail closed at the owning layer.
- Package and sample checks are compatibility evidence for exercised versions, not a general support promise.
- This repository does not ship a Screenplay-to-Marten/Wolverine renderer; the canonical contract for any such target is the [Stage renderer-target guide](/screenplay/stage/guides/build-renderer-target/).
- Studio workflows, Stage execution, automatic materialization, and production support are outside this adapter's accepted evidence.
