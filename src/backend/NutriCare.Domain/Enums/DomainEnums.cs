namespace NutriCare.Domain.Enums;

/// <summary>
/// Role types in the system
/// </summary>
public enum RoleType
{
    Admin = 1,
    Nutritionist = 2,
    Patient = 3
}

/// <summary>
/// Appointment status
/// </summary>
public enum AppointmentStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    NoShow,
    Rescheduled
}

/// <summary>
/// Appointment type
/// </summary>
public enum AppointmentType
{
    Initial,
    FollowUp,
    Video,
    InPerson
}

/// <summary>
/// Payment status
/// </summary>
public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
    PartialRefund
}

/// <summary>
/// Diet plan status
/// </summary>
public enum DietPlanStatus
{
    Active,
    Completed,
    Archived,
    OnHold
}

/// <summary>
/// Gender types
/// </summary>
public enum Gender
{
    Male,
    Female,
    Other
}

/// <summary>
/// Blood group types
/// </summary>
public enum BloodGroup
{
    APlusPositive,
    APlusNegative,
    BPlusPositive,
    BPlusNegative,
    OPlusPositive,
    OPlusNegative,
    ABPlusPositive,
    ABPlusNegative
}
