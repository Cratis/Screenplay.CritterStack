// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_call_is_conditional : given.an_authored_abstraction_application
{
    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Contracts.IRepository<Contracts.Submit> repository)
            {
                if (command.Id != System.Guid.Empty) repository.Append(command);
            }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_invent_unconditional_declarative_production() => Productions.Single().Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_keep_the_production_link_loss_explicit() => Result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.ProductionLinkOmitted).ShouldBeTrue();
}
