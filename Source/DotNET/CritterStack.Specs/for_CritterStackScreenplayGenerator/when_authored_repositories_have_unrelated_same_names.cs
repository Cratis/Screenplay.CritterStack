// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repositories_have_unrelated_same_names : given.an_authored_abstraction_application
{
    protected override string OtherImplementationSource =>
        """
        namespace Other;
        public interface IRepository<T> { void Append(T command); }
        public sealed class Repository(Marten.IDocumentSession session) : IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.OtherMade(command.Id));
        }
        """;

    void Because() => Generate();

    [Fact] void should_ignore_the_unrelated_contract_and_overload() => Productions.Single().Key.Target.Value.EndsWith(".Made", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_not_treat_simple_name_matches_as_ambiguity() => Unresolved.ShouldBeEmpty();
}
