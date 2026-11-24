// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineQueryResult(INamedTypeSymbol Model, bool IsCollection, bool IsOptional);

sealed record WolverineQueryObservation(HttpEndpoint Endpoint, SubjectId Subject, string Name, WolverineQueryResult Result);

internal static partial class WolverineFacts
{
    public static WolverineQueryArtifacts QueryArtifacts(DotNetProjectCompilation project, DotNetAnalysisContext context, DotNetAdapterOptions options, AdapterIdentity adapter) =>
        new(project, context, options, adapter);

    public static WolverineQueryObservation? ObserveQuery(DotNetProjectCompilation project, HttpEndpoint endpoint)
    {
        var (model, isCollection, isOptional) = WolverineReturnTypes.QueryModel(endpoint.Method.ReturnType);
        if (model is null || !IsQueryModel(model, project))
        {
            return null;
        }

        var method = endpoint.Method;
        var name = method.ContainingType.Name.EndsWith("Endpoints", StringComparison.Ordinal)
            ? method.Name
            : method.ContainingType.Name.Replace("Endpoint", string.Empty, StringComparison.Ordinal);
        return new(endpoint, MethodSubject(project, method, "query"), name, new(model, isCollection, isOptional));
    }

    static bool IsQueryModel(INamedTypeSymbol model, DotNetProjectCompilation project) =>
        IsSourceType(model) && !WolverineSagaTypes.IsSagaState(model, project);
}
