// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.Marten;

sealed partial class MartenEventSchemaConfigurationDiscovery
{
    static readonly HashSet<string> _eventAppendModes = ["Quick", "QuickWithServerTimestamps", "Rich"];
    static readonly HashSet<string> _streamIdentities = ["AsGuid", "AsString"];

    static WireSetting? WireSettingFor(string propertyName) => propertyName switch
    {
        "AppendMode" => new(WellKnownTypes.JasperFxEventAppendMode, _eventAppendModes, "event append-mode"),
        "StreamIdentity" => new(WellKnownTypes.JasperFxStreamIdentity, _streamIdentities, "stream-identity"),
        _ => null
    };

    static string WireMessage(string label, string? value) => value is null
        ? $"Marten has an authored {label} declaration with a computed or otherwise unresolved value; no effective setting was guessed"
        : $"Marten has an authored {label} declaration '{value}'";

    static bool IsBinarySerializerRegistration(IMethodSymbol candidate) =>
        string.Equals(candidate.Name, "UseBinarySerializer", StringComparison.Ordinal) &&
        candidate.TypeParameters.Length == 1 &&
        IsEventStoreOptions(candidate.ContainingType);

    void DiscoverBinarySerializerConfiguration(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (!IsBinarySerializerRegistration((method.ReducedFrom ?? method).OriginalDefinition))
        {
            return;
        }

        var eventType = NamedType(method.TypeArguments.SingleOrDefault());
        var serializerExpression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var serializerType = NamedType(serializerExpression is null ? null : semanticModel.GetTypeInfo(serializerExpression).Type);
        if (eventType is null || serializerType is null)
        {
            diagnostics.Add(EventConfiguration(
                eventType,
                "Marten has an authored per-event binary serializer declaration whose event or serializer type could not be resolved safely",
                invocation.GetLocation(),
                GenerationDiagnosticOutcome.Unknown));
            return;
        }

        diagnostics.Add(EventConfiguration(
            eventType,
            $"Marten event type '{eventType.Name}' has an authored binary serializer declaration using '{serializerType.Name}'",
            invocation.GetLocation()));
    }

    void DiscoverWireConfiguration(AssignmentExpressionSyntax assignment)
    {
        if (semanticModel.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property ||
            !IsEventStoreOptions(property.ContainingType) ||
            WireSettingFor(property.Name) is not { } setting)
        {
            return;
        }

        var value = EnumMember(assignment.Right, semanticModel, setting.EnumType, setting.AdmittedValues);
        diagnostics.Add(EventConfiguration(
            null,
            WireMessage(setting.Label, value),
            assignment.GetLocation(),
            value is null ? GenerationDiagnosticOutcome.Unknown : GenerationDiagnosticOutcome.Unsupported));
    }

    sealed record WireSetting(string EnumType, HashSet<string> AdmittedValues, string Label);
}
