// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Marten;

namespace Cratis.CritterStack.Screenplay.for_MartenStoreIndex;

public class when_proving_event_model_names : given.a_marten_store_scope_application
{
    const string Source =
        """
        using Marten;
        namespace StoreScope;
        public static class Configuration
        {
            public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                const string name = "CaseSensitive";
                services.AddMarten(options => { CONFIGURATION });
            }
        }
        """;

    static MartenStoreIndex Index(string configuration)
    {
        var project = ProjectFor(Source.Replace("CONFIGURATION", configuration, StringComparison.Ordinal));
        var context = new DotNetAnalysisContext([project]);
        return MartenStoreIndex.Create(context, new() { Id = "test", Version = "1" }, new(context, context));
    }

    [Fact] void should_keep_a_single_unnamed_default_store_legacy() => Index(string.Empty).Facts.ShouldBeEmpty();
    [Fact] void should_keep_an_explicit_null_qualifier_legacy() => Index("options.EventModelName = null;").Default().Kind.ShouldEqual(BackendOwnershipKind.LegacyUnscoped);
    [Fact] void should_prove_a_constant_name() => Index("options.EventModelName = name;").Default().Scope.EventModelName.ShouldEqual("CaseSensitive");
    [Fact] void should_prove_a_local_options_alias() => Index("var alias = options; alias.EventModelName = name;").Default().Scope.EventModelName.ShouldEqual("CaseSensitive");
    [Fact] void should_not_emit_a_declaration_for_a_computed_qualifier() => Index("options.EventModelName = System.DateTime.Now.ToString();").Facts.ShouldBeEmpty();
    [Fact] void should_not_treat_a_compound_assignment_as_a_literal() => Index("options.EventModelName += name;").Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_not_assume_a_conditional_assignment_runs() => Index("if (System.DateTime.Now.Ticks > 0) options.EventModelName = name;").Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_reject_whitespace_names() => Index("options.EventModelName = \" \";").Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_reject_empty_names() => Index("options.EventModelName = \"\";").Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_reject_control_characters() => Index("options.EventModelName = \"line\\nname\";").Default().Kind.ShouldEqual(BackendOwnershipKind.Unknown);
    [Fact] void should_locate_unproved_ownership() => Index("options.EventModelName = System.DateTime.Now.ToString();").Diagnostics.All(diagnostic => diagnostic.Code == GenerationDiagnosticCodes.UnknownBackendScope && diagnostic.Source is not null && diagnostic.Severity == GenerationDiagnosticSeverity.Error).ShouldBeTrue();
}
