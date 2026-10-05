// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay;

/// <summary>
/// Defines stable diagnostics produced while analyzing source shared by the Marten and Wolverine conventions.
/// </summary>
public static class CritterStackDiagnosticCodes
{
    /// <summary>
    /// A [Flags] enum used by an emitted artifact permits value combinations that an enumeration concept cannot represent,
    /// so it is not declared as a concept and its uses keep the plain type name without a concept reference.
    /// </summary>
    public const string FlagsEnumOmitted = "CRITTERSTACK0001";
}
