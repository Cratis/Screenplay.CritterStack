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
    /// Arbitrary EventProjection document body, value, or predicate flow could not be represented.
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

    /// <summary>
    /// A configured Marten document identity could not be resolved to an emitted member without guessing.
    /// </summary>
    public const string DocumentIdentityUnresolved = "MARTEN0005";

    /// <summary>
    /// A compiled-query call was found in a nested executable scope whose invocation from the containing endpoint could not be proven.
    /// </summary>
    public const string CompiledQueryFlowUnresolved = "MARTEN0006";

    /// <summary>
    /// Authored projection daemon identity metadata could not be represented or resolved exactly.
    /// </summary>
    public const string ProjectionMetadataOmitted = "MARTEN0007";

    /// <summary>
    /// Async daemon hosting or shard configuration could not be represented or resolved exactly.
    /// </summary>
    public const string DaemonConfigurationOmitted = "MARTEN0008";

    /// <summary>
    /// A registered event subscription or its exact configuration could not be represented.
    /// </summary>
    public const string SubscriptionConfigurationOmitted = "MARTEN0009";

    /// <summary>
    /// Arbitrary custom projection or subscription processing consequences were deliberately not inferred.
    /// </summary>
    public const string CustomProcessingOmitted = "MARTEN0010";

    /// <summary>
    /// Authored event alias, schema-version, or naming-style configuration cannot be represented without changing Event artifacts.
    /// </summary>
    public const string EventTypeConfigurationOmitted = "MARTEN0011";

    /// <summary>
    /// Authored event upcast registration cannot be represented without inventing event-evolution behavior.
    /// </summary>
    public const string EventUpcastConfigurationOmitted = "MARTEN0012";

    /// <summary>
    /// Authored logical tenancy configuration cannot be represented without inventing effective runtime behavior.
    /// </summary>
    public const string TenancyConfigurationOmitted = "MARTEN0013";

    /// <summary>
    /// An authored Marten convention-alteration hook may change store shape at runtime.
    /// </summary>
    public const string ConventionAlterationOmitted = "MARTEN0014";

    /// <summary>
    /// A projection side-effect message payload could not be resolved exactly from authored source.
    /// </summary>
    public const string ProjectionSideEffectUnresolved = "MARTEN0015";

    /// <summary>
    /// An authored document-session listener observes commits but its consequences are not represented.
    /// </summary>
    public const string SessionListenerOmitted = "MARTEN0016";
}
