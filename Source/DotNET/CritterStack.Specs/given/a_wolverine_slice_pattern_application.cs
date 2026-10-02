// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_slice_pattern_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract record TimeoutMessage;
        }
        namespace Wolverine.Attributes
        {
            public class WolverineIgnoreAttribute : System.Attribute;
        }
        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineHeadAttribute(string route) : WolverineHttpMethodAttribute(route);
            public class WolverineOptionsAttribute(string route) : WolverineHttpMethodAttribute(route);
        }
        namespace JasperFx.Events.EventModeling
        {
            public enum SlicePattern { Command, View, Automation, Translation }
        }
        namespace Wolverine.Persistence.EventSourcing
        {
            [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class, Inherited = true)]
            public sealed class SlicePatternAttribute(JasperFx.Events.EventModeling.SlicePattern pattern) : System.Attribute
            {
                public JasperFx.Events.EventModeling.SlicePattern Pattern { get; } = pattern;
            }
        }
        namespace JasperFx.Events
        {
            public interface IEventStream<T> { void AppendOne(object value); }
        }
        """;

    const string ApplicationSource =
        """
        using Wolverine.Persistence.EventSourcing;
        using JasperFx.Events.EventModeling;
        namespace Patterns;
        public record Change;
        public record View;
        public record Automate;
        public record Translate;
        public record HttpChange;
        public record HttpView;
        public record Timeout : Wolverine.TimeoutMessage;
        public record Unknown;
        public record Unplaced;
        public record Inherited;
        public record MethodInherited;
        public record Impostor;
        public record Ignored;
        public record GeneratedMetadata;
        public record Cascade;
        public record DeclaredCommand;
        public record Changed;
        public record Viewed;
        public record Automated;
        public record Translated;
        public record HttpChanged;
        public record TimedOut;
        public record UnknownChanged;
        public record InheritedChanged;
        public record MethodInheritedChanged;
        public record ImpostorChanged;
        public record GeneratedChanged;
        public class State;
        [SlicePattern(SlicePattern.Automation)]
        public static class ChangeHandler
        {
            [SlicePattern(SlicePattern.Command)]
            public static void Handle(Change command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Changed());
        }
        public static class ViewHandler
        {
            [SlicePattern(SlicePattern.View)]
            public static void Handle(View command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Viewed());
        }
        [SlicePattern(SlicePattern.Automation)]
        public static class AutomateHandler
        {
            public static void Handle(Automate command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Automated());
        }
        [SlicePattern(SlicePattern.Translation)]
        public static class TranslateHandler
        {
            public static void Handle(Translate command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new Translated());
        }
        public static class HttpEndpoints
        {
            [Wolverine.Http.WolverinePost("/change"), SlicePattern(SlicePattern.Automation)]
            public static void Post(HttpChange command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new HttpChanged());
            [Wolverine.Http.WolverineGet("/view"), SlicePattern(SlicePattern.Command)]
            public static HttpView Get() => new();
        }
        public static class TimeoutHandler
        {
            [SlicePattern(SlicePattern.Command)]
            public static void Handle(Timeout command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new TimedOut());
        }
        public static class UnknownHandler
        {
            [SlicePattern((SlicePattern)99)]
            public static void Handle(Unknown command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new UnknownChanged());
        }
        [SlicePattern(SlicePattern.Automation)]
        public abstract class Parent;
        [SlicePattern(SlicePattern.Translation)]
        public abstract class NearParent : Parent;
        public class InheritedHandler : NearParent
        {
            public void Handle(Inherited command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new InheritedChanged());
        }
        public abstract class MethodParent
        {
            [SlicePattern(SlicePattern.Automation)]
            public abstract void Handle(MethodInherited command, JasperFx.Events.IEventStream<State> stream);
        }
        [SlicePattern(SlicePattern.View)]
        public class MethodInheritedHandler : MethodParent
        {
            public override void Handle(MethodInherited command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new MethodInheritedChanged());
        }
        public class FakeSlicePatternAttribute(SlicePattern value) : System.Attribute;
        public static class ImpostorHandler
        {
            [FakeSlicePattern(SlicePattern.Automation)]
            public static void Handle(Impostor command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new ImpostorChanged());
        }
        public static class UnplacedHandler
        {
            [SlicePattern(SlicePattern.Automation)]
            public static void Handle(Unplaced command) { }
        }
        [Wolverine.Attributes.WolverineIgnore, SlicePattern(SlicePattern.Automation)]
        public static class IgnoredHandler { public static void Handle(Ignored command) { } }
        public static partial class GeneratedMetadataHandler
        {
            public static void Handle(GeneratedMetadata command, JasperFx.Events.IEventStream<State> stream) => stream.AppendOne(new GeneratedChanged());
        }
        public static class DeclaredCommandHandler
        {
            [SlicePattern(SlicePattern.Command)]
            public static Cascade Handle(DeclaredCommand command) => new();
        }
        """;

    protected DotNetProjectCompilation Project = null!;

    protected DotNetProjectCompilation CreateApplication(string source, bool strict = false, bool reverse = false)
    {
        var framework = Project.Compilation.SyntaxTrees.First();
        var application = CSharpSyntaxTree.ParseText(source, path: "/workspace/Patterns/Source/Patterns/Transfer/Conventions.cs");
        SyntaxTree[] trees = reverse ? [application, framework] : [framework, application];
        return Project with
        {
            Compilation = CSharpCompilation.Create("Patterns", trees, Project.Compilation.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = trees.ToHashSet(),
            SourceContext = strict ? DotNetSourcePaths.Create(
                "Patterns/Patterns",
                new DotNetSourcePathPolicy { Version = 1, DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [
                    new DotNetSourceDocument { SyntaxTree = framework, ProjectRelativePath = "Framework.cs", WorkspaceRelativePath = "Patterns/Framework.cs" },
                    new DotNetSourceDocument { SyntaxTree = application, ProjectRelativePath = "Source/Patterns/Transfer/Conventions.cs", WorkspaceRelativePath = "Patterns/Source/Patterns/Transfer/Conventions.cs" }
                ]) : null
        };
    }

    void Establish()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Patterns/Handlers.cs");
        var generated = CSharpSyntaxTree.ParseText("[Wolverine.Persistence.EventSourcing.SlicePattern(JasperFx.Events.EventModeling.SlicePattern.Automation)] public static partial class GeneratedMetadataHandler;", path: "/workspace/Patterns/Generated.cs");
        generated = CSharpSyntaxTree.ParseText("namespace Patterns; " + generated.GetText(), path: generated.FilePath);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        Project = new DotNetProjectCompilation
        {
            Name = "Patterns",
            ProjectPath = "/workspace/Patterns/Patterns.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Patterns", [framework, application, generated], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application }
        };
    }
}
