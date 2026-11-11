// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// A generated community kitchen graph and diagnostics under a fixed hand-written source, so canonical expectations are
/// checked against literal values.
/// </summary>
public class a_canonical_kitchen_result : a_community_kitchen_application
{
    /// <summary>
    /// The fixed source text of the result.
    /// </summary>
    protected const string Source = "domain Kitchen\n\nmodule Sales\n";

    /// <summary>
    /// The SHA-256 of <see cref="Source"/> as upper-case hexadecimal.
    /// </summary>
    protected const string SourceHash = "32CE2FBAF12416FE738679715E16D7D1AC4B3410E7C197776030142D084F689C";

    /// <summary>
    /// Gets the result expectations are checked against.
    /// </summary>
    protected GeneratedScreenplayDefinition Result { get; private set; } = null!;

    void Establish()
    {
        var generated = new CritterStackScreenplayGenerator().Generate(
            [CreateFeatureFolderProject()],
            new CritterStackScreenplayOptions { Domain = "CommunityKitchen" });
        Result = new GeneratedScreenplayDefinition
        {
            Application = null!,
            Source = Source,
            Diagnostics = generated.Diagnostics,
            Graph = generated.Graph
        };
    }

    /// <summary>
    /// Checks one expectation against the result.
    /// </summary>
    /// <param name="kind">The expectation kind.</param>
    /// <param name="value">The expectation value.</param>
    /// <returns><see langword="true"/> when the expectation is met.</returns>
    private protected bool Met(ExpectationKind kind, string value) => new Expectation(kind, value).IsMetBy(Result);
}
