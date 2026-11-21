// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_implementations_disagree : given.an_authored_abstraction_application
{
    protected override string OtherImplementationSource =>
        """
        namespace Other;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.OtherMade(command.Id));
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_report_unknown_dispatch_without_invented_persistence() => ShouldRejectWithoutPersistence();
    [Fact] void should_name_the_unresolved_abstraction() => Unresolved.Single().Message.ShouldContain("IRepository");
    [Fact] void should_locate_the_unresolved_call() => Unresolved.Single().Source!.Path.ShouldEqual("Application/Source0.cs");
}
