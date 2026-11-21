// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_rebinds_a_stream_argument : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => Store(command.Id);
            public void Append(string value) { }
            void Store(System.Guid id)
            {
                id = System.Guid.NewGuid();
                session.Events.Append(id, new Contracts.Made(id));
            }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_link_a_reassigned_parameter_to_the_original_command() => ShouldRejectWithoutPersistence();
}
