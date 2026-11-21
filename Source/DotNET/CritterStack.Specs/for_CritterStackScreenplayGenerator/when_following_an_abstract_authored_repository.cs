// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_following_an_abstract_authored_repository : given.an_authored_abstraction_application
{
    protected override string ContractSource =>
        """
        namespace Contracts;
        public record Submit(System.Guid Id);
        public record Made(System.Guid Id);
        public abstract class Repository { public abstract void Append(Submit command); }
        """;

    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(Marten.IDocumentSession session) : Contracts.Repository
        {
            public override void Append(Contracts.Submit command) => session.Events.Append(command.Id, new Contracts.Made(command.Id));
        }
        """;

    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.Repository repository) => repository.Append(command);
        }
        """;

    void Because() => Generate();

    [Fact] void should_resolve_the_selected_override() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_not_confuse_abstract_dispatch_with_a_missing_body() => Unresolved.ShouldBeEmpty();
}
