// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

static class WolverineReturnTypes
{
    static readonly HashSet<string> _nonEventTypes =
    [
        "Microsoft.AspNetCore.Http.IResult",
        WellKnownTypes.WolverineEvents,
        WellKnownTypes.WolverineEventsToAppend,
        WellKnownTypes.WolverineOutgoingMessages,
        WellKnownTypes.WolverineStartStream,
        WellKnownTypes.WolverineSideEffect,
        "Wolverine.Http.IResponseAware",
        "Wolverine.Http.IWolverineReturnType",
        "Wolverine.Http.UpdatedAggregate",
        "Wolverine.Marten.UpdatedAggregate",
        "Wolverine.Persistence.EventSourcing.UpdatedAggregate"
    ];

    public static IReadOnlyList<ITypeSymbol> CreatedValues(IMethodSymbol method)
    {
        var returnType = UnwrapTask(method.ReturnType);
        if (returnType is null)
        {
            return [];
        }

        if (returnType is INamedTypeSymbol tuple && tuple.IsTupleType)
        {
            return [.. tuple.TupleElements.Select(_ => _.Type)];
        }

        return [returnType];
    }

    public static ITypeSymbol? UnwrapTask(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return null;
        }

        if (type is not INamedTypeSymbol named)
        {
            return type;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        return metadataName switch
        {
            "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask" => null,
            "System.Threading.Tasks.Task`1" or "System.Threading.Tasks.ValueTask`1" => named.TypeArguments[0],
            _ => type
        };
    }

    public static bool IsSpecialReturn(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var metadataName = DotNetSubjectIds.MetadataName(named.OriginalDefinition);
        if (named.Name.StartsWith("UpdatedAggregate", StringComparison.Ordinal) ||
            named.Name.StartsWith("CreationResponse", StringComparison.Ordinal) ||
            _nonEventTypes.Contains(metadataName))
        {
            return true;
        }

        return named.AllInterfaces.Any(_ => _nonEventTypes.Contains(DotNetSubjectIds.MetadataName(_.OriginalDefinition)));
    }

    public static (INamedTypeSymbol? Model, bool IsCollection, bool IsOptional) QueryModel(ITypeSymbol returnType)
    {
        var unwrapped = UnwrapTask(returnType);
        if (unwrapped is null)
        {
            return (null, false, false);
        }

        var optional = unwrapped.NullableAnnotation == NullableAnnotation.Annotated;
        if (unwrapped is INamedTypeSymbol nullable && nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            unwrapped = nullable.TypeArguments[0];
            optional = true;
        }

        if (unwrapped is IArrayTypeSymbol array)
        {
            return (array.ElementType as INamedTypeSymbol, true, optional);
        }

        if (unwrapped is INamedTypeSymbol named)
        {
            var enumerable = named.AllInterfaces
                .Concat([named])
                .FirstOrDefault(_ =>
                    _.IsGenericType &&
                    DotNetSubjectIds.MetadataName(_.OriginalDefinition) == "System.Collections.Generic.IEnumerable`1");
            if (enumerable is not null)
            {
                return (enumerable.TypeArguments[0] as INamedTypeSymbol, true, optional);
            }

            return (named, false, optional);
        }

        return (null, false, optional);
    }
}
