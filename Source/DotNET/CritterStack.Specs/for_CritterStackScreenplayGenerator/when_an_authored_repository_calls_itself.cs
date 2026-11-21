// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_calls_itself : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command)
            {
                session.Events.Append(command.Id, new Contracts.Made(command.Id));
                Append(command);
            }
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_discard_even_the_partial_production_before_the_cycle() => ShouldRejectWithoutPersistence();
    [Fact] void should_identify_the_cycle() => Unresolved.Single().Message.ShouldContain("cycle");
}
