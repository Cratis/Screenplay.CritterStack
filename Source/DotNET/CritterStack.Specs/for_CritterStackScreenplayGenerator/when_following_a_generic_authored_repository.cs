// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_following_a_generic_authored_repository : given.an_authored_abstraction_application
{
    protected override string ContractSource =>
        """
        namespace Contracts;
        public record Submit(System.Guid Id);
        public record Made(System.Guid Id);
        public interface IRepository<T> { void Append(System.Guid id); }
        """;

    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository<T>(Marten.IDocumentSession session) : Contracts.IRepository<T>
        {
            public void Append(System.Guid id) => session.Events.Append(id, new Contracts.Made(id));
        }
        """;

    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.IRepository<Contracts.Submit> repository) => repository.Append(command.Id);
        }
        """;

    void Because() => Generate();

    [Fact] void should_bind_the_constructed_generic_contract_across_compilations() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_not_confuse_type_parameters_with_missing_bodies() => Unresolved.ShouldBeEmpty();
}
