using System.ComponentModel.DataAnnotations;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Application.DTOs.Members;

public class MemberDto
{
    public Guid    Id             { get; set; }
    public int     CustomId       { get; set; }
    public Gender  Gender         { get; set; }
    public string  FirstName      { get; set; } = string.Empty;
    public string  LastName       { get; set; } = string.Empty;
    public string  FullName       { get; set; } = string.Empty;
    public string? PhoneNumber    { get; set; }
    public DateTime DateOfBirth   { get; set; }
    public Guid?   TroopId        { get; set; }   // null = unassigned
    public string? TroopName      { get; set; }
    public Guid    GroupId        { get; set; }
    public string  GroupName      { get; set; } = string.Empty;
    public string  QrCode         { get; set; } = string.Empty;
    public decimal TotalPoints    { get; set; }
    public DateTime CreatedAt     { get; set; }

    // Extended profile fields
    public string? Address         { get; set; }
    public string? Region          { get; set; }
    public bool    HasNeckerchief  { get; set; }
    public int?    YearJoined      { get; set; }
    public string? AcademicYear    { get; set; }
    public string? FatherPhone     { get; set; }
    public string? MotherPhone     { get; set; }
    public string? Notes           { get; set; }
    public bool    HasActiveExcuse { get; set; }
}

public class CreateMemberDto
{
    [Required] public string   FirstName      { get; set; } = string.Empty;
    [Required] public string   LastName       { get; set; } = string.Empty;
    public string?   PhoneNumber    { get; set; }
    [Required] public DateTime DateOfBirth    { get; set; }
    [Required] public Guid     TroopId        { get; set; }
    public Guid?     UserId         { get; set; }
    [Required] public Gender   Gender         { get; set; } = Gender.Male;

    // Extended fields
    public string?   Address        { get; set; }
    public string?   Region         { get; set; }
    public bool      HasNeckerchief { get; set; } = false;
    public int?      YearJoined     { get; set; }
    public string?   AcademicYear   { get; set; }
    public string?   FatherPhone    { get; set; }
    public string?   MotherPhone    { get; set; }
    public string?   Notes          { get; set; }
}

public class UpdateMemberDto
{
    [Required] public string   FirstName      { get; set; } = string.Empty;
    [Required] public string   LastName       { get; set; } = string.Empty;
    public string?   PhoneNumber    { get; set; }
    public DateTime  DateOfBirth    { get; set; }
    public Guid?     TroopId        { get; set; }
    public Gender    Gender         { get; set; } = Gender.Male;

    // Extended fields
    public string?   Address        { get; set; }
    public string?   Region         { get; set; }
    public bool      HasNeckerchief { get; set; }
    public int?      YearJoined     { get; set; }
    public string?   AcademicYear   { get; set; }
    public string?   FatherPhone    { get; set; }
    public string?   MotherPhone    { get; set; }
    public string?   Notes          { get; set; }
}

/// <summary>Bulk update academic year / grade at start of year.</summary>
public class BulkYearUpdateDto
{
    [Required] public Guid   TroopId      { get; set; }
    public string?           AcademicYear { get; set; }
    /// <summary>If true, increment each member's YearJoined by 1 (upgrade grade).</summary>
    public bool              AdvanceGrade { get; set; } = false;
}
