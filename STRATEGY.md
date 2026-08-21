<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Strategic rationale: Critter Stack to Screenplay

## Decision

`Cratis/Screenplay.CritterStack` remains a **public, source-available interoperability adapter**.

It converts source evidence from Marten and Wolverine applications into a verified Screenplay definition. That definition can then be visualized and reviewed in Studio, executed by Stage where supported, or used as an intermediate model for generating Cratis application code.

The adapter is positioned as an interoperability and migration tool, not as an unofficial Critter Stack implementation.

## Why this is strategically valuable

### Reduce the cost of evaluating and adopting Cratis

A mature application rarely starts from an empty repository. Importing an existing event model, commands, read models, projections, queries, and workflows into Screenplay gives a team a credible path to:

1. understand its current system in Studio;
2. inspect where semantics are exact, inferred, or missing;
3. evolve the model in Screenplay;
4. generate or run a Cratis implementation incrementally.

This removes a large migration barrier and creates a practical adoption funnel from adjacent stacks.

### Establish Screenplay as an interoperability model

Screenplay becomes more valuable when it can describe systems that were not originally built with Cratis. A framework-neutral semantic model is a stronger ecosystem position than a format that only Cratis applications can produce.

The same architecture supports future adapters for other frameworks, ecosystems, and source languages without coupling them to Arc or Critter Stack.

### Make conversion honest and reviewable

Source conversion is not perfectly lossless. Public code and stable diagnostics let users verify:

- which framework conventions are recognized;
- how Wolverine responses, messages, side effects, and Marten events are distinguished;
- which mappings are exact or inferred;
- what Screenplay cannot represent yet;
- that the adapter does not start the application, connect to PostgreSQL, or exfiltrate source.

Transparency is particularly important for migration tooling because users must trust the resulting model before generating a new implementation.

## Why the repository should be public

### Shipping a private .NET package provides little secrecy

A package embedded in Cratis CLI or distributed through NuGet can be inspected and decompiled. A private repository would add friction for legitimate users and contributors without creating a meaningful technical moat.

The durable Cratis differentiation is not hiding metadata names or Roslyn matching rules. It is the combined Screenplay language, Studio experience, Stage runtime, Cratis framework capabilities, code generation, migration workflow, and quality of semantic diagnostics.

### Public ownership improves adoption

A public adapter:

- demonstrates that migration is supported rather than theoretical;
- lowers fear of vendor lock-in;
- lets users audit source handling and security;
- allows compatibility fixes from Marten/Wolverine users;
- makes package behavior and limitations discoverable;
- can become a neutral bridge even for teams not yet ready to migrate.

Broader Screenplay adoption benefits Cratis even when the first use is visualization rather than immediate code conversion.

### Public is consistent with upstream licensing

Marten, Wolverine, CritterStackSamples, and CritterStackHelpDesk use permissive MIT licensing. The adapter analyzes public framework contracts and conventions by metadata name and does not copy or embed private implementation code.

Pinned fixtures must retain license attribution and should use the minimum source necessary for deterministic compatibility verification.

## What may remain private

Public adapter source does not require every commercial capability to be public. Potential private or hosted differentiation may include:

- customer-specific migration rules and reports;
- confidential application mappings;
- proprietary recommendation/risk scoring;
- managed migration execution;
- premium Studio workflows;
- large-scale portfolio analysis;
- human-assisted migration services;
- private connectors requiring customer credentials.

Those capabilities should consume the public semantic adapter rather than forking its framework interpretation.

## Positioning and communication

Use compatibility-focused language:

- “Generate Screenplay from Marten and Wolverine source.”
- “Visualize and review a Critter Stack application in Studio.”
- “Create a migration-ready intermediate model with explicit diagnostics.”

Avoid adversarial framing such as “extracting” or “taking” competitor applications. The adapter should be useful even when a team only wants documentation or system understanding.

The README and package metadata should state:

- this is an independent Cratis compatibility project;
- it is not affiliated with or endorsed by JasperFx;
- Marten, Wolverine, JasperFx, and Critter Stack names belong to their respective owners;
- source conversion may require human review where diagnostics report semantic loss.

## Safety and legal boundaries

- Analyze only source the user is authorized to process.
- Never upload or transmit source without explicit user action.
- Never start the target application or connect to its infrastructure by default.
- Never include customer source, secrets, connection strings, or private endpoints in fixtures or diagnostics.
- Use public framework APIs and documented conventions; do not depend on confidential or unlawfully obtained information.
- Preserve attribution for copied MIT-licensed fixture material.
- Do not claim perfect behavioral equivalence when source analysis cannot establish it.

## Success criteria

The strategy succeeds when a team can point the Cratis CLI at an existing Marten/Wolverine project and receive:

1. a deterministic, compiling Screenplay document;
2. a visible model in Studio;
3. source-linked provenance;
4. explicit diagnostics for every important semantic gap;
5. a credible path toward generated or running Cratis code;
6. no requirement to run the original application or database.
