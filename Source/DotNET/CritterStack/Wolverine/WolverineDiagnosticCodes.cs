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
    /// Exact handler validation behavior could not be represented by the current generation contracts.
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

    /// <summary>
    /// Exact Wolverine validation policy activation could not be represented by the current generation contracts.
    /// </summary>
    public const string ValidationPolicyOmitted = "WOLVERINE0008";

    /// <summary>
    /// Authored Wolverine validation configuration could not be resolved exactly from source.
    /// </summary>
    public const string ValidationConfigurationUnresolved = "WOLVERINE0009";

    /// <summary>
    /// Exact ASP.NET or Wolverine HTTP authorization behavior could not be represented by the current generation contracts.
    /// </summary>
    public const string AuthorizationOmitted = "WOLVERINE0010";

    /// <summary>
    /// Authored Wolverine HTTP authorization configuration could not be resolved exactly from source.
    /// </summary>
    public const string AuthorizationConfigurationUnresolved = "WOLVERINE0011";

    /// <summary>
    /// Parameter-specific metadata for a handler with multiple event streams could not be lowered faithfully.
    /// </summary>
    public const string MultipleStreamMetadataOmitted = "WOLVERINE0012";

    /// <summary>
    /// An exact event stream append had an unresolved receiver target or payload.
    /// </summary>
    public const string EventWriteTargetUnresolved = "WOLVERINE0013";

    /// <summary>
    /// Dynamic Consistency Boundary routing or concurrency semantics could not be lowered exactly.
    /// </summary>
    public const string DcbBoundaryOmitted = "WOLVERINE0014";

    /// <summary>
    /// An admitted Dynamic Consistency Boundary query was outside the bounded source shapes.
    /// </summary>
    public const string DcbQueryUnresolved = "WOLVERINE0015";

    /// <summary>
    /// Wolverine-managed saga lifecycle is reported as realization and provenance because authored source does not safely establish a portable domain workflow.
    /// </summary>
    public const string SagaLifecycleRealization = "WOLVERINE0016";

    /// <summary>
    /// Wolverine must resolve saga correlation from the runtime envelope because no authored message member was found.
    /// </summary>
    public const string SagaCorrelationRuntime = "WOLVERINE0017";

    /// <summary>
    /// An authored saga lifecycle method could not be admitted as an exact legal Wolverine role.
    /// </summary>
    public const string SagaRoleUnresolved = "WOLVERINE0018";

    /// <summary>
    /// An authored Wolverine convention-alteration hook may change handler discovery or chain behavior at runtime.
    /// </summary>
    public const string ConventionAlterationOmitted = "WOLVERINE0019";

    /// <summary>
    /// A compound-handler stage participates in an entry point but its data loading or continuation control is not fully represented.
    /// </summary>
    public const string CompoundStageOmitted = "WOLVERINE0020";

    /// <summary>
    /// Per-handler chain configuration may alter retry or discard delivery semantics that are not represented.
    /// </summary>
    public const string HandlerChainConfigurationOmitted = "WOLVERINE0021";
}
