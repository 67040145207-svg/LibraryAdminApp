using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using RoomReservation.Contracts;

namespace RoomReservation.Web.Services;

/// <summary>
/// Core Business Engine ที่เชื่อมต่อกับฐานข้อมูล MySQL บน WAMP Server (library_reservation)
/// - ระบบ Login & Register จริงผ่านตาราง `users` ใน MySQL
/// - จองห้องผ่านตาราง `reservations` โดยตรวจสอบจำนวนผู้เข้าใช้งานจากตัวเลข `ExpectedAttendees` โดยตรง (ห้องกลาง >= 5 คน, ห้องใหญ่ >= 7 คน)
/// - เปิดบริการเวลา 09:00 - 17:00 น. (4 ห้อง: MED-101, MED-102, MED-103, LRG-201)
/// </summary>
public class ReservationEngineService
{
    private readonly string _serverConnStr;
    private readonly string _dbConnStr;

    public const int OpenHour = 9;   // เปิด 09:00 น.
    public const int CloseHour = 17; // ปิด 17:00 น.

    // เงื่อนไขเวลาเช็คอินล่วงหน้า: สามารถเข้ามาเช็คอินล่วงหน้าได้ตั้งแต่ 30 นาที ถึง 1 ชั่วโมง (60 นาที) ก่อนถึงเวลาจอง
    public const int EarlyCheckInMinMinutes = 30;
    public const int EarlyCheckInMaxMinutes = 60; // 1 ชั่วโมงก่อนเวลาจอง
    public const int LateCheckInGraceMinutes = 15; // สายได้ไม่เกิน 15 นาทีหลังเวลาเริ่มจอง

    public event Action? OnStateChanged;

    public ReservationEngineService(IConfiguration config)
    {
        _dbConnStr = config.GetConnectionString("MySqlConnection")
            ?? "Server=localhost;Port=3306;Database=library_reservation;User=root;Password=;CharSet=utf8mb4;AllowUserVariables=True;";

        var builder = new MySqlConnectionStringBuilder(_dbConnStr)
        {
            Database = ""
        };
        _serverConnStr = builder.ConnectionString;

        InitializeMySqlDatabase();
    }

    private MySqlConnection OpenConnection()
    {
        var conn = new MySqlConnection(_dbConnStr);
        conn.Open();
        return conn;
    }

    private static Guid ReadGuid(MySqlDataReader reader, string column)
    {
        var val = reader.GetValue(reader.GetOrdinal(column));
        if (val is Guid g) return g;
        return Guid.TryParse(val?.ToString(), out var parsed) ? parsed : Guid.Empty;
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    private void InitializeMySqlDatabase()
    {
        try
        {
            using (var serverConn = new MySqlConnection(_serverConnStr))
            {
                serverConn.Open();
                using var createDbCmd = serverConn.CreateCommand();
                createDbCmd.CommandText = "CREATE DATABASE IF NOT EXISTS `library_reservation` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                createDbCmd.ExecuteNonQuery();
            }

            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS `users` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `username` VARCHAR(64) NOT NULL UNIQUE,
    `password_hash` VARCHAR(255) NOT NULL,
    `full_name` VARCHAR(150) NOT NULL,
    `email` VARCHAR(150) NOT NULL,
    `role` VARCHAR(32) NOT NULL DEFAULT 'Student',
    `daily_quota_minutes` INT NOT NULL DEFAULT 180,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `rooms` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `room_code` VARCHAR(32) NOT NULL UNIQUE,
    `room_name` VARCHAR(150) NOT NULL,
    `room_type` VARCHAR(32) NOT NULL,
    `floor` INT NOT NULL DEFAULT 2,
    `building` VARCHAR(150) NOT NULL DEFAULT 'อาคารหอสมุดกลาง',
    `min_capacity` INT NOT NULL,
    `max_capacity` INT NOT NULL,
    `max_booking_hours` INT NOT NULL DEFAULT 3,
    `status` VARCHAR(32) NOT NULL DEFAULT 'Available',
    `qr_secret_key` VARCHAR(128) NOT NULL,
    `equipments_json` TEXT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `reservations` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `booking_no` VARCHAR(32) NOT NULL UNIQUE,
    `room_id` CHAR(36) NOT NULL,
    `host_username` VARCHAR(64) NOT NULL,
    `host_full_name` VARCHAR(150) NOT NULL,
    `purpose` VARCHAR(500) NOT NULL,
    `start_time` DATETIME NOT NULL,
    `end_time` DATETIME NOT NULL,
    `expected_attendees` INT NOT NULL,
    `status` VARCHAR(32) NOT NULL DEFAULT 'Reserved',
    `checkin_pin_code` VARCHAR(16) NOT NULL,
    `cancel_reason` VARCHAR(500) NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `checked_in_at` DATETIME NULL,
    `pre_start_notified` TINYINT(1) NOT NULL DEFAULT 0,
    `pre_end_notified` TINYINT(1) NOT NULL DEFAULT 0,
    INDEX `idx_reservations_room_time` (`room_id`, `start_time`, `end_time`),
    INDEX `idx_reservations_status_start` (`status`, `start_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `penalties` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `username` VARCHAR(64) NOT NULL,
    `full_name` VARCHAR(150) NOT NULL,
    `booking_id` CHAR(36) NOT NULL,
    `booking_no` VARCHAR(32) NOT NULL,
    `room_name` VARCHAR(150) NOT NULL,
    `reason` VARCHAR(500) NOT NULL,
    `penalty_days` INT NOT NULL DEFAULT 2,
    `ban_start_at` DATETIME NOT NULL,
    `ban_end_at` DATETIME NOT NULL,
    `is_active` TINYINT(1) NOT NULL DEFAULT 1
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `maintenance_reports` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `room_id` CHAR(36) NOT NULL,
    `room_code` VARCHAR(32) NOT NULL,
    `room_name` VARCHAR(150) NOT NULL,
    `equipment_name` VARCHAR(150) NOT NULL,
    `reported_by` VARCHAR(150) NOT NULL,
    `title` VARCHAR(255) NOT NULL,
    `description` TEXT NOT NULL,
    `priority` VARCHAR(32) NOT NULL DEFAULT 'Medium',
    `status` VARCHAR(32) NOT NULL DEFAULT 'Open',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `resolved_at` DATETIME NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `notification_logs` (
    `id` CHAR(36) NOT NULL PRIMARY KEY,
    `channel` VARCHAR(64) NOT NULL,
    `recipient` VARCHAR(150) NOT NULL,
    `subject` VARCHAR(255) NOT NULL,
    `message` TEXT NOT NULL,
    `sent_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
";
            cmd.ExecuteNonQuery();

            // ลบห้องเก่าที่ไม่ใช่ 4 ห้องมาตรฐาน (601 = ห้องใหญ่ 1 ห้อง, 602-604 = ห้องกลาง 3 ห้อง)
            using (var cleanCmd = conn.CreateCommand())
            {
                cleanCmd.CommandText = @"
                    DELETE FROM `rooms`
                    WHERE `id` NOT IN (
                        '22222222-2222-2222-2222-222222222201',
                        '22222222-2222-2222-2222-222222222101',
                        '22222222-2222-2222-2222-222222222102',
                        '22222222-2222-2222-2222-222222222103'
                    );";
                cleanCmd.ExecuteNonQuery();
            }

            // Seed ผู้ใช้งานเริ่มต้นและห้อง 4 ห้อง (ห้องใหญ่: 601, ห้องกลาง: 602, 603, 604)
            using (var seedCmd = conn.CreateCommand())
            {
                seedCmd.CommandText = @"
INSERT IGNORE INTO `users` (`id`, `username`, `password_hash`, `full_name`, `email`, `role`, `daily_quota_minutes`) VALUES
('11111111-1111-1111-1111-111111111101', '6610400001', '123456', 'กิตติพงศ์ รัตนโชติ', 'kittipong.r@ku.th', 'Student', 180),
('11111111-1111-1111-1111-111111111102', '6610400002', '123456', 'พิมพ์ชนก วงศ์สวัสดิ์', 'pimchanok.w@ku.th', 'Student', 180),
('11111111-1111-1111-1111-111111111103', 'STAFF-001', '123456', 'ดร.ปิยะพงษ์ วิชาการ', 'piyapong.w@ku.th', 'Staff', 180),
('11111111-1111-1111-1111-111111111104', 'admin', 'admin123', 'เจ้าหน้าที่ดูแลระบบห้องสมุด', 'admin.library@ku.th', 'Admin', 480);

INSERT INTO `rooms` (`id`, `room_code`, `room_name`, `room_type`, `floor`, `building`, `min_capacity`, `max_capacity`, `max_booking_hours`, `status`, `qr_secret_key`, `equipments_json`) VALUES
('22222222-2222-2222-2222-222222222201', '601', 'ห้อง 601 (ห้องขนาดใหญ่ / Large Seminar Room)', 'Large', 6, 'อาคารหอสมุดกลาง', 7, 15, 3, 'Available', 'secret-room-601', '[{""Code"":""SMART_TV"",""Name"":""Interactive Smart Board 75 นิ้ว"",""Quantity"":1},{""Code"":""PROJECTOR"",""Name"":""เลเซอร์โปรเจคเตอร์ 4K"",""Quantity"":1},{""Code"":""WHITEBOARD"",""Name"":""ไวท์บอร์ดขนาดใหญ่"",""Quantity"":2},{""Code"":""POWER_PLUG"",""Name"":""ปลั๊กไฟรอบโต๊ะประชุมใหญ่"",""Quantity"":15}]'),
('22222222-2222-2222-2222-222222222101', '602', 'ห้อง 602 (ห้องขนาดกลาง 1 / Medium Study Room 1)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 8, 3, 'Available', 'secret-room-602', '[{""Code"":""SMART_TV"",""Name"":""Smart TV 55 นิ้ว"",""Quantity"":1},{""Code"":""WHITEBOARD"",""Name"":""กระดานไวท์บอร์ด"",""Quantity"":1},{""Code"":""POWER_PLUG"",""Name"":""ปลั๊กไฟกลางโต๊ะ"",""Quantity"":8}]'),
('22222222-2222-2222-2222-222222222102', '603', 'ห้อง 603 (ห้องขนาดกลาง 2 / Medium Study Room 2)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 8, 3, 'Available', 'secret-room-603', '[{""Code"":""SMART_TV"",""Name"":""Smart TV 55 นิ้ว"",""Quantity"":1},{""Code"":""WHITEBOARD"",""Name"":""กระดานไวท์บอร์ด"",""Quantity"":1},{""Code"":""POWER_PLUG"",""Name"":""ปลั๊กไฟกลางโต๊ะ"",""Quantity"":8}]'),
('22222222-2222-2222-2222-222222222103', '604', 'ห้อง 604 (ห้องขนาดกลาง 3 / Medium Study Room 3)', 'Medium', 6, 'อาคารหอสมุดกลาง (ใกล้ห้อง Admin)', 5, 10, 3, 'Available', 'secret-room-604', '[{""Code"":""SMART_TV"",""Name"":""Smart TV 65 นิ้ว (Wireless Share)"",""Quantity"":1},{""Code"":""WHITEBOARD"",""Name"":""กระดานไวท์บอร์ดกระจก"",""Quantity"":2},{""Code"":""POWER_PLUG"",""Name"":""ปลั๊กไฟและพอร์ต USB-C"",""Quantity"":10}]')
ON DUPLICATE KEY UPDATE
    `room_code` = VALUES(`room_code`),
    `room_name` = VALUES(`room_name`),
    `room_type` = VALUES(`room_type`),
    `floor` = VALUES(`floor`),
    `min_capacity` = VALUES(`min_capacity`),
    `max_capacity` = VALUES(`max_capacity`),
    `equipments_json` = VALUES(`equipments_json`);
";
                seedCmd.ExecuteNonQuery();
            }

            // หากยังไม่มีรายการจองในตาราง reservations เลย ให้สร้างข้อมูลตัวอย่างเริ่มต้น 2 รายการ
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM `reservations`;";
            var count = Convert.ToInt32(countCmd.ExecuteScalar());
            if (count == 0)
            {
                var today = DateTime.Today;
                using var seedResCmd = conn.CreateCommand();
                seedResCmd.CommandText = @"
INSERT INTO `reservations` (`id`, `booking_no`, `room_id`, `host_username`, `host_full_name`, `purpose`, `start_time`, `end_time`, `expected_attendees`, `status`, `checkin_pin_code`, `created_at`, `checked_in_at`) VALUES
(@id1, @bno1, '22222222-2222-2222-2222-222222222101', '6610400001', 'กิตติพงศ์ รัตนโชติ', 'ประชุมกลุ่มโครงงานวิศวกรรมซอฟต์แวร์', @s1, @e1, 5, 'CheckedIn', '482910', NOW(), @c1),
(@id2, @bno2, '22222222-2222-2222-2222-222222222102', '6610400002', 'พิมพ์ชนก วงศ์สวัสดิ์', 'ติวสอบและสรุปบทเรียนกลุ่มย่อย', @s2, @e2, 6, 'Reserved', '739201', NOW(), NULL);
";
                seedResCmd.Parameters.AddWithValue("@id1", Guid.NewGuid().ToString());
                seedResCmd.Parameters.AddWithValue("@bno1", $"BK-{today:yyyyMMdd}-001");
                seedResCmd.Parameters.AddWithValue("@s1", today.AddHours(9));
                seedResCmd.Parameters.AddWithValue("@e1", today.AddHours(11));
                seedResCmd.Parameters.AddWithValue("@c1", today.AddHours(9).AddMinutes(5));

                seedResCmd.Parameters.AddWithValue("@id2", Guid.NewGuid().ToString());
                seedResCmd.Parameters.AddWithValue("@bno2", $"BK-{today:yyyyMMdd}-002");
                seedResCmd.Parameters.AddWithValue("@s2", today.AddHours(13));
                seedResCmd.Parameters.AddWithValue("@e2", today.AddHours(15));
                seedResCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("MySQL Initialization Warning: " + ex.Message);
        }
    }

    // ==================== 1. REAL LOGIN & REGISTRATION (MySQL `users`) ====================

    public ApiResult<AuthSessionDto> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return ApiResult<AuthSessionDto>.Fail("กรุณากรอกรหัสผู้ใช้งานและรหัสผ่านให้ครบถ้วน");

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT `id`, `username`, `password_hash`, `full_name`, `email`, `role`, `daily_quota_minutes`
            FROM `users`
            WHERE `username` = @u
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@u", req.Username.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return ApiResult<AuthSessionDto>.Fail("ไม่พบรหัสผู้ใช้งานนี้ในระบบ (หากเป็นผู้ใช้งานใหม่ กรุณากด 'สมัครสมาชิกใหม่')");
        }

        var userId = ReadGuid(reader, "id");
        var username = reader.GetString("username");
        var storedPwd = reader.GetString("password_hash");
        var fullName = reader.GetString("full_name");
        var email = reader.GetString("email");
        var roleStr = reader.GetString("role");
        reader.Close();

        if (!storedPwd.Equals(req.Password.Trim(), StringComparison.Ordinal))
        {
            return ApiResult<AuthSessionDto>.Fail("รหัสผ่านไม่ถูกต้อง กรุณาลองใหม่อีกครั้ง");
        }

        Enum.TryParse<UserRole>(roleStr, true, out var parsedRole);
        var quota = GetUserQuota(username, DateTime.Today);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{parsedRole}:{DateTime.UtcNow.Ticks}"));

        return ApiResult<AuthSessionDto>.Ok(new AuthSessionDto
        {
            UserId = userId,
            AccessToken = token,
            StudentOrStaffId = username,
            FullName = fullName,
            Email = email,
            Role = parsedRole,
            Quota = quota
        }, $"ยินดีต้อนรับ {fullName} ({username})");
    }

    public ApiResult<AuthSessionDto> RegisterUser(RegisterUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StudentOrStaffId) ||
            string.IsNullOrWhiteSpace(req.Password) ||
            string.IsNullOrWhiteSpace(req.FullName))
        {
            return ApiResult<AuthSessionDto>.Fail("กรุณากรอกรหัสผู้ใช้งาน, รหัสผ่าน และชื่อ-นามสกุลให้ครบถ้วน");
        }

        var username = req.StudentOrStaffId.Trim();
        using var conn = OpenConnection();

        using (var checkCmd = conn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM `users` WHERE `username` = @u;";
            checkCmd.Parameters.AddWithValue("@u", username);
            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
            {
                return ApiResult<AuthSessionDto>.Fail($"รหัสผู้ใช้งาน '{username}' มีอยู่ในระบบแล้ว กรุณาเข้าสู่ระบบ");
            }
        }

        var newId = Guid.NewGuid();
        var email = string.IsNullOrWhiteSpace(req.Email) ? $"{username.ToLowerInvariant()}@university.ac.th" : req.Email.Trim();

        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = @"
                INSERT INTO `users` (`id`, `username`, `password_hash`, `full_name`, `email`, `role`, `daily_quota_minutes`, `created_at`)
                VALUES (@id, @u, @p, @fn, @em, @role, 180, NOW());";
            insCmd.Parameters.AddWithValue("@id", newId.ToString());
            insCmd.Parameters.AddWithValue("@u", username);
            insCmd.Parameters.AddWithValue("@p", req.Password.Trim());
            insCmd.Parameters.AddWithValue("@fn", req.FullName.Trim());
            insCmd.Parameters.AddWithValue("@em", email);
            insCmd.Parameters.AddWithValue("@role", req.Role.ToString());
            insCmd.ExecuteNonQuery();
        }

        return Login(new LoginRequest { Username = username, Password = req.Password });
    }

    public ApiResult<AuthSessionDto> SsoLogin(SsoLoginRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            return Login(new LoginRequest { Username = req.StudentOrStaffId, Password = req.Password });
        }
        return Login(new LoginRequest { Username = req.StudentOrStaffId, Password = "123456" });
    }

    public UserQuotaDto GetUserQuota(string studentOrStaffId, DateTime? targetDate = null)
    {
        var date = (targetDate ?? DateTime.Today).Date;
        var username = studentOrStaffId.Trim();

        using var conn = OpenConnection();

        Guid userId = Guid.Empty;
        string fullName = username;
        int dailyQuota = 180;

        using (var uCmd = conn.CreateCommand())
        {
            uCmd.CommandText = "SELECT `id`, `full_name`, `daily_quota_minutes` FROM `users` WHERE `username` = @u LIMIT 1;";
            uCmd.Parameters.AddWithValue("@u", username);
            using var r = uCmd.ExecuteReader();
            if (r.Read())
            {
                userId = ReadGuid(r, "id");
                fullName = r.GetString("full_name");
                dailyQuota = r.GetInt32("daily_quota_minutes");
            }
        }

        bool isSuspended = false;
        DateTime? suspendedUntil = null;
        string? suspensionReason = null;

        using (var pCmd = conn.CreateCommand())
        {
            pCmd.CommandText = @"
                SELECT `ban_end_at`, `reason`
                FROM `penalties`
                WHERE `username` = @u AND `is_active` = 1 AND `ban_end_at` > NOW()
                ORDER BY `ban_end_at` DESC
                LIMIT 1;";
            pCmd.Parameters.AddWithValue("@u", username);
            using var pr = pCmd.ExecuteReader();
            if (pr.Read())
            {
                isSuspended = true;
                suspendedUntil = pr.GetDateTime("ban_end_at");
                suspensionReason = pr.GetString("reason");
            }
        }

        int usedMinutes = 0;
        using (var bCmd = conn.CreateCommand())
        {
            bCmd.CommandText = @"
                SELECT COALESCE(SUM(TIMESTAMPDIFF(MINUTE, `start_time`, `end_time`)), 0)
                FROM `reservations`
                WHERE `host_username` = @u
                  AND DATE(`start_time`) = @dt
                  AND `status` NOT IN ('Cancelled', 'EmergencyOverride');";
            bCmd.Parameters.AddWithValue("@u", username);
            bCmd.Parameters.AddWithValue("@dt", date);
            usedMinutes = Convert.ToInt32(bCmd.ExecuteScalar());
        }

        return new UserQuotaDto
        {
            UserId = userId,
            StudentOrStaffId = username,
            FullName = fullName,
            DailyQuotaMinutes = dailyQuota,
            UsedMinutesToday = usedMinutes,
            IsSuspended = isSuspended,
            SuspendedUntil = suspendedUntil,
            SuspensionReason = suspensionReason
        };
    }

    // ==================== 2. ROOMS & REAL-TIME TIMELINE (MySQL `rooms` & `reservations`) ====================

    public List<RoomDto> GetRooms(
        DateTime? date = null,
        RoomType? roomType = null,
        int? minCapacity = null,
        string? equipmentCode = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var rooms = new List<RoomDto>();

        using var conn = OpenConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM `rooms` ORDER BY `room_code` ASC;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var eqJson = r.GetString("equipments_json");
                var equipments = JsonSerializer.Deserialize<List<EquipmentDto>>(eqJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<EquipmentDto>();

                Enum.TryParse<RoomType>(r.GetString("room_type"), true, out var rType);
                Enum.TryParse<RoomStatus>(r.GetString("status"), true, out var rStatus);

                rooms.Add(new RoomDto
                {
                    Id = ReadGuid(r, "id"),
                    RoomCode = r.GetString("room_code"),
                    RoomName = r.GetString("room_name"),
                    RoomType = rType,
                    Floor = r.GetInt32("floor"),
                    Building = r.GetString("building"),
                    MinCapacity = r.GetInt32("min_capacity"),
                    MaxCapacity = r.GetInt32("max_capacity"),
                    MaxBookingHours = r.GetInt32("max_booking_hours"),
                    Status = rStatus,
                    Equipments = equipments
                });
            }
        }

        // ดึงรายการจองของวันที่เลือกจากตาราง reservations ใน MySQL
        var bookingsOnDate = GetBookingsForDate(conn, targetDate);

        foreach (var room in rooms)
        {
            var slots = new List<TimelineSlotDto>();
            for (int hour = OpenHour; hour < CloseHour; hour++)
            {
                var slotStart = targetDate.AddHours(hour);
                var slotEnd = slotStart.AddHours(1);

                var overlapping = bookingsOnDate.FirstOrDefault(b =>
                    b.RoomId == room.Id &&
                    (b.Status == BookingStatus.Reserved || b.Status == BookingStatus.CheckedIn) &&
                    b.StartTime < slotEnd &&
                    b.EndTime > slotStart);

                if (overlapping != null)
                {
                    slots.Add(new TimelineSlotDto
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        SlotState = overlapping.Status == BookingStatus.CheckedIn ? "Occupied" : "Reserved",
                        BookingId = overlapping.Id,
                        BookingNo = overlapping.BookingNo,
                        BookedByName = overlapping.HostFullName,
                        Purpose = overlapping.Purpose
                    });
                }
                else
                {
                    slots.Add(new TimelineSlotDto
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        SlotState = "Available"
                    });
                }
            }
            room.TodaySlots = slots;
        }

        var query = rooms.AsEnumerable();
        if (roomType.HasValue)
            query = query.Where(r => r.RoomType == roomType.Value);

        if (minCapacity.HasValue && minCapacity.Value > 0)
            query = query.Where(r => r.MaxCapacity >= minCapacity.Value);

        if (!string.IsNullOrWhiteSpace(equipmentCode))
        {
            query = query.Where(r => r.Equipments.Any(e =>
                e.Code.Equals(equipmentCode, StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains(equipmentCode, StringComparison.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }

    private List<BookingDto> GetBookingsForDate(MySqlConnection conn, DateTime date)
    {
        var list = new List<BookingDto>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT r.*, rm.`room_code`, rm.`room_name`, rm.`room_type`, rm.`min_capacity`
            FROM `reservations` r
            INNER JOIN `rooms` rm ON r.`room_id` = rm.`id`
            WHERE DATE(r.`start_time`) = @dt
            ORDER BY r.`start_time` ASC;";
        cmd.Parameters.AddWithValue("@dt", date.Date);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadBookingDto(reader));
        }
        return list;
    }

    public DynamicQrTokenDto? GetDynamicQrToken(Guid roomId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT `id`, `room_code`, `room_name`, `qr_secret_key` FROM `rooms` WHERE `id` = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", roomId.ToString());

        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var code = r.GetString("room_code");
        var name = r.GetString("room_name");
        var secret = r.GetString("qr_secret_key");

        var now = DateTime.Now;
        var windowIndex = now.Ticks / TimeSpan.FromSeconds(30).Ticks;
        var token = ComputeTotpToken(secret, windowIndex);
        var expiresAt = new DateTime((windowIndex + 1) * TimeSpan.FromSeconds(30).Ticks);

        return new DynamicQrTokenDto
        {
            RoomId = roomId,
            RoomCode = code,
            RoomName = name,
            DynamicToken = token,
            QrPayload = $"ADMIN-DESK|{code}|{token}",
            GeneratedAt = now,
            ExpiresAt = expiresAt
        };
    }

    private static string ComputeTotpToken(string secret, long timeStep)
    {
        var bytes = Encoding.UTF8.GetBytes($"{secret}:{timeStep}");
        var hash = SHA256.HashData(bytes);
        var code = Math.Abs(BitConverter.ToInt32(hash, 0)) % 1000000;
        return code.ToString("D6");
    }

    // ==================== 3. CREATE BOOKING (ตรวจจำนวนคนจากตัวเลข ExpectedAttendees & บันทึกลง MySQL) ====================

    public ApiResult<BookingDto> CreateBooking(CreateBookingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.HostStudentOrStaffId))
            return ApiResult<BookingDto>.Fail("กรุณาเข้าสู่ระบบก่อนทำการจองห้อง");

        if (string.IsNullOrWhiteSpace(req.Purpose))
            return ApiResult<BookingDto>.Fail("กรุณาระบุวัตถุประสงค์การใช้งานห้อง");

        if (req.EndTime <= req.StartTime)
            return ApiResult<BookingDto>.Fail("เวลาสิ้นสุดต้องมากกว่าเวลาเริ่มต้น");

        if (req.StartTime.Hour < OpenHour || req.EndTime.Hour > CloseHour || (req.EndTime.Hour == CloseHour && req.EndTime.Minute > 0))
            return ApiResult<BookingDto>.Fail($"ห้องบริการเปิดให้ใช้งานเฉพาะเวลา {OpenHour:00}:00 – {CloseHour:00}:00 น. เท่านั้น");

        var requestedMinutes = (int)(req.EndTime - req.StartTime).TotalMinutes;
        if (requestedMinutes < 60)
            return ApiResult<BookingDto>.Fail("ระยะเวลาการจองขั้นต่ำคือ 1 ชั่วโมง");

        using var conn = OpenConnection();

        // 1) ดึงข้อมูลห้องจาก MySQL
        string roomCode = "";
        string roomName = "";
        RoomType roomType = RoomType.Medium;
        int minCap = 5;
        int maxCap = 8;
        int maxHours = 3;
        string roomStatus = "Available";

        using (var roomCmd = conn.CreateCommand())
        {
            roomCmd.CommandText = "SELECT * FROM `rooms` WHERE `id` = @id LIMIT 1;";
            roomCmd.Parameters.AddWithValue("@id", req.RoomId.ToString());
            using var rr = roomCmd.ExecuteReader();
            if (!rr.Read())
                return ApiResult<BookingDto>.Fail("ไม่พบข้อมูลห้องที่เลือกในฐานข้อมูล MySQL");

            roomCode = rr.GetString("room_code");
            roomName = rr.GetString("room_name");
            Enum.TryParse(rr.GetString("room_type"), true, out roomType);
            minCap = rr.GetInt32("min_capacity");
            maxCap = rr.GetInt32("max_capacity");
            maxHours = rr.GetInt32("max_booking_hours");
            roomStatus = rr.GetString("status");
        }

        if (!roomStatus.Equals("Available", StringComparison.OrdinalIgnoreCase))
            return ApiResult<BookingDto>.Fail($"ห้อง {roomCode} ไม่พร้อมให้บริการ");

        if (requestedMinutes > maxHours * 60)
            return ApiResult<BookingDto>.Fail($"ห้องนี้จำกัดการจองสูงสุดไม่เกิน {maxHours} ชั่วโมงต่อครั้ง");

        // 2) ตรวจสอบเงื่อนไขจำนวนผู้เข้าใช้งานจากค่าตัวเลข ExpectedAttendees โดยตรง (ไม่ต้องกรอกรหัสเพื่อน)
        int totalAttendees = req.ExpectedAttendees;
        if (totalAttendees < minCap || totalAttendees > maxCap)
        {
            string roomTypeDesc = roomType == RoomType.Large ? "ห้องขนาดใหญ่ (ขั้นต่ำ 7 คน)" : "ห้องขนาดกลาง (ขั้นต่ำ 5 คน)";
            return ApiResult<BookingDto>.Fail(
                $"ไม่สามารถจองได้: ห้อง {roomCode} เป็น{roomTypeDesc} ต้องมีจำนวนผู้เข้าใช้งาน {minCap}–{maxCap} คน " +
                $"(คุณระบุ {totalAttendees} คน)");
        }

        // 3) ตรวจสอบโทษแบนและโควตารายวันของผู้จองหลักจาก MySQL
        var hostUsername = req.HostStudentOrStaffId.Trim();
        var quota = GetUserQuota(hostUsername, req.StartTime.Date);
        if (quota.IsSuspended)
        {
            return ApiResult<BookingDto>.Fail(
                $"บัญชีของคุณ ({hostUsername}) ถูกระงับสิทธิ์การจองชั่วคราวจนถึง {quota.SuspendedUntil:dd/MM/yyyy HH:mm} ({quota.SuspensionReason})");
        }

        if (quota.UsedMinutesToday + requestedMinutes > quota.DailyQuotaMinutes)
        {
            return ApiResult<BookingDto>.Fail(
                $"โควตาของคุณในวันที่ {req.StartTime:dd/MM/yyyy} เหลือเพียง {quota.RemainingMinutesToday} นาที " +
                $"(จำกัดสูงสุด {quota.DailyQuotaMinutes / 60.0:0.#} ชม./วัน แต่ต้องการจองเพิ่ม {requestedMinutes} นาที)");
        }

        // 4) ตรวจสอบการจองเวลาชนกันในตาราง reservations (MySQL)
        using (var conflictCmd = conn.CreateCommand())
        {
            conflictCmd.CommandText = @"
                SELECT `booking_no`
                FROM `reservations`
                WHERE `room_id` = @rid
                  AND `status` IN ('Reserved', 'CheckedIn')
                  AND `start_time` < @endTime
                  AND `end_time` > @startTime
                LIMIT 1;";
            conflictCmd.Parameters.AddWithValue("@rid", req.RoomId.ToString());
            conflictCmd.Parameters.AddWithValue("@startTime", req.StartTime);
            conflictCmd.Parameters.AddWithValue("@endTime", req.EndTime);

            var existingBno = conflictCmd.ExecuteScalar()?.ToString();
            if (!string.IsNullOrEmpty(existingBno))
            {
                return ApiResult<BookingDto>.Fail(
                    $"ช่วงเวลา {req.StartTime:HH:mm}–{req.EndTime:HH:mm} ถูกจองแล้วโดยหมายเลข {existingBno}");
            }
        }

        // 5) บันทึกข้อมูลการจองลงตาราง reservations ใน MySQL
        var newId = Guid.NewGuid();
        var pin = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        int seqNo;
        using (var seqCmd = conn.CreateCommand())
        {
            seqCmd.CommandText = "SELECT COUNT(*) + 1 FROM `reservations`;";
            seqNo = Convert.ToInt32(seqCmd.ExecuteScalar());
        }
        var bookingNo = $"BK-{req.StartTime:yyyyMMdd}-{seqNo:D3}";
        var hostFullName = !string.IsNullOrWhiteSpace(req.HostFullName) ? req.HostFullName.Trim() : quota.FullName;

        using (var insCmd = conn.CreateCommand())
        {
            insCmd.CommandText = @"
                INSERT INTO `reservations`
                (`id`, `booking_no`, `room_id`, `host_username`, `host_full_name`, `purpose`, `start_time`, `end_time`, `expected_attendees`, `status`, `checkin_pin_code`, `created_at`)
                VALUES
                (@id, @bno, @rid, @hu, @hfn, @purp, @st, @et, @att, 'Reserved', @pin, NOW());";
            insCmd.Parameters.AddWithValue("@id", newId.ToString());
            insCmd.Parameters.AddWithValue("@bno", bookingNo);
            insCmd.Parameters.AddWithValue("@rid", req.RoomId.ToString());
            insCmd.Parameters.AddWithValue("@hu", hostUsername);
            insCmd.Parameters.AddWithValue("@hfn", hostFullName);
            insCmd.Parameters.AddWithValue("@purp", req.Purpose.Trim());
            insCmd.Parameters.AddWithValue("@st", req.StartTime);
            insCmd.Parameters.AddWithValue("@et", req.EndTime);
            insCmd.Parameters.AddWithValue("@att", totalAttendees);
            insCmd.Parameters.AddWithValue("@pin", pin);
            insCmd.ExecuteNonQuery();
        }

        AddNotification(
            conn,
            "LINE Messaging API & Email",
            $"{hostUsername} ({hostFullName})",
            $"ยืนยันการจองห้อง {roomCode} สำเร็จ ({bookingNo})",
            $"เวลา {req.StartTime:dd/MM/yyyy HH:mm}-{req.EndTime:HH:mm} น. (จำนวน {totalAttendees} คน) | รหัสติดต่อห้อง Admin: {pin}");

        NotifyStateChanged();

        var createdBooking = GetBookingById(conn, newId);
        return ApiResult<BookingDto>.Ok(
            createdBooking!,
            $"จองห้อง {roomCode} สำเร็จ! รหัสการจอง {bookingNo} (PIN: {pin}) — กรุณาไปติดต่อเช็คอินที่ห้อง Admin เพื่อเปิดประตูห้อง");
    }

    // ==================== 4. CHECK-IN AT ADMIN ROOM ====================

    public ApiResult<BookingDto> VerifyGroupParticipant(GroupVerifyRequest req)
    {
        using var conn = OpenConnection();
        var b = GetBookingById(conn, req.BookingId);
        if (b == null) return ApiResult<BookingDto>.Fail("ไม่พบรายการจอง");
        return ApiResult<BookingDto>.Ok(b, "ผู้จองหลักยืนยันจำนวนผู้เข้าใช้งานเรียบร้อยแล้ว");
    }

    public ApiResult<BookingDto> CheckIn(CheckInRequest req)
    {
        try
        {
            using var conn = OpenConnection();
            BookingDto? booking = null;

            // หากระบุรหัส PIN หรือรหัสการจองมา ให้ค้นหาจากรหัส PIN / รหัสการจอง / รหัสนักศึกษา
            if (!string.IsNullOrWhiteSpace(req.CodeOrQrToken))
            {
                var code = req.CodeOrQrToken.Trim();
                using var pinCmd = conn.CreateCommand();
                var sql = @"
                    SELECT r.*, rm.`room_code`, rm.`room_name`, rm.`room_type`, rm.`min_capacity`
                    FROM `reservations` r
                    INNER JOIN `rooms` rm ON r.`room_id` = rm.`id`
                    WHERE (r.`checkin_pin_code` = @code OR r.`booking_no` = @code)";

                if (!string.IsNullOrWhiteSpace(req.StudentOrStaffId))
                {
                    sql += " AND r.`host_username` = @sid";
                    pinCmd.Parameters.AddWithValue("@sid", req.StudentOrStaffId.Trim());
                }

                sql += " ORDER BY r.`created_at` DESC LIMIT 1;";
                pinCmd.CommandText = sql;
                pinCmd.Parameters.AddWithValue("@code", code);

                using var reader = pinCmd.ExecuteReader();
                if (reader.Read())
                {
                    booking = ReadBookingDto(reader);
                }
            }
            else if (req.BookingId != Guid.Empty)
            {
                booking = GetBookingById(conn, req.BookingId);
            }

            if (booking == null)
                return ApiResult<BookingDto>.Fail("ไม่พบรายการ");

            if (booking.Status == BookingStatus.CheckedIn)
                return ApiResult<BookingDto>.Fail("รายการจองนี้เช็คอินและเปิดประตูห้องไปแล้ว");

            if (booking.Status != BookingStatus.Reserved)
                return ApiResult<BookingDto>.Fail($"ไม่สามารถเช็คอินได้เนื่องจากสถานะปัจจุบันคือ {booking.Status}");

            // ตรวจสอบเงื่อนไขเวลาการเช็คอินล่วงหน้า:
            // อนุญาตให้เข้ามาเช็คอินล่วงหน้าได้ตั้งแต่ 30 นาที ถึง 1 ชั่วโมง (60 นาที) ก่อนถึงเวลาจอง และไม่เกิน 15 นาทีหลังเวลาเริ่มจอง
            var now = DateTime.Now;
            var earliestCheckInTime = booking.StartTime.AddMinutes(-EarlyCheckInMaxMinutes); // ล่วงหน้าได้ถึง 1 ชั่วโมง (60 นาที) ก่อนเวลาเริ่มจอง
            var latestCheckInTime = booking.StartTime.AddMinutes(LateCheckInGraceMinutes);   // ไม่เกิน 15 นาทีหลังเวลาเริ่มจอง

            if (req.Method != CheckInMethod.AdminCounter && (now < earliestCheckInTime || now > latestCheckInTime))
            {
                return ApiResult<BookingDto>.Fail(
                    $"สามารถเข้ามาเช็คอินล่วงหน้าได้ตั้งแต่ 30 นาที ถึง 1 ชั่วโมง ก่อนถึงเวลาจอง (ช่วงเวลาที่เช็คอินได้: {earliestCheckInTime:HH:mm} – {latestCheckInTime:HH:mm} น.)");
            }

            using (var updCmd = conn.CreateCommand())
            {
                updCmd.CommandText = @"
                    UPDATE `reservations`
                    SET `status` = 'CheckedIn', `checked_in_at` = NOW()
                    WHERE `id` = @id;";
                updCmd.Parameters.AddWithValue("@id", booking.Id.ToString());
                updCmd.ExecuteNonQuery();
            }

            AddNotification(
                conn,
                "LINE Messaging API",
                $"{booking.HostStudentOrStaffId} ({booking.HostFullName})",
                $"เช็คอินที่ห้อง Admin สำเร็จ — เปิดประตูห้อง {booking.RoomCode}",
                $"เช็คอินเวลา {DateTime.Now:HH:mm} น. สามารถใช้งานห้องได้ถึงเวลา {booking.EndTime:HH:mm} น.");

            NotifyStateChanged();

            var updated = GetBookingById(conn, booking.Id)!;
            return ApiResult<BookingDto>.Ok(
                updated,
                $"เช็คอินที่ห้อง Admin สำเร็จ! เจ้าหน้าที่เปิดประตูห้อง {booking.RoomCode} ให้ผู้ใช้งาน ({booking.ExpectedAttendees} คน) เรียบร้อยแล้ว");
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่พบข้อมูล หรือเกิดข้อผิดพลาดในการเชื่อมต่อฐานข้อมูล: {ex.Message}");
        }
    }

    // ==================== 5. AUTO-RELEASE (15 MIN) & PENALTY (2 DAYS) ====================

    public int RunAutomatedChecks(DateTime? simulatedCurrentTime = null)
    {
        var now = simulatedCurrentTime ?? DateTime.Now;
        int affectedCount = 0;

        using var conn = OpenConnection();
        var activeBookings = GetBookings();

        foreach (var booking in activeBookings)
        {
            if (booking.Status == BookingStatus.Reserved && now > booking.StartTime.AddMinutes(15))
            {
                using (var updCmd = conn.CreateCommand())
                {
                    updCmd.CommandText = @"
                        UPDATE `reservations`
                        SET `status` = 'AutoReleased',
                            `cancel_reason` = 'Auto-released: ไม่มาติดต่อเช็คอินที่ห้อง Admin ภายใน 15 นาทีหลังถึงเวลาเริ่มจอง'
                        WHERE `id` = @id;";
                    updCmd.Parameters.AddWithValue("@id", booking.Id.ToString());
                    updCmd.ExecuteNonQuery();
                }

                var banStart = now;
                var banEnd = now.AddDays(2);

                using (var penCmd = conn.CreateCommand())
                {
                    penCmd.CommandText = @"
                        INSERT INTO `penalties`
                        (`id`, `username`, `full_name`, `booking_id`, `booking_no`, `room_name`, `reason`, `penalty_days`, `ban_start_at`, `ban_end_at`, `is_active`)
                        VALUES
                        (@id, @u, @fn, @bid, @bno, @rname, @reason, 2, @bs, @be, 1);";
                    penCmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                    penCmd.Parameters.AddWithValue("@u", booking.HostStudentOrStaffId);
                    penCmd.Parameters.AddWithValue("@fn", booking.HostFullName);
                    penCmd.Parameters.AddWithValue("@bid", booking.Id.ToString());
                    penCmd.Parameters.AddWithValue("@bno", booking.BookingNo);
                    penCmd.Parameters.AddWithValue("@rname", booking.RoomName);
                    penCmd.Parameters.AddWithValue("@reason", $"No-Show ({booking.BookingNo}): ไม่มาเช็คอินที่ห้อง Admin ภายใน 15 นาทีหลังเวลาเริ่ม ({booking.StartTime:HH:mm} น.)");
                    penCmd.Parameters.AddWithValue("@bs", banStart);
                    penCmd.Parameters.AddWithValue("@be", banEnd);
                    penCmd.ExecuteNonQuery();
                }

                AddNotification(
                    conn,
                    "LINE Messaging API & Email",
                    $"{booking.HostStudentOrStaffId} ({booking.HostFullName})",
                    $"ตัดสิทธิ์การจองอัตโนมัติ & ระงับสิทธิ์ 2 วัน ({booking.BookingNo})",
                    $"ระบบคืน Slot ห้อง {booking.RoomCode} เนื่องจากไม่มาเช็คอินที่ห้อง Admin ภายใน 15 นาที และระงับสิทธิ์ 2 วัน");

                affectedCount++;
            }
            else if (booking.Status == BookingStatus.CheckedIn && now >= booking.EndTime)
            {
                using var compCmd = conn.CreateCommand();
                compCmd.CommandText = "UPDATE `reservations` SET `status` = 'Completed' WHERE `id` = @id;";
                compCmd.Parameters.AddWithValue("@id", booking.Id.ToString());
                compCmd.ExecuteNonQuery();
                affectedCount++;
            }
        }

        if (affectedCount > 0)
            NotifyStateChanged();

        return affectedCount;
    }

    // ==================== 6. BOOKING MANAGEMENT & EMERGENCY OVERRIDE ====================

    public List<BookingDto> GetBookings(string? statusFilter = null, Guid? roomId = null, string? studentId = null)
    {
        var list = new List<BookingDto>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();

        var sql = new StringBuilder(@"
            SELECT r.*, rm.`room_code`, rm.`room_name`, rm.`room_type`, rm.`min_capacity`
            FROM `reservations` r
            INNER JOIN `rooms` rm ON r.`room_id` = rm.`id`
            WHERE 1=1 ");

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            sql.Append(" AND r.`status` = @status ");
            cmd.Parameters.AddWithValue("@status", statusFilter.Trim());
        }

        if (roomId.HasValue && roomId.Value != Guid.Empty)
        {
            sql.Append(" AND r.`room_id` = @rid ");
            cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(studentId))
        {
            sql.Append(" AND (r.`host_username` LIKE @sid OR r.`host_full_name` LIKE @sid) ");
            cmd.Parameters.AddWithValue("@sid", $"%{studentId.Trim()}%");
        }

        sql.Append(" ORDER BY r.`start_time` DESC;");
        cmd.CommandText = sql.ToString();

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadBookingDto(reader));
        }

        return list;
    }

    private BookingDto? GetBookingById(MySqlConnection conn, Guid bookingId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT r.*, rm.`room_code`, rm.`room_name`, rm.`room_type`, rm.`min_capacity`
            FROM `reservations` r
            INNER JOIN `rooms` rm ON r.`room_id` = rm.`id`
            WHERE r.`id` = @id
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", bookingId.ToString());

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadBookingDto(reader) : null;
    }

    private static BookingDto ReadBookingDto(MySqlDataReader reader)
    {
        Enum.TryParse<RoomType>(reader.GetString("room_type"), true, out var rType);
        Enum.TryParse<BookingStatus>(reader.GetString("status"), true, out var bStatus);
        var hostUsername = reader.GetString("host_username");
        var hostFullName = reader.GetString("host_full_name");
        var attendees = reader.GetInt32("expected_attendees");

        return new BookingDto
        {
            Id = ReadGuid(reader, "id"),
            BookingNo = reader.GetString("booking_no"),
            RoomId = ReadGuid(reader, "room_id"),
            RoomCode = reader.GetString("room_code"),
            RoomName = reader.GetString("room_name"),
            RoomType = rType,
            MinRequiredAttendees = reader.GetInt32("min_capacity"),
            HostStudentOrStaffId = hostUsername,
            HostFullName = hostFullName,
            Purpose = reader.GetString("purpose"),
            StartTime = reader.GetDateTime("start_time"),
            EndTime = reader.GetDateTime("end_time"),
            ExpectedAttendees = attendees,
            VerifiedAttendeesCount = attendees,
            Status = bStatus,
            CheckInPinCode = reader.GetString("checkin_pin_code"),
            CancelReason = reader.IsDBNull(reader.GetOrdinal("cancel_reason")) ? null : reader.GetString("cancel_reason"),
            CreatedAt = reader.GetDateTime("created_at"),
            CheckedInAt = reader.IsDBNull(reader.GetOrdinal("checked_in_at")) ? null : reader.GetDateTime("checked_in_at"),
            Participants = new List<ParticipantDto>
            {
                new()
                {
                    StudentOrStaffId = hostUsername,
                    FullName = $"{hostFullName} (ผู้จองหลัก • รวม {attendees} คน)",
                    IsHost = true,
                    VerificationStatus = ParticipantVerificationStatus.Verified
                }
            }
        };
    }

    public ApiResult<BookingDto> CancelBookingByUser(Guid bookingId, string reason = "ผู้ใช้งานยกเลิกการจองล่วงหน้า")
    {
        using var conn = OpenConnection();
        var booking = GetBookingById(conn, bookingId);
        if (booking == null)
            return ApiResult<BookingDto>.Fail("ไม่พบรายการจอง");

        if (booking.Status != BookingStatus.Reserved)
            return ApiResult<BookingDto>.Fail("สามารถยกเลิกได้เฉพาะการจองที่ยังไม่ได้เช็คอินเท่านั้น");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE `reservations` SET `status` = 'Cancelled', `cancel_reason` = @r WHERE `id` = @id;";
            cmd.Parameters.AddWithValue("@r", reason);
            cmd.Parameters.AddWithValue("@id", bookingId.ToString());
            cmd.ExecuteNonQuery();
        }

        NotifyStateChanged();
        return ApiResult<BookingDto>.Ok(GetBookingById(conn, bookingId)!, "ยกเลิกการจองเรียบร้อยแล้ว (คืน Slot ให้ระบบสำเร็จ)");
    }

    public ApiResult<BookingDto> EmergencyOverride(EmergencyOverrideRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            return ApiResult<BookingDto>.Fail("กรุณาระบุเหตุผลการยกเลิกฉุกเฉิน");

        using var conn = OpenConnection();
        var booking = GetBookingById(conn, req.BookingId);
        if (booking == null)
            return ApiResult<BookingDto>.Fail("ไม่พบรายการจอง");

        var reasonText = $"[Emergency Override โดย {req.AdminId}]: {req.Reason.Trim()}";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE `reservations` SET `status` = 'EmergencyOverride', `cancel_reason` = @r WHERE `id` = @id;";
            cmd.Parameters.AddWithValue("@r", reasonText);
            cmd.Parameters.AddWithValue("@id", req.BookingId.ToString());
            cmd.ExecuteNonQuery();
        }

        AddNotification(
            conn,
            "LINE Messaging API & Email (Urgent)",
            $"{booking.HostStudentOrStaffId} ({booking.HostFullName})",
            $"แจ้งยกเลิกการจองกรณีฉุกเฉิน ({booking.BookingNo})",
            $"การจองห้อง {booking.RoomCode} เวลา {booking.StartTime:dd/MM/yyyy HH:mm} ถูกยกเลิกฉุกเฉินเนื่องจาก: {req.Reason.Trim()}");

        NotifyStateChanged();
        return ApiResult<BookingDto>.Ok(
            GetBookingById(conn, req.BookingId)!,
            $"ดำเนินการ Emergency Override สำหรับ {booking.BookingNo} สำเร็จ");
    }

    public ApiResult<BookingDto> UpdateBookingStatus(Guid bookingId, BookingStatus newStatus, string? reason = null)
    {
        using var conn = OpenConnection();
        var booking = GetBookingById(conn, bookingId);
        if (booking == null)
            return ApiResult<BookingDto>.Fail("ไม่พบรายการจอง");

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                UPDATE `reservations`
                SET `status` = @st,
                    `checked_in_at` = CASE WHEN @st = 'CheckedIn' AND `checked_in_at` IS NULL THEN NOW() ELSE `checked_in_at` END,
                    `cancel_reason` = COALESCE(@r, `cancel_reason`)
                WHERE `id` = @id;";
            cmd.Parameters.AddWithValue("@st", newStatus.ToString());
            cmd.Parameters.AddWithValue("@r", (object?)reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", bookingId.ToString());
            cmd.ExecuteNonQuery();
        }

        NotifyStateChanged();
        var updatedDto = GetBookingById(conn, bookingId)!;
        return ApiResult<BookingDto>.Ok(updatedDto, $"อัปเดตสถานะเป็น {updatedDto.StatusDisplay} สำเร็จ");
    }

    // ==================== 7. PENALTIES, MAINTENANCE & ANALYTICS (MySQL) ====================

    public List<PenaltyDto> GetPenalties()
    {
        var list = new List<PenaltyDto>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM `penalties` ORDER BY `ban_start_at` DESC;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var banEnd = r.GetDateTime("ban_end_at");
            var isAct = r.GetBoolean("is_active") && banEnd > DateTime.Now;
            list.Add(new PenaltyDto
            {
                Id = ReadGuid(r, "id"),
                StudentOrStaffId = r.GetString("username"),
                FullName = r.GetString("full_name"),
                BookingNo = r.GetString("booking_no"),
                RoomName = r.GetString("room_name"),
                Reason = r.GetString("reason"),
                PenaltyDays = r.GetInt32("penalty_days"),
                BanStartAt = r.GetDateTime("ban_start_at"),
                BanEndAt = banEnd,
                IsActive = isAct
            });
        }
        return list;
    }

    public ApiResult<bool> PardonPenalty(Guid penaltyId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE `penalties` SET `is_active` = 0 WHERE `id` = @id;";
        cmd.Parameters.AddWithValue("@id", penaltyId.ToString());
        int rows = cmd.ExecuteNonQuery();
        if (rows == 0)
            return ApiResult<bool>.Fail("ไม่พบรายการระงับสิทธิ์");

        NotifyStateChanged();
        return ApiResult<bool>.Ok(true, "ปลดแบนสำเร็จ");
    }

    public List<MaintenanceReportDto> GetMaintenanceReports()
    {
        var list = new List<MaintenanceReportDto>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM `maintenance_reports` ORDER BY `created_at` DESC;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<MaintenancePriority>(r.GetString("priority"), true, out var prio);
            Enum.TryParse<MaintenanceStatus>(r.GetString("status"), true, out var st);
            list.Add(new MaintenanceReportDto
            {
                Id = ReadGuid(r, "id"),
                RoomId = ReadGuid(r, "room_id"),
                RoomCode = r.GetString("room_code"),
                RoomName = r.GetString("room_name"),
                EquipmentName = r.GetString("equipment_name"),
                ReportedBy = r.GetString("reported_by"),
                Title = r.GetString("title"),
                Description = r.GetString("description"),
                Priority = prio,
                Status = st,
                CreatedAt = r.GetDateTime("created_at"),
                ResolvedAt = r.IsDBNull(r.GetOrdinal("resolved_at")) ? null : r.GetDateTime("resolved_at")
            });
        }
        return list;
    }

    public ApiResult<MaintenanceReportDto> CreateMaintenanceReport(CreateMaintenanceReportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return ApiResult<MaintenanceReportDto>.Fail("กรุณาระบุหัวข้อปัญหา/อาการชำรุด");

        var rooms = GetRooms();
        var room = rooms.FirstOrDefault(r => r.Id == req.RoomId);
        if (room == null)
            return ApiResult<MaintenanceReportDto>.Fail("ไม่พบห้องที่ระบุ");

        var report = new MaintenanceReportDto
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName,
            EquipmentName = string.IsNullOrWhiteSpace(req.EquipmentName) ? "อุปกรณ์ทั่วไปในห้อง" : req.EquipmentName.Trim(),
            ReportedBy = string.IsNullOrWhiteSpace(req.ReportedBy) ? "ผู้ใช้งานห้องสมุด" : req.ReportedBy.Trim(),
            Title = req.Title.Trim(),
            Description = req.Description.Trim(),
            Priority = req.Priority,
            Status = MaintenanceStatus.Open,
            CreatedAt = DateTime.Now
        };

        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO `maintenance_reports`
            (`id`, `room_id`, `room_code`, `room_name`, `equipment_name`, `reported_by`, `title`, `description`, `priority`, `status`, `created_at`)
            VALUES
            (@id, @rid, @rcode, @rname, @eq, @rep, @title, @desc, @prio, 'Open', NOW());";
        cmd.Parameters.AddWithValue("@id", report.Id.ToString());
        cmd.Parameters.AddWithValue("@rid", report.RoomId.ToString());
        cmd.Parameters.AddWithValue("@rcode", report.RoomCode);
        cmd.Parameters.AddWithValue("@rname", report.RoomName);
        cmd.Parameters.AddWithValue("@eq", report.EquipmentName);
        cmd.Parameters.AddWithValue("@rep", report.ReportedBy);
        cmd.Parameters.AddWithValue("@title", report.Title);
        cmd.Parameters.AddWithValue("@desc", report.Description);
        cmd.Parameters.AddWithValue("@prio", report.Priority.ToString());
        cmd.ExecuteNonQuery();

        NotifyStateChanged();
        return ApiResult<MaintenanceReportDto>.Ok(report, "ส่งเรื่องแจ้งอุปกรณ์ชำรุดไปยังห้อง Admin สำเร็จ");
    }

    public ApiResult<MaintenanceReportDto> UpdateMaintenanceStatus(Guid reportId, MaintenanceStatus status)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE `maintenance_reports`
            SET `status` = @st,
                `resolved_at` = CASE WHEN @st = 'Resolved' THEN NOW() ELSE `resolved_at` END
            WHERE `id` = @id;";
        cmd.Parameters.AddWithValue("@st", status.ToString());
        cmd.Parameters.AddWithValue("@id", reportId.ToString());
        cmd.ExecuteNonQuery();

        NotifyStateChanged();
        var updated = GetMaintenanceReports().FirstOrDefault(m => m.Id == reportId);
        return updated != null
            ? ApiResult<MaintenanceReportDto>.Ok(updated, $"อัปเดตสถานะงานซ่อมเป็น {status} สำเร็จ")
            : ApiResult<MaintenanceReportDto>.Fail("ไม่พบรายการแจ้งซ่อม");
    }

    public DashboardSummaryDto GetDashboardSummary()
    {
        var today = DateTime.Today;
        var rooms = GetRooms(today);
        var allBookings = GetBookings();
        var penalties = GetPenalties();
        var maint = GetMaintenanceReports();
        int totalOperatingHoursPerDay = CloseHour - OpenHour; // 8 ชม./วัน (09:00 - 17:00)

        var roomUtils = new List<RoomUtilizationItemDto>();
        foreach (var r in rooms)
        {
            var roomBookingsToday = allBookings.Where(b => b.RoomId == r.Id && b.StartTime.Date == today).ToList();
            var bookedHours = roomBookingsToday
                .Where(b => b.Status == BookingStatus.Reserved || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed)
                .Sum(b => (b.EndTime - b.StartTime).TotalHours);

            var checkedInHours = roomBookingsToday
                .Where(b => b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed)
                .Sum(b => (b.EndTime - b.StartTime).TotalHours);

            var noShows = allBookings.Count(b => b.RoomId == r.Id && b.Status == BookingStatus.AutoReleased);

            roomUtils.Add(new RoomUtilizationItemDto
            {
                RoomCode = r.RoomCode,
                RoomName = r.RoomName,
                RoomType = r.RoomType == RoomType.Large ? "ขนาดใหญ่ (ขั้นต่ำ 7 คน)" : "ขนาดกลาง (ขั้นต่ำ 5 คน)",
                TotalAvailableHours = totalOperatingHoursPerDay,
                BookedHours = Math.Round(bookedHours, 1),
                CheckedInHours = Math.Round(checkedInHours, 1),
                NoShowCount = noShows
            });
        }

        var notifs = new List<NotificationLogDto>();
        using (var conn = OpenConnection())
        using (var nCmd = conn.CreateCommand())
        {
            nCmd.CommandText = "SELECT * FROM `notification_logs` ORDER BY `sent_at` DESC LIMIT 15;";
            using var nr = nCmd.ExecuteReader();
            while (nr.Read())
            {
                notifs.Add(new NotificationLogDto
                {
                    Id = ReadGuid(nr, "id"),
                    Channel = nr.GetString("channel"),
                    Recipient = nr.GetString("recipient"),
                    Subject = nr.GetString("subject"),
                    Message = nr.GetString("message"),
                    SentAt = nr.GetDateTime("sent_at")
                });
            }
        }

        var avgUtil = roomUtils.Count > 0 ? Math.Round(roomUtils.Average(u => u.UtilizationRatePercent), 1) : 0;

        return new DashboardSummaryDto
        {
            TotalRooms = rooms.Count,
            AvailableRoomsCount = rooms.Count(r => r.Status == RoomStatus.Available),
            ActiveBookingsToday = allBookings.Count(b => b.StartTime.Date == today && (b.Status == BookingStatus.Reserved || b.Status == BookingStatus.CheckedIn)),
            CheckedInNowCount = allBookings.Count(b => b.Status == BookingStatus.CheckedIn),
            AutoReleasedTodayCount = allBookings.Count(b => b.Status == BookingStatus.AutoReleased),
            ActivePenaltiesCount = penalties.Count(p => p.IsActive),
            OpenMaintenanceCount = maint.Count(m => m.Status != MaintenanceStatus.Resolved),
            OverallUtilizationPercent = avgUtil,
            RoomUtilizations = roomUtils,
            RecentNotifications = notifs
        };
    }

    private static void AddNotification(MySqlConnection conn, string channel, string recipient, string subject, string message)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO `notification_logs` (`id`, `channel`, `recipient`, `subject`, `message`, `sent_at`)
            VALUES (@id, @ch, @rec, @sub, @msg, NOW());";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("@ch", channel);
        cmd.Parameters.AddWithValue("@rec", recipient);
        cmd.Parameters.AddWithValue("@sub", subject);
        cmd.Parameters.AddWithValue("@msg", message);
        cmd.ExecuteNonQuery();
    }
}

