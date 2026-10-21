using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RoomReservation.Contracts;

namespace LibraryAdminApp.Services;

/// <summary>
/// Service สำหรับเชื่อมต่อกับ ASP.NET Core Web API (http://localhost:5180/api/v1)
/// มีการดักจับ Exception (try-catch) ทุกจุดเพื่อป้องกันแอปพลิเคชัน Crash
/// </summary>
[DebuggerStepThrough]
public class ReservationApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public string BaseUrl { get; set; } = "http://localhost:5180";

    public ReservationApiClient(string? baseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
            BaseUrl = baseUrl.TrimEnd('/');

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task<DashboardSummaryDto?> GetDashboardAsync()
    {
        try
        {
            using var resp = await _http.GetAsync($"{BaseUrl}/api/v1/admin/dashboard");
            if (!resp.IsSuccessStatusCode) return null;
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<DashboardSummaryDto>>(_jsonOptions);
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<RoomDto>> GetRoomsAsync(RoomType? roomType = null, int? minCap = null, string? equipment = null)
    {
        try
        {
            var query = new List<string>();
            if (roomType.HasValue) query.Add($"roomType={roomType.Value}");
            if (minCap.HasValue && minCap.Value > 0) query.Add($"minCapacity={minCap.Value}");
            if (!string.IsNullOrWhiteSpace(equipment)) query.Add($"equipment={Uri.EscapeDataString(equipment)}");

            var url = $"{BaseUrl}/api/v1/rooms" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return new List<RoomDto>();
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<List<RoomDto>>>(_jsonOptions);
            return res?.Data ?? new List<RoomDto>();
        }
        catch
        {
            return new List<RoomDto>();
        }
    }

    public async Task<UserQuotaDto?> GetUserQuotaAsync(string studentOrStaffId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(studentOrStaffId)) return null;
            using var resp = await _http.GetAsync(
                $"{BaseUrl}/api/v1/users/{Uri.EscapeDataString(studentOrStaffId.Trim())}/quota");
            if (!resp.IsSuccessStatusCode) return null;
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<UserQuotaDto>>(_jsonOptions);
            return res?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<BookingDto>> GetBookingsAsync(string? status = null, string? studentId = null)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(studentId)) query.Add($"studentId={Uri.EscapeDataString(studentId)}");

            var url = $"{BaseUrl}/api/v1/bookings" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return new List<BookingDto>();
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<List<BookingDto>>>(_jsonOptions);
            return res?.Data ?? new List<BookingDto>();
        }
        catch
        {
            return new List<BookingDto>();
        }
    }

    public async Task<ApiResult<BookingDto>> CreateBookingAsync(CreateBookingRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/v1/bookings", request, _jsonOptions);
            return await ReadApiResultAsync<BookingDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่สามารถเชื่อมต่อฐานข้อมูล/เซิร์ฟเวอร์ได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<BookingDto>> UpdateBookingStatusAsync(Guid bookingId, BookingStatus status, string? reason = null)
    {
        try
        {
            var url = $"{BaseUrl}/api/v1/bookings/{bookingId}/status?status={status}";
            if (!string.IsNullOrWhiteSpace(reason))
                url += $"&reason={Uri.EscapeDataString(reason)}";

            using var req = new HttpRequestMessage(HttpMethod.Patch, url);
            using var response = await _http.SendAsync(req);
            return await ReadApiResultAsync<BookingDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่สามารถอัปเดตสถานะได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<BookingDto>> EmergencyOverrideAsync(EmergencyOverrideRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/v1/admin/emergency-override", request, _jsonOptions);
            return await ReadApiResultAsync<BookingDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่สามารถดำเนินการได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<BookingDto>> VerifyParticipantAsync(GroupVerifyRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/v1/bookings/verify-participant", request, _jsonOptions);
            return await ReadApiResultAsync<BookingDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่พบรายการ หรือไม่สามารถเชื่อมต่อได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<BookingDto>> CheckInAsync(CheckInRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/v1/bookings/check-in", request, _jsonOptions);
            return await ReadApiResultAsync<BookingDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<BookingDto>.Fail($"ไม่พบรายการ หรือไม่สามารถเชื่อมต่อได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<int>> TriggerAutoReleaseAsync()
    {
        try
        {
            using var response = await _http.PostAsync($"{BaseUrl}/api/v1/admin/run-auto-release", null);
            return await ReadApiResultAsync<int>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<int>.Fail($"ไม่สามารถรันระบบตรวจสอบได้: {ex.Message}");
        }
    }

    public async Task<List<PenaltyDto>> GetPenaltiesAsync()
    {
        try
        {
            using var resp = await _http.GetAsync($"{BaseUrl}/api/v1/admin/penalties");
            if (!resp.IsSuccessStatusCode) return new List<PenaltyDto>();
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<List<PenaltyDto>>>(_jsonOptions);
            return res?.Data ?? new List<PenaltyDto>();
        }
        catch
        {
            return new List<PenaltyDto>();
        }
    }

    public async Task<ApiResult<bool>> PardonPenaltyAsync(Guid penaltyId)
    {
        try
        {
            using var response = await _http.DeleteAsync($"{BaseUrl}/api/v1/admin/penalties/{penaltyId}");
            return await ReadApiResultAsync<bool>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<bool>.Fail($"ไม่สามารถปลดแบนได้: {ex.Message}");
        }
    }

    public async Task<List<MaintenanceReportDto>> GetMaintenanceReportsAsync()
    {
        try
        {
            using var resp = await _http.GetAsync($"{BaseUrl}/api/v1/maintenance/reports");
            if (!resp.IsSuccessStatusCode) return new List<MaintenanceReportDto>();
            var res = await resp.Content.ReadFromJsonAsync<ApiResult<List<MaintenanceReportDto>>>(_jsonOptions);
            return res?.Data ?? new List<MaintenanceReportDto>();
        }
        catch
        {
            return new List<MaintenanceReportDto>();
        }
    }

    public async Task<ApiResult<MaintenanceReportDto>> CreateMaintenanceReportAsync(CreateMaintenanceReportRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync($"{BaseUrl}/api/v1/maintenance/reports", request, _jsonOptions);
            return await ReadApiResultAsync<MaintenanceReportDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<MaintenanceReportDto>.Fail($"ไม่สามารถบันทึกแจ้งซ่อมได้: {ex.Message}");
        }
    }

    public async Task<ApiResult<MaintenanceReportDto>> UpdateMaintenanceStatusAsync(Guid reportId, MaintenanceStatus status)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/api/v1/maintenance/reports/{reportId}/status?status={status}");
            using var response = await _http.SendAsync(req);
            return await ReadApiResultAsync<MaintenanceReportDto>(response);
        }
        catch (Exception ex)
        {
            return ApiResult<MaintenanceReportDto>.Fail($"ไม่สามารถอัปเดตสถานะงานซ่อมได้: {ex.Message}");
        }
    }

    private async Task<ApiResult<T>> ReadApiResultAsync<T>(HttpResponseMessage response)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<ApiResult<T>>(_jsonOptions);
            return result ?? ApiResult<T>.Fail("ไม่พบรายการ");
        }
        catch
        {
            return ApiResult<T>.Fail("ไม่พบข้อมูล");
        }
    }
}

