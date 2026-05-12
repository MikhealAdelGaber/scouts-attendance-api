using ScoutsAttendance.Application.DTOs.Members;

namespace ScoutsAttendance.Application.Interfaces;

public interface IMemberImportService
{
    /// <summary>Returns a pre-formatted .xlsx template file the user should fill in.</summary>
    byte[] GenerateTemplate();

    /// <summary>
    /// Reads the uploaded Excel file, validates each row, auto-generates CustomIds,
    /// skips duplicates, and saves all valid members in a single transaction.
    /// </summary>
    Task<ImportMembersResultDto> ImportAsync(Stream fileStream, Guid troopId);
}
