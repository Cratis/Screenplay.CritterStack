// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Two authored projects pinning the alternative forms of Wolverine validation, authorization and handler discovery
/// configuration, each split over an earlier-path file and a later-path file whose textual order is reversed.
/// </summary>
public class a_wolverine_policy_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Core.TypeScanning
        {
            public class TypeQuery;
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public static class AuthorizationEndpointConventionBuilderExtensions
            {
                public static T RequireAuthorization<T>(this T builder, params string[] policyNames) => builder;
            }
        }

        namespace Wolverine.Configuration
        {
            public class HandlerDiscovery
            {
                public HandlerDiscovery DisableConventionalDiscovery(bool value = true) => this;
                public HandlerDiscovery IncludeType<T>() => this;
                public HandlerDiscovery IncludeType(System.Type type) => this;
                public HandlerDiscovery IncludeAssembly(System.Reflection.Assembly assembly) => this;
                public HandlerDiscovery CustomizeHandlerDiscovery(System.Action<JasperFx.Core.TypeScanning.TypeQuery> configure) => this;
                public void IgnoreAssembly(System.Reflection.Assembly assembly) { }
            }
        }

        namespace Wolverine
        {
            public interface IWolverineHandler;
            public class WolverineOptions
            {
                public Wolverine.Configuration.HandlerDiscovery Discovery { get; } = new();
            }
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
                public bool Unsupported { get; set; }
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

                public static Wolverine.WolverineOptions UseFluentValidation(
                    this Wolverine.WolverineOptions options,
                    int level) => options;
            }
        }

        namespace Wolverine.DataAnnotationsValidation
        {
            public static class DataAnnotationsValidationExtensions
            {
                public static Wolverine.WolverineOptions UseDataAnnotationsValidation(this Wolverine.WolverineOptions options) => options;
            }
        }

        namespace Wolverine.Http
        {
            public class HttpChain
            {
                public object Metadata { get; } = new();
            }

            public class WolverineHttpOptions
            {
                public void UseDataAnnotationsValidationProblemDetailMiddleware() { }
                public void RequireAuthorizeOnAll() { }
                public void ConfigureEndpoints(System.Action<HttpChain> configure) { }
            }
        }

        namespace Wolverine.Http.FluentValidation
        {
            public static class WolverineHttpOptionsExtensions
            {
                public static void UseFluentValidationProblemDetailMiddleware(this Wolverine.Http.WolverineHttpOptions options) { }
            }
        }
        """;

    const string PolicyProgramSource =
        """
        using Microsoft.AspNetCore.Builder;
        using Wolverine.DataAnnotationsValidation;
        using Wolverine.FluentValidation;
        using Wolverine.Http.FluentValidation;

        namespace PolicyVariants;

        public static class Configuration
        {
            public static void Configure(
                Wolverine.WolverineOptions options,
                Wolverine.Http.WolverineHttpOptions http,
                bool runtime,
                int runtimeValue,
                string policy,
                FluentValidationConfiguration captured,
                System.Action<FluentValidationConfiguration> configure)
            {
                options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration, true);
                options.UseFluentValidation(RegistrationBehavior.DiscoverAndRegisterValidators);
                options.UseFluentValidation(delegate (FluentValidationConfiguration c) { });
                options.UseFluentValidation(configure);
                options.UseFluentValidation(c => { c.IncludeInternalTypes = true; c.RegistrationBehavior = RegistrationBehavior.ExplicitRegistration; });
                options.UseFluentValidation(c => c.IncludeInternalTypes = true);
                options.UseFluentValidation(c => { c.IncludeInternalTypes = runtime; });
                options.UseFluentValidation(c => { c.IncludeInternalTypes = true; System.Console.WriteLine(); });
                options.UseFluentValidation(c => captured.IncludeInternalTypes = true);
                options.UseFluentValidation(c => c.RegistrationBehavior = (RegistrationBehavior)runtimeValue);
                options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration, runtime);
                options.UseFluentValidation(c => { });
                options.UseFluentValidation(c => c.Unsupported = true);
                options.UseFluentValidation(3);
                options.UseDataAnnotationsValidation();
                http.UseFluentValidationProblemDetailMiddleware();
                http.UseDataAnnotationsValidationProblemDetailMiddleware();
                http.RequireAuthorizeOnAll();
                http.ConfigureEndpoints(chain => chain.Metadata.RequireAuthorization("fallback"));
                http.ConfigureEndpoints((Wolverine.Http.HttpChain chain) => chain.Metadata.RequireAuthorization());
                http.ConfigureEndpoints(chain => { });
                http.ConfigureEndpoints(chain => { if (runtime) { chain.Metadata.RequireAuthorization("inner"); } });
                http.ConfigureEndpoints(chain => chain.Metadata.RequireAuthorization(policy));
                if (runtime)
                {
                    options.UseFluentValidation();
                    options.UseDataAnnotationsValidation();
                    http.ConfigureEndpoints(chain => chain.Metadata.RequireAuthorization("outer"));
                    void Local()
                    {
                        http.RequireAuthorizeOnAll();
                    }

                    Local();
                }
            }
        }
        """;

    const string PolicyEarlierSource =
        """
        using Wolverine.FluentValidation;

        namespace PolicyVariants;

        public static class EarlierConfiguration
        {
            public static void Configure(Wolverine.WolverineOptions options, Wolverine.Http.WolverineHttpOptions http, bool runtime)
            {
                http.RequireAuthorizeOnAll();
                options.UseFluentValidation(c => { c.IncludeInternalTypes = runtime; });
                if (runtime) { http.RequireAuthorizeOnAll(); }
                options.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
            }
        }
        """;

    const string DiscoveryProgramSource =
        """
        namespace DiscoveryVariants;

        public record IncludedMessage(System.Guid Id);
        public record ExplicitMessage(System.Guid Id);

        public static class Configuration
        {
            public static void Configure(
                Wolverine.WolverineOptions options,
                System.Type runtimeType,
                System.Reflection.Assembly runtimeAssembly)
            {
                options.Discovery.DisableConventionalDiscovery();
                options.Discovery.DisableConventionalDiscovery(false);
                options.Discovery.IncludeType<IncludedHandler>();
                options.Discovery.IncludeType<IncludedHandler>();
                options.Discovery.IncludeType(typeof(ExplicitActions));
                options.Discovery.IncludeType<string>();
                options.Discovery.IncludeType(typeof(int));
                options.Discovery.IncludeType(runtimeType);
                options.Discovery.IncludeAssembly(typeof(Configuration).Assembly);
                options.Discovery.IncludeAssembly(typeof(string).Assembly);
                options.Discovery.IncludeAssembly(runtimeAssembly);
                options.Discovery.CustomizeHandlerDiscovery(query => { });
                options.Discovery.IgnoreAssembly(typeof(Configuration).Assembly);
            }

            public sealed class Nested
            {
                public void Apply(Wolverine.WolverineOptions options) => options.Discovery.IncludeType(typeof(Nested));
            }
        }

        public class IncludedHandler
        {
            public static void Handle(IncludedMessage message) { }
        }

        public class ExplicitActions : Wolverine.IWolverineHandler
        {
            public void Handle(ExplicitMessage message) { }
        }
        """;

    const string DiscoveryEarlierSource =
        """
        namespace DiscoveryVariants;

        public static class EarlierConfiguration
        {
            public static void Configure(Wolverine.WolverineOptions options, bool runtime)
            {
                options.Discovery.CustomizeHandlerDiscovery(query => { });
                options.Discovery.DisableConventionalDiscovery(runtime);
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation PolicyProject = null!;
    protected DotNetProjectCompilation DiscoveryProject = null!;
    protected AdapterContribution PolicyContribution = null!;
    protected AdapterContribution DiscoveryContribution = null!;

    void Establish()
    {
        PolicyProject = Project(
            "PolicyVariants",
            (PolicyProgramSource, "/workspace/PolicyVariants/Program.cs"),
            (PolicyEarlierSource, "/workspace/PolicyVariants/Earlier.cs"));
        DiscoveryProject = Project(
            "DiscoveryVariants",
            (DiscoveryProgramSource, "/workspace/DiscoveryVariants/Program.cs"),
            (DiscoveryEarlierSource, "/workspace/DiscoveryVariants/Earlier.cs"));
        var adapter = new CritterStackScreenplayAdapter();
        PolicyContribution = adapter.Analyze(new DotNetAnalysisContext([PolicyProject]), new DotNetAdapterOptions());
        DiscoveryContribution = adapter.Analyze(new DotNetAnalysisContext([DiscoveryProject]), new DotNetAdapterOptions());
    }

    protected static string[] Diagnostics(AdapterContribution contribution, params string[] codes) =>
        [.. diagnostic_ranges.Of(contribution).Where(_ => codes.Any(code => _.StartsWith(code + "|", StringComparison.Ordinal)))];

    static DotNetProjectCompilation Project(string name, params (string Source, string Path)[] sources)
    {
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs") };
        trees.AddRange(sources.Select(_ => CSharpSyntaxTree.ParseText(_.Source, path: _.Path)));
        var compilation = CSharpCompilation.Create(
            name,
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = name,
            ProjectPath = $"/workspace/{name}/{name}.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
