// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Wolverine;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Isolated projects, one per FluentValidation option form, each holding a public and an internal validator, so the
/// resolved option values are visible through the validation diagnostics the discovery produces for each request.
/// </summary>
public class a_wolverine_validator_option_scenarios : Specification
{
    const string FrameworkSource =
        """
        namespace FluentValidation
        {
            public interface IValidator<T>;
        }

        namespace Wolverine
        {
            public class WolverineOptions;
        }

        namespace Wolverine.FluentValidation
        {
            public enum RegistrationBehavior
            {
                DiscoverAndRegisterValidators,
                ExplicitRegistration
            }

            public class FluentValidationConfiguration
            {
                public RegistrationBehavior RegistrationBehavior { get; set; }
                public bool IncludeInternalTypes { get; set; }
            }

            public static class WolverineFluentValidationExtensions
            {
                public static Wolverine.WolverineOptions UseFluentValidation(
                    this Wolverine.WolverineOptions options,
                    RegistrationBehavior behavior = RegistrationBehavior.DiscoverAndRegisterValidators,
                    bool includeInternalTypes = false) => options;

                public static Wolverine.WolverineOptions UseFluentValidation(
                    this Wolverine.WolverineOptions options,
                    System.Action<FluentValidationConfiguration> configure) => options;
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected IReadOnlyDictionary<string, string[]> Outcomes = null!;

    void Establish() => Outcomes = new Dictionary<string, string[]>
    {
        ["default"] = Run("options.UseFluentValidation();"),
        ["positional-include-internal"] = Run("options.UseFluentValidation(RegistrationBehavior.DiscoverAndRegisterValidators, true);"),
        ["positional-exclude-internal"] = Run("options.UseFluentValidation(RegistrationBehavior.DiscoverAndRegisterValidators, false);"),
        ["positional-explicit-registration"] = Run("options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration, true);"),
        ["callback-include-internal"] = Run("options.UseFluentValidation(c => { c.IncludeInternalTypes = true; });"),
        ["callback-include-internal-then-exclude"] = Run("options.UseFluentValidation(c => { c.IncludeInternalTypes = true; c.IncludeInternalTypes = false; });"),
        ["callback-exclude-internal-then-include"] = Run("options.UseFluentValidation(c => { c.IncludeInternalTypes = false; c.IncludeInternalTypes = true; });"),
        ["callback-explicit-registration"] = Run("options.UseFluentValidation(c => c.RegistrationBehavior = RegistrationBehavior.ExplicitRegistration);"),
        ["callback-explicit-then-discover"] = Run("options.UseFluentValidation(c => { c.RegistrationBehavior = RegistrationBehavior.ExplicitRegistration; c.RegistrationBehavior = RegistrationBehavior.DiscoverAndRegisterValidators; c.IncludeInternalTypes = true; });"),
        ["callback-discover-then-explicit"] = Run("options.UseFluentValidation(c => { c.IncludeInternalTypes = true; c.RegistrationBehavior = RegistrationBehavior.DiscoverAndRegisterValidators; c.RegistrationBehavior = RegistrationBehavior.ExplicitRegistration; });")
    };

    static string[] Run(string configuration)
    {
        var source =
            $$"""
            using Wolverine.FluentValidation;

            namespace Scenario;

            public record PublicRequest(System.Guid Id);
            public record InternalRequest(System.Guid Id);

            public class PublicRequestValidator : FluentValidation.IValidator<PublicRequest>;
            internal class InternalRequestValidator : FluentValidation.IValidator<InternalRequest>;

            public static class Handler
            {
                public static void Handle(PublicRequest request) { }
                public static void Handle(InternalRequest request) { }
            }

            public static class Configuration
            {
                public static void Configure(Wolverine.WolverineOptions options)
                {
                    {{configuration}}
                }
            }
            """;
        var compilation = CSharpCompilation.Create(
            "Scenario",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(source, path: "/workspace/Scenario/Program.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Scenario",
            ProjectPath = "/workspace/Scenario/Scenario.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };

        var discovery = WolverineValidationAuthorizationDiscovery.Discover(project, new CritterStackSubjectResolver(new([project])));
        var subject = new SubjectId { Value = "dotnet://Scenario/Scenario/Scenario.Handler" };
        var handlers = compilation.GetTypeByMetadataName("Scenario.Handler")!.GetMembers("Handle").OfType<IMethodSymbol>().ToArray();
        return
        [
            .. discovery.Diagnostics.Select(Format),
            .. handlers.SelectMany(handler => discovery.ValidationDiagnostics(
                handler,
                (INamedTypeSymbol)handler.Parameters[0].Type,
                subject,
                false)).Select(Format)
        ];
    }

    static string Format(GenerationDiagnostic diagnostic) =>
        $"{diagnostic.Code}|{diagnostic.Severity}|{diagnostic.Outcome}|{diagnostic.Subject?.Value}|{Range(diagnostic.Source)}|{diagnostic.Message}";

    static string Range(SourceRange? source) => source is null
        ? "no-source"
        : $"{source.Path}:{source.StartLine}:{source.StartColumn}-{source.EndLine}:{source.EndColumn}";
}
