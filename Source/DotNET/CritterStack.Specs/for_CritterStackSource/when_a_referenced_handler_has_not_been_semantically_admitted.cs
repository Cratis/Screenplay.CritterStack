// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_a_referenced_handler_has_not_been_semantically_admitted : given.a_critter_stack_source_context
{
    AdapterContribution _contribution = null!;

    void Because()
    {
        var referencedTree = SourceTree(
            """
            namespace Wolverine
            {
                public sealed class WolverineOptions;
            }

            namespace Referenced
            {
                public sealed record DoThing;
                public static class DoThingHandler
                {
                    public static void Handle(DoThing command) { }
                }
            }
            """,
            "/checkout/Referenced/Handlers.cs");
        var referencedCompilation = SourceCompilation("Referenced", [referencedTree]);
        var project = ReferencingProject(referencedCompilation, "/checkout");
        var adapter = new CritterStackScreenplayAdapter();

        _contribution = adapter.Analyze(new DotNetAnalysisContext([project]), new DotNetAdapterOptions());
    }

    [Fact] void should_not_create_artifacts_from_compatibility_source_fallback() =>
        _contribution.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
}
