// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_traversal_exceeds_the_depth_limit : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command)
            {
                session.Events.Append(command.Id, new Contracts.Made(command.Id));
                Step0(command);
            }
            public void Append(string value) { }
        """ + string.Join('\n', Enumerable.Range(0, 10).Select(index => $"void Step{index}(Contracts.Submit command) => Step{index + 1}(command);")) +
        "void Step10(Contracts.Submit command) { } }";

    void Because() => Generate();

    [Fact] void should_stop_without_retaining_earlier_partial_production() => ShouldRejectWithoutPersistence();
    [Fact] void should_identify_the_depth_limit() => Unresolved.Single().Message.ShouldContain("limit");
}
