using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Infrastructure.Data;

namespace ScoutsAttendance.API.Controllers;

/// <summary>
/// Temporary diagnostic controller — SystemAdmin only.
/// Hit GET /api/diagnostic to see the live PostgreSQL DB state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin")]
public class DiagnosticController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DiagnosticController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var isPostgres = _db.Database.ProviderName?
            .Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;

        var result = new Dictionary<string, object>();

        result["provider"] = _db.Database.ProviderName ?? "unknown";

        // ── 1. Member counts ──────────────────────────────────────────────────
        var memberCounts = await RunQueryAsync(@"
            SELECT
                COUNT(*)                                              AS total_members,
                SUM(CASE WHEN ""IsDeleted"" = FALSE THEN 1 ELSE 0 END) AS active_members,
                SUM(CASE WHEN ""IsDeleted"" = TRUE  THEN 1 ELSE 0 END) AS deleted_members,
                SUM(CASE WHEN ""TroopId"" IS NULL
                          AND ""IsDeleted"" = FALSE THEN 1 ELSE 0 END) AS active_unassigned_members
            FROM ""Members""", isPostgres);
        result["member_counts"] = memberCounts;

        // ── 2. Troop counts ───────────────────────────────────────────────────
        var troopCounts = await RunQueryAsync(@"
            SELECT
                COUNT(*)                                              AS total_troops,
                SUM(CASE WHEN ""IsDeleted"" = FALSE THEN 1 ELSE 0 END) AS active_troops,
                SUM(CASE WHEN ""IsDeleted"" = TRUE  THEN 1 ELSE 0 END) AS deleted_troops
            FROM ""Troops""", isPostgres);
        result["troop_counts"] = troopCounts;

        // ── 3. Members grouped by TroopId (active members only) ───────────────
        var membersByTroop = await RunQueryAsync(@"
            SELECT
                m.""TroopId"",
                t.""Name"" AS troop_name,
                t.""IsDeleted"" AS troop_is_deleted,
                COUNT(m.""Id"") AS member_count
            FROM ""Members"" m
            LEFT JOIN ""Troops"" t ON m.""TroopId"" = t.""Id""
            WHERE m.""IsDeleted"" = FALSE
            GROUP BY m.""TroopId"", t.""Name"", t.""IsDeleted""
            ORDER BY member_count DESC", isPostgres);
        result["active_members_by_troop"] = membersByTroop;

        // ── 4. TroopId column definition ──────────────────────────────────────
        var columnInfo = await RunQueryAsync(@"
            SELECT column_name, is_nullable, data_type, udt_name
            FROM information_schema.columns
            WHERE table_name = 'Members' AND column_name = 'TroopId'", isPostgres,
            useLowerCase: true);
        result["TroopId_column_definition"] = columnInfo;

        // ── 5. FK constraints on Members table ────────────────────────────────
        var fkConstraints = await RunQueryAsync(@"
            SELECT
                tc.constraint_name,
                tc.constraint_type,
                kcu.column_name,
                rc.unique_constraint_name,
                rc.delete_rule,
                rc.update_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            LEFT JOIN information_schema.referential_constraints rc
                ON tc.constraint_name = rc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND (tc.table_name = 'Members' OR tc.table_name = 'members')", isPostgres,
            useLowerCase: true);
        result["fk_constraints_on_members"] = fkConstraints;

        // ── 6. All FK constraints touching Troops ─────────────────────────────
        var troopFks = await RunQueryAsync(@"
            SELECT
                tc.table_name,
                tc.constraint_name,
                kcu.column_name,
                rc.delete_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
            LEFT JOIN information_schema.referential_constraints rc
                ON tc.constraint_name = rc.constraint_name
            JOIN information_schema.key_column_usage kcu2
                ON rc.unique_constraint_name = kcu2.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND LOWER(kcu2.table_name) = 'troops'", isPostgres,
            useLowerCase: true);
        result["all_fks_referencing_troops"] = troopFks;

        // ── 7. User TroopId counts ────────────────────────────────────────────
        var userCounts = await RunQueryAsync(@"
            SELECT
                COUNT(*) AS total_users,
                SUM(CASE WHEN ""TroopId"" IS NOT NULL
                          AND ""IsDeleted"" = FALSE THEN 1 ELSE 0 END) AS users_with_troop_scope
            FROM ""Users""", isPostgres);
        result["user_troop_scope"] = userCounts;

        return Ok(result);
    }

    private async Task<List<Dictionary<string, object?>>> RunQueryAsync(
        string sql, bool isPostgres, bool useLowerCase = false)
    {
        // For SQL Server use square-bracket quoting instead of double-quotes
        if (!isPostgres)
            sql = sql.Replace("\"", "");   // SQL Server doesn't need quoting for PascalCase

        var rows = new List<Dictionary<string, object?>>();
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            rows.Add(new Dictionary<string, object?> { ["error"] = ex.Message });
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        return rows;
    }
}
