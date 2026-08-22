// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_validation_authorization_application : Specification
{
    const string FrameworkSource =
        """
        namespace FluentValidation
        {
            public interface IValidator<T>;
            public abstract class AbstractValidator<T> : IValidator<T>;
        }

        namespace Microsoft.AspNetCore.Authorization
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
            public class AuthorizeAttribute : System.Attribute
            {
                public AuthorizeAttribute() { }
                public AuthorizeAttribute(string policy) => Policy = policy;
                public string? Policy { get; set; }
                public string? Roles { get; set; }
                public string? AuthenticationSchemes { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, Inherited = true)]
            public class AllowAnonymousAttribute : System.Attribute;
        }

        namespace Microsoft.AspNetCore.Builder
        {
            public static class AuthorizationEndpointConventionBuilderExtensions
            {
                public static T RequireAuthorization<T>(this T builder, params string[] policyNames) => builder;
            }
        }

        namespace Wolverine
        {
            public interface IWolverineHandler;
            public class WolverineOptions;
            public interface IMessageBus
            {
                System.Threading.Tasks.ValueTask PublishAsync<T>(T message);
            }
        }

        namespace Marten
        {
            public interface IDocumentSession
            {
                EventOperations Events { get; }
            }

            public class EventOperations
            {
                public void Append(System.Guid id, object @event) { }
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

        namespace Wolverine.DataAnnotationsValidation
        {
            public static class DataAnnotationsValidationExtensions
            {
                public static Wolverine.WolverineOptions UseDataAnnotationsValidation(this Wolverine.WolverineOptions options) => options;
            }
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class HttpChain
            {
                public object Metadata { get; } = new();
            }
            public class WolverineHttpOptions
            {
                public void UseDataAnnotationsValidationProblemDetailMiddleware() { }
                public void RequireAuthorizeOnAll() { }
                public void ConfigureEndpoints(System.Action<HttpChain> configure) { }
                public void AddPolicy<T>() { }
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

    const string PositiveConfigurationSource =
        """
        using Microsoft.AspNetCore.Builder;
        using Wolverine.DataAnnotationsValidation;
        using Wolverine.FluentValidation;
        using Wolverine.Http.FluentValidation;

        namespace ValidationAuthorization;

        public static class Configuration
        {
            public static void Configure(Wolverine.WolverineOptions options, Wolverine.Http.WolverineHttpOptions http)
            {
                options.UseFluentValidation(configuration => configuration.IncludeInternalTypes = true);
                options.UseDataAnnotationsValidation();
                http.UseFluentValidationProblemDetailMiddleware();
                http.UseDataAnnotationsValidationProblemDetailMiddleware();
                http.RequireAuthorizeOnAll();
                http.ConfigureEndpoints(chain => chain.Metadata.RequireAuthorization("fallback"));
            }
        }
        """;

    const string PositiveEndpointSource =
        """
        using FluentValidation;
        using Microsoft.AspNetCore.Authorization;
        using Wolverine.Http;

        namespace ValidationAuthorization;

        public record CreateOrder(System.Guid Id)
        {
            public sealed class Validator : AbstractValidator<CreateOrder>;
        }

        public static class CreateOrderEndpoint
        {
            [Authorize("orders", Roles = "operator")]
            [WolverinePost("/orders")]
            public static string Post(CreateOrder command) => command.Id.ToString();
        }

        public record RegisterUser([property: System.ComponentModel.DataAnnotations.Required] string Name);

        public static class RegisterUserEndpoint
        {
            [WolverinePost("/users")]
            public static string Post(RegisterUser command) => command.Name;
        }

        public record CloseOrder(System.Guid Id);

        [AllowAnonymous]
        public static class CloseOrderEndpoint
        {
            public static string[] Validate(CloseOrder command) => [];
            public static System.Threading.Tasks.Task<string[]> ValidateAsync(CloseOrder command) =>
                System.Threading.Tasks.Task.FromResult(System.Array.Empty<string>());

            [WolverinePost("/orders/close")]
            public static string Post(CloseOrder command) => command.Id.ToString();
        }

        public record ProcessPayment(System.Guid Id)
        {
            public sealed class Validator : AbstractValidator<ProcessPayment>;
        }

        public record PaymentProcessed(System.Guid Id);

        public static class ProcessPaymentHandler
        {
            public static void Handle(ProcessPayment command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, new PaymentProcessed(command.Id));
        }

        public record ImportUser(System.Guid Id, [property: System.ComponentModel.DataAnnotations.Required] string Name);
        public record UserImported(System.Guid Id);

        public static class ImportUserHandler
        {
            public static void Handle(ImportUser command, Marten.IDocumentSession session) =>
                session.Events.Append(command.Id, new UserImported(command.Id));
        }

        public record ValidateAutomationTrigger(System.Guid Id)
        {
            public sealed class Validator : AbstractValidator<ValidateAutomationTrigger>;
        }

        public record ValidateAutomationPublished(System.Guid Id);

        public static class ValidateAutomationHandler
        {
            public static void Handle(ValidateAutomationTrigger message, Wolverine.IMessageBus bus) =>
                _ = bus.PublishAsync(new ValidateAutomationPublished(message.Id));
        }
        """;

    const string PackageOnlySource =
        """
        using FluentValidation;
        using Wolverine.Http;

        namespace PackageOnly;

        public record PackageOnlyCommand([property: System.ComponentModel.DataAnnotations.Required] string Name)
        {
            public sealed class Validator : AbstractValidator<PackageOnlyCommand>;
        }

        public static class PackageOnlyEndpoint
        {
            [WolverinePost("/package-only")]
            public static string Post(PackageOnlyCommand command) => command.Name;
        }
        """;

    const string UnresolvedSource =
        """
        using Microsoft.AspNetCore.Builder;
        using Wolverine.DataAnnotationsValidation;
        using Wolverine.FluentValidation;
        using Wolverine.Http.FluentValidation;

        namespace UnresolvedPolicies;

        public sealed class CustomPolicy;

        public static class Configuration
        {
            public static void Configure(
                Wolverine.WolverineOptions options,
                Wolverine.Http.WolverineHttpOptions http,
                bool enabled,
                System.Action<FluentValidationConfiguration> configure)
            {
                options.UseFluentValidation(configure);
                if (enabled)
                {
                    http.UseFluentValidationProblemDetailMiddleware();
                    http.UseDataAnnotationsValidationProblemDetailMiddleware();
                    http.RequireAuthorizeOnAll();
                    Apply(options, configured => configured.UseDataAnnotationsValidation());
                }

                var unrelated = new object();
                http.ConfigureEndpoints(chain => unrelated.RequireAuthorization("captured"));
                http.ConfigureEndpoints(ApplyPolicies);
                http.AddPolicy<CustomPolicy>();
            }

            static void Apply(Wolverine.WolverineOptions options, System.Action<Wolverine.WolverineOptions> configure) { }
            static void ApplyPolicies(Wolverine.Http.HttpChain chain) { }
        }
        """;

    const string CapturedConfigurationSource =
        """
        using Wolverine.FluentValidation;

        namespace CapturedConfiguration;

        public static class Configuration
        {
            public static void Configure(
                Wolverine.WolverineOptions options,
                FluentValidationConfiguration captured) =>
                options.UseFluentValidation(active => captured.IncludeInternalTypes = true);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation PositiveProject = null!;
    protected DotNetProjectCompilation PackageOnlyProject = null!;
    protected DotNetProjectCompilation UnresolvedProject = null!;
    protected DotNetProjectCompilation CapturedConfigurationProject = null!;

    void Establish()
    {
        PositiveProject = Project(
            "ValidationAuthorization",
            (PositiveConfigurationSource, "/workspace/ValidationAuthorization/Program.cs"),
            (PositiveEndpointSource, "/workspace/ValidationAuthorization/Endpoints.cs"));
        PackageOnlyProject = Project(
            "PackageOnly",
            (PackageOnlySource, "/workspace/PackageOnly/Endpoints.cs"));
        UnresolvedProject = Project(
            "UnresolvedPolicies",
            (UnresolvedSource, "/workspace/UnresolvedPolicies/Program.cs"));
        CapturedConfigurationProject = Project(
            "CapturedConfiguration",
            (CapturedConfigurationSource, "/workspace/CapturedConfiguration/Program.cs"));
    }

    static DotNetProjectCompilation Project(
        string name,
        params (string Source, string Path)[] sources)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs")
        };
        trees.AddRange(sources.Select(_ => CSharpSyntaxTree.ParseText(_.Source, path: _.Path)));
        var compilation = CSharpCompilation.Create(
            name,
            trees,
            _references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = name,
            ProjectPath = $"/workspace/{name}/{name}.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation
        };
    }
}
