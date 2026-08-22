// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_composed_vogen_critter_stack_application : Specification
{
    const string FrameworkSource =
        """
        namespace Vogen
        {
            public sealed class ValueObjectAttribute<T> : System.Attribute;
            public readonly struct Validation
            {
                public static Validation Ok => default;
                public static Validation Invalid(string message) => default;
            }
        }

        namespace Wolverine
        {
            public sealed class WolverineOptions;
        }

        namespace Marten
        {
            public sealed class StoreOptions;
            public interface IDocumentSession
            {
                void Delete<T>(object id);
                void Store<T>(T document);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Ordering;

        [Vogen.ValueObject<System.Guid>]
        public partial struct OrderId;

        [Vogen.ValueObject<string>]
        public partial struct CustomerCode
        {
            private const string InvalidMessage = "Customer codes cannot be blank";
            private static Vogen.Validation Validate(string value) =>
                string.IsNullOrWhiteSpace(value) ? Vogen.Validation.Invalid(InvalidMessage) : Vogen.Validation.Ok;
        }

        public sealed record PlaceOrder(OrderId Id, CustomerCode Code, CustomerCode? ReferralCode);
        public sealed record Order(OrderId Id, CustomerCode Code, CustomerCode? ReferralCode);

        public static class PlaceOrderHandler
        {
            public static void Handle(PlaceOrder command, Marten.IDocumentSession session)
            {
                session.Store(new Order(command.Id, command.Code, command.ReferralCode));
                session.Delete<Order>(command.Id);
            }
        }
        """;

    const string GeneratedSource =
        """
        namespace Ordering;

        [Vogen.ValueObject<System.Guid>]
        public partial struct GeneratedOnly;

        [System.CodeDom.Compiler.GeneratedCode("Vogen", "8.0.7")]
        public partial struct OrderId
        {
            public System.Guid Value { get; }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Ordering/Application.cs");
        var compilation = CSharpCompilation.Create(
            "Ordering",
            [
                frameworkTree,
                applicationTree,
                CSharpSyntaxTree.ParseText(GeneratedSource, path: "/workspace/obj/Vogen.g.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Ordering",
            ProjectPath = "/workspace/Ordering/Ordering.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
