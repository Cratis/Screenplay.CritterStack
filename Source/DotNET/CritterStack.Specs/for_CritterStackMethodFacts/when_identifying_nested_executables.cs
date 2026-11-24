// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackMethodFacts;

public class when_identifying_nested_executables : given.a_method_fact_application
{
    SubjectId[] _subjects = [];
    MethodFactSupportAlternative _nested = null!;
    MethodFactSupportAlternative _ordinary = null!;
    SubjectId _otherProject = null!;

    void Because()
    {
        _subjects = [.. Locals.Concat(Lambdas).Select(method => CritterStackMethodFacts.SubjectFor(Project, method))];
        _nested = CritterStackMethodFacts.Observe(Project, Locals.Single(method => method.Name == "Nested"));
        _ordinary = CritterStackMethodFacts.Observe(Project, IntegerHandler);
        _otherProject = CritterStackMethodFacts.SubjectFor(
            Project with { Name = "OtherMethods", ProjectPath = "/workspace/OtherMethods/OtherMethods.csproj" },
            Lambdas[0]);
    }

    [Fact] void should_keep_ordinary_method_identity_compatible() => _ordinary.Method.ShouldEqual(DotNetMethodIdentity.SubjectFor(Project, IntegerHandler));
    [Fact] void should_not_replace_compatibility_lambda_subjects() => (_subjects[Locals.Length] == DotNetMethodIdentity.SubjectFor(Project, Lambdas[0])).ShouldBeFalse();
    [Fact] void should_not_replace_compatibility_local_function_subjects() => (_subjects[0] == DotNetMethodIdentity.SubjectFor(Project, Locals[0])).ShouldBeFalse();
    [Fact] void should_not_treat_overloads_as_the_same_executable() => (_ordinary.Method == CritterStackMethodFacts.SubjectFor(Project, StringHandler)).ShouldBeFalse();
    [Fact] void should_separate_same_named_local_functions_and_anonymous_functions() => _subjects.Distinct().Count().ShouldEqual(5);
    [Fact] void should_repeat_stable_executable_identities() => Locals.Concat(Lambdas).Select(method => CritterStackMethodFacts.SubjectFor(Project, method)).ShouldEqual(_subjects);
    [Fact] void should_include_the_immediate_lexical_executable_first() => _nested.EnclosingExecutables[0].ShouldEqual(CritterStackMethodFacts.SubjectFor(Project, Lambdas[0]));
    [Fact] void should_include_the_lexical_handler() => _nested.EnclosingExecutables[1].ShouldEqual(_ordinary.Method);
    [Fact] void should_not_include_unrelated_executables() => _nested.EnclosingExecutables.Count.ShouldEqual(2);
    [Fact] void should_leave_ordinary_methods_without_lexical_executables() => _ordinary.EnclosingExecutables.Count.ShouldEqual(0);
    [Fact] void should_keep_projects_distinct() => (_otherProject == CritterStackMethodFacts.SubjectFor(Project, Lambdas[0])).ShouldBeFalse();
    [Fact] void should_default_to_an_unscoped_observation() => _ordinary.Scope.ShouldBeNull();
    [Fact] void should_not_invent_conditional_diagnostics() => _ordinary.Diagnostics.Count.ShouldEqual(0);
}
