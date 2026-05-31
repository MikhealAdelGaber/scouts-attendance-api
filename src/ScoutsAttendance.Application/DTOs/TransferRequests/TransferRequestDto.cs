using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.TransferRequests;

// ─── Read DTOs ────────────────────────────────────────────────────────────────

public class TransferRequestDto
{
    public Guid                  Id              { get; set; }
    public Guid                  MemberId        { get; set; }
    public string                MemberName      { get; set; } = string.Empty;
    public Guid                  FromGroupId     { get; set; }
    public string                FromGroupName   { get; set; } = string.Empty;
    public Guid                  ToGroupId       { get; set; }
    public string                ToGroupName     { get; set; } = string.Empty;
    public string                RequestedBy     { get; set; } = string.Empty;
    public DateTime              RequestedAt     { get; set; }
    public TransferRequestStatus Status          { get; set; }
    public string                StatusLabel     { get; set; } = string.Empty;
    public string?               ReviewedBy      { get; set; }
    public DateTime?             ReviewedAt      { get; set; }
    public string?               RejectionReason { get; set; }
    public string?               Notes           { get; set; }
}

// ─── Write DTOs ───────────────────────────────────────────────────────────────

public class CreateTransferRequestDto
{
    [Required]
    public Guid    MemberId  { get; set; }

    [Required]
    public Guid    ToGroupId { get; set; }

    [MaxLength(500)]
    public string? Notes     { get; set; }
}

public class ReviewTransferRequestDto
{
    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}

// ─── Archive DTO ──────────────────────────────────────────────────────────────

public class MemberTransferArchiveDto
{
    public Guid     Id                    { get; set; }
    public Guid     MemberId              { get; set; }
    public string   MemberName            { get; set; } = string.Empty;
    public Guid     FromGroupId           { get; set; }
    public string   FromGroupName         { get; set; } = string.Empty;
    public Guid     ToGroupId             { get; set; }
    public string   ToGroupName           { get; set; } = string.Empty;
    public DateTime TransferDate          { get; set; }
    public decimal  TotalPointsAtTransfer { get; set; }
    public int      TotalAttendanceCount  { get; set; }
    public int      TotalEventsAttended   { get; set; }
    public int      TotalExcusesCount     { get; set; }
    public DateTime ArchivedAt            { get; set; }
}
