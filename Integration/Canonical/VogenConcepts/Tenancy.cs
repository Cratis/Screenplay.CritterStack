// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JasperFx.MultiTenancy;
using Marten;
using Marten.Schema;

namespace Cratis.CritterStack.Screenplay.Canonical.VogenConcepts;

/// <summary>
/// An attribute-only type used to prove exact authored multi-tenancy evidence without establishing a document.
/// </summary>
[MultiTenanted]
public sealed class MultiTenantedEvidenceOnly;

/// <summary>
/// An attribute-only type used to prove exact authored single-tenancy evidence without establishing a document.
/// </summary>
[SingleTenanted]
public sealed class SingleTenantedEvidenceOnly;

/// <summary>
/// Supplies canonical authored logical tenancy declarations without asserting their runtime effects.
/// </summary>
public static class TenancyConfiguration
{
    /// <summary>
    /// Configures authored tenancy evidence against the pinned current Marten APIs.
    /// </summary>
    /// <param name="options">The Marten options.</param>
    public static void Configure(StoreOptions options)
    {
        options.Events.TenancyStyle = TenancyStyle.Single;
        options.Events.TenancyStyle = TenancyStyle.Conjoined;

        options.Schema.For<Order>().MultiTenanted();
        options.Schema.For<Order>().SingleTenanted();
        options.Schema.For<Order>().MultiTenantedWithPartitioning(partitioning =>
            partitioning.ByHash("north", "south"));

        options.Policies.AllDocumentsAreMultiTenanted();
        options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(partitioning =>
            partitioning.ByHash("east", "west"));
        options.Policies.AllDocumentsAreMultiTenantedWithPartitioning(
            partitioning => partitioning.ByHash("one", "two"),
            PrimaryKeyTenancyOrdering.TenantId_Then_Id);
    }
}
