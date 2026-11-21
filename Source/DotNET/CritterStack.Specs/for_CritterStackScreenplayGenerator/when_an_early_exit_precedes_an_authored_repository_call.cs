// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_early_exit_precedes_an_authored_repository_call : given.an_authored_abstraction_application
{
    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.IRepository<Contracts.Submit> repository)
            {
                if (command.Id == System.Guid.Empty) return;
                repository.Append(command);
            }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_describe_the_guarded_call_as_unconditional() => Productions.Single().Key.Discriminator.ShouldEqual("imperative");
}
