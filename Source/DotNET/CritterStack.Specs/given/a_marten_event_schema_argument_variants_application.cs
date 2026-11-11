// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Event schema declarations whose arguments are valid in one position and unresolvable in another, written in
/// positional, named and reordered-named forms and as generic, typeof, static-call and inline-constructor variants.
/// </summary>
public class a_marten_event_schema_argument_variants_application : a_marten_event_schema_configuration_application
{
    const string VariantsSource =
        """
        using Marten.Events;
        using Marten.Services.Json.Transformations;

        namespace Variants;

        public sealed record MapGood;
        public sealed record MapBadAlias;
        public sealed record MapTypeofGood;
        public sealed record MapTypeofBadAlias;
        public sealed record MapNamedReordered;
        public sealed record SuffixGood;
        public sealed record SuffixBadSuffix;
        public sealed record SuffixBadBase;
        public sealed record SuffixNamedReordered;
        public sealed record VersionGood;
        public sealed record VersionBadVersion;
        public sealed record VersionBadBase;
        public sealed record VersionNamedReordered;
        public sealed record RawGoodAlias;
        public sealed record RawBadAlias;
        public sealed record RawTypeofBadAlias;
        public sealed record RawTypeofNamedReordered;
        public sealed record RawVersionGood;
        public sealed record RawVersionBad;
        public sealed record RawTypeofVersionGood;
        public sealed record RawTypeofVersionBad;
        public sealed record RawTypeofConventionGood;
        public sealed record RawConventionGeneric;
        public sealed record OldA;
        public sealed record NewA;
        public sealed record OldB;
        public sealed record NewB;
        public sealed record OldC;
        public sealed record NewC;
        public sealed record OldD;
        public sealed record NewD;
        public sealed record InlineFirstTarget;
        public sealed record InlineSecondTarget;

        public sealed class InlineFirstUpcaster : Marten.Services.Json.Transformations.SystemTextJson.EventUpcaster<InlineFirstTarget>;
        public sealed class InlineSecondUpcaster : Marten.Services.Json.Transformations.JsonNet.AsyncOnlyEventUpcaster<InlineSecondTarget>;
        public sealed class NotAnUpcasterShape : IEventUpcaster;

        public static class Configuration
        {
            public static void Configure(Marten.Events.IEventStoreOptions events, System.Type computedType, IEventUpcaster computedUpcaster)
            {
                const string alias = "variant-alias";
                const uint version = 9;
                var badAlias = System.Guid.NewGuid().ToString();
                var badVersion = (uint)System.Environment.TickCount;
                var badSuffix = badAlias + "-suffix";

                events.MapEventType<MapGood>(alias);
                events.MapEventType<MapBadAlias>(badAlias);
                events.MapEventType(typeof(MapTypeofGood), alias);
                events.MapEventType(typeof(MapTypeofBadAlias), badAlias);
                events.MapEventType(computedType, alias);
                events.MapEventType(eventTypeName: alias, eventType: typeof(MapNamedReordered));

                events.MapEventTypeWithNameSuffix<SuffixGood>(alias, "suffix");
                events.MapEventTypeWithNameSuffix<SuffixBadSuffix>(alias, badSuffix);
                events.MapEventTypeWithNameSuffix<SuffixBadBase>(badAlias, "suffix");
                events.MapEventTypeWithNameSuffix<SuffixNamedReordered>(suffix: "suffix", eventTypeName: alias);
                EventStoreOptionsExtensions.MapEventTypeWithNameSuffix<SuffixGood>(events, badSuffix);

                events.MapEventTypeWithSchemaVersion<VersionGood>(alias, version);
                events.MapEventTypeWithSchemaVersion<VersionBadVersion>(alias, badVersion);
                events.MapEventTypeWithSchemaVersion<VersionBadBase>(badAlias, version);
                events.MapEventTypeWithSchemaVersion<VersionNamedReordered>(schemaVersion: version, eventTypeName: alias);
                EventStoreOptionsExtensions.MapEventTypeWithSchemaVersion<VersionGood>(events, badVersion);

                events.Upcast<RawGoodAlias>(alias, json => json);
                events.Upcast<RawBadAlias>(badAlias, json => json);
                events.Upcast(typeof(RawTypeofBadAlias), badAlias, json => json);
                events.Upcast(computedType, alias, json => json);
                events.Upcast(jsonTransformation: json => json, eventTypeName: alias, eventType: typeof(RawTypeofNamedReordered));
                events.Upcast(typeof(RawTypeofConventionGood), json => json);
                events.Upcast(computedType, json => json);
                events.Upcast<RawConventionGeneric>(json => json);

                events.Upcast<RawVersionGood>(version, json => json);
                events.Upcast<RawVersionBad>(badVersion, json => json);
                events.Upcast(typeof(RawTypeofVersionGood), version, json => json);
                events.Upcast(typeof(RawTypeofVersionBad), badVersion, json => json);
                events.Upcast(computedType, version, json => json);

                events.Upcast<OldA, NewA>(alias, old => new NewA());
                events.Upcast<OldA, NewA>(badAlias, old => new NewA());
                events.Upcast<OldB, NewB>(alias, (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new NewB()));
                events.Upcast<OldB, NewB>(badAlias, (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new NewB()));
                events.Upcast<OldC, NewC>(upcast: old => new NewC(), eventTypeName: alias);
                events.Upcast<OldC, NewC>(version, old => new NewC());
                events.Upcast<OldC, NewC>(badVersion, old => new NewC());
                events.Upcast<OldD, NewD>(version, (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new NewD()));
                events.Upcast<OldD, NewD>(badVersion, (old, cancellationToken) => System.Threading.Tasks.Task.FromResult(new NewD()));
                EventStoreOptionsExtensions.Upcast<OldD, NewD>(events, upcast: old => new NewD(), schemaVersion: version);

                events.Upcast(new InlineFirstUpcaster(), new InlineSecondUpcaster());
                events.Upcast(new InlineFirstUpcaster(), computedUpcaster, new InlineSecondUpcaster());
                events.Upcast(new InlineFirstUpcaster(), new NotAnUpcasterShape());
                events.Upcast(computedUpcaster, new InlineFirstUpcaster());
                events.Upcast(new NotAnUpcasterShape());
                events.Upcast<NotAnUpcasterShape>();
                events.Upcast<InlineSecondUpcaster>();
            }
        }
        """;

    protected AdapterContribution VariantContribution = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var variantsTree = CSharpSyntaxTree.ParseText(VariantsSource, path: "/workspace/Variants/ArgumentVariants.cs");
        var compilation = CSharpCompilation.Create(
            "Variants",
            [frameworkTree, variantsTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Variants",
            ProjectPath = "/workspace/Variants/Variants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, variantsTree }
        };
        VariantContribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
