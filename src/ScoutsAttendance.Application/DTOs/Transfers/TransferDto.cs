using System.ComponentModel.DataAnnotations;

namespace ScoutsAttendance.Application.DTOs.Transfers;

public class TransferDto
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public Guid FromTroopId { get; set; }
    public string FromTroopName { get; set; } = string.Empty;
    public Guid ToTroopId { get; set; }
    public string ToTroopName { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTransferDto
{
    [Required] public Guid MemberId { get; set; }
    [Required] public Guid ToTroopId { get; set; }
    public string? Reason { get; set; }
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
}
