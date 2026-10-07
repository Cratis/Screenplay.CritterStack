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

    /// <summary>
    /// An enum used by an emitted artifact has no named values, or has values that collide or are not valid identifiers
    /// after Screenplay naming, so it is not declared as a concept and its uses keep the plain type name without a concept reference.
    /// </summary>
    public const string EnumValuesUnsupported = "CRITTERSTACK0002";

    /// <summary>
    /// An enum used by an emitted artifact shares its concept name with another source subject's concept, or with a different
    /// type used without a concept reference, so the enum is not declared as a concept and its uses keep the plain type name
    /// without a concept reference; non-enum concepts are kept.
    /// </summary>
    public const string EnumConceptNameConflict = "CRITTERSTACK0003";
}
