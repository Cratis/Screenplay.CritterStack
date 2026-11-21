// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden.given;

public class golden_fixtures : Specification
{
    protected readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), $"canonical-golden-{Guid.NewGuid():N}");
    protected readonly StringWriter Error = new(CultureInfo.InvariantCulture);
    protected GeneratedScreenplayDefinition Result = null!;
    protected string Prefix = null!;
    protected string ActualDirectory = null!;

    void Establish()
    {
        Directory.CreateDirectory(DirectoryPath);
        Prefix = Path.Combine(DirectoryPath, "Sample");
        ActualDirectory = Path.Combine(DirectoryPath, "actual");
        Result = new GeneratedScreenplayDefinition
        {
            Source = "domain Kitchen \r\n\r\n",
            Application = null!,
            Graph = null!,
            Diagnostics = []
        };
    }

    void Destroy()
    {
        Error.Dispose();
        Directory.Delete(DirectoryPath, recursive: true);
    }
}
