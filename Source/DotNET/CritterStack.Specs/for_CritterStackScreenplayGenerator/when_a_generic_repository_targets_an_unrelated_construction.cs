// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_a_generic_repository_targets_an_unrelated_construction : given.an_authored_abstraction_application
{
    protected override string OtherImplementationSource =>
        """
        namespace Other;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.OtherMade>
        {
            public void Append(Contracts.OtherMade command) => session.Events.Append(command.Id, new Contracts.OtherMade(command.Id));
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_match_generic_arguments_rather_than_only_the_definition() => Productions.Single().Key.Target.Value.EndsWith(".Made", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_not_count_a_different_construction_as_a_candidate() => Unresolved.ShouldBeEmpty();
}
