// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Marten;
using Wolverine.Attributes;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// A Marten document whose identity is explicitly configured independently of its Vogen declarations.
/// </summary>
/// <param name="Key">The configured Marten identity.</param>
/// <param name="CorrelationId">A Guid-backed Id-suffixed concept that is not the identity.</param>
/// <param name="CustomerCode">The customer code.</param>
/// <param name="ReferralCode">An optional concept usage.</param>
/// <param name="NormalizedCode">A normalized concept usage.</param>
public sealed record Order(
    OrderKey Key,
    CorrelationId CorrelationId,
    CustomerCode CustomerCode,
    CustomerCode? ReferralCode,
    NormalizedCode NormalizedCode);

/// <summary>
/// Registers an order through a Wolverine handler.
/// </summary>
/// <param name="Key">The order key.</param>
/// <param name="CorrelationId">The correlation value.</param>
/// <param name="CustomerCode">The customer code.</param>
/// <param name="ReferralCode">The optional referral code.</param>
/// <param name="NormalizedCode">The normalized code.</param>
public sealed record RegisterOrder(
    OrderKey Key,
    CorrelationId CorrelationId,
    CustomerCode CustomerCode,
    CustomerCode? ReferralCode,
    NormalizedCode NormalizedCode);

/// <summary>
/// Records that an order was registered.
/// </summary>
/// <param name="CorrelationId">The correlation value.</param>
/// <param name="CustomerCode">The customer code.</param>
/// <param name="ReferralCode">The optional referral code.</param>
/// <param name="NormalizedCode">The normalized code.</param>
public sealed record OrderRegistered(
    CorrelationId CorrelationId,
    CustomerCode CustomerCode,
    CustomerCode? ReferralCode,
    NormalizedCode NormalizedCode);

/// <summary>
/// Configures the exact Marten identity member.
/// </summary>
public static class StorageConfiguration
{
    /// <summary>
    /// Configures order storage.
    /// </summary>
    /// <param name="options">The Marten options.</param>
    public static void Configure(StoreOptions options) => options.Schema.For<Order>().Identity(_ => _.Key);
}

/// <summary>
/// Handles order registration through Wolverine and appends one Marten event.
/// </summary>
public static class RegisterOrderHandler
{
    /// <summary>
    /// Registers the order.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="session">The Marten session.</param>
    [WolverineHandler]
    public static void Handle(RegisterOrder command, IDocumentSession session) =>
        session.Events.Append(
            command.Key.Value,
            new OrderRegistered(command.CorrelationId, command.CustomerCode, command.ReferralCode, command.NormalizedCode));
}
