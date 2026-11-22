// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineMiddlewareRegistration(
    INamedTypeSymbol? Type,
    INamedTypeSymbol Owner,
    bool IsHttpEndpoint,
    bool IsResolved,
    SourceRange? Source);

/// <summary>
/// The immutable middleware registrations discovered for one project in one analysis pass.
/// </summary>
/// <param name="registrations">The discovered middleware registrations.</param>
sealed class WolverineMiddlewareRegistrations(IEnumerable<WolverineMiddlewareRegistration> registrations)
{
    readonly ImmutableArray<WolverineMiddlewareRegistration> _registrations = [.. registrations];

    public IEnumerable<WolverineMiddlewareRegistration> Unresolved => _registrations.Where(_ => !_.IsResolved);

    public IEnumerable<WolverineMiddlewareApplication> ApplicationsFor(bool isHttpEndpoint) =>
        _registrations.Where(_ => _.IsResolved && _.IsHttpEndpoint == isHttpEndpoint)
            .Select(_ => new WolverineMiddlewareApplication(_.Type!, _.Source));
}
