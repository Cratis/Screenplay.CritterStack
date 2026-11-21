// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_traversal_exceeds_the_call_budget : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) {
        """ + string.Join('\n', Enumerable.Repeat("Store(command.Id);", 130)) +
        """
            }
            public void Append(string value) { }
            void Store(System.Guid id) => session.Events.Append(id, new Contracts.Made(id));
        }
        """;

    void Because() => Generate();

    [Fact] void should_discard_partial_facts_after_reaching_the_budget() => ShouldRejectWithoutPersistence();
    [Fact] void should_explain_the_bound() => Unresolved.Single().Message.ShouldContain("limit");
}
