// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static void AddDcbFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        WolverineDcbDiscovery? dcb)
    {
        if (dcb is null)
        {
            return;
        }

        var aggregateSubject = scope.Subjects.SubjectForType(scope.Project, dcb.ModelType);
        var evidence = new Evidence
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Exact,
            Source = dcb.Source,
            Explanation = $"Authored Wolverine DCB model parameter '{dcb.Parameter.Name}' with an EventTagQuery companion"
        };
        AddAggregateArtifact(scope.Project, aggregateSubject, dcb.ModelType, evidence, scope.Facts);
        scope.Facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{dcb.Discriminator}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: dcb.SourceMember,
            discriminator: dcb.Discriminator));

        AddDcbConditionFacts(scope, commandSubject, dcb, aggregateSubject, evidence);
        AddDcbQueryEventFacts(scope, commandSubject, commandName, dcb, evidence);
        AddDcbDiagnostics(scope, commandSubject, commandName, dcb);
    }

    static void AddDcbConditionFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        WolverineDcbDiscovery dcb,
        SubjectId aggregateSubject,
        Evidence evidence)
    {
        foreach (var condition in dcb.Conditions.Where(condition =>
                     condition.EventType is null || !WolverineSagaTypes.IsSagaState(condition.EventType, scope.Project)))
        {
            var tagSubject = scope.Subjects.SubjectForType(scope.Project, condition.TagType);
            var eventSubject = condition.EventType is null ? null : scope.Subjects.SubjectForType(scope.Project, condition.EventType);
            var discriminator = $"{dcb.Discriminator}:condition:{condition.Ordinal}:tag:{tagSubject.Value}:event:{eventSubject?.Value ?? "any"}";
            var conditionEvidence = evidence with
            {
                Source = condition.Source ?? dcb.QuerySource,
                Explanation = condition.EventType is null
                    ? $"Exact DCB condition {condition.Ordinal} matches any event tagged with '{condition.TagType.Name}'"
                    : $"Exact DCB condition {condition.Ordinal} matches '{condition.EventType.Name}' tagged with '{condition.TagType.Name}'"
            };
            scope.Facts.Add(Relationship(
                $"wolverine:reads:{commandSubject.Value}:{discriminator}:{aggregateSubject.Value}",
                commandSubject,
                RelationshipKind.Reads,
                aggregateSubject,
                conditionEvidence,
                sourceMember: condition.SourceMember,
                discriminator: discriminator));
        }
    }

    static void AddDcbQueryEventFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        WolverineDcbDiscovery dcb,
        Evidence evidence)
    {
        foreach (var eventType in dcb.QueryEventTypes.Where(eventType => !WolverineSagaTypes.IsSagaState(eventType, scope.Project)))
        {
            var eventSubject = scope.Subjects.SubjectForType(scope.Project, eventType);
            var eventEvidence = evidence with
            {
                Source = dcb.QuerySource,
                Explanation = $"Explicit historical event type in the authored DCB EventTagQuery for '{commandName}'"
            };
            scope.Facts.Add(Artifact(
                $"wolverine:dcb:query-event:{commandSubject.Value}:{eventSubject.Value}",
                new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event },
                eventType.Name,
                SourceFileOf(eventType, scope.Project),
                DotNetTypeShapes.PropertiesOf(eventType),
                eventEvidence));
        }
    }

    static void AddDcbDiagnostics(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        WolverineDcbDiscovery dcb)
    {
        scope.Diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.DcbBoundaryOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"The DCB consistency boundary for '{commandName}' parameter '{dcb.Parameter.Name}' is retained as neutral Aggregate/Reads evidence, but tag routing and boundary concurrency cannot be represented by the current Screenplay language",
            Source = dcb.Source,
            Subject = commandSubject
        });

        if (!dcb.QueryResolved)
        {
            scope.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.DcbQueryUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"The EventTagQuery companion '{dcb.Companion.Name}' for '{commandName}' is outside the bounded direct fluent-chain shapes and was not interpreted",
                Source = dcb.QuerySource,
                Subject = commandSubject
            });
        }
    }

    static void AddEventStreamBindingFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        IReadOnlyList<WolverineStateBinding> bindings,
        bool isHttpEndpoint)
    {
        var aggregateSubjects = new HashSet<SubjectId>();
        var hasMultipleStreams = bindings.Count(binding => binding.Parameter is not null) > 1;
        foreach (var binding in bindings.Where(_ => _.LoadsModel))
        {
            if (binding.Fetched is not null)
            {
                AddFetchedStreamBindingFacts(scope, commandSubject, commandName, binding);
                continue;
            }

            AddStreamParameterRead(scope, commandSubject, binding, aggregateSubjects);
            if (isHttpEndpoint && binding.IdentityMember is null)
            {
                scope.Diagnostics.Add(RouteIdentityOmitted(commandSubject, commandName, binding));
            }

            if (OmitsVersionMetadata(binding))
            {
                scope.Diagnostics.Add(StreamVersionOmitted(commandSubject, commandName, binding));
            }

            if (hasMultipleStreams)
            {
                scope.Diagnostics.Add(MultipleStreamMetadataOmitted(commandSubject, commandName, binding));
            }
        }
    }

    static void AddStreamParameterRead(
        WolverineFactScope scope,
        SubjectId commandSubject,
        WolverineStateBinding binding,
        HashSet<SubjectId> aggregateSubjects)
    {
        var evidence = StateBindingEvidence(
            scope.Adapter,
            binding,
            $"Wolverine loads '{binding.ModelType.Name}' through exact IEventStream<T> parameter '{binding.Parameter!.Name}'");
        var aggregateSubject = scope.Subjects.SubjectForType(scope.Project, binding.ModelType);
        if (aggregateSubjects.Add(aggregateSubject))
        {
            AddAggregateArtifact(scope.Project, aggregateSubject, binding.ModelType, evidence, scope.Facts);
        }

        scope.Facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{binding.Discriminator}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: binding.IdentityMember is null ? null : LowerFirst(binding.IdentityMember.Name),
            discriminator: binding.Discriminator));
    }

    static bool OmitsVersionMetadata(WolverineStateBinding binding) =>
        binding.Version.Value is not null ||
        binding.HasAmbiguousConventionalVersion ||
        !string.Equals(binding.LoadStyle.Value, "Optimistic", StringComparison.Ordinal) ||
        binding.Consistency.Value;

    static GenerationDiagnostic RouteIdentityOmitted(SubjectId commandSubject, string commandName, WolverineStateBinding binding) => new()
    {
        Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"The '{binding.ModelType.Name}' identity for '{commandName}' stream parameter '{binding.Parameter!.Name}' comes from HTTP route or binding metadata rather than a command property and cannot be marked as a Screenplay identifier",
        Source = binding.Identity.Source ?? binding.Source,
        Subject = commandSubject
    };

    static GenerationDiagnostic StreamVersionOmitted(SubjectId commandSubject, string commandName, WolverineStateBinding binding) => new()
    {
        Code = WolverineDiagnosticCodes.StreamVersionOmitted,
        Severity = GenerationDiagnosticSeverity.Information,
        Outcome = binding.HasAmbiguousConventionalVersion
            ? GenerationDiagnosticOutcome.Unknown
            : GenerationDiagnosticOutcome.Unsupported,
        Message = binding.HasAmbiguousConventionalVersion
            ? $"The conventional Version member for '{commandName}' cannot be attributed safely to stream parameter '{binding.Parameter!.Name}' because the handler loads multiple streams"
            : $"The version, load style, or consistency metadata for '{commandName}' stream parameter '{binding.Parameter!.Name}' cannot be represented exactly by Screenplay concurrency",
        Source = binding.Version.Source ?? binding.LoadStyle.Source ?? binding.Consistency.Source ?? binding.Source,
        Subject = commandSubject
    };

    static GenerationDiagnostic MultipleStreamMetadataOmitted(SubjectId commandSubject, string commandName, WolverineStateBinding binding) => new()
    {
        Code = WolverineDiagnosticCodes.MultipleStreamMetadataOmitted,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"Handler '{commandName}' binds multiple event streams; target and identity for parameter '{binding.Parameter!.Name}' are retained as neutral relationship metadata, but parameter-specific loading metadata cannot be lowered faithfully to the current Screenplay language",
        Source = binding.Source,
        Subject = commandSubject
    };

    static void AddFetchedStreamBindingFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        WolverineStateBinding binding)
    {
        var fetched = binding.Fetched!;
        var evidence = StateBindingEvidence(
            scope.Adapter,
            binding,
            $"Exact FetchManyForWriting<{binding.ModelType.Name}> loading at '{fetched.Site}', identity expression '{fetched.IdentityExpression}'");
        if (!fetched.IsEmpty)
        {
            var aggregateSubject = scope.Subjects.SubjectForType(scope.Project, binding.ModelType);
            AddAggregateArtifact(scope.Project, aggregateSubject, binding.ModelType, evidence, scope.Facts);
            scope.Facts.Add(Relationship(
                $"wolverine:reads:{commandSubject.Value}:{binding.Discriminator}:{aggregateSubject.Value}",
                commandSubject,
                RelationshipKind.Reads,
                aggregateSubject,
                evidence,
                sourceMember: binding.IdentityMember is null ? null : LowerFirst(binding.IdentityMember.Name),
                discriminator: binding.Discriminator,
                isCollection: fetched.IsCollection));
        }

        var boundary = fetched switch
        {
            { IsEmpty: true } => "the statically empty input establishes no stream instances",
            { IsCollection: true } => "unknown runtime cardinality and quantified identity correspondence are retained as collection relationship metadata",
            _ => $"target and identity for slot {fetched.Index} are retained as neutral relationship metadata"
        };
        scope.Diagnostics.Add(new()
        {
            Code = WolverineDiagnosticCodes.MultipleStreamMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported,
            Message = $"Handler '{commandName}' uses FetchManyForWriting; {boundary}, but per-stream loading and optimistic version semantics cannot be lowered faithfully to the current Screenplay language{DistinctnessLoss(fetched)}",
            Source = binding.Source,
            Subject = commandSubject
        });
    }

    static string DistinctnessLoss(WolverineFetchedStreamBinding fetched) => fetched switch
    {
        { HasUncomparableIdentity: true } => "; authored identity expressions that cannot be compared semantically are not proven distinct, so append targets are not inferred; the framework rejects duplicate IDs at runtime",
        { HasRepeatedIdentity: true } => "; repeated authored identity expressions are not proven distinct, so append targets are not inferred; the framework rejects duplicate IDs at runtime",
        _ => string.Empty
    };

    static void AddEventStreamAppendFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        SubjectId eventOwner,
        ArtifactPlacement compatibilityPlacement,
        WolverineEventStreamAppendDiscovery discovery)
    {
        foreach (var unresolved in discovery.Unresolved)
        {
            scope.Diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.EventWriteTargetUnresolved,
                Severity = GenerationDiagnosticSeverity.Warning,
                Outcome = GenerationDiagnosticOutcome.Unknown,
                Message = $"An exact IEventStream<T> append was not represented because {unresolved.Reason}",
                Source = unresolved.Source,
                Subject = commandSubject
            });
        }

        var context = new AppendContext
        {
            CommandSubject = commandSubject,
            EventOwner = eventOwner,
            CompatibilityPlacement = compatibilityPlacement
        };
        foreach (var append in discovery.Appends)
        {
            AddAppendFacts(scope, context, append);
        }
    }

    static void AddAppendFacts(WolverineFactScope scope, AppendContext context, WolverineEventStreamAppend append)
    {
        var binding = append.Binding;
        var aggregateSubject = scope.Subjects.SubjectForType(scope.Project, binding.ModelType);
        var evidence = new Evidence
        {
            Adapter = scope.Adapter,
            Strength = EvidenceStrength.Exact,
            Source = append.Source,
            Explanation = binding.Fetched is { } fetched
                ? $"Exact IEventStream<{binding.ModelType.Name}> append through fetched binding '{binding.Discriminator}', identity expression '{fetched.IdentityExpression}'"
                : $"Exact IEventStream<{binding.ModelType.Name}> append through handler parameter '{binding.Parameter!.Name}'"
        };
        if (context.AggregateSubjects.Add(aggregateSubject))
        {
            AddAggregateArtifact(scope.Project, aggregateSubject, binding.ModelType, evidence, scope.Facts);
        }

        foreach (var eventType in append.EventTypes)
        {
            AddAppendedEvent(scope, context, append, aggregateSubject, evidence, eventType);
        }
    }

    static void AddAppendedEvent(
        WolverineFactScope scope,
        AppendContext context,
        WolverineEventStreamAppend append,
        SubjectId aggregateSubject,
        Evidence evidence,
        INamedTypeSymbol eventType)
    {
        var binding = append.Binding;
        var eventSubject = scope.Subjects.SubjectForType(scope.Project, eventType);
        if (context.ProducedEvents.Add(eventSubject))
        {
            AddEventAndProduction(
                scope,
                context.CommandSubject,
                context.EventOwner,
                new WolverineProductionDecision(eventType, Declarative: false, StreamIdentity: null),
                context.CompatibilityPlacement,
                evidence);
        }

        var relationshipDiscriminator = $"{binding.Discriminator}:event:{eventSubject.Value}";
        if (!context.AppendRelationships.Add(relationshipDiscriminator))
        {
            return;
        }

        scope.Facts.Add(Relationship(
            $"wolverine:appends:{context.CommandSubject.Value}:{relationshipDiscriminator}:{aggregateSubject.Value}",
            context.CommandSubject,
            RelationshipKind.Appends,
            aggregateSubject,
            evidence,
            sourceMember: binding.IdentityMember is null ? null : LowerFirst(binding.IdentityMember.Name),
            discriminator: relationshipDiscriminator,
            isCollection: binding.Fetched?.IsCollection ?? false));
    }

    static void AddAggregateArtifact(
        DotNetProjectCompilation project,
        SubjectId aggregateSubject,
        INamedTypeSymbol aggregateType,
        Evidence evidence,
        List<GenerationFact> facts) => facts.Add(Artifact(
        $"wolverine:aggregate:{aggregateSubject.Value}",
        new ArtifactKey { Subject = aggregateSubject, Kind = ArtifactKind.Aggregate },
        aggregateType.Name,
        SourceFileOf(aggregateType, project),
        DotNetTypeShapes.PropertiesOf(aggregateType),
        evidence));

    static Evidence StateBindingEvidence(
        AdapterIdentity adapter,
        WolverineStateBinding binding,
        string explanation) => new()
        {
            Adapter = adapter,
            Strength = EvidenceStrength.Exact,
            Source = binding.Source,
            Explanation = explanation
        };

    static void AddEventAndProduction(
        WolverineFactScope scope,
        SubjectId commandSubject,
        SubjectId eventOwner,
        WolverineProductionDecision production,
        ArtifactPlacement compatibilityPlacement,
        Evidence evidence)
    {
        var eventType = production.EventType;
        if (WolverineSagaTypes.IsSagaState(eventType, scope.Project))
        {
            return;
        }

        var eventSubject = scope.Subjects.SubjectForType(scope.Project, eventType);
        var eventKey = new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event };
        scope.Facts.Add(Artifact(
            $"wolverine:event:{eventSubject.Value}",
            eventKey,
            eventType.Name,
            SourceFileOf(eventType, scope.Project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        scope.Placements.Add(new(
            $"wolverine:placement:event:{eventSubject.Value}:{commandSubject.Value}",
            eventKey,
            eventOwner,
            compatibilityPlacement,
            evidence));
        scope.Facts.Add(Relationship(
            $"wolverine:produces:{commandSubject.Value}:{eventSubject.Value}",
            commandSubject,
            RelationshipKind.Produces,
            eventSubject,
            evidence,
            sourceMember: production.StreamIdentity is null ? null : LowerFirst(production.StreamIdentity.Name),
            discriminator: production.Declarative ? "declarative" : "imperative"));
    }

    static IPropertySymbol? LinkedStreamIdentity(IReadOnlyList<WolverineProductionDecision> productions)
    {
        var identities = productions
            .Where(_ => _.Declarative)
            .Select(_ => _.StreamIdentity)
            .OfType<IPropertySymbol>()
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<IPropertySymbol>()
            .ToArray();
        return identities.Length == 1 ? identities[0] : null;
    }

    sealed class AppendContext
    {
        public required SubjectId CommandSubject { get; init; }

        public required SubjectId EventOwner { get; init; }

        public required ArtifactPlacement CompatibilityPlacement { get; init; }

        public HashSet<SubjectId> AggregateSubjects { get; } = [];

        public HashSet<SubjectId> ProducedEvents { get; } = [];

        public HashSet<string> AppendRelationships { get; } = new(StringComparer.Ordinal);
    }
}
