// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineFailureStageObservation(
    IMethodSymbol Method,
    Evidence Evidence,
    GenerationDiagnostic OmissionReason);
