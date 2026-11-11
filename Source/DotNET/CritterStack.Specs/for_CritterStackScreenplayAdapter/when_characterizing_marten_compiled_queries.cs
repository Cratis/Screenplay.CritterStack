// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_marten_compiled_queries : given.a_marten_compiled_query_application
{
    (string Expected, string Actual) _transcript;

    const string ExpectedDiagnostics =
        """
        MARTEN0003|Information|Unsupported|dotnet://Students/Students/Students.OtherStudent|Students/Students.cs:79:9-79:80|Marten document 'OtherStudent' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        MARTEN0003|Information|Unsupported|dotnet://Students/Students/Students.Student|Students/Students.cs:70:23-70:77|Marten document 'Student' is persisted, queried, or explicitly configured, but the current Screenplay language has no ordinary document-state declaration
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.Search%28System.String%2CMarten.IQuerySession%29:query|Students/Students.cs:65:5-71:6|HTTP GET route '/students/search' for query 'Search' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.First%28Marten.Services.BatchQuerying.IBatchedQuery%29:query|Students/Students.cs:73:5-75:51|HTTP GET route '/students/first' for query 'First' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.MultiDocument%28Marten.IQuerySession%29:query|Students/Students.cs:77:5-79:81|HTTP GET route '/students/multi-document' for query 'MultiDocument' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.MultiDocumentStudent%28Marten.IQuerySession%29:query|Students/Students.cs:81:5-83:71|HTTP GET route '/students/multi-document-student' for query 'MultiDocumentStudent' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.CalledLocal%28Marten.IQuerySession%29:query|Students/Students.cs:85:5-90:6|HTTP GET route '/students/called-local' for query 'CalledLocal' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.CalledLambda%28Marten.IQuerySession%29:query|Students/Students.cs:92:5-94:121|HTTP GET route '/students/called-lambda' for query 'CalledLambda' is not represented by the current Screenplay language
        MARTEN0006|Warning|Unknown|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.Nested%28Marten.IQuerySession%29:query|Students/Students.cs:99:57-99:105|Marten compiled query 'FirstStudent' is inside a nested executable scope whose invocation from endpoint 'StudentEndpoints.Nested' could not be proven
        MARTEN0006|Warning|Unknown|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.Nested%28Marten.IQuerySession%29:query|Students/Students.cs:100:74-100:122|Marten compiled query 'FirstStudent' is inside a nested executable scope whose invocation from endpoint 'StudentEndpoints.Nested' could not be proven
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.Nested%28Marten.IQuerySession%29:query|Students/Students.cs:96:5-104:6|HTTP GET route '/students/nested' for query 'Nested' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.StudentEndpoints#method:M%3AStudents.StudentEndpoints.General%28Marten.IQuerySession%29:query|Students/Students.cs:106:5-108:53|HTTP GET route '/students/general-plan' for query 'General' is not represented by the current Screenplay language
        WOLVERINE0002|Information|Unsupported|dotnet://Students/Students/Students.UnrelatedEndpoint#method:M%3AStudents.UnrelatedEndpoint.Get%28Students.UnrelatedSession%29:query|Students/Students.cs:125:5-127:58|HTTP GET route '/students/unrelated' for query 'Unrelated' is not represented by the current Screenplay language
        """;

    void Because() => _transcript = given.contribution_transcript.Compare(Contribution, "marten-compiled-queries");

    [Fact] void should_preserve_every_fact_and_diagnostic() => _transcript.Actual.ShouldEqual(_transcript.Expected);
    [Fact] void should_preserve_full_diagnostic_source_ranges() => string.Join('\n', given.diagnostic_ranges.Of(Contribution)).ShouldEqual(ExpectedDiagnostics);
}
