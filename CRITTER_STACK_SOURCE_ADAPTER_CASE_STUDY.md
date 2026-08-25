<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Marten and Wolverine source-adapter case study

> Compatibility pointer: the maintained case study now lives at [`Documentation/guides/extend-source-adapter.md`](Documentation/guides/extend-source-adapter.md).

The generic source-adapter contract is owned by Screenplay.Generation. Use the canonical [Writing a .NET source adapter](https://github.com/Cratis/Screenplay.Generation/blob/main/WRITING_SOURCE_ADAPTERS.md) guide for adapter contracts, neutral facts, evidence, diagnostics, source identity, composition, and verification.

The repository guide applies that generic contract to the Marten and Wolverine compatibility adapter. Keep framework-specific additions within those boundaries and treat generated output as a candidate for human review wherever diagnostics report loss or ambiguity.
