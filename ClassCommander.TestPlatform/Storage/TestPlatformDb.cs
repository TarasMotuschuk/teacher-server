using Microsoft.Data.Sqlite;

namespace ClassCommander.TestPlatform.Storage;

internal sealed class TestPlatformDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _sync = new();

    public TestPlatformDb(TestPlatformPaths paths)
    {
        paths.EnsureCreated();
        _connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
        _connection.Open();
        InitializeSchema();
    }

    public SqliteConnection Connection => _connection;

    public T InTransaction<T>(Func<SqliteConnection, T> action)
    {
        lock (_sync)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                var result = action(_connection);
                transaction.Commit();
                return result;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    public void InTransaction(Action<SqliteConnection> action)
    {
        InTransaction(connection =>
        {
            action(connection);
            return true;
        });
    }

    public void Dispose() => _connection.Dispose();

    private void InitializeSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS test_definitions (
              public_id TEXT NOT NULL,
              version INTEGER NOT NULL,
              title TEXT NOT NULL,
              description TEXT NULL,
              grade TEXT NULL,
              subjects_json TEXT NOT NULL,
              status INTEGER NOT NULL,
              question_count INTEGER NOT NULL,
              definition_json TEXT NOT NULL,
              created_at_utc TEXT NOT NULL,
              updated_at_utc TEXT NOT NULL,
              PRIMARY KEY (public_id, version)
            );

            CREATE TABLE IF NOT EXISTS assignments (
              public_id TEXT NOT NULL PRIMARY KEY,
              test_public_id TEXT NOT NULL,
              test_version INTEGER NOT NULL,
              title TEXT NOT NULL,
              audience_json TEXT NOT NULL,
              availability_json TEXT NOT NULL,
              attempt_policy_json TEXT NOT NULL,
              result_policy_json TEXT NOT NULL,
              status INTEGER NOT NULL,
              created_at_utc TEXT NOT NULL,
              updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS attempts (
              public_id TEXT NOT NULL PRIMARY KEY,
              assignment_public_id TEXT NOT NULL,
              test_public_id TEXT NOT NULL,
              test_version INTEGER NOT NULL,
              student_json TEXT NOT NULL,
              status INTEGER NOT NULL,
              attempt_token TEXT NOT NULL UNIQUE,
              answers_json TEXT NOT NULL,
              started_at_utc TEXT NOT NULL,
              last_saved_at_utc TEXT NULL,
              submitted_at_utc TEXT NULL,
              created_at_utc TEXT NOT NULL,
              updated_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS results (
              attempt_public_id TEXT NOT NULL PRIMARY KEY,
              result_json TEXT NOT NULL,
              completed_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_assignments_test ON assignments(test_public_id, test_version);
            CREATE INDEX IF NOT EXISTS ix_attempts_assignment ON attempts(assignment_public_id);
            """;
        command.ExecuteNonQuery();
    }
}
