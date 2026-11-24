// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay.Wolverine;

sealed record WolverineCompoundStageSource(
    DotNetProjectCompilation Project,
    WolverineMiddlewareRegistrations MiddlewareRegistrations);
