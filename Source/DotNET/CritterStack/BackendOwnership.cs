// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;

namespace Cratis.CritterStack.Screenplay;

enum BackendOwnershipKind
{
    LegacyUnscoped,
    Exact,
    Unknown,
    Conflict
}

sealed record BackendOwnership
{
    public required BackendOwnershipKind Kind { get; init; }

    public BackendScopeKey? Scope { get; init; }

    public bool CanEmit => Kind is BackendOwnershipKind.LegacyUnscoped or BackendOwnershipKind.Exact;

    public static BackendOwnership Legacy { get; } = new() { Kind = BackendOwnershipKind.LegacyUnscoped };

    public static BackendOwnership Unknown { get; } = new() { Kind = BackendOwnershipKind.Unknown };

    public static BackendOwnership Conflict { get; } = new() { Kind = BackendOwnershipKind.Conflict };

    public static BackendOwnership Exact(BackendScopeKey scope) => new() { Kind = BackendOwnershipKind.Exact, Scope = scope };

    public GenerationDiagnostic Diagnostic(SubjectId subject, SourceRange? source, string explanation) => new()
    {
        Code = Kind == BackendOwnershipKind.Conflict
            ? GenerationDiagnosticCodes.AmbiguousBackendScopeOwnership
            : GenerationDiagnosticCodes.UnknownBackendScope,
        Severity = GenerationDiagnosticSeverity.Error,
        Outcome = Kind == BackendOwnershipKind.Conflict ? GenerationDiagnosticOutcome.Conflict : GenerationDiagnosticOutcome.Unknown,
        Subject = subject,
        Source = source,
        Message = explanation
    };
}
