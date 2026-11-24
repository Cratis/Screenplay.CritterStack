// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineBehaviorIdentity(SubjectId Subject, string Name)
{
    public static WolverineBehaviorIdentity ForMethod(DotNetProjectCompilation project, IMethodSymbol method, string role) => new(
        new() { Value = $"{DotNetMethodIdentity.SubjectFor(project, method).Value}:{role}" },
        DotNetMethodIdentity.DisplayName(method));
}
