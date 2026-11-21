// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_contract_is_variant : given.an_authored_abstraction_application
{
    protected override string ContractSource => base.ContractSource.Replace("IRepository<T>", "IRepository<in T>", StringComparison.Ordinal);

    void Because() => Generate();

    [Fact] void should_not_treat_only_exact_generic_constructions_as_the_complete_dispatch_set() => ShouldRejectWithoutPersistence();
}
