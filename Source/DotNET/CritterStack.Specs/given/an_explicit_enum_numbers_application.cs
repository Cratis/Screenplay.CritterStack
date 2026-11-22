// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_explicit_enum_numbers_application : an_enum_application
{
    protected const string PositionalDeclaration = "public enum Priority { First, Second, Third }";
    protected GeneratedScreenplayDefinition Result = null!;
    protected string PositionalSource = null!;

    void Establish() => PositionalSource = Generate(CreateProject(declaration: PositionalDeclaration)).Source;

    protected string WithNumbers(params int[] numbers) => PositionalSource
        .Replace("\n  first\n", $"\n  first = {numbers[0]}\n")
        .Replace("\n  second\n", $"\n  second = {numbers[1]}\n")
        .Replace("\n  third\n", $"\n  third = {numbers[2]}\n");

    protected GenerationDiagnostic NumberingDiagnostic => Result.Diagnostics.Single(_ => _.Code == CritterStackDiagnosticCodes.EnumNumbersNotPreserved);

    protected static SourceRange DeclarationRange(string declaration) => new()
    {
        Path = "Ordering/Priority.cs",
        FileIdentity = new SourceFileIdentity { Project = "Ordering/Ordering", Path = "Priority.cs" },
        StartLine = 2,
        StartColumn = 1,
        EndLine = 2,
        EndColumn = declaration.Length + 1
    };
}
