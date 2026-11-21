// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_following_an_authored_repository_from_an_endpoint : given.an_authored_abstraction_application
{
    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitEndpoint
        {
            [Wolverine.Http.WolverinePost("/submit")]
            public static void Post(Contracts.Submit command, Contracts.IRepository<Contracts.Submit> repository) => repository.Append(command);
        }
        """;

    void Because() => Generate();

    [Fact] void should_follow_the_same_explicit_context_from_an_http_command() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_resolve_the_endpoint_repository_without_an_adapter_order_join() => Unresolved.ShouldBeEmpty();
}
