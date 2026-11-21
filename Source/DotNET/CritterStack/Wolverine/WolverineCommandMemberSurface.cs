// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// The declared members of a command type, with no persistence role applied. The source type is
/// retained so later symbol-based emission does not need to reconstruct it from lowered type strings.
/// </summary>
/// <param name="SourceType">The command type that declares the members.</param>
/// <param name="Properties">The ordered, unannotated declaration properties.</param>
internal sealed record WolverineCommandMemberSurface(
    INamedTypeSymbol SourceType,
    IReadOnlyList<PropertyDefinition> Properties);

/// <summary>
/// The inputs the identifier decision depends on, independent of the member declarations.
/// </summary>
/// <param name="CommandType">The command type whose identifier is selected.</param>
/// <param name="AggregateType">The aggregate the command targets, when any.</param>
/// <param name="ParameterBindings">The event stream parameter bindings of the handling method.</param>
/// <param name="HasFetchedStreams">Whether the handling method fetches event streams.</param>
/// <param name="LinkedStreamIdentity">The identity of a linked production stream, when any.</param>
internal sealed record WolverineCommandIdentityInput(
    INamedTypeSymbol CommandType,
    INamedTypeSymbol? AggregateType,
    IReadOnlyList<WolverineStateBinding> ParameterBindings,
    bool HasFetchedStreams,
    IPropertySymbol? LinkedStreamIdentity);

/// <summary>
/// The outcome of identifier selection for a command.
/// </summary>
/// <param name="Member">The selected identity member, or null when no member identifies the command.</param>
internal sealed record WolverineCommandIdentifier(IPropertySymbol? Member)
{
    /// <summary>
    /// Gets the selected identifier name in the lower-first form used for command properties.
    /// </summary>
    public string? Name => Member is null ? null : WolverineSagaFacts.LowerFirst(Member.Name);
}
