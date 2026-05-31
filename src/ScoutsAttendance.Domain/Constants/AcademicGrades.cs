namespace ScoutsAttendance.Domain.Constants;

/// <summary>
/// Canonical list of allowed academic-grade values (stored as Arabic strings).
/// Referenced by DTO validation attributes and import/export logic.
/// </summary>
public static class AcademicGrades
{
    public static readonly string[] AllowedValues =
    [
        "3 ابتدائي",
        "4 ابتدائي",
        "5 ابتدائي",
        "6 ابتدائي",
        "1 اعدادي",
        "2 اعدادي",
        "3 اعدادي",
        "1 ثانوي",
        "2 ثانوي",
        "3 ثانوي",
        "1 جامعة",
        "2 جامعة",
        "3 جامعة",
        "4 جامعة",
        "5 جامعة",
        "6 جامعة",
        "خريج",
    ];

    /// <summary>Returns true if <paramref name="value"/> is null/empty or one of the allowed grades.</summary>
    public static bool IsValid(string? value) =>
        string.IsNullOrEmpty(value) || AllowedValues.Contains(value);
}
