// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Locator;

namespace Cratis.CritterStack.Screenplay.Canonical;

static class Bootstrap
{
    public static int Run(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: canonical <project.csproj> <expected.txt> <output.play>");
            return 2;
        }

        MSBuildLocator.RegisterDefaults();
        return CanonicalRunner.Run(args[0], args[1], args[2]).GetAwaiter().GetResult();
    }
}
