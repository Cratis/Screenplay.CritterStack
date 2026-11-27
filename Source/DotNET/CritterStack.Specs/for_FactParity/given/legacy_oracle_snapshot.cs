// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

// Test only: legacy marten:/wolverine: fact IDs cannot pass the combined adapter's prefix validator.
static class legacy_oracle_snapshot
{
    public static AdapterRunSnapshot Create(IEnumerable<(AdapterDescriptor Descriptor, AdapterContribution Contribution)> contributions) => new()
    {
        Adapters =
        [
            .. contributions.Select(item => new AdapterRunRecord
            {
                Considered = true,
                Probed = true,
                Executed = true,
                Descriptor = item.Descriptor,
                Probe = new AdapterProbeApplicable(),
                Disposition = AdapterRunDisposition.Admitted,
                Execution = new AdapterExecutionCompleted
                {
                    Contribution = new AdapterContributionSnapshot
                    {
                        Descriptor = item.Descriptor,
                        Facts = [.. item.Contribution.Facts],
                        Diagnostics = [.. item.Contribution.Diagnostics]
                    }
                }
            })
        ]
    };
}
