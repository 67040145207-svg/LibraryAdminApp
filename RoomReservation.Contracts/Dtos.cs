namespace RoomReservation.Contracts;

// ==================== 1. AUTH & QUOTA DTOs ====================

public class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterUserRequest
{
    public string StudentOrStaffId { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Student;
}

public class SsoLoginRequest
{
    public string Provider { get; set; } = "MySQLAuth";
    public string StudentOrStaffId { get; set; } = "";
    public string Password { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Student;
}

public class AuthSessionDto
{
    public Guid UserId { get; set; }
    public string AccessToken { get; set; } = "";
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public UserQuotaDto Quota { get; set; } = new();
}

public class UserQuotaDto
{
    public Guid UserId { get; set; }
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public int DailyQuotaMinutes { get; set; } = 180; // ค่าเริ่มต้น 3 ชั่วโมง (180 นาที) ต่อวัน
    public int UsedMinutesToday { get; set; }
    public int RemainingMinutesToday => Math.Max(0, DailyQuotaMinutes - UsedMinutesToday);
    public bool IsSuspended { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public string? SuspensionReason { get; set; }
}

// ==================== 2. ROOM & TIMELINE DTOs ====================

public class EquipmentDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Working;
}

public class RoomDto
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public RoomType RoomType { get; set; }
    public string RoomTypeDisplay => RoomType == RoomType.Large
        ? "ห้องขนาดใหญ่ (ขั้นต่ำ 7 คน)"
        : "ห้องขนาดกลาง (ขั้นต่ำ 5 คน)";
    public int Floor { get; set; }
    public string Building { get; set; } = "";
    public int MinCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public int MaxBookingHours { get; set; } = 3;
    public string AllowedSubnetCidr { get; set; } = "10.20.0.0/16";
    public double GeoLatitude { get; set; } = 13.8476;
    public double GeoLongitude { get; set; } = 100.5696;
    public int GeoRadiusMeters { get; set; } = 150;
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public List<EquipmentDto> Equipments { get; set; } = new();
    public List<TimelineSlotDto> TodaySlots { get; set; } = new();
}

public class TimelineSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TimeLabel => $"{StartTime:HH:mm} - {EndTime:HH:mm}";
    /// <summary>
    /// Available, Reserved, Occupied, Maintenance
    /// </summary>
    public string SlotState { get; set; } = "Available";
    public Guid? BookingId { get; set; }
    public string? BookingNo { get; set; }
    public string? BookedByName { get; set; }
    public string? Purpose { get; set; }
}

public class DynamicQrTokenDto
{
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string DynamicToken { get; set; } = "";
    public string QrPayload { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int SecondsRemaining => Math.Max(0, (int)(ExpiresAt - DateTime.Now).TotalSeconds);
}

// ==================== 3. BOOKING & PARTICIPANTS DTOs ====================

public class CreateBookingRequest
{
    public Guid RoomId { get; set; }
    public string HostStudentOrStaffId { get; set; } = "";
    public string HostFullName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    /// <summary>
    /// จำนวนผู้เข้าใช้งาน (กรอกเป็นตัวเลข เช่น ห้องกลาง >= 5 คน, ห้องใหญ่ >= 7 คน)
    /// </summary>
    public int ExpectedAttendees { get; set; } = 5;
    public List<string> ParticipantStudentIds { get; set; } = new();
}

public class ParticipantDto
{
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsHost { get; set; }
    public ParticipantVerificationStatus VerificationStatus { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public class BookingDto
{
    public Guid Id { get; set; }
    public string BookingNo { get; set; } = "";
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public RoomType RoomType { get; set; }
    public int MinRequiredAttendees { get; set; }
    public string HostStudentOrStaffId { get; set; } = "";
    public string HostFullName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationHours => Math.Round((EndTime - StartTime).TotalHours, 1);
    public int ExpectedAttendees { get; set; }
    public int VerifiedAttendeesCount { get; set; }
    public BookingStatus Status { get; set; }
    public string StatusDisplay => Status switch
    {
        BookingStatus.Reserved => "จองสำเร็จ (ติดต่อเช็คอินที่ห้อง Admin)",
        BookingStatus.CheckedIn => "เช็คอินแล้ว (Admin เปิดประตูห้องแล้ว)",
        BookingStatus.Completed => "ใช้งานเสร็จสิ้น",
        BookingStatus.AutoReleased => "ตัดสิทธิ์อัตโนมัติ (ไม่มาเช็คอินที่ห้อง Admin ภายใน 15 นาที)",
        BookingStatus.Cancelled => "ยกเลิกแล้ว",
        BookingStatus.EmergencyOverride => "ยกเลิกฉุกเฉินโดย Admin",
        _ => Status.ToString()
    };
    public string CheckInPinCode { get; set; } = "";
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();
}

// ==================== 4. CHECK-IN & GROUP VERIFICATION DTOs ====================

public class CheckInRequest
{
    public Guid BookingId { get; set; }
    public string StudentOrStaffId { get; set; } = "";
    public CheckInMethod Method { get; set; } = CheckInMethod.PinCode;
    public string CodeOrQrToken { get; set; } = "";
    public string ClientIpAddress { get; set; } = "10.20.1.55";
    public double ClientLatitude { get; set; } = 13.8476;
    public double ClientLongitude { get; set; } = 100.5696;
}

public class GroupVerifyRequest
{
    public Guid BookingId { get; set; }
    public string MemberStudentOrStaffId { get; set; } = "";
    public string ClientIpAddress { get; set; } = "10.20.1.55";
    public double ClientLatitude { get; set; } = 13.8476;
    public double ClientLongitude { get; set; } = 100.5696;
}

public class EmergencyOverrideRequest
{
    public Guid BookingId { get; set; }
    public string AdminId { get; set; } = "ADMIN-01";
    public string Reason { get; set; } = "";
}

// ==================== 5. PENALTIES, NOTIFICATIONS & MAINTENANCE DTOs ====================

public class PenaltyDto
{
    public Guid Id { get; set; }
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string BookingNo { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string Reason { get; set; } = "No-Show (ไม่เช็คอินภายใน 15 นาที)";
    public int PenaltyDays { get; set; } = 2;
    public DateTime BanStartAt { get; set; }
    public DateTime BanEndAt { get; set; }
    public bool IsActive { get; set; }
}

public class MaintenanceReportDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string EquipmentName { get; set; } = "";
    public string ReportedBy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class CreateMaintenanceReportRequest
{
    public Guid RoomId { get; set; }
    public string EquipmentName { get; set; } = "";
    public string ReportedBy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
}

public class NotificationLogDto
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = "LINE / Email";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; }
}

public class RoomUtilizationItemDto
{
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string RoomType { get; set; } = "";
    public int TotalAvailableHours { get; set; }
    public double BookedHours { get; set; }
    public double CheckedInHours { get; set; }
    public int NoShowCount { get; set; }
    public double UtilizationRatePercent => TotalAvailableHours > 0
        ? Math.Min(100, Math.Round((BookedHours / TotalAvailableHours) * 100, 1))
        : 0;
}

public class DashboardSummaryDto
{
    public int TotalRooms { get; set; }
    public int AvailableRoomsCount { get; set; }
    public int ActiveBookingsToday { get; set; }
    public int CheckedInNowCount { get; set; }
    public int AutoReleasedTodayCount { get; set; }
    public int ActivePenaltiesCount { get; set; }
    public int OpenMaintenanceCount { get; set; }
    public double OverallUtilizationPercent { get; set; }
    public List<RoomUtilizationItemDto> RoomUtilizations { get; set; } = new();
    public List<NotificationLogDto> RecentNotifications { get; set; } = new();
}

public class ApiResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string message = "สำเร็จ") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResult<T> Fail(string message) =>
        new() { Success = false, Message = message };
}
