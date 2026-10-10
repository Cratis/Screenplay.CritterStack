// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineFacts
{
    static StateChangeShape DiscoverStateChangeShape(
        IMethodSymbol method,
        IParameterSymbol? request,
        INamedTypeSymbol? messageType,
        DotNetProjectCompilation project,
        bool isHttpEndpoint)
    {
        var aggregateWorkflow = IsAggregateWorkflow(method);
        var dcb = WolverineDcb.Discover(method, request, project, isHttpEndpoint);
        var parameterBindings = WolverineEventStreams.ParameterBindings(method, messageType, project);
        var fetchedBindings = WolverineFetchedEventStreams.Bindings(method, messageType, project);
        IReadOnlyList<WolverineStateBinding> streamBindings = [.. parameterBindings, .. fetchedBindings];
        var aggregate = dcb is null ? AggregateParameter(method, request, aggregateWorkflow) : null;
        var yieldsAggregateEvents = dcb is null && aggregateWorkflow && parameterBindings.Count == 0;
        var returnIdentity = messageType is not null && aggregate?.Type is INamedTypeSymbol identityAggregate
            ? IdentityProperty(messageType, identityAggregate)
            : null;
        return new StateChangeShape
        {
            Method = method,
            Dcb = dcb,
            AggregateWorkflow = aggregateWorkflow,
            ParameterBindings = parameterBindings,
            FetchedBindings = fetchedBindings,
            StreamBindings = streamBindings,
            AppendDiscovery = WolverineEventStreams.Appends(method, project, streamBindings),
            Aggregate = aggregate,
            ReturnEvents = ReturnEvents(method, dcb, yieldsAggregateEvents, project),
            BodyEvents = dcb is null ? [.. PersistenceEvents(method, project)] : [],
            ReturnIdentity = returnIdentity,
            SessionAppends = dcb is null ? WolverineProductionLinks.SessionAppends(method, request, project) : [],
            Yields = yieldsAggregateEvents ? WolverineProductionLinks.AggregateYields(method, returnIdentity, project) : [],
            ReturnConsequences = ReturnConsequencesFor(method, dcb, aggregateWorkflow, parameterBindings.Count > 0, project, isHttpEndpoint),
            OutgoingMessages = DiscoverOutgoingMessages(method, project),
            BusConsequences = WolverineBusConsequences.Discover(method, project)
        };
    }

    static IReadOnlyList<WolverineReturnConsequence> ReturnConsequencesFor(
        IMethodSymbol method,
        WolverineDcbDiscovery? dcb,
        bool aggregateWorkflow,
        bool hasParameterBindings,
        DotNetProjectCompilation project,
        bool isHttpEndpoint) =>
        WolverineReturnConsequences.Classify(
            method,
            project,
            isHttpEndpoint,
            aggregateWorkflow || dcb is { IsBoundaryParameter: false },
            dcb is null && hasParameterBindings);

    static IReadOnlyList<ITypeSymbol> ReturnEvents(
        IMethodSymbol method,
        WolverineDcbDiscovery? dcb,
        bool yieldsAggregateEvents,
        DotNetProjectCompilation project)
    {
        if (dcb is not null)
        {
            return dcb.EventTypes;
        }

        return yieldsAggregateEvents ? [.. AggregateReturnEvents(method, project)] : [];
    }

    static void AddStateReadFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        string commandName,
        StateChangeShape shape,
        bool isHttpEndpoint)
    {
        AddPersistenceBoundReads(scope, commandSubject, shape.Method, shape.Aggregate);
        AddEventStreamBindingFacts(scope, commandSubject, commandName, shape.StreamBindings, isHttpEndpoint);
        AddDcbFacts(scope, commandSubject, commandName, shape.Dcb);
    }

    static void AddProductionFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        SubjectId eventOwner,
        IReadOnlyList<WolverineProductionDecision> productions,
        StateChangeShape shape,
        ArtifactPlacement compatibilityPlacement,
        Evidence evidence)
    {
        foreach (var production in productions)
        {
            AddEventAndProduction(scope, commandSubject, eventOwner, production, compatibilityPlacement, evidence);
        }

        AddEventStreamAppendFacts(scope, commandSubject, eventOwner, compatibilityPlacement, shape.AppendDiscovery);
    }

    static void AddEffectFacts(
        WolverineFactScope scope,
        SubjectId commandSubject,
        StateChangeShape shape,
        Evidence evidence)
    {
        var emission = EmissionFor(scope, commandSubject, evidence);
        var method = shape.Method;
        AddDocumentDeletes(emission, method);
        AddReturnConsequences(emission, shape.ReturnConsequences);
        AddStorageActionConsequences(scope, commandSubject, method, shape.ReturnConsequences, evidence);
        AddDirectBusConsequences(emission, method, scope.Diagnostics, shape.BusConsequences);
        AddOutgoingMessages(emission, method, shape.OutgoingMessages, scope.Diagnostics);
    }

    static void AddReadModelAndRelationship(
        WolverineFactScope scope,
        SubjectId commandSubject,
        INamedTypeSymbol? commandType,
        INamedTypeSymbol aggregateType,
        Evidence evidence)
    {
        var project = scope.Project;
        var aggregateSubject = scope.Subjects.SubjectForType(project, aggregateType);
        scope.Facts.Add(Artifact(
            $"wolverine:read-model:{aggregateSubject.Value}",
            new ArtifactKey { Subject = aggregateSubject, Kind = ArtifactKind.ReadModel },
            aggregateType.Name,
            SourceFileOf(aggregateType, project),
            DotNetTypeShapes.PropertiesOf(aggregateType),
            CritterStackSource.EvidenceFor(aggregateType, scope.Adapter, project, EvidenceStrength.Conventional, "Wolverine loads this model as aggregate decision state")));
        scope.Facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: commandType is null ? null : IdentityPropertyName(commandType, aggregateType)));
    }

    static void AddPersistenceBoundReads(
        WolverineFactScope scope,
        SubjectId sourceSubject,
        IMethodSymbol method,
        IParameterSymbol? aggregateParameter)
    {
        var artifactIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in method.Parameters.Where(IsPersistenceBoundParameter))
        {
            if (SymbolEqualityComparer.Default.Equals(parameter, aggregateParameter) ||
                PersistenceBoundRead(parameter) is not { } binding)
            {
                continue;
            }

            var documentSubject = scope.Subjects.SubjectForType(scope.Project, binding.DocumentType);
            var artifactId = $"wolverine:read-model:{documentSubject.Value}";
            var evidence = CritterStackSource.EvidenceFor(
                parameter,
                scope.Adapter,
                scope.Project,
                EvidenceStrength.Exact,
                binding.Explanation);
            if (artifactIds.Add(artifactId))
            {
                scope.Facts.Add(Artifact(
                    artifactId,
                    new ArtifactKey { Subject = documentSubject, Kind = ArtifactKind.ReadModel },
                    binding.DocumentType.Name,
                    SourceFileOf(binding.DocumentType, scope.Project),
                    DotNetTypeShapes.PropertiesOf(binding.DocumentType),
                    evidence));
            }

            scope.Facts.Add(Relationship(
                $"wolverine:reads:{sourceSubject.Value}:{documentSubject.Value}:{parameter.Name}",
                sourceSubject,
                RelationshipKind.Reads,
                documentSubject,
                evidence,
                discriminator: binding.Discriminator,
                isCollection: binding.IsCollection,
                isOptional: binding.IsOptional));
        }
    }

    static (INamedTypeSymbol DocumentType, string Discriminator, string Explanation, bool IsCollection, bool IsOptional)? PersistenceBoundRead(
        IParameterSymbol parameter)
    {
        var isNullable = parameter.Type.NullableAnnotation == NullableAnnotation.Annotated ||
                         parameter.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        if (IsEntityParameter(parameter))
        {
            var documentType = UnwrapNullable(parameter.Type);
            return documentType is null
                ? null
                : (documentType, "entity", "Wolverine loads this entity by identity for the handler", false, isNullable || IsOptionalEntity(parameter));
        }

        if (DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineFirstOrDefaultAttribute))
        {
            var documentType = UnwrapNullable(parameter.Type);
            return documentType is null
                ? null
                : (documentType, "first-or-default", "Wolverine loads the first matching document for the handler", false, isNullable);
        }

        return QueryableElementType(parameter) is { } elementType
            ? (elementType, "queryable", "Wolverine exposes this document set as a queryable to the handler", true, isNullable)
            : null;
    }

    static INamedTypeSymbol? QueryableElementType(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineQueryableAttribute) &&
        parameter.Type is INamedTypeSymbol queryable &&
        DotNetSubjectIds.MetadataName(queryable.OriginalDefinition) == "System.Linq.IQueryable`1"
            ? queryable.TypeArguments[0] as INamedTypeSymbol
            : null;

    static INamedTypeSymbol? UnwrapNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0] as INamedTypeSymbol
            : type as INamedTypeSymbol;

    static bool IsOptionalEntity(IParameterSymbol parameter) =>
        parameter.GetAttributes()
            .Where(_ => _.AttributeClass is not null && DotNetSymbols.IsOrInheritsFrom(_.AttributeClass, WellKnownTypes.WolverineEntityAttribute))
            .SelectMany(_ => _.NamedArguments)
            .Any(_ => string.Equals(_.Key, "Required", StringComparison.Ordinal) && _.Value.Value is false);

    static IReadOnlyList<PropertyDefinition> CommandProperties(
        INamedTypeSymbol commandType,
        INamedTypeSymbol? aggregateType,
        IReadOnlyList<WolverineStateBinding> parameterBindings,
        bool hasFetchedStreams,
        IPropertySymbol? linkedStreamIdentity = null)
    {
        var identity = linkedStreamIdentity ?? parameterBindings.Count switch
        {
            0 when aggregateType is null && hasFetchedStreams => null,
            0 => IdentityProperty(commandType, aggregateType),
            1 when parameterBindings[0].LoadsModel => parameterBindings[0].IdentityMember as IPropertySymbol,
            _ => null
        };
        return
        [
            .. DotNetTypeShapes.PropertiesOf(commandType)
                .Select(_ => _ with
                {
                    IsIdentifier = identity is not null && string.Equals(_.Name, LowerFirst(identity.Name), StringComparison.Ordinal)
                })
        ];
    }

    static IReadOnlyList<PropertyDefinition> QueryProperties(
        IMethodSymbol method,
        CritterStackSubjectResolver subjects,
        IEnumerable<PropertyDefinition>? compiledParameters = null)
    {
        var included = method.Parameters
            .Where(_ => !IsInfrastructureParameter(_.Type) && !IsSourceType(_.Type))
            .ToArray();

        // Authored enum parameters are kept as properties, but they never take the identifier
        // from the first non-enum parameter that was chosen before enums were admitted.
        var identifier = included.FirstOrDefault(_ => !IsAuthoredEnum(_.Type));
        var endpointParameters = included
            .Select(parameter => new PropertyDefinition
            {
                Name = LowerFirst(parameter.Name),
                Type = subjects.TypeReferenceFor(parameter.Type),
                IsIdentifier = SymbolEqualityComparer.Default.Equals(parameter, identifier)
            });
        return
        [
            .. endpointParameters
                .Concat(compiledParameters ?? [])
                .GroupBy(_ => _.Name, StringComparer.Ordinal)
                .Select(_ => _.First())
        ];
    }

    static IParameterSymbol? RequestParameter(IMethodSymbol method, DotNetProjectCompilation project) => method.Parameters.FirstOrDefault(_ =>
        MessageElementType(_.Type) is not null &&
        !IsInfrastructureParameter(_.Type) &&
        !IsAggregateParameter(_) &&
        !IsPersistenceBoundParameter(_) &&
        !WolverineEventStreams.IsEventStream(_.Type) &&
        !WolverineDcb.HasAttributedParameter(_, project));

    static IParameterSymbol? AggregateParameter(
        IMethodSymbol method,
        IParameterSymbol? request,
        bool aggregateWorkflow)
    {
        var attributed = method.Parameters.FirstOrDefault(_ =>
            IsAggregateParameter(_) && !WolverineEventStreams.IsEventStream(_.Type));
        return attributed ?? (aggregateWorkflow
            ? method.Parameters.FirstOrDefault(_ =>
                !SymbolEqualityComparer.Default.Equals(_, request) &&
                IsSourceType(_.Type) &&
                !IsInfrastructureParameter(_.Type) &&
                !WolverineEventStreams.IsEventStream(_.Type))
            : null);
    }

    static bool IsEntityParameter(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineEntityAttribute);

    static bool IsPersistenceBoundParameter(IParameterSymbol parameter) =>
        IsEntityParameter(parameter) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineFirstOrDefaultAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineQueryableAttribute);

    static bool IsAggregateParameter(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineWriteModelAttribute) ||
        DotNetSymbols.HasAttribute(parameter, WellKnownTypes.WolverineLegacyWriteAggregateAttribute) ||
        DotNetSymbols.HasAttribute(parameter, WellKnownTypes.WolverineHttpAggregateAttribute);

    static bool IsAggregateWorkflow(IMethodSymbol method) =>
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineLegacyAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method.ContainingType, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        method.ContainingType.Name.EndsWith("AggregateHandler", StringComparison.Ordinal) ||
        method.Parameters.Any(IsAggregateParameter);

    static IEnumerable<ITypeSymbol> AggregateReturnEvents(
        IMethodSymbol method,
        DotNetProjectCompilation project) =>
        WolverineReturnConsequences.Classify(
                method,
                project,
                isHttpEndpoint: false,
                aggregateWorkflow: true,
                hasEventStream: false)
            .Where(_ => _.Kind == WolverineReturnConsequenceKind.PersistedEvent)
            .Select(_ => _.Type);

    static string StateFeature(
        string requestName,
        INamedTypeSymbol? aggregateType,
        IReadOnlyList<WolverineStateBinding> streamBindings,
        INamedTypeSymbol? dcbModelType = null)
    {
        if (aggregateType is not null)
        {
            return aggregateType.Name;
        }

        if (dcbModelType is not null)
        {
            return dcbModelType.Name;
        }

        // Explicit parameter conventions own the primary state feature; additional
        // fetched streams must not move an otherwise identical handler's placement.
        var primaryBindings = streamBindings.Any(binding => binding.Parameter is not null)
            ? streamBindings.Where(binding => binding.Parameter is not null)
            : streamBindings;
        var models = primaryBindings
            .Select(_ => _.ModelType)
            .GroupBy(DotNetSubjectIds.MetadataName, StringComparer.Ordinal)
            .Select(_ => _.First())
            .ToArray();
        return models.Length == 1 ? models[0].Name : requestName;
    }

    static string? IdentityPropertyName(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var identity = IdentityProperty(command, aggregate);
        return identity is null ? null : LowerFirst(identity.Name);
    }

    static IPropertySymbol? IdentityProperty(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var properties = command.GetMembers().OfType<IPropertySymbol>().ToArray();
        var attributed = properties.FirstOrDefault(_ => _.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeType &&
            DotNetSubjectIds.MetadataName(attributeType.OriginalDefinition) == WellKnownTypes.JasperFxIdentityAttribute));
        if (attributed is not null)
        {
            return attributed;
        }

        if (aggregate is not null)
        {
            var aggregateId = properties.FirstOrDefault(_ => string.Equals(_.Name, $"{aggregate.Name}Id", StringComparison.OrdinalIgnoreCase));
            if (aggregateId is not null)
            {
                return aggregateId;
            }
        }

        return properties.FirstOrDefault(_ => string.Equals(_.Name, "Id", StringComparison.OrdinalIgnoreCase));
    }

    sealed class StateChangeShape
    {
        public required IMethodSymbol Method { get; init; }

        public required WolverineDcbDiscovery? Dcb { get; init; }

        public required bool AggregateWorkflow { get; init; }

        public required IReadOnlyList<WolverineStateBinding> ParameterBindings { get; init; }

        public required IReadOnlyList<WolverineStateBinding> FetchedBindings { get; init; }

        public required IReadOnlyList<WolverineStateBinding> StreamBindings { get; init; }

        public required WolverineEventStreamAppendDiscovery AppendDiscovery { get; init; }

        public required IParameterSymbol? Aggregate { get; init; }

        public required IReadOnlyList<ITypeSymbol> ReturnEvents { get; init; }

        public required ITypeSymbol[] BodyEvents { get; init; }

        public required IPropertySymbol? ReturnIdentity { get; init; }

        public required IReadOnlyList<WolverineProduction> SessionAppends { get; init; }

        public required IReadOnlyList<WolverineProduction> Yields { get; init; }

        public required IReadOnlyList<WolverineReturnConsequence> ReturnConsequences { get; init; }

        public required List<WolverineOutgoingMessageConsequence> OutgoingMessages { get; init; }

        public required IReadOnlyList<WolverineBusConsequence> BusConsequences { get; init; }

        public INamedTypeSymbol? AggregateType => Aggregate?.Type as INamedTypeSymbol;

        public WolverineProductionSources ToProductionSources(
            string commandName,
            SubjectId commandSubject,
            SourceRange? commandSource,
            bool hasCompoundValidation) => new(
                commandName,
                commandSubject,
                commandSource,
                ReturnEvents,
                ReturnIdentity,
                BodyEvents,
                SessionAppends,
                Yields,
                Dcb?.ImperativeEventTypes ?? [],
                [.. AppendDiscovery.Appends.SelectMany(_ => _.EventTypes)],
                hasCompoundValidation);
    }
}
