// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_safe_http_verbs_have_effects : given.a_wolverine_slice_pattern_application
{
    AdapterContribution _result = null!;

    void Establish() => Project = CreateApplication(
        """
        namespace Microsoft.AspNetCore.Http
        {
            public interface IResult;
        }
        namespace Patterns
        {
            public record Ping;
            public record Pinged;
            public record Probe;
            public record Probed;
            public class State;
            public static class SafeEffectEndpoints
            {
                [Wolverine.Http.WolverineHead("/ping")]
                public static void Head(Ping command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Pinged());
                [Wolverine.Http.WolverineOptions("/probe")]
                public static Microsoft.AspNetCore.Http.IResult Options(Probe command, JasperFx.Events.IEventStream<State> stream)
                {
                    stream.AppendOne(new Probed());
                    return null!;
                }
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_void_head_command() => Command("Ping").ShouldNotBeNull();
    [Fact] void should_keep_the_result_options_command() => Command("Probe").ShouldNotBeNull();
    [Fact] void should_keep_the_void_head_append() => Appends(Command("Ping")!).ShouldBeTrue();
    [Fact] void should_keep_the_result_options_append() => Appends(Command("Probe")!).ShouldBeTrue();
    [Fact] void should_keep_the_appended_events() => _result.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Event).Select(fact => fact.Definition.Name).ShouldContainOnly(["Pinged", "Probed"]);
    [Fact] void should_not_claim_a_slice_kind_from_the_verb() => _result.Facts.OfType<ArtifactPlacementFact>().All(fact => fact.Placement.SliceKind == GenerationSliceKind.StateChange && fact.Evidence.Explanation?.Contains("sliceClassification", StringComparison.Ordinal) != true).ShouldBeTrue();

    ArtifactFact? Command(string name) => _result.Facts.OfType<ArtifactFact>().SingleOrDefault(fact => fact.Definition.Key.Kind == ArtifactKind.Command && fact.Definition.Name == name);

    bool Appends(ArtifactFact command) => _result.Facts.OfType<RelationshipFact>().Any(fact => fact.Definition.Key.Kind == RelationshipKind.Appends && fact.Subject == command.Subject);
}
