using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoomReservation.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole
{
    Student,
    Staff,
    Admin
}

/// <summary>
/// ประเภทห้องบริการ (มีเฉพาะห้องขนาดกลาง 3 ห้อง ขั้นต่ำ 5 คน และห้องขนาดใหญ่ 1 ห้อง ขั้นต่ำ 7 คน)
/// ใช้ FlexibleRoomTypeJsonConverter เพื่อป้องกัน Error เวลาแปลงค่า JSON ระหว่าง Web API และ Admin App
/// </summary>
[JsonConverter(typeof(FlexibleRoomTypeJsonConverter))]
public enum RoomType
{
    Medium = 0,  // ห้องขนาดกลาง (จำนวน 3 ห้อง, ขั้นต่ำ 5 คนขึ้นไป)
    Large = 1    // ห้องขนาดใหญ่ (จำนวน 1 ห้อง, ขั้นต่ำ 7 คนขึ้นไป)
}

public class FlexibleRoomTypeJsonConverter : JsonConverter<RoomType>
{
    public override RoomType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString()?.Trim() ?? "";
            if (raw.Equals("Large", StringComparison.OrdinalIgnoreCase) ||
                raw.Equals("Multimedia", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("ใหญ่", StringComparison.OrdinalIgnoreCase))
            {
                return RoomType.Large;
            }
            // ค่าอื่นๆ ทั้งหมดรวมถึง Medium หรือค่าเก่าจากแคช จะถูกแปลงเป็น Medium อย่างปลอดภัย ไม่เกิด Error
            return RoomType.Medium;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int num))
        {
            return num == 1 ? RoomType.Large : RoomType.Medium;
        }

        return RoomType.Medium;
    }

    public override void Write(Utf8JsonWriter writer, RoomType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value == RoomType.Large ? "Large" : "Medium");
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoomStatus
{
    Available,
    Maintenance,
    Inactive
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EquipmentCondition
{
    Working,
    Damaged,
    UnderRepair
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    Reserved,           // จองสำเร็จ รอไปเช็คอินที่ห้อง Admin
    CheckedIn,          // เช็คอินที่ห้อง Admin และเปิดประตูห้องแล้ว
    Completed,          // ใช้งานเสร็จสิ้นตามเวลา
    AutoReleased,       // ถูกตัดสิทธิ์อัตโนมัติ (ไม่มาเช็คอินที่ห้อง Admin ภายใน 15 นาที)
    Cancelled,          // ผู้ใช้ยกเลิกเองล่วงหน้า
    EmergencyOverride   // Admin ยกเลิก/ปรับเปลี่ยนกรณีฉุกเฉิน
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParticipantVerificationStatus
{
    Pending,
    Verified,
    Rejected
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CheckInMethod
{
    AdminCounter,   // เช็คอินที่ห้อง Admin และเจ้าหน้าที่ไปเปิดประตูห้องให้
    PinCode,
    DynamicQR,
    AdminOverride
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenancePriority
{
    Low,
    Medium,
    High,
    Critical
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStatus
{
    Open,
    InProgress,
    Resolved
}

