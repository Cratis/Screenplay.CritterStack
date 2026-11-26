// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>
/// Compares the native Wolverine delete commands composed with the real native Marten adapter, in both producer orders and both public
/// roster orders, to the compatibility oracle. Every artifact comparison runs the gap10 guard.
/// </summary>
public abstract class a_document_delete_http_command_comparison : a_native_http_command_comparison
{
    protected IEnumerable<GeneratedScreenplayDefinition> Composites => [Composed, ComposedReverse, PublicComposed, PublicComposedReverse];

    protected bool AllArtifactsMatch() => Composites.All(_ => ArtifactsMatchWithOnlyTheDocumentedDelta(_, Expected));

    protected bool AllRelationshipsMatch() => Composites.All(_ => Relationships(_) == Relationships(Expected));

    protected bool AllDiagnosticsMatch() => Composites.All(_ => Diagnostics(_).SequenceEqual(Diagnostics(Expected)));

    protected bool AllSourcesMatch() => Composites.All(_ => _.Source == Expected.Source);

    protected bool AllStatusesMatch() => Composites.All(_ => _.IsSuccess == Expected.IsSuccess);

    protected IEnumerable<RelationshipFact> NativeDeletes => Native.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Deletes);

    protected bool WasAdmitted() => Composites.All(Admitted) &&
        new[] { GenerationFactCapability.ArtifactDeclaration, GenerationFactCapability.EntryPoint, GenerationFactCapability.Relationship }.All(Declares);
}
