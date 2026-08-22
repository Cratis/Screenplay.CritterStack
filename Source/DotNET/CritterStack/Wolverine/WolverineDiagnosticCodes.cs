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

    /// <summary>
    /// HTTP verb, route, response, or binding metadata could not be represented by the current Screenplay language.
    /// </summary>
    public const string HttpMetadataOmitted = "WOLVERINE0002";

    /// <summary>
    /// Wolverine optimistic/exclusive stream version semantics could not be represented exactly.
    /// </summary>
    public const string StreamVersionOmitted = "WOLVERINE0003";

    /// <summary>
    /// The aggregate identity comes from the HTTP route rather than a command property.
    /// </summary>
    public const string RouteIdentityOmitted = "WOLVERINE0004";

    /// <summary>
    /// Compound-handler validation was preserved through a handler file but not declared as Screenplay validation.
    /// </summary>
    public const string ValidationOmitted = "WOLVERINE0005";

    /// <summary>
    /// A direct Wolverine send, publish, request/reply, or delivery option could not be represented by Screenplay.
    /// </summary>
    public const string DirectMessageDeliveryOmitted = "WOLVERINE0006";

    /// <summary>
    /// Authored Wolverine handler discovery configuration could not be resolved exactly from source.
    /// </summary>
    public const string HandlerDiscoveryConfigurationUnresolved = "WOLVERINE0007";
}
