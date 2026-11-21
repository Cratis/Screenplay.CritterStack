// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_interfaces_form_a_diamond : given.an_authored_abstraction_application
{
    protected override string ContractSource => base.ContractSource +
        """
        public interface ILeft : IRepository<Submit> { }
        public interface IRight : IRepository<Submit> { }
        public interface IDiamond : ILeft, IRight { }
        """;

    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.IDiamond
        {
            public void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.Made(command.Id));
            public void Append(string value) { }
        }
        """;

    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.IDiamond repository) => repository.Append(command);
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_count_inherited_paths_as_distinct_implementations() => Productions.Count.ShouldEqual(1);
    [Fact] void should_not_report_a_false_cycle_or_ambiguity() => Unresolved.ShouldBeEmpty();
}
