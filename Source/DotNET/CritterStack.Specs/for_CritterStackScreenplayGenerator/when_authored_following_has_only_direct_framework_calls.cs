// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_following_has_only_direct_framework_calls : given.an_authored_abstraction_application
{
    protected override string HandlerSource =>
        """
        namespace Application;
        public static class SubmitHandler
        {
            public static void Handle(Contracts.Submit command, Marten.IDocumentSession session) => session.Events.Append(command.Id, new Contracts.Made(command.Id));
        }
        """;

    void Because() => Generate();

    [Fact] void should_preserve_direct_production() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_keep_direct_framework_output_byte_identical() => Transcript(GenerateFrom(Projects, false)).ShouldEqual(Transcript(Result));
}
