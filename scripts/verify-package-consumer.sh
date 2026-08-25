#!/usr/bin/env bash
# Copyright (c) Cratis. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <package-version> <package-feed>" >&2
    exit 2
fi

version=$1
feed=$(cd "$2" && pwd)
repo_root=$(cd "$(dirname "$0")/.." && pwd)
work_dir=$(mktemp -d)
trap 'rm -rf "$work_dir"' EXIT

cp "$repo_root/Integration/PackageConsumer/Program.cs" "$work_dir/Program.cs"
cat >"$work_dir/PackageConsumer.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Cratis.CritterStack.Screenplay" Version="$version" />
  </ItemGroup>
</Project>
PROJECT

cat >"$work_dir/CandidateApi.cs" <<'CSHARP'
// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Cratis.CritterStack.Screenplay;
using Cratis.CritterStack.Screenplay.Marten;
using Cratis.CritterStack.Screenplay.Wolverine;
using Cratis.Screenplay.Generation.DotNet;
using Microsoft.CodeAnalysis.CSharp;

static class CandidateApi
{
    [ModuleInitializer]
    internal static void Verify()
    {
        var tree = CSharpSyntaxTree.ParseText("namespace Candidate; public sealed class Marker;", path: "/workspace/Marker.cs");
        var context = DotNetSourcePaths.Create(
            "Candidate/Candidate",
            new DotNetSourcePathPolicy
            {
                Version = 1,
                DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                CasePolicy = DotNetSourcePathCasePolicy.Ordinal
            },
            [
                new DotNetSourceDocument
                {
                    SyntaxTree = tree,
                    ProjectRelativePath = "Marker.cs",
                    WorkspaceRelativePath = "Candidate/Marker.cs"
                }
            ]);
        _ = context.Files[tree].Identity;
        _ = new CritterStackScreenplayGenerator([new CritterStackScreenplayAdapter()]);

        if (MartenDiagnosticCodes.ConventionAlterationOmitted != "MARTEN0014" ||
            MartenDiagnosticCodes.ProjectionSideEffectUnresolved != "MARTEN0015" ||
            MartenDiagnosticCodes.SessionListenerOmitted != "MARTEN0016" ||
            WolverineDiagnosticCodes.ConventionAlterationOmitted != "WOLVERINE0019" ||
            WolverineDiagnosticCodes.CompoundStageOmitted != "WOLVERINE0020" ||
            WolverineDiagnosticCodes.HandlerChainConfigurationOmitted != "WOLVERINE0021")
        {
            throw new InvalidOperationException("The candidate diagnostic-code contract changed");
        }
    }
}
CSHARP

export NUGET_PACKAGES="${NUGET_PACKAGES:-$work_dir/.nuget/packages}"
dotnet restore "$work_dir/PackageConsumer.csproj" \
    --source "$feed" \
    --source https://api.nuget.org/v3/index.json \
    --nologo
dotnet run --project "$work_dir/PackageConsumer.csproj" \
    --no-restore \
    --configuration Release \
    --nologo
