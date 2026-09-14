using ClassCommander.TestPlatform.Serialization;
using Microsoft.Data.Sqlite;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestPlatform.Storage;

internal sealed class TestPlatformRepository
{
    private readonly TestPlatformDb _db;

    public TestPlatformRepository(TestPlatformDb db)
    {
        _db = db;
    }

    public TestDefinitionDto UpsertDefinition(TestDefinitionDto definition, TestStatus status = TestStatus.Draft)
    {
        var now = DateTime.UtcNow;
        var questionCount = definition.Groups.Sum(group => group.Questions.Count);
        var json = TestPlatformJson.Serialize(definition);
        var subjectsJson = TestPlatformJson.Serialize(definition.Subjects);

        _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO test_definitions (
                  public_id, version, title, description, grade, subjects_json, status, question_count,
                  definition_json, created_at_utc, updated_at_utc)
                VALUES ($publicId, $version, $title, $description, $grade, $subjectsJson, $status, $questionCount,
                  $definitionJson, $createdAt, $updatedAt)
                ON CONFLICT(public_id, version) DO UPDATE SET
                  title = excluded.title,
                  description = excluded.description,
                  grade = excluded.grade,
                  subjects_json = excluded.subjects_json,
                  status = excluded.status,
                  question_count = excluded.question_count,
                  definition_json = excluded.definition_json,
                  updated_at_utc = excluded.updated_at_utc;
                """;
            command.Parameters.AddWithValue("$publicId", definition.PublicId);
            command.Parameters.AddWithValue("$version", definition.Version);
            command.Parameters.AddWithValue("$title", definition.Title);
            command.Parameters.AddWithValue("$description", (object?)definition.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("$grade", (object?)definition.Grade ?? DBNull.Value);
            command.Parameters.AddWithValue("$subjectsJson", subjectsJson);
            command.Parameters.AddWithValue("$status", (int)status);
            command.Parameters.AddWithValue("$questionCount", questionCount);
            command.Parameters.AddWithValue("$definitionJson", json);
            command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        });

        return definition;
    }

    public PagedResponseDto<TestDefinitionListItemDto> ListDefinitions(
        string? search = null,
        string? grade = null,
        string? subject = null,
        TestStatus? status = null)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT public_id, version, title, description, grade, subjects_json, question_count, updated_at_utc, status
                FROM test_definitions
                ORDER BY updated_at_utc DESC, public_id ASC, version DESC;
                """;

            var items = new List<TestDefinitionListItemDto>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var itemStatus = (TestStatus)reader.GetInt32(8);
                if (status is not null && itemStatus != status.Value)
                {
                    continue;
                }

                var title = reader.GetString(2);
                var description = reader.IsDBNull(3) ? null : reader.GetString(3);
                var itemGrade = reader.IsDBNull(4) ? null : reader.GetString(4);
                var subjects = TestPlatformJson.Deserialize<List<string>>(reader.GetString(5));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var haystack = $"{title} {description}".ToLowerInvariant();
                    if (!haystack.Contains(search.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(grade)
                    && !string.Equals(itemGrade, grade, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(subject)
                    && !subjects.Any(s => string.Equals(s, subject, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                items.Add(new TestDefinitionListItemDto(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    title,
                    description,
                    itemGrade,
                    subjects,
                    reader.GetInt32(6),
                    DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind)));
            }

            return new PagedResponseDto<TestDefinitionListItemDto>(items, items.Count);
        });
    }

    public TestDefinitionDto? GetDefinition(string publicId, int? version = null)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            if (version is null)
            {
                command.CommandText =
                    """
                    SELECT definition_json
                    FROM test_definitions
                    WHERE public_id = $publicId
                    ORDER BY version DESC
                    LIMIT 1;
                    """;
            }
            else
            {
                command.CommandText =
                    """
                    SELECT definition_json
                    FROM test_definitions
                    WHERE public_id = $publicId AND version = $version
                    LIMIT 1;
                    """;
                command.Parameters.AddWithValue("$version", version.Value);
            }

            command.Parameters.AddWithValue("$publicId", publicId);
            var json = command.ExecuteScalar() as string;
            return json is null ? null : TestPlatformJson.Deserialize<TestDefinitionDto>(json);
        });
    }

    public AssignmentDto CreateAssignment(CreateAssignmentRequest request)
    {
        var definition = GetDefinition(request.TestPublicId, request.TestVersion)
            ?? throw new InvalidOperationException($"Test definition '{request.TestPublicId}' v{request.TestVersion} was not found.");

        var assignment = new AssignmentDto(
            $"assignment_{Guid.NewGuid():N}",
            definition.PublicId,
            definition.Version,
            request.Title,
            request.Audience,
            request.Availability,
            request.AttemptPolicy,
            request.ResultPolicy,
            AssignmentStatus.Published);

        var now = DateTime.UtcNow;
        _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO assignments (
                  public_id, test_public_id, test_version, title, audience_json, availability_json,
                  attempt_policy_json, result_policy_json, status, created_at_utc, updated_at_utc)
                VALUES (
                  $publicId, $testPublicId, $testVersion, $title, $audienceJson, $availabilityJson,
                  $attemptPolicyJson, $resultPolicyJson, $status, $createdAt, $updatedAt);
                """;
            command.Parameters.AddWithValue("$publicId", assignment.PublicId);
            command.Parameters.AddWithValue("$testPublicId", assignment.TestPublicId);
            command.Parameters.AddWithValue("$testVersion", assignment.TestVersion);
            command.Parameters.AddWithValue("$title", assignment.Title);
            command.Parameters.AddWithValue("$audienceJson", TestPlatformJson.Serialize(assignment.Audience));
            command.Parameters.AddWithValue("$availabilityJson", TestPlatformJson.Serialize(assignment.Availability));
            command.Parameters.AddWithValue("$attemptPolicyJson", TestPlatformJson.Serialize(assignment.AttemptPolicy));
            command.Parameters.AddWithValue("$resultPolicyJson", TestPlatformJson.Serialize(assignment.ResultPolicy));
            command.Parameters.AddWithValue("$status", (int)assignment.Status);
            command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        });

        return assignment;
    }

    public AssignmentDto? GetAssignment(string publicId)
    {
        return _db.InTransaction(connection => ReadAssignment(connection, publicId));
    }

    public PagedResponseDto<AssignmentDto> ListAssignments(AssignmentStatus? status = null, string? testPublicId = null)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT public_id, test_public_id, test_version, title, audience_json, availability_json,
                       attempt_policy_json, result_policy_json, status
                FROM assignments
                ORDER BY created_at_utc DESC;
                """;

            var items = new List<AssignmentDto>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var item = MapAssignment(reader);
                if (status is not null && item.Status != status.Value)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(testPublicId)
                    && !string.Equals(item.TestPublicId, testPublicId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                items.Add(item);
            }

            return new PagedResponseDto<AssignmentDto>(items, items.Count);
        });
    }

    public AssignmentDto? SetAssignmentStatus(string publicId, AssignmentStatus status)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE assignments
                SET status = $status, updated_at_utc = $updatedAt
                WHERE public_id = $publicId;
                """;
            command.Parameters.AddWithValue("$status", (int)status);
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$publicId", publicId);
            if (command.ExecuteNonQuery() == 0)
            {
                return null;
            }

            return ReadAssignment(connection, publicId);
        });
    }

    public IReadOnlyList<ActiveAssignmentDto> ListActiveAssignmentsForStudent(AttemptStudentDto student)
    {
        var now = DateTime.UtcNow;
        return ListAssignments(AssignmentStatus.Published).Items
            .Where(assignment => IsAssignmentAvailable(assignment, now))
            .Where(assignment => MatchesAudience(assignment.Audience, student))
            .Select(assignment => new ActiveAssignmentDto(
                assignment.PublicId,
                assignment.Title,
                assignment.Availability.StartUtc,
                assignment.Availability.EndUtc))
            .ToList();
    }

    public AttemptDto? GetAttempt(string publicId)
    {
        return _db.InTransaction(connection => ReadAttempt(connection, publicId));
    }

    public AttemptDto? GetAttemptByToken(string attemptToken)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT public_id FROM attempts WHERE attempt_token = $token LIMIT 1;
                """;
            command.Parameters.AddWithValue("$token", attemptToken);
            var publicId = command.ExecuteScalar() as string;
            return publicId is null ? null : ReadAttempt(connection, publicId);
        });
    }

    public (AttemptDto Attempt, string AttemptToken) StartOrResumeAttempt(StartAttemptRequest request)
    {
        var assignment = GetAssignment(request.AssignmentPublicId)
            ?? throw new InvalidOperationException($"Assignment '{request.AssignmentPublicId}' was not found.");

        if (assignment.Status != AssignmentStatus.Published)
        {
            throw new InvalidOperationException("Assignment is not published.");
        }

        if (!IsAssignmentAvailable(assignment, DateTime.UtcNow))
        {
            throw new InvalidOperationException("Assignment is outside its availability window.");
        }

        var existing = FindInProgressAttempt(assignment.PublicId, request.Student);
        if (existing is not null)
        {
            var token = GetAttemptToken(existing.PublicId)
                ?? throw new InvalidOperationException("Attempt token was not found.");
            return (existing, token);
        }

        var now = DateTime.UtcNow;
        var attempt = new AttemptDto(
            $"attempt_{Guid.NewGuid():N}",
            assignment.PublicId,
            assignment.TestPublicId,
            assignment.TestVersion,
            request.Student,
            AttemptStatus.InProgress,
            now,
            null,
            null,
            []);
        var attemptToken = Guid.NewGuid().ToString("N");

        _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO attempts (
                  public_id, assignment_public_id, test_public_id, test_version, student_json, status,
                  attempt_token, answers_json, started_at_utc, last_saved_at_utc, submitted_at_utc,
                  created_at_utc, updated_at_utc)
                VALUES (
                  $publicId, $assignmentPublicId, $testPublicId, $testVersion, $studentJson, $status,
                  $attemptToken, $answersJson, $startedAt, NULL, NULL, $createdAt, $updatedAt);
                """;
            command.Parameters.AddWithValue("$publicId", attempt.PublicId);
            command.Parameters.AddWithValue("$assignmentPublicId", attempt.AssignmentPublicId);
            command.Parameters.AddWithValue("$testPublicId", attempt.TestPublicId);
            command.Parameters.AddWithValue("$testVersion", attempt.TestVersion);
            command.Parameters.AddWithValue("$studentJson", TestPlatformJson.Serialize(attempt.Student));
            command.Parameters.AddWithValue("$status", (int)attempt.Status);
            command.Parameters.AddWithValue("$attemptToken", attemptToken);
            command.Parameters.AddWithValue("$answersJson", TestPlatformJson.Serialize(attempt.Answers));
            command.Parameters.AddWithValue("$startedAt", attempt.StartedAtUtc.ToString("O"));
            command.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            command.ExecuteNonQuery();
        });

        return (attempt, attemptToken);
    }

    public AttemptDto SaveProgress(string attemptPublicId, IReadOnlyList<AttemptAnswerDto> answers)
    {
        return _db.InTransaction(connection =>
        {
            var existing = ReadAttempt(connection, attemptPublicId)
                ?? throw new InvalidOperationException($"Attempt '{attemptPublicId}' was not found.");
            if (existing.Status is AttemptStatus.Submitted or AttemptStatus.Scored or AttemptStatus.Cancelled)
            {
                throw new InvalidOperationException("Attempt is already closed.");
            }

            var now = DateTime.UtcNow;
            var merged = MergeAnswers(existing.Answers, answers, now);
            var updated = existing with
            {
                Answers = merged,
                LastSavedAtUtc = now,
                Status = AttemptStatus.InProgress,
            };

            using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE attempts
                SET answers_json = $answersJson,
                    last_saved_at_utc = $lastSavedAt,
                    status = $status,
                    updated_at_utc = $updatedAt
                WHERE public_id = $publicId;
                """;
            command.Parameters.AddWithValue("$answersJson", TestPlatformJson.Serialize(updated.Answers));
            command.Parameters.AddWithValue("$lastSavedAt", now.ToString("O"));
            command.Parameters.AddWithValue("$status", (int)updated.Status);
            command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            command.Parameters.AddWithValue("$publicId", attemptPublicId);
            command.ExecuteNonQuery();
            return updated;
        });
    }

    public (AttemptDto Attempt, ResultDto Result) SubmitAttempt(
        string attemptPublicId,
        IReadOnlyList<AttemptAnswerDto> answers,
        ResultDto result)
    {
        return _db.InTransaction(connection =>
        {
            var existing = ReadAttempt(connection, attemptPublicId)
                ?? throw new InvalidOperationException($"Attempt '{attemptPublicId}' was not found.");
            if (existing.Status is AttemptStatus.Submitted or AttemptStatus.Scored)
            {
                var existingResult = ReadResult(connection, attemptPublicId)
                    ?? throw new InvalidOperationException("Submitted attempt is missing a result.");
                return (existing, existingResult);
            }

            var now = DateTime.UtcNow;
            var merged = MergeAnswers(existing.Answers, answers, now);
            var updated = existing with
            {
                Answers = merged,
                Status = AttemptStatus.Scored,
                LastSavedAtUtc = now,
                SubmittedAtUtc = now,
            };

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    UPDATE attempts
                    SET answers_json = $answersJson,
                        status = $status,
                        last_saved_at_utc = $lastSavedAt,
                        submitted_at_utc = $submittedAt,
                        updated_at_utc = $updatedAt
                    WHERE public_id = $publicId;
                    """;
                command.Parameters.AddWithValue("$answersJson", TestPlatformJson.Serialize(updated.Answers));
                command.Parameters.AddWithValue("$status", (int)updated.Status);
                command.Parameters.AddWithValue("$lastSavedAt", now.ToString("O"));
                command.Parameters.AddWithValue("$submittedAt", now.ToString("O"));
                command.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
                command.Parameters.AddWithValue("$publicId", attemptPublicId);
                command.ExecuteNonQuery();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    INSERT INTO results (attempt_public_id, result_json, completed_at_utc)
                    VALUES ($attemptPublicId, $resultJson, $completedAt)
                    ON CONFLICT(attempt_public_id) DO UPDATE SET
                      result_json = excluded.result_json,
                      completed_at_utc = excluded.completed_at_utc;
                    """;
                command.Parameters.AddWithValue("$attemptPublicId", attemptPublicId);
                command.Parameters.AddWithValue("$resultJson", TestPlatformJson.Serialize(result));
                command.Parameters.AddWithValue("$completedAt", result.CompletedAtUtc.ToString("O"));
                command.ExecuteNonQuery();
            }

            return (updated, result);
        });
    }

    public ResultDto? GetResult(string attemptPublicId)
    {
        return _db.InTransaction(connection => ReadResult(connection, attemptPublicId));
    }

    public PagedResponseDto<AttemptListItemDto> ListAttempts(string assignmentPublicId)
    {
        var rows = _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT public_id, student_json, status, started_at_utc, last_saved_at_utc, answers_json,
                       test_public_id, test_version
                FROM attempts
                WHERE assignment_public_id = $assignmentPublicId
                ORDER BY started_at_utc DESC;
                """;
            command.Parameters.AddWithValue("$assignmentPublicId", assignmentPublicId);

            var result = new List<(string PublicId, AttemptStudentDto Student, AttemptStatus Status, DateTime StartedAtUtc, DateTime? LastSavedAtUtc, List<AttemptAnswerDto> Answers, string TestPublicId, int TestVersion)>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add((
                    reader.GetString(0),
                    TestPlatformJson.Deserialize<AttemptStudentDto>(reader.GetString(1)),
                    (AttemptStatus)reader.GetInt32(2),
                    DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    reader.IsDBNull(4)
                        ? null
                        : DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    TestPlatformJson.Deserialize<List<AttemptAnswerDto>>(reader.GetString(5)),
                    reader.GetString(6),
                    reader.GetInt32(7)));
            }

            return result;
        });

        var items = rows.Select(row =>
        {
            var definition = GetDefinition(row.TestPublicId, row.TestVersion);
            var questionCount = definition?.Groups.Sum(g => g.Questions.Count) ?? 0;
            return new AttemptListItemDto(
                row.PublicId,
                row.Student,
                row.Status,
                row.StartedAtUtc,
                row.LastSavedAtUtc,
                row.Answers.Select(a => a.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                questionCount);
        }).ToList();

        return new PagedResponseDto<AttemptListItemDto>(items, items.Count);
    }

    private AttemptDto? FindInProgressAttempt(string assignmentPublicId, AttemptStudentDto student)
    {
        var candidateIds = _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT public_id, student_json
                FROM attempts
                WHERE assignment_public_id = $assignmentPublicId
                  AND status IN ($inProgress, $notStarted)
                ORDER BY started_at_utc DESC;
                """;
            command.Parameters.AddWithValue("$assignmentPublicId", assignmentPublicId);
            command.Parameters.AddWithValue("$inProgress", (int)AttemptStatus.InProgress);
            command.Parameters.AddWithValue("$notStarted", (int)AttemptStatus.NotStarted);

            var matches = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var existingStudent = TestPlatformJson.Deserialize<AttemptStudentDto>(reader.GetString(1));
                if (StudentsMatch(existingStudent, student))
                {
                    matches.Add(reader.GetString(0));
                }
            }

            return matches;
        });

        return candidateIds.Count == 0 ? null : GetAttempt(candidateIds[0]);
    }

    private string? GetAttemptToken(string attemptPublicId)
    {
        return _db.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT attempt_token FROM attempts WHERE public_id = $publicId LIMIT 1;";
            command.Parameters.AddWithValue("$publicId", attemptPublicId);
            return command.ExecuteScalar() as string;
        });
    }

    private static AssignmentDto? ReadAssignment(SqliteConnection connection, string publicId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT public_id, test_public_id, test_version, title, audience_json, availability_json,
                   attempt_policy_json, result_policy_json, status
            FROM assignments
            WHERE public_id = $publicId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$publicId", publicId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAssignment(reader) : null;
    }

    private static AssignmentDto MapAssignment(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            TestPlatformJson.Deserialize<AssignmentAudienceDto>(reader.GetString(4)),
            TestPlatformJson.Deserialize<AssignmentAvailabilityDto>(reader.GetString(5)),
            TestPlatformJson.Deserialize<AttemptPolicyDto>(reader.GetString(6)),
            TestPlatformJson.Deserialize<ResultPolicyDto>(reader.GetString(7)),
            (AssignmentStatus)reader.GetInt32(8));

    private static AttemptDto? ReadAttempt(SqliteConnection connection, string publicId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT public_id, assignment_public_id, test_public_id, test_version, student_json, status,
                   answers_json, started_at_utc, last_saved_at_utc, submitted_at_utc
            FROM attempts
            WHERE public_id = $publicId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$publicId", publicId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new AttemptDto(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt32(3),
            TestPlatformJson.Deserialize<AttemptStudentDto>(reader.GetString(4)),
            (AttemptStatus)reader.GetInt32(5),
            DateTime.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(8)
                ? null
                : DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(9)
                ? null
                : DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
            TestPlatformJson.Deserialize<List<AttemptAnswerDto>>(reader.GetString(6)));
    }

    private static ResultDto? ReadResult(SqliteConnection connection, string attemptPublicId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT result_json FROM results WHERE attempt_public_id = $attemptPublicId LIMIT 1;
            """;
        command.Parameters.AddWithValue("$attemptPublicId", attemptPublicId);
        var json = command.ExecuteScalar() as string;
        return json is null ? null : TestPlatformJson.Deserialize<ResultDto>(json);
    }

    private static IReadOnlyList<AttemptAnswerDto> MergeAnswers(
        IReadOnlyList<AttemptAnswerDto> existing,
        IReadOnlyList<AttemptAnswerDto> incoming,
        DateTime savedAtUtc)
    {
        var map = existing.ToDictionary(a => a.QuestionId, StringComparer.OrdinalIgnoreCase);
        foreach (var answer in incoming)
        {
            map[answer.QuestionId] = answer with { SavedAtUtc = savedAtUtc };
        }

        return map.Values.OrderBy(a => a.QuestionId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsAssignmentAvailable(AssignmentDto assignment, DateTime utcNow)
    {
        if (assignment.Availability.StartUtc is { } start && utcNow < start)
        {
            return false;
        }

        if (assignment.Availability.EndUtc is { } end && utcNow > end)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesAudience(AssignmentAudienceDto audience, AttemptStudentDto student)
    {
        return audience.Type switch
        {
            AudienceType.Class => string.IsNullOrWhiteSpace(audience.ClassPublicId)
                || string.Equals(audience.ClassPublicId, student.ClassName, StringComparison.OrdinalIgnoreCase),
            AudienceType.StudentSelection => audience.StudentPublicIds is null
                || audience.StudentPublicIds.Count == 0
                || (!string.IsNullOrWhiteSpace(student.StudentPublicId)
                    && audience.StudentPublicIds.Contains(student.StudentPublicId, StringComparer.OrdinalIgnoreCase)),
            _ => true,
        };
    }

    private static bool StudentsMatch(AttemptStudentDto left, AttemptStudentDto right)
    {
        if (!string.IsNullOrWhiteSpace(left.StudentPublicId)
            && !string.IsNullOrWhiteSpace(right.StudentPublicId))
        {
            return string.Equals(left.StudentPublicId, right.StudentPublicId, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.Surname, right.Surname, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.ClassName ?? string.Empty, right.ClassName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
