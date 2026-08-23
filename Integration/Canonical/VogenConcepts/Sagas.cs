// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Wolverine;
using Wolverine.Persistence.Sagas;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// Starts the canonical order saga.
/// </summary>
/// <param name="OrderSagaId">The saga identity.</param>
/// <param name="CustomerCode">The customer code tracked by the saga.</param>
public sealed record BeginOrderSaga(OrderKey OrderSagaId, CustomerCode CustomerCode);

/// <summary>
/// Confirms an order through an explicitly identified saga message.
/// </summary>
/// <param name="OrderId">The saga identity.</param>
public sealed record ConfirmOrderSaga([property: SagaIdentity] OrderKey OrderId);

/// <summary>
/// Completes the canonical order saga through parameter-level identity configuration.
/// </summary>
/// <param name="OrderReference">The saga identity.</param>
public sealed record CompleteOrderSaga(OrderKey OrderReference);

/// <summary>
/// Enforces the canonical order saga timeout.
/// </summary>
/// <param name="SagaId">The saga identity.</param>
public sealed record OrderSagaTimeout(OrderKey SagaId) : TimeoutMessage(TimeSpan.FromMinutes(15));

/// <summary>
/// Notifies downstream handlers that the order saga completed.
/// </summary>
/// <param name="OrderId">The completed order identity.</param>
public sealed record OrderSagaCompleted(OrderKey OrderId);

/// <summary>
/// Coordinates the canonical order workflow through Wolverine's authored saga API.
/// </summary>
public sealed class OrderSaga : Saga
{
    /// <summary>
    /// Gets or sets the saga identity.
    /// </summary>
    public OrderKey Id { get; set; } = OrderKey.From(Guid.Empty);

    /// <summary>
    /// Gets or sets the customer code tracked by the workflow.
    /// </summary>
    public CustomerCode CustomerCode { get; set; } = CustomerCode.Unspecified;

    /// <summary>
    /// Starts the saga and schedules its timeout.
    /// </summary>
    /// <param name="message">The starting message.</param>
    /// <returns>The new saga state and timeout message.</returns>
    public static (OrderSaga, OrderSagaTimeout) Start(BeginOrderSaga message) =>
        (new OrderSaga { Id = message.OrderSagaId, CustomerCode = message.CustomerCode }, new OrderSagaTimeout(message.OrderSagaId));

    /// <summary>
    /// Confirms the saga while retaining exact message-member correlation evidence.
    /// </summary>
    /// <param name="message">The confirmation message.</param>
    public void Handle(ConfirmOrderSaga message)
    {
    }

    /// <summary>
    /// Completes the saga and emits an ordinary cascading message.
    /// </summary>
    /// <param name="message">The completion message.</param>
    /// <returns>The cascading completion message.</returns>
    public OrderSagaCompleted Handle([SagaIdentityFrom(nameof(CompleteOrderSaga.OrderReference))] CompleteOrderSaga message)
    {
        MarkCompleted();
        return new(message.OrderReference);
    }

    /// <summary>
    /// Handles the scheduled timeout if the saga still exists.
    /// </summary>
    /// <param name="message">The timeout message.</param>
    public void Handle(OrderSagaTimeout message)
    {
    }
}
