// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_command_members_application : Specification
{
    const string Source =
        """
        namespace JasperFx { public class IdentityAttribute : System.Attribute; }
        namespace Members
        {
            public enum Mode { Fast, Slow }
            public class Aggregate;
            public record Plain(string Title, int Count, Mode Mode, int? Optional, System.Collections.Generic.List<string> Tags);
            public record Conventional(string Title, System.Guid Id, System.Guid AggregateId);
            public record Attributed(string Title, System.Guid AggregateId, [property: JasperFx.Identity] System.Guid Key);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected CSharpCompilation Compilation { get; } = CSharpCompilation.Create(
        "Members",
        [CSharpSyntaxTree.ParseText(Source, path: "/workspace/Members/Members.cs")],
        _references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    protected INamedTypeSymbol Type(string name) => Compilation.GetTypeByMetadataName($"Members.{name}")!;

    protected IPropertySymbol Member(string type, string name) => Type(type).GetMembers(name).OfType<IPropertySymbol>().Single();

    protected static WolverineStateBinding Binding(WolverineStateBindingKind kind, ISymbol? identityMember, INamedTypeSymbol modelType) => new(
        "handler",
        null,
        modelType,
        kind,
        new WolverineStateBindingEvidence<string?>(null, null),
        new WolverineStateBindingEvidence<string?>(null, null),
        new WolverineStateBindingEvidence<string>("load", null),
        new WolverineStateBindingEvidence<bool>(false, null),
        identityMember,
        null,
        false,
        null);

    protected WolverineCommandIdentityInput Input(
        string commandType,
        string? aggregateType = null,
        IReadOnlyList<WolverineStateBinding>? bindings = null,
        bool hasFetchedStreams = false,
        IPropertySymbol? linkedStreamIdentity = null) => new(
            Type(commandType),
            aggregateType is null ? null : Type(aggregateType),
            bindings ?? [],
            hasFetchedStreams,
            linkedStreamIdentity);

    protected static string Identified(IEnumerable<PropertyDefinition> properties) =>
        string.Join(",", properties.Where(_ => _.IsIdentifier).Select(_ => _.Name));
}
