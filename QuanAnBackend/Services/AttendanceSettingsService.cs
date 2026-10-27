using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using QuanAnBackend.Data;

namespace QuanAnBackend.Services;

public sealed record AttendanceLocationSettings(
    bool IsConfigured,
    double? Latitude,
    double? Longitude,
    double RadiusMeters,
    double MaxAccuracyMeters,
    DateTime? UpdatedAt,
    int? UpdatedBy
);

public class AttendanceSettingsService
{
    private readonly AppDbContext _db;

    public AttendanceSettingsService(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureAsync()
    {
        await _db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(
                N'dbo.AttendanceSettings',
                N'U'
            ) IS NULL
            BEGIN
                CREATE TABLE dbo.AttendanceSettings
                (
                    SettingId INT NOT NULL
                        CONSTRAINT PK_AttendanceSettings
                        PRIMARY KEY,
                    RestaurantLatitude
                        DECIMAL(9, 6) NULL,
                    RestaurantLongitude
                        DECIMAL(9, 6) NULL,
                    RadiusMeters FLOAT NOT NULL
                        CONSTRAINT DF_AttendanceSettings_Radius
                        DEFAULT (100),
                    MaxAccuracyMeters FLOAT NOT NULL
                        CONSTRAINT DF_AttendanceSettings_Accuracy
                        DEFAULT (50),
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy INT NULL
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM dbo.AttendanceSettings
                WHERE SettingId = 1
            )
            BEGIN
                INSERT INTO dbo.AttendanceSettings
                (
                    SettingId,
                    RestaurantLatitude,
                    RestaurantLongitude,
                    RadiusMeters,
                    MaxAccuracyMeters,
                    UpdatedAt,
                    UpdatedBy
                )
                VALUES
                (
                    1,
                    NULL,
                    NULL,
                    100,
                    50,
                    NULL,
                    NULL
                );
            END;
            """
        );
    }

    public async Task<AttendanceLocationSettings>
        GetAsync()
    {
        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            return await ReadAsync(
                connection,
                null
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<AttendanceLocationSettings>
        GetAsync(
            DbConnection connection,
            DbTransaction? transaction)
    {
        return await ReadAsync(
            connection,
            transaction
        );
    }

    public async Task<AttendanceLocationSettings>
        UpdateAsync(
            double latitude,
            double longitude,
            double radiusMeters,
            double maxAccuracyMeters,
            int? updatedBy)
    {
        var connection =
            _db.Database.GetDbConnection();

        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
                UPDATE dbo.AttendanceSettings
                SET RestaurantLatitude = @Latitude,
                    RestaurantLongitude = @Longitude,
                    RadiusMeters = @RadiusMeters,
                    MaxAccuracyMeters = @MaxAccuracyMeters,
                    UpdatedAt = GETDATE(),
                    UpdatedBy = @UpdatedBy
                WHERE SettingId = 1;
                """;

            AddParameter(
                command,
                "@Latitude",
                latitude
            );

            AddParameter(
                command,
                "@Longitude",
                longitude
            );

            AddParameter(
                command,
                "@RadiusMeters",
                radiusMeters
            );

            AddParameter(
                command,
                "@MaxAccuracyMeters",
                maxAccuracyMeters
            );

            AddParameter(
                command,
                "@UpdatedBy",
                updatedBy
            );

            await command.ExecuteNonQueryAsync();

            return await ReadAsync(
                connection,
                null
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<
        AttendanceLocationSettings> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction)
    {
        await using var command =
            connection.CreateCommand();

        command.Transaction = transaction;

        command.CommandText =
            """
            SELECT TOP 1
                RestaurantLatitude,
                RestaurantLongitude,
                RadiusMeters,
                MaxAccuracyMeters,
                UpdatedAt,
                UpdatedBy
            FROM dbo.AttendanceSettings
            WHERE SettingId = 1;
            """;

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return new AttendanceLocationSettings(
                false,
                null,
                null,
                100,
                50,
                null,
                null
            );
        }

        var latitude =
            reader.IsDBNull(0)
                ? (double?)null
                : Convert.ToDouble(
                    reader.GetValue(0)
                );

        var longitude =
            reader.IsDBNull(1)
                ? (double?)null
                : Convert.ToDouble(
                    reader.GetValue(1)
                );

        var radius =
            reader.IsDBNull(2)
                ? 100
                : Convert.ToDouble(
                    reader.GetValue(2)
                );

        var maxAccuracy =
            reader.IsDBNull(3)
                ? 50
                : Convert.ToDouble(
                    reader.GetValue(3)
                );

        return new AttendanceLocationSettings(
            latitude.HasValue &&
                longitude.HasValue,
            latitude,
            longitude,
            radius,
            maxAccuracy,
            reader.IsDBNull(4)
                ? null
                : reader.GetDateTime(4),
            reader.IsDBNull(5)
                ? null
                : reader.GetInt32(5)
        );
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter =
            command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value =
            value ?? DBNull.Value;

        command.Parameters.Add(parameter);
    }
}
