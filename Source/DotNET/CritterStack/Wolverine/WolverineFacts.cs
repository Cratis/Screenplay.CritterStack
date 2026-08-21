// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineDiscoveryResult(
    IReadOnlyList<GenerationFact> Facts,
    IReadOnlyList<GenerationDiagnostic> Diagnostics);

sealed record HttpEndpoint(IMethodSymbol Method, string Verb, string? Route);

static class WolverineFacts
{
    static readonly HashSet<string> _persistenceMethods =
    [
        "Append",
        "AppendMany",
        "AppendOne",
        "AppendOptimistic",
        "StartStream"
    ];

    static readonly HashSet<string> _handlerMethodNames =
    [
        "Handle",
        "HandleAsync",
        "Handles",
        "HandlesAsync",
        "Consume",
        "ConsumeAsync",
        "Consumes",
        "ConsumesAsync"
    ];

    public static WolverineDiscoveryResult Discover(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter)
    {
        if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is null)
        {
            return new([], []);
        }

        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var catalog = new DotNetArtifactCatalog(project.Compilation);
        foreach (var type in catalog.Types.Where(IsPublicSourceType))
        {
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(IsPublicSourceMethod))
            {
                var endpoint = EndpointFor(method);
                if (endpoint is not null)
                {
                    AnalyzeEndpoint(project, options, adapter, endpoint, facts, diagnostics);
                }
                else if (IsHandler(type, method))
                {
                    AnalyzeHandler(project, options, adapter, method, facts, diagnostics);
                }
            }
        }

        return new(facts, diagnostics);
    }

    static void AnalyzeEndpoint(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        HttpEndpoint endpoint,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        if (string.Equals(endpoint.Verb, "GET", StringComparison.Ordinal) ||
            string.Equals(endpoint.Verb, "QUERY", StringComparison.Ordinal))
        {
            AnalyzeQuery(project, options, adapter, endpoint, facts, diagnostics);
            return;
        }

        var method = endpoint.Method;
        var aggregateWorkflow = IsAggregateWorkflow(method);
        var request = RequestParameter(method);
        var aggregate = AggregateParameter(method, request, aggregateWorkflow);
        var commandSubject = request?.Type is INamedTypeSymbol requestType
            ? project.SubjectForType(requestType)
            : MethodSubject(project, method, "command");
        var commandName = request?.Type.Name ?? method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint");
        var file = evidence.Source?.Path;
        var properties = request?.Type is INamedTypeSymbol commandType
            ? CommandProperties(commandType, aggregate?.Type as INamedTypeSymbol)
            : RouteProperties(method);
        var placement = BehaviorPlacement(project, options, aggregate?.Type.Name ?? commandName, commandName, GenerationSliceKind.StateChange);
        var commandKey = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };

        facts.Add(Artifact($"wolverine:command:{commandSubject.Value}", commandKey, commandName, file, properties, evidence));
        facts.Add(Placement($"wolverine:placement:command:{commandSubject.Value}", commandKey, placement, evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for '{commandName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = commandSubject
        });

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            var commandTypeSymbol = request?.Type as INamedTypeSymbol;
            AddReadModelAndRelationship(project, adapter, commandSubject, commandTypeSymbol, aggregateType, facts, evidence);
            if (commandTypeSymbol is not null && IdentityProperty(commandTypeSymbol, aggregateType) is null)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.RouteIdentityOmitted,
                    Severity = GenerationDiagnosticSeverity.Warning,
                    Message = $"The '{aggregateType.Name}' identity for '{commandName}' comes from the HTTP route rather than a command property and cannot be marked as a Screenplay identifier",
                    Source = evidence.Source,
                    Subject = commandSubject
                });
            }
            if (commandTypeSymbol?.GetMembers().OfType<IPropertySymbol>().Any(_ => _.Name == "Version") == true)
            {
                diagnostics.Add(new GenerationDiagnostic
                {
                    Code = WolverineDiagnosticCodes.StreamVersionOmitted,
                    Severity = GenerationDiagnosticSeverity.Information,
                    Message = $"The expected stream version on '{commandName}' cannot be represented exactly by Screenplay concurrency",
                    Source = evidence.Source,
                    Subject = commandSubject
                });
            }
        }

        if (HasLifecycleValidation(method.ContainingType))
        {
            diagnostics.Add(new GenerationDiagnostic
            {
                Code = WolverineDiagnosticCodes.ValidationOmitted,
                Severity = GenerationDiagnosticSeverity.Warning,
                Message = $"Compound-handler validation for '{commandName}' is preserved by its handler file but cannot be declared as Screenplay validation",
                Source = evidence.Source,
                Subject = commandSubject
            });
        }

        var eventTypes = aggregateWorkflow && !HasEventStreamParameter(method)
            ? AggregateReturnEvents(method).ToArray()
            : [];
        var bodyEvents = PersistenceEvents(method, project).ToArray();
        foreach (var eventType in eventTypes.Concat(bodyEvents).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var declarative = eventTypes.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !bodyEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !HasLifecycleValidation(method.ContainingType);
            AddEventAndProduction(project, commandSubject, eventType, placement, evidence, declarative, facts);
        }

        AddDocumentDeletes(project, commandSubject, method, evidence, facts);
        AddOutgoingMessages(project, commandSubject, method, evidence, facts, diagnostics);
    }

    static void AnalyzeHandler(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        IMethodSymbol method,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var request = method.Parameters.FirstOrDefault(_ => IsSourceType(_.Type));
        if (request?.Type is not INamedTypeSymbol requestType)
        {
            return;
        }

        var aggregateWorkflow = IsAggregateWorkflow(method);
        var aggregate = AggregateParameter(method, request, aggregateWorkflow);
        var bodyEvents = PersistenceEvents(method, project).ToArray();
        var returnEvents = aggregateWorkflow && !HasEventStreamParameter(method)
            ? AggregateReturnEvents(method).ToArray()
            : [];
        var deletedDocuments = DocumentDeletes(method, project).ToArray();
        if (bodyEvents.Length == 0 && returnEvents.Length == 0 && deletedDocuments.Length == 0)
        {
            return;
        }

        var commandSubject = project.SubjectForType(requestType);
        var evidence = MethodEvidence(method, project, adapter, EvidenceStrength.Exact, "Wolverine message handler with persistence effects");
        var placement = BehaviorPlacement(project, options, aggregate?.Type.Name ?? requestType.Name, requestType.Name, GenerationSliceKind.StateChange);
        var key = new ArtifactKey { Subject = commandSubject, Kind = ArtifactKind.Command };
        facts.Add(Artifact(
            $"wolverine:command:{commandSubject.Value}",
            key,
            requestType.Name,
            evidence.Source?.Path,
            CommandProperties(requestType, aggregate?.Type as INamedTypeSymbol),
            evidence));
        facts.Add(Placement($"wolverine:placement:command:{commandSubject.Value}", key, placement, evidence));

        if (aggregate?.Type is INamedTypeSymbol aggregateType)
        {
            AddReadModelAndRelationship(project, adapter, commandSubject, requestType, aggregateType, facts, evidence);
        }

        foreach (var eventType in returnEvents.Concat(bodyEvents).Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var declarative = returnEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType)) &&
                              !bodyEvents.Any(_ => SymbolEqualityComparer.Default.Equals(_, eventType));
            AddEventAndProduction(project, commandSubject, eventType, placement, evidence, declarative, facts);
        }

        AddDocumentDeletes(project, commandSubject, method, evidence, facts);
        AddOutgoingMessages(project, commandSubject, method, evidence, facts, diagnostics);
    }

    static void AnalyzeQuery(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        AdapterIdentity adapter,
        HttpEndpoint endpoint,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        var (model, isCollection, isOptional) = WolverineReturnTypes.QueryModel(endpoint.Method.ReturnType);
        if (model is null || !IsSourceType(model))
        {
            return;
        }

        var evidence = MethodEvidence(endpoint.Method, project, adapter, EvidenceStrength.Exact, $"Wolverine HTTP {endpoint.Verb} endpoint");
        var querySubject = MethodSubject(project, endpoint.Method, "query");
        var queryName = endpoint.Method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
        var placement = BehaviorPlacement(project, options, model.Name, queryName, GenerationSliceKind.StateView);
        var queryKey = new ArtifactKey { Subject = querySubject, Kind = ArtifactKind.Query };
        var modelSubject = project.SubjectForType(model);
        var modelKey = new ArtifactKey { Subject = modelSubject, Kind = ArtifactKind.ReadModel };

        facts.Add(Artifact(
            $"wolverine:query:{querySubject.Value}",
            queryKey,
            queryName,
            evidence.Source?.Path,
            QueryProperties(endpoint.Method),
            evidence));
        facts.Add(Placement($"wolverine:placement:query:{querySubject.Value}", queryKey, placement, evidence));
        diagnostics.Add(new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
            Severity = GenerationDiagnosticSeverity.Information,
            Message = $"HTTP {endpoint.Verb} route '{endpoint.Route}' for query '{queryName}' is not represented by the current Screenplay language",
            Source = evidence.Source,
            Subject = querySubject
        });
        facts.Add(Artifact(
            $"wolverine:read-model:{modelSubject.Value}",
            modelKey,
            model.Name,
            SourceFileOf(model, project),
            DotNetTypeShapes.PropertiesOf(model),
            evidence));
        facts.Add(Placement(
            $"wolverine:placement:read-model:{modelSubject.Value}",
            modelKey,
            BehaviorPlacement(project, options, model.Name, model.Name, GenerationSliceKind.StateView),
            evidence with
            {
                Strength = EvidenceStrength.Heuristic,
                Explanation = "The returned model name provides the default Screenplay placement"
            }));
        facts.Add(Relationship(
            $"wolverine:returns:{querySubject.Value}:{modelSubject.Value}",
            querySubject,
            RelationshipKind.Returns,
            modelSubject,
            evidence,
            isCollection: isCollection,
            isOptional: isOptional));
    }

    static void AddReadModelAndRelationship(
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        SubjectId commandSubject,
        INamedTypeSymbol? commandType,
        INamedTypeSymbol aggregateType,
        List<GenerationFact> facts,
        Evidence evidence)
    {
        var aggregateSubject = project.SubjectForType(aggregateType);
        facts.Add(Artifact(
            $"wolverine:read-model:{aggregateSubject.Value}",
            new ArtifactKey { Subject = aggregateSubject, Kind = ArtifactKind.ReadModel },
            aggregateType.Name,
            SourceFileOf(aggregateType, project),
            DotNetTypeShapes.PropertiesOf(aggregateType),
            DotNetSource.EvidenceFor(
                aggregateType,
                adapter,
                EvidenceStrength.Conventional,
                project.SourceRoot,
                "Wolverine loads this model as aggregate decision state")));
        facts.Add(Relationship(
            $"wolverine:reads:{commandSubject.Value}:{aggregateSubject.Value}",
            commandSubject,
            RelationshipKind.Reads,
            aggregateSubject,
            evidence,
            sourceMember: commandType is null ? null : IdentityPropertyName(commandType, aggregateType)));
    }

    static void AddEventAndProduction(
        DotNetProjectCompilation project,
        SubjectId commandSubject,
        INamedTypeSymbol eventType,
        ArtifactPlacement placement,
        Evidence evidence,
        bool declarative,
        List<GenerationFact> facts)
    {
        var eventSubject = project.SubjectForType(eventType);
        var eventKey = new ArtifactKey { Subject = eventSubject, Kind = ArtifactKind.Event };
        facts.Add(Artifact(
            $"wolverine:event:{eventSubject.Value}",
            eventKey,
            eventType.Name,
            SourceFileOf(eventType, project),
            DotNetTypeShapes.PropertiesOf(eventType),
            evidence));
        facts.Add(Placement($"wolverine:placement:event:{eventSubject.Value}:{commandSubject.Value}", eventKey, placement, evidence));
        facts.Add(Relationship(
            $"wolverine:produces:{commandSubject.Value}:{eventSubject.Value}",
            commandSubject,
            RelationshipKind.Produces,
            eventSubject,
            evidence,
            discriminator: declarative ? "declarative" : "imperative"));
    }

    static void AddOutgoingMessages(
        DotNetProjectCompilation project,
        SubjectId sourceSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts,
        List<GenerationDiagnostic> diagnostics)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax declaration)
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var collection in declaration.DescendantNodes().OfType<CollectionExpressionSyntax>())
            {
                if (semanticModel.GetTypeInfo(collection).ConvertedType is not INamedTypeSymbol collectionType ||
                    DotNetSubjectIds.MetadataName(collectionType.OriginalDefinition) != WellKnownTypes.WolverineOutgoingMessages)
                {
                    continue;
                }

                foreach (var element in collection.Elements.OfType<ExpressionElementSyntax>())
                {
                    if (semanticModel.GetTypeInfo(element.Expression).Type is not INamedTypeSymbol elementType)
                    {
                        continue;
                    }

                    var messageType = elementType.Name == "DeliveryMessage" && elementType.TypeArguments.FirstOrDefault() is INamedTypeSymbol delivered
                        ? delivered
                        : elementType;
                    if (!IsEventPayloadType(messageType))
                    {
                        continue;
                    }

                    var messageSubject = project.SubjectForType(messageType);
                    var delayed = element.Expression.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(_ => _.Expression.ToString().Contains("Delayed", StringComparison.Ordinal));
                    facts.Add(Artifact(
                        $"wolverine:message:{messageSubject.Value}",
                        new ArtifactKey { Subject = messageSubject, Kind = ArtifactKind.Message },
                        messageType.Name,
                        SourceFileOf(messageType, project),
                        DotNetTypeShapes.PropertiesOf(messageType),
                        evidence));
                    facts.Add(Relationship(
                        $"wolverine:cascades:{sourceSubject.Value}:{messageSubject.Value}",
                        sourceSubject,
                        RelationshipKind.Cascades,
                        messageSubject,
                        evidence,
                        discriminator: delayed ? "delayed" : "immediate"));

                    if (delayed)
                    {
                        diagnostics.Add(new GenerationDiagnostic
                        {
                            Code = WolverineDiagnosticCodes.DelayedMessageOmitted,
                            Severity = GenerationDiagnosticSeverity.Warning,
                            Message = $"Handler '{method.ContainingType.Name}.{method.Name}' dispatches '{messageType.Name}' after a delay, which the current Screenplay language cannot represent",
                            Source = evidence.Source,
                            Subject = sourceSubject
                        });
                    }
                }
            }
        }
    }

    static void AddDocumentDeletes(
        DotNetProjectCompilation project,
        SubjectId commandSubject,
        IMethodSymbol method,
        Evidence evidence,
        List<GenerationFact> facts)
    {
        foreach (var documentType in DocumentDeletes(method, project))
        {
            var documentSubject = project.SubjectForType(documentType);
            facts.Add(Artifact(
                $"wolverine:document:{documentSubject.Value}",
                new ArtifactKey { Subject = documentSubject, Kind = ArtifactKind.Document },
                documentType.Name,
                SourceFileOf(documentType, project),
                DotNetTypeShapes.PropertiesOf(documentType),
                evidence));
            facts.Add(Relationship(
                $"wolverine:deletes:{commandSubject.Value}:{documentSubject.Value}",
                commandSubject,
                RelationshipKind.Deletes,
                documentSubject,
                evidence));
        }
    }

    static IEnumerable<INamedTypeSymbol> DocumentDeletes(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax declaration)
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invoked &&
                    invoked.Name == "Delete" &&
                    IsPersistenceNamespace(invoked) &&
                    invoked.TypeArguments.FirstOrDefault() is INamedTypeSymbol documentType)
                {
                    yield return documentType;
                }
            }
        }
    }

    static IReadOnlyList<PropertyDefinition> CommandProperties(
        INamedTypeSymbol commandType,
        INamedTypeSymbol? aggregateType)
    {
        var identity = aggregateType is null ? IdentityProperty(commandType, null) : IdentityProperty(commandType, aggregateType);
        return
        [
            .. DotNetTypeShapes.PropertiesOf(commandType)
                .Select(_ => _ with
                {
                    IsIdentifier = identity is not null && string.Equals(_.Name, LowerFirst(identity.Name), StringComparison.Ordinal)
                })
        ];
    }

    static IReadOnlyList<PropertyDefinition> QueryProperties(IMethodSymbol method)
    {
        var parameters = method.Parameters
            .Where(_ => !IsInfrastructureParameter(_.Type) && !IsSourceType(_.Type))
            .ToArray();
        return
        [
            .. parameters.Select((parameter, index) => new PropertyDefinition
            {
                Name = LowerFirst(parameter.Name),
                Type = DotNetTypeShapes.TypeReferenceFor(parameter.Type),
                IsIdentifier = index == 0
            })
        ];
    }

    static IReadOnlyList<PropertyDefinition> RouteProperties(IMethodSymbol method) => QueryProperties(method);

    static IParameterSymbol? RequestParameter(IMethodSymbol method) => method.Parameters.FirstOrDefault(_ =>
        IsSourceType(_.Type) && !IsAggregateParameter(_));

    static IParameterSymbol? AggregateParameter(
        IMethodSymbol method,
        IParameterSymbol? request,
        bool aggregateWorkflow)
    {
        var attributed = method.Parameters.FirstOrDefault(IsAggregateParameter);
        return attributed ?? (aggregateWorkflow
            ? method.Parameters.FirstOrDefault(_ => !SymbolEqualityComparer.Default.Equals(_, request) && IsSourceType(_.Type))
            : null);
    }

    static bool IsAggregateParameter(IParameterSymbol parameter) =>
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineWriteModelAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineLegacyWriteAggregateAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(parameter, WellKnownTypes.WolverineHttpAggregateAttribute);

    static bool IsAggregateWorkflow(IMethodSymbol method) =>
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method, WellKnownTypes.WolverineLegacyAggregateHandlerAttribute) ||
        DotNetSymbols.HasAttributeAssignableTo(method.ContainingType, WellKnownTypes.WolverineAggregateHandlerAttribute) ||
        method.ContainingType.Name.EndsWith("AggregateHandler", StringComparison.Ordinal) ||
        method.Parameters.Any(IsAggregateParameter);

    static bool HasEventStreamParameter(IMethodSymbol method) => method.Parameters.Any(_ =>
        _.Type is INamedTypeSymbol named && named.IsGenericType && named.Name == "IEventStream");

    static IEnumerable<ITypeSymbol> AggregateReturnEvents(IMethodSymbol method) =>
        WolverineReturnTypes.CreatedValues(method)
            .Where(_ => !WolverineReturnTypes.IsSpecialReturn(_) && IsEventPayloadType(_));

    static IEnumerable<ITypeSymbol> PersistenceEvents(IMethodSymbol method, DotNetProjectCompilation project)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax declaration)
            {
                continue;
            }

            var semanticModel = project.Compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol invoked ||
                    !_persistenceMethods.Contains(invoked.Name) ||
                    !IsPersistenceNamespace(invoked))
                {
                    continue;
                }

                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    if (semanticModel.GetTypeInfo(argument.Expression).Type is INamedTypeSymbol eventType &&
                        IsEventPayloadType(eventType) &&
                        !SymbolEqualityComparer.Default.Equals(eventType, method.ContainingType))
                    {
                        yield return eventType;
                    }
                }
            }

            foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddAssignmentExpression) ||
                    semanticModel.GetTypeInfo(assignment.Left).Type is not INamedTypeSymbol collection ||
                    !IsEventCollection(collection) ||
                    semanticModel.GetTypeInfo(assignment.Right).Type is not INamedTypeSymbol eventType ||
                    !IsEventPayloadType(eventType))
                {
                    continue;
                }

                yield return eventType;
            }

            foreach (var collectionExpression in declaration.DescendantNodes().OfType<CollectionExpressionSyntax>())
            {
                if (semanticModel.GetTypeInfo(collectionExpression).ConvertedType is not INamedTypeSymbol collectionType ||
                    !IsEventCollection(collectionType))
                {
                    continue;
                }

                foreach (var element in collectionExpression.Elements.OfType<ExpressionElementSyntax>())
                {
                    if (semanticModel.GetTypeInfo(element.Expression).Type is INamedTypeSymbol eventType &&
                        IsEventPayloadType(eventType))
                    {
                        yield return eventType;
                    }
                }
            }
        }
    }

    static bool IsEventCollection(INamedTypeSymbol type)
    {
        var metadataName = DotNetSubjectIds.MetadataName(type.OriginalDefinition);
        return metadataName == WellKnownTypes.WolverineEvents ||
               metadataName == WellKnownTypes.WolverineEventsToAppend;
    }

    static bool IsPersistenceNamespace(IMethodSymbol method)
    {
        var candidate = method.ReducedFrom ?? method;
        var @namespace = candidate.ContainingNamespace.ToDisplayString();
        return @namespace.StartsWith("Marten", StringComparison.Ordinal) ||
               @namespace.StartsWith("JasperFx.Events", StringComparison.Ordinal) ||
               @namespace.StartsWith("Wolverine.Marten", StringComparison.Ordinal) ||
               candidate.ContainingType.Name == "IEventStream";
    }

    static string? IdentityPropertyName(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var identity = IdentityProperty(command, aggregate);
        return identity is null ? null : LowerFirst(identity.Name);
    }

    static IPropertySymbol? IdentityProperty(INamedTypeSymbol command, INamedTypeSymbol? aggregate)
    {
        var properties = command.GetMembers().OfType<IPropertySymbol>().ToArray();
        var attributed = properties.FirstOrDefault(_ => _.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "IdentityAttribute"));
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

    static HttpEndpoint? EndpointFor(IMethodSymbol method)
    {
        var attribute = method.GetAttributes().FirstOrDefault(_ =>
            _.AttributeClass is not null &&
            (DotNetSymbols.IsOrInheritsFrom(_.AttributeClass, WellKnownTypes.WolverineHttpMethodAttribute) ||
             (_.AttributeClass.ContainingNamespace.ToDisplayString() == "Wolverine.Http" &&
              _.AttributeClass.Name.StartsWith("Wolverine", StringComparison.Ordinal) &&
              _.AttributeClass.Name.EndsWith("Attribute", StringComparison.Ordinal))));
        if (attribute?.AttributeClass is null)
        {
            return null;
        }

        var verb = attribute.AttributeClass.Name
            .Replace("Wolverine", string.Empty, StringComparison.Ordinal)
            .Replace("Attribute", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        var route = attribute.ConstructorArguments.FirstOrDefault().Value as string;
        return new(method, verb, route);
    }

    static bool HasLifecycleValidation(INamedTypeSymbol type) => type.GetMembers()
        .OfType<IMethodSymbol>()
        .Any(_ => string.Equals(_.Name, "Validate", StringComparison.Ordinal) ||
                  string.Equals(_.Name, "ValidateAsync", StringComparison.Ordinal));

    static bool IsHandler(INamedTypeSymbol type, IMethodSymbol method) =>
        (type.Name.EndsWith("Handler", StringComparison.Ordinal) ||
         type.Name.EndsWith("Consumer", StringComparison.Ordinal) ||
         DotNetSymbols.Implements(type, "Wolverine.IWolverineHandler") ||
         DotNetSymbols.HasAttribute(type, "Wolverine.WolverineHandlerAttribute")) &&
        (_handlerMethodNames.Contains(method.Name) || DotNetSymbols.HasAttribute(method, "Wolverine.WolverineHandlerAttribute"));

    static bool IsPublicSourceType(INamedTypeSymbol type) =>
        type.DeclaredAccessibility == Accessibility.Public && type.Locations.Any(_ => _.IsInSource);

    static bool IsPublicSourceMethod(IMethodSymbol method) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.MethodKind == MethodKind.Ordinary &&
        method.Locations.Any(_ => _.IsInSource);

    static bool IsSourceType(ITypeSymbol type) =>
        type is INamedTypeSymbol named && named.Locations.Any(_ => _.IsInSource);

    static bool IsEventPayloadType(ITypeSymbol type) =>
        type is INamedTypeSymbol named &&
        type.SpecialType == SpecialType.None &&
        !WolverineReturnTypes.IsSpecialReturn(named) &&
        !DotNetSubjectIds.MetadataName(named.OriginalDefinition).StartsWith("System.", StringComparison.Ordinal);

    static bool IsInfrastructureParameter(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None || type is not INamedTypeSymbol named)
        {
            return type.SpecialType != SpecialType.None;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        return metadataName.StartsWith("Marten.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Wolverine.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
               metadataName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ||
               metadataName == "System.Threading.CancellationToken";
    }

    static ArtifactFact Artifact(
        string id,
        ArtifactKey key,
        string name,
        string? file,
        IReadOnlyList<PropertyDefinition> properties,
        Evidence evidence) => new()
    {
        Id = new FactId { Value = id },
        Subject = key.Subject,
        Definition = new ArtifactDefinition
        {
            Key = key,
            Name = name,
            File = file,
            Properties = properties
        },
        Evidence = evidence
    };

    static ArtifactPlacementFact Placement(
        string id,
        ArtifactKey artifact,
        ArtifactPlacement placement,
        Evidence evidence) => new()
    {
        Id = new FactId { Value = id },
        Subject = artifact.Subject,
        Artifact = artifact,
        Placement = placement,
        Evidence = evidence
    };

    static RelationshipFact Relationship(
        string id,
        SubjectId source,
        RelationshipKind kind,
        SubjectId target,
        Evidence evidence,
        string? sourceMember = null,
        string? discriminator = null,
        bool isCollection = false,
        bool isOptional = false) => new()
    {
        Id = new FactId { Value = id },
        Subject = source,
        Definition = new RelationshipDefinition
        {
            Key = new RelationshipKey
            {
                Kind = kind,
                Source = source,
                Target = target,
                Discriminator = discriminator
            },
            SourceMember = sourceMember,
            IsCollection = isCollection,
            IsOptional = isOptional
        },
        Evidence = evidence
    };

    static ArtifactPlacement BehaviorPlacement(
        DotNetProjectCompilation project,
        DotNetAdapterOptions options,
        string feature,
        string slice,
        GenerationSliceKind kind) => new()
    {
        Module = ScreenplayNames.Declaration(options.Module ?? project.Name),
        Features = [feature],
        Slice = slice,
        SliceKind = kind
    };

    static Evidence MethodEvidence(
        IMethodSymbol method,
        DotNetProjectCompilation project,
        AdapterIdentity adapter,
        EvidenceStrength strength,
        string explanation) =>
        DotNetSource.EvidenceFor(method, adapter, strength, project.SourceRoot, explanation);

    static SubjectId MethodSubject(DotNetProjectCompilation project, IMethodSymbol method, string role) => new()
    {
        Value = $"{project.SubjectForType(method.ContainingType).Value}#{role}:{method.MetadataName}"
    };

    static string? SourceFileOf(ISymbol symbol, DotNetProjectCompilation project) =>
        DotNetSource.EvidenceFor(symbol, new AdapterIdentity { Id = "source", Version = "1" }, EvidenceStrength.Exact, project.SourceRoot).Source?.Path;

    static string LowerFirst(string value) => value.Length == 0
        ? value
        : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
}
