// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackSource;

public class when_a_current_compilation_declaration_is_out_of_context : given.a_critter_stack_source_context
{
    Evidence _evidence = null!;

    void Because()
    {
        var outOfContextTree = SourceTree(
            "namespace Application; public sealed class OutOfContext;",
            "/checkout/Application/OutOfContext.cs");
        var compilation = SourceCompilation("Application", [outOfContextTree]);
        var project = new DotNetProjectCompilation
        {
            Name = "Application",
            SourceRoot = "/checkout",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>()
        };

        _evidence = EvidenceFor(compilation.GetTypeByMetadataName("Application.OutOfContext")!, project);
    }

    [Fact] void should_not_reclassify_the_declaration_as_authored_evidence() => _evidence.Source.ShouldBeNull();
}
