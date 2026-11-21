// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repositories_share_an_inherited_implementation : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public abstract class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.Made(command.Id));
            public void Append(string value) { }
        }
        public sealed class Left(Marten.IDocumentSession session) : Repository(session);
        public sealed class Right(Marten.IDocumentSession session) : Repository(session);
        """;

    void Because() => Generate();

    [Fact] void should_accept_both_inheritance_paths_with_identical_consequences() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_not_mistake_repeated_body_visits_for_a_cycle() => Unresolved.ShouldBeEmpty();
}
