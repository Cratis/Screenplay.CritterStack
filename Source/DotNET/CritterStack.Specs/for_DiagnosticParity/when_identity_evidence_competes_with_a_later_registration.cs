// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.for_FactParity.given;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cratis.CritterStack.Screenplay.for_DiagnosticParity;

/// <summary>Preserves selected identity-warning evidence when another registration follows it.</summary>
public class when_identity_evidence_competes_with_a_later_registration : Specification
{
    const string Source = """
        namespace Marten
        {
            public interface IDocumentStore;
            public sealed class StoreOptions { public MartenRegistry Schema { get; } = new(); }
            public sealed class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();
                public sealed class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> Identity(System.Linq.Expressions.Expression<System.Func<T, object>> member) => this;
                }
            }
        }
        namespace IdentityEvidence
        {
            public sealed class Document { public int Id { get; set; } public int Key; }
            public static class Configuration
            {
                public static void Configure(Marten.StoreOptions options)
                {
                    options.Schema.For<Document>().Identity(document => document.Key);
                    options.Schema.For<Document>();
                }
            }
        }
        """;

    GenerationDiagnostic _compatibility = null!;
    GenerationDiagnostic _native = null!;
    GenerationDiagnostic _reversed = null!;
    SourceRange _identity = null!;
    bool _verified;

    void Because()
    {
        var project = fact_vectors.Project(Source);
        var context = new DotNetAnalysisContext([project]);
        var compatibility = new CritterStackScreenplayAdapter().Analyze(context, new());
        var contribution = new MartenScreenplayAdapter().Analyze(context, new());
        var generator = new ScreenplayDefinitionGenerator();
        var options = new ScreenplayGenerationOptions { Domain = "IdentityEvidence" };
        var native = generator.Generate([contribution], options);
        var reversed = generator.Generate([contribution with { Facts = [.. contribution.Facts.Reverse()] }], options);
        _verified = native.IsSuccess && reversed.IsSuccess;
        _compatibility = compatibility.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved);
        _native = native.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved);
        _reversed = reversed.Diagnostics.Single(diagnostic => diagnostic.Code == MartenDiagnosticCodes.DocumentIdentityUnresolved);
        var calls = project.AuthoredSyntaxTrees.Single().GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        _identity = CritterStackSource.RangeForProject(calls.Single(call => Name(call) == "Identity").GetLocation(), project);
    }

    [Fact] void should_verify_the_native_candidates_in_both_fact_orders() => _verified.ShouldBeTrue();
    [Fact] void should_keep_the_compatibility_warning_at_the_selected_identity() => _compatibility.Source.ShouldEqual(_identity);
    [Fact] void should_keep_the_native_warning_at_the_selected_identity() => _native.Source.ShouldEqual(_identity);
    [Fact] void should_preserve_the_complete_payload_when_facts_are_reversed() => given.diagnostic_parity.Meaning([_reversed]).ShouldEqual(given.diagnostic_parity.Meaning([_compatibility]));
    [Fact] void should_match_the_complete_diagnostic_payload() => given.diagnostic_parity.Meaning([_native]).ShouldEqual(given.diagnostic_parity.Meaning([_compatibility]));

    static string? Name(InvocationExpressionSyntax call) => (call.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText;
}
