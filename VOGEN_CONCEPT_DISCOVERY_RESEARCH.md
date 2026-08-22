<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Discovering Vogen concepts for Screenplay

## Purpose

Critter Stack applications do not require a Cratis `ConceptAs<T>` value type. Marten and Wolverine support ordinary CLR values and structurally compatible strongly typed values. Vogen is a common source-generated value-object implementation in that ecosystem, but a Vogen value object is not automatically an identity, validator, event, or persistence boundary.

This document defines how source-to-Screenplay generation should discover Vogen declarations as concepts without coupling concept discovery to Marten, Wolverine, generated members, or the Screenplay AST.

## Research baseline

The external source and package review used:

| Product | Stable package | Reviewed source |
| --- | ---: | --- |
| Vogen | 8.0.7 (`9.0.0-beta.1` also exists) | [`6d3b5f4`](https://github.com/SteveDunn/Vogen/tree/6d3b5f463ebe83032d6d8c184f367f27b1285439) |
| Marten | 9.29.0 | [`c921fa5`](https://github.com/JasperFx/marten/tree/c921fa5dfdf7d540b5fe85d833831080c07c94d6) |
| Wolverine | 6.29.2 | [`38b65dd`](https://github.com/JasperFx/wolverine/tree/38b65dd0b14d99ac9b2d5f81759aed38c683a481) |

Important qualifications:

- Marten 9.29.0 pins its Vogen integration tests to Vogen 7.0.0. Vogen 8.0.7 retains the structural shape Marten uses, but that is not the same as direct Marten test coverage for every Vogen 8 behavior.
- WolverineFx.Marten 6.29.2 depends on Marten 9.23.0.
- Vogen's generated-code assembly version can differ from its NuGet patch version. Package provenance remains a CLI workspace concern.

Authoritative references include:

- [Vogen README](https://github.com/SteveDunn/Vogen/blob/6d3b5f463ebe83032d6d8c184f367f27b1285439/README.md)
- [Vogen value-object attribute](https://github.com/SteveDunn/Vogen/blob/6d3b5f463ebe83032d6d8c184f367f27b1285439/src/Vogen.SharedTypes/ValueObjectAttribute.cs)
- [Vogen validation tutorial](https://github.com/SteveDunn/Vogen/blob/6d3b5f463ebe83032d6d8c184f367f27b1285439/docs/site/Writerside/topics/tutorials/ValidationTutorial.md)
- [Vogen null handling](https://github.com/SteveDunn/Vogen/blob/6d3b5f463ebe83032d6d8c184f367f27b1285439/docs/site/Writerside/topics/Handling-nulls.md)
- [Marten identity documentation](https://martendb.io/documents/identity.html#strong-typed-identifiers)
- [Marten value-type identity source](https://github.com/JasperFx/marten/blob/c921fa5dfdf7d540b5fe85d833831080c07c94d6/src/Marten/Schema/Identity/ValueTypeIdGeneration.cs)
- [Marten Vogen fixtures](https://github.com/JasperFx/marten/tree/c921fa5dfdf7d540b5fe85d833831080c07c94d6/src/ValueTypeTests/VogenIds)
- [Wolverine strong identifier guidance](https://github.com/JasperFx/wolverine/blob/38b65dd0b14d99ac9b2d5f81759aed38c683a481/docs/guide/durability/marten/event-sourcing.md#strong-typed-identifiers)
- [Wolverine route parsing](https://github.com/JasperFx/wolverine/blob/38b65dd0b14d99ac9b2d5f81759aed38c683a481/docs/guide/http/routing.md#strong-typed-identifiers)

## Core decision

Vogen support is a **reusable .NET concept-discovery capability**, not a Marten or Wolverine reader feature.

```text
authored .NET concept evidence
  -> neutral concept facts
  -> deterministic concept resolution
  -> concept-capability analysis
  -> Screenplay ConceptSyntax
```

Repository ownership should be:

| Concern | Owner |
| --- | --- |
| Concept grammar, AST, parser, printer, compiler | `Cratis/Screenplay` |
| Neutral concept facts, conflicts, diagnostics, lowering | `Cratis/Screenplay.Generation` |
| Authored-source filtering, semantic identities, CLR primitive mapping | `Cratis/Screenplay.Generation.DotNet` |
| Vogen metadata interpretation | New `Cratis.Screenplay.Generation.DotNet.Vogen` package |
| Marten/Wolverine persistence and handler usage | `Cratis.CritterStack.Screenplay` |
| Composition of Vogen and Critter Stack contributions | `CritterStackScreenplayGenerator` |
| Existing `ConceptAs<T>` generation | Existing Arc path, unchanged initially |

Low-level Vogen interpretation must emit facts, never `ConceptSyntax`.

## Independent assertions

The following statements must remain independent:

1. A source type is a concept.
2. The concept has a primitive or enumeration representation.
3. The concept has validation.
4. A property is optional at one usage site.
5. A property identifies a Marten document, stream, aggregate, saga, or HTTP route.

A Vogen declaration proves the first statement and can prove the second. It does not prove identity merely because it wraps `Guid`, ends in `Id`, exposes generated `Value`/`From`, or participates in equality.

Identity requires separate usage evidence from Marten/Wolverine configuration, attributes, handler context, saga correlation, or an exact storage API.

## Exact Vogen recognition

Primary semantic metadata names are:

```text
Vogen.ValueObjectAttribute
Vogen.ValueObjectAttribute`1
Vogen.VogenDefaultsAttribute
Vogen.InstanceAttribute
```

Supporting metadata includes:

```text
Vogen.Validation
Vogen.ValueObjectValidationException
Vogen.ValueObjectOrError`1
Vogen.EfCoreConverterAttribute`1
Vogen.BsonSerializerAttribute`1
Vogen.MessagePackAttribute`1
```

Recognition rules:

- Bind `AttributeData.AttributeClass` by fully qualified metadata name.
- For `[ValueObject<T>]`, take the backing type from `AttributeClass.TypeArguments[0]`.
- For `[ValueObject(typeof(T))]`, take the backing type from constructor argument zero.
- Merge exact assembly-level `[VogenDefaults(...)]` configuration where the declaration omits a backing type.
- Anchor primary evidence at the authored attribute's `ApplicationSyntaxReference`.
- Require an authored partial declaration. Generated members and generated-only types may corroborate but never originate a concept.
- Do not recognize short-name lookalikes, one-property records, generated `Value` members, generated conversion operators, or types merely ending in `Id`, `Code`, or `Name`.

Generated `System.CodeDom.Compiler.GeneratedCodeAttribute("Vogen", ...)` is corroboration only. Its version is not package provenance.

## Concept representation

Map only exact supported CLR representations:

| CLR backing type | Screenplay concept type |
| --- | --- |
| `Guid` | `Uuid` |
| `string` | `String` |
| integral primitives | `Int` |
| `decimal`, `double`, `float` | `Decimal` |
| `bool` | `Bool` |
| `DateOnly` | `Date` |
| `DateTime`, `DateTimeOffset` | `DateTime` |

Do not silently map `TimeOnly`, `char`, custom classes, collections, constructed generic backings, or unresolved symbols to `String`.

Vogen can wrap constructed generic types when conversions permit it. Open generic backing declarations cannot be expressed as attribute type arguments. Vogen prohibits collection backing types with `VOG003`. Unsupported but valid backings remain concept evidence with an explicit representation-loss diagnostic; they do not become an invented primitive.

## Validation

Canonical Vogen validation is an authored method with the exact semantic contract:

```csharp
private static Vogen.Validation Validate(T value)
```

`NormalizeInput(T)` runs before validation but is normalization, not a validation rule.

Safe Screenplay mapping:

1. Prove an authored `Validate(T) -> Vogen.Validation` method attached to the exact Vogen declaration.
2. Emit a named external concept rule with the authored implementation file.
3. Preserve a constant `Validation.Invalid("...")` message only when statically known.
4. Translate to built-in `Minimum`, `Maximum`, `NotEmpty`, `Length`, or `Matches` only through a deliberately bounded matcher that proves exact equivalence.
5. Never interpret an arbitrary method body, `NormalizeInput`, direct exception throwing, or a coincidental method named `Validate` as declarative validation.

Vogen creation and deserialization paths run normalization/validation. `From` throws the configured validation exception; `TryFrom` returns failure. Those runtime mechanics do not create additional Screenplay rules.

## Null, default, and named instances

Optionality belongs to usage (`OrderId?`), not the concept declaration.

A Vogen struct has a CLR default but guards uninitialized access. A Vogen class can be null, uninitialized, or initialized. These states must not be collapsed.

`[Instance("Unspecified", ...)]` declares a named domain value that may intentionally bypass ordinary validation. It is not null, optionality, a default constructor value, or event-source identity. Until Screenplay represents named non-enum concept instances, preserve it as evidence and emit a loss diagnostic.

## Marten and Wolverine interoperability

Marten and Wolverine do not have a Vogen-specific domain model. They use structural behavior.

Marten strong document IDs currently require a public struct with one eligible public primitive property and a public one-argument constructor or public static one-argument builder. Vogen's generated `Value` and `From(T)` can satisfy that shape. Marten also supports explicit `RegisterValueType<T>()` for non-ID values used in queries.

Wolverine aggregate workflows accept strong IDs supported by Marten. Wolverine HTTP route binding accepts types with a compatible `TryParse` shape. Marten-backed saga identity follows Wolverine saga identity rules.

These usages can establish identity or lookup evidence, but the Vogen declaration itself cannot.

## Neutral contract additions

`ArtifactKind.Concept` already exists, but the neutral graph cannot currently describe or lower concepts. Add AST-independent contracts for:

- concept representation (`Primitive` or `Enumeration`);
- neutral primitive kind;
- enumeration values;
- concept attributes;
- concept validation and operands;
- optional authored implementation file;
- subject-aware type references.

`TypeReferenceDefinition` should gain an additive optional `SubjectId` so two same-named source types cannot be merged by display name.

Concepts are top-level declarations and require no module/feature/slice placement.

Resolution must merge identical evidence, retain conflicts, and diagnose:

- incompatible representations;
- conflicting validation definitions;
- same emitted concept name for distinct subjects;
- concept references whose target cannot be emitted;
- proven concept identity with no supported representation.

## Lowering

Extend the neutral lowerer with a separate top-level concept path:

1. Select unconflicted Concept artifacts.
2. Require one proven representation.
3. Attach attributes and validations.
4. Build deterministic `ConceptSyntax[]`.
5. Populate `ApplicationSyntax.Concepts` independently of behavioral placement.
6. Resolve subject-aware artifact properties to the emitted concept name.
7. Preserve the authored concept and validation files.

No initial Screenplay grammar change is required. Existing concept syntax already supports primitives, enums, attributes, declarative rules, named external rules, and implementation files.

If representation is unknown, retain the semantic fact and omit syntax with a diagnostic. Never invent `String`.

## Adapter composition

Recommended composition is:

```text
DotNetAnalysisContext
  -> VogenConceptScreenplayAdapter.Analyze
  -> CritterStackScreenplayAdapter.Analyze
  -> ScreenplayDefinitionGenerator.Generate(all contributions)
```

Contributions remain separate so adapter identity and provenance are not collapsed.

Rollout should be opt-in first, then default after canonical evidence. Existing non-Vogen Critter Stack output must remain byte-identical. Arc stays on its existing generator/model path during this work.

## Verification matrix

### Neutral generation

- concept facts need no placement;
- primitive and enum lowering;
- named external validation rule;
- representation and validation conflicts;
- same-name/different-subject conflicts;
- subject-aware artifact references;
- missing representation never falls back to `String`;
- shuffled contributions remain byte-identical;
- compile and print/compile/print stability.

### Reusable .NET discovery

- authored declaration plus generated partial declarations;
- generated-only exclusion;
- exact authored attribute evidence;
- fake same-short-name attributes;
- exact CLR primitive mapping;
- generated `Value`, conversions, and validation members cannot establish facts;
- cross-project semantic subjects.

### Vogen interpreter

Positive cases:

- generic and non-generic Vogen declaration forms;
- assembly defaults;
- supported primitive backings;
- struct, class, and record forms;
- authored validation and constant messages;
- normalization plus validation kept distinct;
- named instances retained as loss evidence;
- nullable usages;
- Marten document/aggregate identity usage;
- Wolverine route and saga identity usage.

Negative cases:

- Guid-backed non-ID remains only a concept;
- `CustomerId` naming alone does not establish identity;
- missing validation implies no rule;
- normalization is not validation;
- unsupported/custom backing never becomes `String`;
- class-backed Vogen value is not accepted as a Marten structural strong ID;
- fake attributes and generated-only shapes are ignored;
- named `Unspecified` is not optionality;
- generated paths never become primary evidence.

### Canonical application

Add a pinned, Cratis-owned Vogen application fixture that proves top-level concepts, primitive representations, concept-typed artifact properties, external validation rules, identity-by-usage, generated-source exclusion, deterministic bytes, and compiler round-trip stability without starting a host or database.

## Delivery tracking

- [`Cratis/Screenplay.Generation#6`](https://github.com/Cratis/Screenplay.Generation/issues/6) — neutral concept facts, resolution, subject-aware references, and lowering.
- [`Cratis/Screenplay.Generation#7`](https://github.com/Cratis/Screenplay.Generation/issues/7) — authored-source .NET discovery and the Vogen interpreter package.
- [`Cratis/Screenplay.CritterStack#25`](https://github.com/Cratis/Screenplay.CritterStack/issues/25) — composition and pinned canonical Vogen fixture.

## Delivery sequence

1. Freeze current Arc and Critter Stack output baselines.
2. Add neutral concept representation/attribute/validation facts.
3. Extend deterministic resolution and conflict diagnostics.
4. Add concept lowering and subject-aware type references.
5. Harden authored-source helpers in `Generation.DotNet`.
6. Pin Vogen 8.0.7 and compile a real semantic fixture.
7. Implement `Cratis.Screenplay.Generation.DotNet.Vogen`.
8. Add opt-in composition to the Critter Stack generator.
9. Add a canonical Vogen fixture and prove non-Vogen output remains unchanged.
10. Enable Vogen composition by default after canonical review.
11. Handle event-source identity and query-key migration as separate, usage-driven work.

## Conclusion

The missing component is not a Vogen-specific Marten reader. It is a neutral, provenance-preserving concept model plus reusable authored-source .NET concept discovery, with Vogen as the first semantic interpreter.

This architecture lets future non-Cratis value-object libraries contribute concepts through the same seam while preserving the central rules: no generated-source invention, no primitive fallback, no identity-by-name, no validation guesses, and no adapter-owned AST construction.
