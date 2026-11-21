// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_unselected_repository_competes_with_a_selected_one : given.an_authored_abstraction_application
{
    protected override bool IncludeOtherImplementation => false;
    protected override string OtherImplementationSource =>
        """
        namespace Other;
        public sealed class Repository : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) { }
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_treat_the_visible_implementation_as_complete() => ShouldRejectWithoutPersistence();
}
