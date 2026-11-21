// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

internal static partial class WolverineCompoundStages
{
    static bool IsException(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.ToDisplayString(), "System.Exception", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    static bool MatchesHandler(
        IMethodSymbol handler,
        ITypeSymbol requestType,
        WolverineCompoundStage stage)
    {
        if (stage.IsFailurePath)
        {
            return stage.Method.Parameters.Any(_ => SymbolEqualityComparer.Default.Equals(_.Type, requestType));
        }

        if (stage.IsExplicit)
        {
            return true;
        }

        if (!string.Equals(stage.StageKind, "load", StringComparison.Ordinal))
        {
            return stage.Method.Parameters.FirstOrDefault()?.Type is { } stageRequestType &&
                   SymbolEqualityComparer.Default.Equals(stageRequestType, requestType);
        }

        var handlerParameterTypes = handler.Parameters.Select(_ => _.Type).ToArray();
        var consumesHandlerData = stage.Method.Parameters.Any(parameter =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, parameter.Type)));
        var suppliesHandlerData = WolverineReturnTypes.CreatedValues(stage.Method).Any(returnType =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, returnType)));

        return consumesHandlerData && suppliesHandlerData;
    }

    static bool MatchesValidationParameters(IMethodSymbol handler, IMethodSymbol validation)
    {
        if (validation.Parameters.Length == 0)
        {
            return true;
        }

        var handlerParameterTypes = handler.Parameters.Select(_ => _.Type).ToArray();
        return validation.Parameters.Any(parameter =>
            handlerParameterTypes.Any(handlerType => SymbolEqualityComparer.Default.Equals(handlerType, parameter.Type)));
    }
}
