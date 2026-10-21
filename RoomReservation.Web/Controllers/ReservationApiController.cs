using Microsoft.AspNetCore.Mvc;
using RoomReservation.Contracts;
using RoomReservation.Web.Services;

namespace RoomReservation.Web.Controllers;

[ApiController]
[Route("api/v1")]
public class ReservationApiController : ControllerBase
{
    private readonly ReservationEngineService _engine;

    public ReservationApiController(ReservationEngineService engine)
    {
        _engine = engine;
    }

    // ==================== 1. AUTH & QUOTA (MySQL `users`) ====================

    [HttpPost("auth/login")]
    public ActionResult<ApiResult<AuthSessionDto>> Login([FromBody] LoginRequest request)
    {
        var result = _engine.Login(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("auth/register")]
    public ActionResult<ApiResult<AuthSessionDto>> Register([FromBody] RegisterUserRequest request)
    {
        var result = _engine.RegisterUser(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("auth/sso-login")]
    public ActionResult<ApiResult<AuthSessionDto>> SsoLogin([FromBody] SsoLoginRequest request)
    {
        var result = _engine.SsoLogin(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("users/{studentOrStaffId}/quota")]
    public ActionResult<ApiResult<UserQuotaDto>> GetQuota(string studentOrStaffId, [FromQuery] DateTime? date)
    {
        var quota = _engine.GetUserQuota(studentOrStaffId, date);
        return Ok(ApiResult<UserQuotaDto>.Ok(quota));
    }

    // ==================== 2. ROOMS & TIMELINE ====================

    [HttpGet("rooms")]
    public ActionResult<ApiResult<List<RoomDto>>> GetRooms(
        [FromQuery] DateTime? date,
        [FromQuery] RoomType? roomType,
        [FromQuery] int? minCapacity,
        [FromQuery] string? equipment)
    {
        var rooms = _engine.GetRooms(date, roomType, minCapacity, equipment);
        return Ok(ApiResult<List<RoomDto>>.Ok(rooms));
    }

    [HttpGet("rooms/{roomId:guid}/qr-token")]
    public ActionResult<ApiResult<DynamicQrTokenDto>> GetRoomDynamicQr(Guid roomId)
    {
        var qr = _engine.GetDynamicQrToken(roomId);
        if (qr == null)
            return NotFound(ApiResult<DynamicQrTokenDto>.Fail("ไม่พบห้องที่ระบุ"));
        return Ok(ApiResult<DynamicQrTokenDto>.Ok(qr));
    }

    // ==================== 3. BOOKINGS ====================

    [HttpGet("bookings")]
    public ActionResult<ApiResult<List<BookingDto>>> GetBookings(
        [FromQuery] string? status,
        [FromQuery] Guid? roomId,
        [FromQuery] string? studentId)
    {
        var list = _engine.GetBookings(status, roomId, studentId);
        return Ok(ApiResult<List<BookingDto>>.Ok(list));
    }

    [HttpPost("bookings")]
    public ActionResult<ApiResult<BookingDto>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var result = _engine.CreateBooking(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    public ActionResult<ApiResult<BookingDto>> CancelBooking(Guid id, [FromQuery] string? reason)
    {
        var result = _engine.CancelBookingByUser(id, reason ?? "ผู้ใช้ยกเลิกการจองล่วงหน้า");
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("bookings/{id:guid}/status")]
    public ActionResult<ApiResult<BookingDto>> UpdateStatus(
        Guid id,
        [FromQuery] BookingStatus status,
        [FromQuery] string? reason)
    {
        var result = _engine.UpdateBookingStatus(id, status, reason);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ==================== 4. CHECK-IN & GROUP VERIFICATION ====================

    [HttpPost("bookings/verify-participant")]
    public ActionResult<ApiResult<BookingDto>> VerifyParticipant([FromBody] GroupVerifyRequest request)
    {
        var result = _engine.VerifyGroupParticipant(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("bookings/check-in")]
    public ActionResult<ApiResult<BookingDto>> CheckIn([FromBody] CheckInRequest request)
    {
        var result = _engine.CheckIn(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ==================== 5. ADMIN DASHBOARD, OVERRIDE, PENALTIES & MAINTENANCE ====================

    [HttpGet("admin/dashboard")]
    public ActionResult<ApiResult<DashboardSummaryDto>> GetDashboardSummary()
    {
        return Ok(ApiResult<DashboardSummaryDto>.Ok(_engine.GetDashboardSummary()));
    }

    [HttpPost("admin/emergency-override")]
    public ActionResult<ApiResult<BookingDto>> EmergencyOverride([FromBody] EmergencyOverrideRequest request)
    {
        var result = _engine.EmergencyOverride(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("admin/run-auto-release")]
    public ActionResult<ApiResult<int>> TriggerAutoReleaseNow()
    {
        int count = _engine.RunAutomatedChecks();
        return Ok(ApiResult<int>.Ok(count, $"รันตรวจสอบ Auto-release & No-show สำเร็จ (อัปเดต {count} รายการ)"));
    }

    [HttpGet("admin/penalties")]
    public ActionResult<ApiResult<List<PenaltyDto>>> GetPenalties()
    {
        return Ok(ApiResult<List<PenaltyDto>>.Ok(_engine.GetPenalties()));
    }

    [HttpDelete("admin/penalties/{penaltyId:guid}")]
    public ActionResult<ApiResult<bool>> PardonPenalty(Guid penaltyId)
    {
        var result = _engine.PardonPenalty(penaltyId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("maintenance/reports")]
    public ActionResult<ApiResult<List<MaintenanceReportDto>>> GetMaintenanceReports()
    {
        return Ok(ApiResult<List<MaintenanceReportDto>>.Ok(_engine.GetMaintenanceReports()));
    }

    [HttpPost("maintenance/reports")]
    public ActionResult<ApiResult<MaintenanceReportDto>> CreateMaintenanceReport([FromBody] CreateMaintenanceReportRequest request)
    {
        var result = _engine.CreateMaintenanceReport(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("maintenance/reports/{reportId:guid}/status")]
    public ActionResult<ApiResult<MaintenanceReportDto>> UpdateMaintenanceStatus(
        Guid reportId,
        [FromQuery] MaintenanceStatus status)
    {
        var result = _engine.UpdateMaintenanceStatus(reportId, status);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
