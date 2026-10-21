using RoomReservation.Contracts;

namespace RoomReservation.Web.Domain;

public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalSsoId { get; set; } = "";
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Student;
    public string? LineUserId { get; set; }
    public int DailyQuotaMinutes { get; set; } = 180; // 3 hours/day
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class RoomEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomCode { get; set; } = "";
    public string RoomName { get; set; } = "";
    public RoomType RoomType { get; set; }
    public int Floor { get; set; }
    public string Building { get; set; } = "อาคารหอสมุดกลาง";
    public int MinCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public int MaxBookingHours { get; set; } = 3;
    public string AllowedSubnetCidr { get; set; } = "10.20.0.0/16";
    public double GeoLatitude { get; set; } = 13.8476;
    public double GeoLongitude { get; set; } = 100.5696;
    public int GeoRadiusMeters { get; set; } = 150;
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public string QrSecretKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<EquipmentDto> Equipments { get; set; } = new();
}

public class BookingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BookingNo { get; set; } = "";
    public Guid RoomId { get; set; }
    public string HostStudentOrStaffId { get; set; } = "";
    public string HostFullName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExpectedAttendees { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Reserved;
    public string CheckInPinCode { get; set; } = "";
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CheckedInAt { get; set; }
    public bool PreStartNotified { get; set; }
    public bool PreEndNotified { get; set; }
    public List<ParticipantDto> Participants { get; set; } = new();
}

public class CheckInEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public string StudentOrStaffId { get; set; } = "";
    public CheckInMethod Method { get; set; }
    public string ClientIpAddress { get; set; } = "";
    public double ClientLatitude { get; set; }
    public double ClientLongitude { get; set; }
    public bool IsLocationValid { get; set; }
    public DateTime CheckedInAt { get; set; } = DateTime.Now;
}

public class PenaltyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentOrStaffId { get; set; } = "";
    public string FullName { get; set; } = "";
    public Guid BookingId { get; set; }
    public string BookingNo { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string Reason { get; set; } = "No-Show (ไม่เช็คอินภายใน 15 นาทีหลังเวลาเริ่มจอง)";
    public int PenaltyDays { get; set; } = 2;
    public DateTime BanStartAt { get; set; }
    public DateTime BanEndAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DatabaseState
{
    public List<UserEntity> Users { get; set; } = new();
    public List<RoomEntity> Rooms { get; set; } = new();
    public List<BookingEntity> Bookings { get; set; } = new();
    public List<CheckInEntity> CheckIns { get; set; } = new();
    public List<PenaltyEntity> Penalties { get; set; } = new();
    public List<MaintenanceReportDto> MaintenanceReports { get; set; } = new();
    public List<NotificationLogDto> NotificationLogs { get; set; } = new();
}
