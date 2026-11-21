// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_following_authored_repository_dispatch : given.an_authored_abstraction_application
{
    void Because() => Generate();

    [Fact] void should_generate_a_verified_document() => Result.IsSuccess.ShouldBeTrue();
    [Fact] void should_link_the_hidden_event_declaratively() => Productions.Single().Key.Discriminator.ShouldEqual("declarative");
    [Fact] void should_bind_the_exact_repository_overload() => Productions.Single().Key.Target.Value.EndsWith(".Made", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_retain_the_handler_call_site() => Productions.Single().Evidence.Select(evidence => evidence.Source?.Path).ShouldContain("Application/Source0.cs");
    [Fact] void should_retain_the_implementation_and_append_sites() => Productions.Single().Evidence.Count(evidence => evidence.Source?.Path == "Persistence/Source0.cs").ShouldBeGreaterThan(1);
    [Fact] void should_not_warn_about_complete_dispatch() => Unresolved.ShouldBeEmpty();
    [Fact] void should_ignore_project_order() => Transcript(GenerateFrom(Projects.Reverse(), true)).ShouldEqual(Transcript(Result));
    [Fact] void should_ignore_checkout_location() => Transcript(GenerateFrom(CreateProjects("/another-checkout"), true)).ShouldEqual(Transcript(Result));
}
