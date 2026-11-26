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
    /// Per-stream loading, version, or quantified batch identity metadata could not be lowered faithfully.
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

    /// <summary>
    /// A SlicePattern declaration contains an unresolved or undefined value.
    /// </summary>
    public const string SlicePatternUnresolved = "WOLVERINE0022";

    /// <summary>
    /// A SlicePattern declaration disagrees with a proven trigger, which takes precedence.
    /// </summary>
    public const string SlicePatternConflict = "WOLVERINE0023";

    /// <summary>
    /// An admitted handler's SlicePattern declaration cannot obtain a supported artifact placement.
    /// </summary>
    public const string SlicePatternOmitted = "WOLVERINE0024";

    /// <summary>
    /// A command's produced event could not be linked with produces, so the model references the handler instead.
    /// </summary>
    /// <remarks>
    /// Reported as a warning when production evidence is omitted or unresolved, and as information when the handler
    /// reference accurately represents a structural pattern, such as an IEventStream&lt;T&gt; parameter or a dynamic consistency boundary.
    /// </remarks>
    public const string ProductionLinkOmitted = "WOLVERINE0025";

    /// <summary>
    /// A handler's OnException failure stage sends messages only when the handler fails; the exception filter and retry conditions are not represented.
    /// </summary>
    public const string FailureStageConditionOmitted = "WOLVERINE0026";

    /// <summary>
    /// Authored Marten event forwarding or its message transformation could not be represented exactly.
    /// </summary>
    public const string EventForwardingOmitted = "WOLVERINE0027";

    /// <summary>
    /// Authored response replay, always-publish, or topic-routing semantics could not be represented.
    /// </summary>
    public const string EndpointIdempotencyOmitted = "WOLVERINE0028";

    /// <summary>
    /// An authored abstraction call could not be resolved to a proven persistence consequence.
    /// </summary>
    public const string AuthoredAbstractionUnresolved = "WOLVERINE0029";

    /// <summary>
    /// An aggregate's natural-key identity could not be represented without guessing a stream identity.
    /// </summary>
    public const string NaturalKeyIdentityOmitted = "WOLVERINE0030";

    /// <summary>
    /// A Wolverine HTTP command endpoint is not yet emitted by the source-native contribution because it has consequences that contribution does not represent.
    /// </summary>
    public const string NativeHttpCommandOmitted = "WOLVERINE0031";
}
