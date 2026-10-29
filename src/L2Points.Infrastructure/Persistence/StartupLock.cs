using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace L2Points.Infrastructure.Persistence;

/// <summary>
/// Serializes startup work (migrations, seeding) across app instances with a SQL Server application lock.
/// </summary>
internal static class StartupLock
{
    public const string ResourceName = "L2Points.DatabaseInitializer";

    // SQL Server error "Database already exists".
    private const int DatabaseAlreadyExistsError = 1801;

    /// <summary>
    /// Runs <paramref name="action"/> while holding the lock. The lock is session-scoped, so the connection of
    /// <paramref name="db"/> stays open for the whole action; everything that uses the same context instance
    /// (migrations, Identity, seeders) therefore runs on the session that owns the lock.
    /// Providers other than SQL Server (tests on SQLite) just run the action.
    /// When <paramref name="createDatabaseIfMissing"/> is false, a missing database is an error instead of being created
    /// (production: the app connects as server admin and a plain CREATE DATABASE would make an expensive default-tier database).
    /// </summary>
    public static async Task RunExclusiveAsync(
        AppDbContext db, Func<Task> action, TimeSpan timeout, CancellationToken ct = default, bool createDatabaseIfMissing = true)
    {
        if (!db.Database.IsSqlServer())
        {
            await action();
            return;
        }

        // The lock needs an open connection to the app database, which fails (error 4060) on a brand new server
        // where MigrateAsync has not created it yet (docker compose, CI smoke test). Create the empty database first.
        await EnsureDatabaseExistsAsync(db, createDatabaseIfMissing, ct);

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = db.Database.GetDbConnection();

            // Note: the lock belongs to this session. If the connection drops mid-action and is silently re-established
            // (EnableRetryOnFailure), the lock is released. Accepted: the window is tiny and only exists at startup.

            EnsureAcquired(await ExecuteLockCommandAsync(
                connection, "sp_getapplock", timeout, ct, ("@LockMode", "Exclusive"), ("@LockTimeout", (int)timeout.TotalMilliseconds)));
            try
            {
                await action();
            }
            finally
            {
                // The lock is also released when the session ends; the explicit release must not mask an action error.
                try
                {
                    await ExecuteLockCommandAsync(connection, "sp_releaseapplock", timeout, CancellationToken.None);
                }
                catch (Exception)
                {
                }
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Decides what to do with the database at startup: <c>true</c> = create it, <c>false</c> = it exists;
    /// throws when it is missing and creating it is not allowed.
    /// </summary>
    public static bool ShouldCreateDatabase(bool exists, bool createIfMissing)
    {
        if (exists)
        {
            return false;
        }

        if (!createIfMissing)
        {
            throw new InvalidOperationException(
                $"The database does not exist and the app refuses to create it automatically " +
                $"('{DependencyInjection.CreateDatabaseIfMissingKey}' is false). " +
                "Check the connection string and the database name, or restore the database first.");
        }

        return true;
    }

    private static async Task EnsureDatabaseExistsAsync(AppDbContext db, bool createIfMissing, CancellationToken ct)
    {
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!ShouldCreateDatabase(await creator.ExistsAsync(ct), createIfMissing))
        {
            return;
        }

        try
        {
            await creator.CreateAsync(ct);
        }
        catch (SqlException ex) when (ex.Number == DatabaseAlreadyExistsError)
        {
            // Another instance created it in the meantime.
        }
    }

    /// <summary>
    /// <c>sp_getapplock</c> returns 0 (granted) or 1 (granted after waiting); negative values mean the lock was not acquired.
    /// </summary>
    public static void EnsureAcquired(int resultCode)
    {
        if (resultCode < 0)
        {
            throw new InvalidOperationException(
                $"Could not acquire the startup lock '{ResourceName}' (sp_getapplock returned {resultCode}).");
        }
    }

    private static async Task<int> ExecuteLockCommandAsync(
        DbConnection connection,
        string procedure,
        TimeSpan timeout,
        CancellationToken ct,
        params (string Name, object Value)[] extraParameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedure;
        // The wait inside sp_getapplock must not be cut short by the default 30 s command timeout.
        command.CommandTimeout = (int)timeout.TotalSeconds + 30;

        AddParameter(command, "@Resource", ResourceName);
        AddParameter(command, "@LockOwner", "Session");
        foreach (var (name, value) in extraParameters)
        {
            AddParameter(command, name, value);
        }

        var result = command.CreateParameter();
        result.ParameterName = "@Result";
        result.DbType = DbType.Int32;
        result.Direction = ParameterDirection.ReturnValue;
        command.Parameters.Add(result);

        await command.ExecuteNonQueryAsync(ct);
        return (int)result.Value!;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
