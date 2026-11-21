// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_implementations_agree : given.an_authored_abstraction_application
{
    protected override string OtherImplementationSource =>
        """
        namespace Other;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.Made(command.Id));
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_link_the_consensus_event() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_retain_both_implementation_sources() => Productions.Single().Evidence.Select(evidence => evidence.Source?.Path).ShouldContain("Other/Source0.cs");
    [Fact] void should_not_report_ambiguity_for_identical_consequences() => Unresolved.ShouldBeEmpty();
    [Fact] void should_keep_the_consensus_independent_of_project_order() => Transcript(GenerateFrom(Projects.Reverse(), true)).ShouldEqual(Transcript(Result));
}
