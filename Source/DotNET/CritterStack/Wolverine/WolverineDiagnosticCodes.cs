// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Defines stable diagnostics produced while analyzing Wolverine source.
/// </summary>
public static class WolverineDiagnosticCodes
{
    /// <summary>
    /// A one-shot delayed message dispatch could not be represented by the current Screenplay language.
    /// </summary>
    public const string DelayedMessageOmitted = "WOLVERINE0001";
}
