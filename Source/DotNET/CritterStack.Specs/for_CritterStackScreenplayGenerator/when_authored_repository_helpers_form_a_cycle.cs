// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_helpers_form_a_cycle : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => First(command);
            public void Append(string value) { }
            void First(Contracts.Submit command) => Second(command);
            void Second(Contracts.Submit command)
            {
                session.Events.Append(command.Id, new Contracts.Made(command.Id));
                First(command);
            }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_emit_partial_facts_from_a_mutual_cycle() => ShouldRejectWithoutPersistence();
}
