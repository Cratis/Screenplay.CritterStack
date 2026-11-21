// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_implementation_does_not_persist : given.an_authored_abstraction_application
{
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

    [Fact] void should_not_choose_only_the_persisting_implementation() => ShouldRejectWithoutPersistence();
}
