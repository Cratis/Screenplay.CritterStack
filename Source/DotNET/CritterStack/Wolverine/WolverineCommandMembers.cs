// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Separates command member declarations from identifier selection, then joins them into the
/// compatibility property list used by the legacy aggregate command fact.
/// </summary>
internal static class WolverineCommandMembers
{
    /// <summary>
    /// Describes the declared members of a command type without any persistence role.
    /// </summary>
    /// <param name="commandType">The command type.</param>
    /// <returns>The declaration surface.</returns>
    internal static WolverineCommandMemberSurface Describe(INamedTypeSymbol commandType) =>
        new(commandType, DotNetTypeShapes.PropertiesOf(commandType));

    /// <summary>
    /// Selects the identifier of a command: a linked production identity first, then, by parameter
    /// bindings, no identity when only fetched streams apply, the conventional identity when nothing
    /// is bound, or the identity of a single model-loading binding.
    /// </summary>
    /// <param name="input">The identifier decision inputs.</param>
    /// <returns>The selected identifier.</returns>
    internal static WolverineCommandIdentifier SelectIdentifier(WolverineCommandIdentityInput input) =>
        new(input.LinkedStreamIdentity ?? SelectBoundIdentity(input));

    /// <summary>
    /// Materializes the compatibility properties from a declaration surface and a selected identifier.
    /// The surface is not modified.
    /// </summary>
    /// <param name="surface">The declaration surface.</param>
    /// <param name="identifier">The selected identifier.</param>
    /// <returns>The ordered properties with the identifier flag applied by name.</returns>
    internal static IReadOnlyList<PropertyDefinition> Materialize(
        WolverineCommandMemberSurface surface,
        WolverineCommandIdentifier identifier) =>
    [
        .. surface.Properties.Select(_ => _ with
        {
            IsIdentifier = identifier.Name is not null && string.Equals(_.Name, identifier.Name, StringComparison.Ordinal)
        })
    ];

    static IPropertySymbol? SelectBoundIdentity(WolverineCommandIdentityInput input) => input.ParameterBindings.Count switch
    {
        0 when input.AggregateType is null && input.HasFetchedStreams => null,
        0 => WolverineFacts.IdentityProperty(input.CommandType, input.AggregateType),
        1 when input.ParameterBindings[0].LoadsModel => input.ParameterBindings[0].IdentityMember as IPropertySymbol,
        _ => null
    };
}
