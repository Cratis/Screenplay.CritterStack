// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.Marten;

/// <summary>
/// Defines stable diagnostics produced while analyzing Marten source.
/// </summary>
public static class MartenDiagnosticCodes
{
    /// <summary>
    /// Multi-stream grouping semantics were approximated by an event reducer.
    /// </summary>
    public const string MultiStreamGroupingOmitted = "MARTEN0001";

    /// <summary>
    /// An arbitrary event projection could not be represented.
    /// </summary>
    public const string EventProjectionOmitted = "MARTEN0002";

    /// <summary>
    /// An ordinary Marten document is directly persisted or queried but cannot be declared by Screenplay.
    /// </summary>
    public const string DocumentModelOmitted = "MARTEN0003";

    /// <summary>
    /// A configured projection lifecycle could not be represented.
    /// </summary>
    public const string ProjectionLifecycleOmitted = "MARTEN0004";
}
