using System.Net.Http.Json;
using System.Text.Json;
using QuanAnWinForm.Models;

namespace QuanAnWinForm.Services;

public class ApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiService()
    {
        _http = new HttpClient(new AuthHeaderHandler()) { BaseAddress = new Uri(ApiConfig.BaseUrl) };
    }

    public async Task<List<TableDto>> GetTablesAsync()
    {
        var res = await _http.GetFromJsonAsync<List<TableDto>>("/api/tables", JsonOpts);
        return res ?? new List<TableDto>();
    }

    public async Task<List<MenuItemDto>> GetMenuAsync()
    {
        var res = await _http.GetFromJsonAsync<List<MenuItemDto>>("/api/menu", JsonOpts);
        return res ?? new List<MenuItemDto>();
    }

    public async Task<LoginResultDto?> LoginAsync(string username, string password)
    {
        var res = await _http.PostAsJsonAsync("/api/auth/login", new { username, password });
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<LoginResultDto>(JsonOpts);
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId)
    {
        return await _http.GetFromJsonAsync<OrderDto>($"/api/orders/{orderId}", JsonOpts);
    }

    // Lấy đơn ĐANG MỞ của 1 bàn kèm các món chưa hủy (GET /api/orders/table/{tableId}).
    // Trả null nếu bàn chưa có đơn đang mở (Backend trả 404) - không coi là lỗi.
    public async Task<OpenTableOrderDto?> GetOpenOrderByTableAsync(int tableId)
    {
        var res = await _http.GetAsync($"/api/orders/table/{tableId}");
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            await EnsureSuccessOrThrowMessageAsync(res);
        }

        return await res.Content.ReadFromJsonAsync<OpenTableOrderDto>(JsonOpts);
    }

    public async Task<int?> AddOrderItemsAsync(int tableId, int userId, List<OrderItemRequest> items)
    {
        var res = await _http.PostAsJsonAsync("/api/orders/add-items", new
        {
            tableId,
            userId,
            items = items.Select(i => new { menuItemId = i.MenuItemId, quantity = i.Quantity, note = i.Note, isTakeaway = i.IsTakeaway })
        });

        if (!res.IsSuccessStatusCode)
        {
            await EnsureSuccessOrThrowMessageAsync(res);
        }

        var result = await res.Content.ReadFromJsonAsync<AddItemsResultDto>(JsonOpts);
        return result?.OrderId;
    }

    // Don MANG VE: khong gan ban. Backend tao don moi + danh so MV-001, MV-002... trong ngay
    // (OrdersController.AddItems, IsTakeaway = true). Tra ve OrderId de thanh toan ngay.
    public async Task<int?> AddTakeawayOrderItemsAsync(int userId, List<OrderItemRequest> items)
    {
        var res = await _http.PostAsJsonAsync("/api/orders/add-items", new
        {
            userId,
            isTakeaway = true,
            items = items.Select(i => new { menuItemId = i.MenuItemId, quantity = i.Quantity, note = i.Note, isTakeaway = i.IsTakeaway })
        });

        if (!res.IsSuccessStatusCode)
        {
            await EnsureSuccessOrThrowMessageAsync(res);
        }

        var result = await res.Content.ReadFromJsonAsync<AddItemsResultDto>(JsonOpts);
        return result?.OrderId;
    }

    // Danh sách đơn MANG VỀ đang mở (GET /api/orders/takeaway/open) - dùng cho tab "Mang về".
    public async Task<List<TakeawayOrderSummaryDto>> GetOpenTakeawayOrdersAsync()
    {
        var res = await _http.GetFromJsonAsync<List<TakeawayOrderSummaryDto>>("/api/orders/takeaway/open", JsonOpts);
        return res ?? new List<TakeawayOrderSummaryDto>();
    }

    public async Task<PaymentPreviewOrderDto?> GetPaymentPreviewAsync(
        int orderId,
        int? campaignId = null)
    {
        var url = $"/api/payments/preview/order/{orderId}";
        if (campaignId.HasValue)
            url += $"?campaignId={campaignId.Value}";

        return await _http.GetFromJsonAsync<PaymentPreviewOrderDto>(
            url,
            JsonOpts);
    }

    public async Task<PaymentPreviewOrderDto?> GetReceiptPreviewAsync(
        decimal amount,
        int orderId = 0)
    {
        var url =
            $"/api/payments/receipt-preview?amount={Uri.EscapeDataString(amount.ToString(System.Globalization.CultureInfo.InvariantCulture))}&orderId={orderId}";

        return await _http.GetFromJsonAsync<PaymentPreviewOrderDto>(
            url,
            JsonOpts);
    }

    // customerId/redeemPoints (tùy chọn - BỔ SUNG cho module Khách hàng thân thiết):
    // gắn khách hàng thành viên vào hóa đơn này (tích điểm tự động) và/hoặc dùng
    // điểm sẵn có để giảm giá - xem PaymentsController.Create bên Backend.
    public async Task<PaymentDto?> CreatePaymentAsync(
        int orderId,
        int method,
        int? campaignId = null,
        int? customerId = null,
        int? redeemPoints = null,
        string? transactionRef = null)
    {
        var res = await _http.PostAsJsonAsync(
            "/api/payments",
            new
            {
                orderId,
                method,
                campaignId,
                customerId,
                redeemPoints,
                transactionRef
            });
        if (!res.IsSuccessStatusCode)
        {
            await EnsureSuccessOrThrowMessageAsync(res);
        }
        return await res.Content.ReadFromJsonAsync<PaymentDto>(JsonOpts);
    }

    public async Task<List<TableDto>> GetTablesAsync(int? areaId = null)
    {
        var url = areaId.HasValue ? $"/api/tables?areaId={areaId}" : "/api/tables";
        var res = await _http.GetFromJsonAsync<List<TableDto>>(url, JsonOpts);
        return res ?? new List<TableDto>();
    }

    public async Task<List<AreaDto>> GetAreasAsync()
    {
        var res = await _http.GetFromJsonAsync<List<AreaDto>>("/api/areas", JsonOpts);
        return res ?? new List<AreaDto>();
    }

    public async Task CreateAreaAsync(string name, string? description)
    {
        var res = await _http.PostAsJsonAsync("/api/areas", new { name, description });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateAreaAsync(int id, string name, string? description)
    {
        var res = await _http.PutAsJsonAsync($"/api/areas/{id}", new { name, description });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteAreaAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/areas/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task CreateTableAsync(string tableName, int capacity, int areaId)
    {
        var res = await _http.PostAsJsonAsync("/api/tables", new { tableName, capacity, areaId });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateTableAsync(int id, string tableName, int capacity, int areaId)
    {
        var res = await _http.PutAsJsonAsync($"/api/tables/{id}", new { tableName, capacity, areaId });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteTableAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/tables/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        var res = await _http.GetFromJsonAsync<List<EmployeeDto>>("/api/users", JsonOpts);
        return res ?? new List<EmployeeDto>();
    }

    public async Task<int> CreateEmployeeAsync(
        string fullName,
        string username,
        string password,
        string? phone,
        string? email,
        string employeeCode,
        int roleId,
        int? stationId = null)
    {
        var res = await _http.PostAsJsonAsync(
            "/api/users",
            new { fullName, username, password, phone, email, employeeCode, roleId, stationId });
        await EnsureSuccessOrThrowMessageAsync(res);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
    }

    public async Task UpdateEmployeeAsync(
        int id,
        string fullName,
        string? phone,
        string? email,
        int roleId,
        int? stationId = null)
    {
        var res = await _http.PutAsJsonAsync(
            $"/api/users/{id}",
            new { fullName, phone, email, roleId, stationId });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task SetEmployeeActiveAsync(int id, bool isActive)
    {
        var res = await _http.PutAsJsonAsync($"/api/users/{id}/active", isActive);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Danh sách vai trò lấy từ DB (thay cho việc hardcode RoleId ở StaffControl)
    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var res = await _http.GetFromJsonAsync<List<RoleDto>>("/api/users/roles", JsonOpts);
        return res ?? new List<RoleDto>();
    }

    // Quản lý đặt lại mật khẩu cho nhân viên (id = EmployeeDto.Id)
    public async Task ResetEmployeePasswordAsync(int id, string newPassword)
    {
        var res = await _http.PutAsJsonAsync($"/api/users/{id}/reset-password", new { newPassword });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Nhân viên tự đổi mật khẩu của chính mình (mọi vai trò)
    public async Task ChangeMyPasswordAsync(string oldPassword, string newPassword)
    {
        var res = await _http.PutAsJsonAsync("/api/auth/change-password", new { oldPassword, newPassword });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<ShiftDto>> GetShiftsAsync()
    {
        var res = await _http.GetFromJsonAsync<List<ShiftDto>>("/api/shifts", JsonOpts);
        return res ?? new List<ShiftDto>();
    }

    public async Task<List<ShiftAssignmentDto>> GetAssignmentsAsync(DateTime date)
    {
        var res = await _http.GetFromJsonAsync<List<ShiftAssignmentDto>>($"/api/shifts/assignments?date={date:yyyy-MM-dd}", JsonOpts);
        return res ?? new List<ShiftAssignmentDto>();
    }

    public async Task AssignShiftAsync(
        int userId,
        int shiftId,
        DateTime workDate,
        IReadOnlyCollection<int> areaIds)
    {
        var res = await _http.PostAsJsonAsync(
            "/api/shifts/assignments",
            new
            {
                userId,
                shiftId,
                workDate,
                areaIds = areaIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToArray()
            });

        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Overload giữ tương thích cho code cũ chỉ gán 1 khu.
    public Task AssignShiftAsync(
        int userId,
        int shiftId,
        DateTime workDate,
        int? areaId = null)
    {
        IReadOnlyCollection<int> areaIds =
            areaId.HasValue
                ? new[] { areaId.Value }
                : Array.Empty<int>();

        return AssignShiftAsync(
            userId,
            shiftId,
            workDate,
            areaIds);
    }

    public async Task UpdateAssignmentAreasAsync(
        int assignmentId,
        IReadOnlyCollection<int> areaIds)
    {
        var res = await _http.PutAsJsonAsync(
            $"/api/shifts/assignments/{assignmentId}/areas",
            new
            {
                areaIds = areaIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToArray()
            });

        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Overload cũ vẫn dùng được nếu chỗ khác chỉ chỉnh một khu.
    public Task UpdateAssignmentAreaAsync(
        int assignmentId,
        int? areaId)
    {
        IReadOnlyCollection<int> areaIds =
            areaId.HasValue
                ? new[] { areaId.Value }
                : Array.Empty<int>();

        return UpdateAssignmentAreasAsync(
            assignmentId,
            areaIds);
    }

    public async Task<List<ShiftAssignmentDto>> GetAssignmentHistoryAsync(int? userId, DateTime from, DateTime to)
    {
        var url = $"/api/shifts/assignments/history?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
        if (userId.HasValue) url += $"&userId={userId}";
        var res = await _http.GetFromJsonAsync<List<ShiftAssignmentDto>>(url, JsonOpts);
        return res ?? new List<ShiftAssignmentDto>();
    }

    public async Task<AttendanceLocationSettingsDto?> GetAttendanceLocationSettingsAsync()
    {
        return await _http.GetFromJsonAsync<AttendanceLocationSettingsDto>(
            "/api/attendance-settings",
            JsonOpts);
    }

    public async Task<AttendanceLocationSettingsDto?> UpdateAttendanceLocationSettingsAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        double maxAccuracyMeters)
    {
        var res = await _http.PutAsJsonAsync(
            "/api/attendance-settings",
            new
            {
                latitude,
                longitude,
                radiusMeters,
                maxAccuracyMeters
            });

        await EnsureSuccessOrThrowMessageAsync(res);

        return await res.Content.ReadFromJsonAsync<AttendanceLocationSettingsDto>(JsonOpts);
    }

    public async Task<List<StaffTaskDto>> GetTasksAsync(int? userId = null)
    {
        var url = userId.HasValue ? $"/api/stafftasks?userId={userId}" : "/api/stafftasks";
        var res = await _http.GetFromJsonAsync<List<StaffTaskDto>>(url, JsonOpts);
        return res ?? new List<StaffTaskDto>();
    }

    public async Task CreateTaskAsync(string title, string? description, int assignedToUserId, DateTime? dueAt)
    {
        var res = await _http.PostAsJsonAsync("/api/stafftasks", new { title, description, assignedToUserId, dueAt });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateTaskStatusAsync(int id, int status)
    {
        var res = await _http.PutAsJsonAsync($"/api/stafftasks/{id}/status", status);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<OrderItemStatusDto>> GetOrderItemsAsync(int? departmentId = null)
    {
        var url = departmentId.HasValue ? $"/api/orderitems?departmentId={departmentId}" : "/api/orderitems";
        var res = await _http.GetFromJsonAsync<List<OrderItemStatusDto>>(url, JsonOpts);
        return res ?? new List<OrderItemStatusDto>();
    }

    // Cập nhật trạng thái món ăn kèm userId, lý do hủy và RowVer - trả về trạng
    // thái THẬT SỰ đang có ở Backend sau khi cập nhật (dùng để đồng bộ lại UI,
    // tránh lệch trạng thái nếu có sự cố mạng/SignalR trong lúc gọi).
    public async Task<OrderItemStatusResultDto?> UpdateOrderItemStatusAsync(int orderItemId, int newStatus, int? userId = null, string? cancelReason = null, byte[]? rowVer = null)
    {
        var payload = new UpdateOrderItemStatusRequestDto
        {
            NewStatus = newStatus,
            UserId = userId,
            CancelReason = cancelReason,
            RowVer = rowVer
        };
        var res = await _http.PutAsJsonAsync($"/api/orderitems/{orderItemId}/status", payload);
        await EnsureSuccessOrThrowMessageAsync(res);
        return await res.Content.ReadFromJsonAsync<OrderItemStatusResultDto>(JsonOpts);
    }

    public async Task UpdateOrderStatusAsync(int orderId, int status)
    {
        var res = await _http.PutAsJsonAsync($"/api/orderitems/order/{orderId}/status", status);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<OrderSummaryDto>> GetOrdersAsync(DateTime? date = null, int? status = null)
    {
        var qs = new List<string>();
        if (date.HasValue) qs.Add($"date={date.Value:yyyy-MM-dd}");
        if (status.HasValue) qs.Add($"status={(OrderStatus)status.Value}");
        var url = "/api/orders" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");

        var res = await _http.GetFromJsonAsync<List<OrderSummaryDto>>(url, JsonOpts);
        return res ?? new List<OrderSummaryDto>();
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId)
    {
        return await _http.GetFromJsonAsync<OrderDto>($"/api/orders/{orderId}", JsonOpts);
    }

    public async Task CancelOrderAsync(int orderId, int userId, string? reason = null)
    {
        var res = await _http.PutAsJsonAsync($"/api/orders/{orderId}/cancel", new { userId, reason });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<int> UpdateOrderItemQuantityAsync(
        int orderItemId,
        int quantity)
    {
        var res = await _http.PutAsJsonAsync(
            $"/api/mobile/order-actions/items/{orderItemId}/quantity",
            new
            {
                quantity,
                employeeCode = (string?)null
            });

        await EnsureSuccessOrThrowMessageAsync(res);

        var payload = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        if (payload.TryGetProperty("quantity", out var quantityElement) &&
            quantityElement.TryGetInt32(out var savedQuantity))
        {
            return savedQuantity;
        }

        throw new InvalidOperationException(
            "Backend không trả về số lượng món sau khi cập nhật.");
    }

    public async Task<int> DecrementOrderItemQuantityAsync(int orderItemId)
    {
        var res = await _http.PostAsJsonAsync(
            $"/api/mobile/order-actions/items/{orderItemId}/decrement",
            new
            {
                orderItemIds = new[] { orderItemId },
                employeeCode = (string?)null
            });

        await EnsureSuccessOrThrowMessageAsync(res);

        var payload = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        if (payload.TryGetProperty("quantity", out var quantityElement) &&
            quantityElement.TryGetInt32(out var quantity))
        {
            return quantity;
        }

        throw new InvalidOperationException(
            "Backend không trả về số lượng món sau khi cập nhật.");
    }

    public async Task CancelOrderItemAsync(int orderItemId, int userId, string? reason = null, byte[]? rowVer = null)
    {
        await UpdateOrderItemStatusAsync(orderItemId, (int)KitchenStatus.DaHuy, userId, reason, rowVer);
    }

    public async Task<List<DiscountCampaignDto>> GetCampaignsAsync()
    {
        var res = await _http.GetFromJsonAsync<List<DiscountCampaignDto>>("/api/discountcampaigns", JsonOpts);
        return res ?? new List<DiscountCampaignDto>();
    }

    public async Task<List<ActiveCampaignDto>> GetActiveCampaignsAsync()
    {
        var res = await _http.GetFromJsonAsync<List<ActiveCampaignDto>>("/api/discountcampaigns/active", JsonOpts);
        return res ?? new List<ActiveCampaignDto>();
    }

    public async Task CreateCampaignAsync(string campaignName, DateTime startDate, DateTime endDate, decimal discountPercent, string? note, int userId)
    {
        var res = await _http.PostAsJsonAsync("/api/discountcampaigns", new { campaignName, startDate, endDate, discountPercent, note, userId });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateCampaignAsync(int id, string campaignName, DateTime startDate, DateTime endDate, decimal discountPercent, string? note)
    {
        var res = await _http.PutAsJsonAsync($"/api/discountcampaigns/{id}", new { campaignName, startDate, endDate, discountPercent, note });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task SetCampaignActiveAsync(int id, bool isActive)
    {
        var res = await _http.PutAsJsonAsync($"/api/discountcampaigns/{id}/active", isActive);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteCampaignAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/discountcampaigns/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<int> CheckInTableAsync(int tableId, int userId, int? reservationId = null, string? note = null)
    {
        var res = await _http.PostAsJsonAsync("/api/orders/checkin", new { tableId, userId, reservationId, note });
        await EnsureSuccessOrThrowMessageAsync(res);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return json.GetProperty("orderId").GetInt32();
    }

    // BỔ SUNG: gắn (hoặc gỡ nếu customerId = null) khách hàng thành viên vào 1
    // đơn đang mở - dùng ở màn Bán hàng để gắn khách trước khi thanh toán.
    public async Task SetOrderCustomerAsync(int orderId, int? customerId)
    {
        var res = await _http.PutAsJsonAsync($"/api/orders/{orderId}/customer", new { customerId });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<ReservationDto>> GetReservationsAsync(DateTime? date = null, int? status = null, string? keyword = null)
    {
        var qs = new List<string>();
        if (date.HasValue) qs.Add($"date={date.Value:yyyy-MM-dd}");
        if (status.HasValue) qs.Add($"status={(ReservationStatusEnum)status.Value}");
        if (!string.IsNullOrWhiteSpace(keyword)) qs.Add($"keyword={Uri.EscapeDataString(keyword)}");
        var url = "/api/reservations" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");

        var res = await _http.GetFromJsonAsync<List<ReservationDto>>(url, JsonOpts);
        return res ?? new List<ReservationDto>();
    }

    public async Task CreateReservationAsync(int tableId, string? customerName, string? customerPhone, DateTime reservationTime, int durationMinutes, int numberOfGuests, decimal depositAmount, string? note)
    {
        var res = await _http.PostAsJsonAsync("/api/reservations", new { tableId, customerName, customerPhone, reservationTime, durationMinutes, numberOfGuests, depositAmount, note });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Đặt NHIỀU bàn cùng lúc cho nhóm khách đông (POST /api/reservations/batch).
    // tables: danh sách (TableId, số khách ngồi ở bàn đó). Backend kiểm tra hết rồi mới lưu.
    public async Task CreateReservationBatchAsync(IEnumerable<(int TableId, int NumberOfGuests)> tables, string? customerName, string? customerPhone, DateTime reservationTime, int durationMinutes, decimal depositAmount, string? note)
    {
        var slots = tables.Select(t => new { tableId = t.TableId, numberOfGuests = t.NumberOfGuests }).ToList();
        var res = await _http.PostAsJsonAsync("/api/reservations/batch", new { tables = slots, customerName, customerPhone, reservationTime, durationMinutes, depositAmount, note });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateReservationAsync(int id, string? customerName, string? customerPhone, DateTime reservationTime, int durationMinutes, int numberOfGuests, decimal depositAmount, string? note)
    {
        var res = await _http.PutAsJsonAsync($"/api/reservations/{id}", new { customerName, customerPhone, reservationTime, durationMinutes, numberOfGuests, depositAmount, note });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // BO SUNG: lay danh sach ban kem trang thai "co dat duoc khong" TAI DUNG
    // khung gio (time -> time+durationMinutes), thay vi loc theo TableDto.Status
    // tinh (nguyen nhan dat hom nay lam khoa luon ca ngay mai). Dung cho man
    // "Dat ban truoc" khi chon Gio den / Thoi luong.
    public async Task<List<TableAvailabilityDto>> GetAvailableTablesAsync(DateTime time, int durationMinutes)
    {
        var url = $"/api/tables/available?time={Uri.EscapeDataString(time.ToString("o"))}&durationMinutes={durationMinutes}";
        var res = await _http.GetFromJsonAsync<List<TableAvailabilityDto>>(url, JsonOpts);
        return res ?? new List<TableAvailabilityDto>();
    }

    // Gợi ý thông tin khách cũ (đã là thành viên chưa, lượt đặt gần nhất) theo SĐT.
    public async Task<ReservationHintDto?> GetReservationHintByPhoneAsync(string phone)
    {
        return await _http.GetFromJsonAsync<ReservationHintDto>(
            $"/api/reservations/last-by-phone?phone={Uri.EscapeDataString(phone)}", JsonOpts);
    }

    public async Task UpdateReservationStatusAsync(int id, int status)
    {
        var res = await _http.PutAsJsonAsync($"/api/reservations/{id}/status", (ReservationStatusEnum)status);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteReservationAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/reservations/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<InvoiceSummaryDto>> GetInvoicesAsync(DateTime? date = null)
    {
        var url = date.HasValue ? $"/api/invoices?date={date.Value:yyyy-MM-dd}" : "/api/invoices";
        var res = await _http.GetFromJsonAsync<List<InvoiceSummaryDto>>(url, JsonOpts);
        return res ?? new List<InvoiceSummaryDto>();
    }

    public async Task<InvoiceDetailDto?> GetInvoiceAsync(int invoiceId)
    {
        return await _http.GetFromJsonAsync<InvoiceDetailDto>($"/api/invoices/{invoiceId}", JsonOpts);
    }

    public async Task<List<OrderItemStatusDto>> GetOrderItemsByTableAsync(int tableId)
    {
        var res = await _http.GetFromJsonAsync<List<OrderItemStatusDto>>($"/api/orderitems?tableId={tableId}", JsonOpts);
        return res ?? new List<OrderItemStatusDto>();
    }

    private static async Task EnsureSuccessOrThrowMessageAsync(HttpResponseMessage res)
    {
        if (res.IsSuccessStatusCode) return;

        if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
        if (res.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Tài khoản của bạn không có quyền thực hiện thao tác này.");

        string errorMessage;
        try
        {
            var err = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
            errorMessage = err.TryGetProperty("message", out var m) ? m.GetString() ?? res.ReasonPhrase ?? "Lỗi không xác định"
                : res.ReasonPhrase ?? "Lỗi không xác định";
        }
        catch
        {
            errorMessage = res.ReasonPhrase ?? "Lỗi không xác định";
        }
        throw new InvalidOperationException(errorMessage);
    }

    public async Task CreateShiftAsync(string name, TimeSpan startTime, TimeSpan endTime)
    {
        var res = await _http.PostAsJsonAsync("/api/shifts", new { name, startTime, endTime });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateShiftAsync(int id, string name, TimeSpan startTime, TimeSpan endTime)
    {
        var res = await _http.PutAsJsonAsync($"/api/shifts/{id}", new { name, startTime, endTime });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteShiftAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/shifts/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateAssignmentStatusAsync(int assignmentId, int status)
    {
        var res = await _http.PutAsJsonAsync($"/api/shifts/assignments/{assignmentId}/status", status);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteAssignmentAsync(int assignmentId)
    {
        var res = await _http.DeleteAsync($"/api/shifts/assignments/{assignmentId}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var res = await _http.GetFromJsonAsync<List<CategoryDto>>("/api/categories", JsonOpts);
        return res ?? new List<CategoryDto>();
    }

    public async Task CreateCategoryAsync(string name, int type)
    {
        var res = await _http.PostAsJsonAsync("/api/categories", new { name, type });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task UpdateCategoryAsync(int id, string name, int type)
    {
        var res = await _http.PutAsJsonAsync($"/api/categories/{id}", new { name, type });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/categories/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<StationDto>> GetStationsAsync()
    {
        var res = await _http.GetFromJsonAsync<List<StationDto>>("/api/stations", JsonOpts);
        return res ?? new List<StationDto>();
    }

    // Trả về Id món vừa tạo (cần để upload ảnh ngay sau khi tạo món mới).
    public async Task<int> CreateMenuItemAsync(string name, decimal price, int categoryId, int? stationId, string? unit, string? description, bool isAvailable)
    {
        var res = await _http.PostAsJsonAsync("/api/menu", new
        {
            name,
            price,
            categoryId,
            departmentId = stationId,
            unit,
            description,
            isAvailable
        });
        await EnsureSuccessOrThrowMessageAsync(res);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
    }

    public async Task UpdateMenuItemAsync(int id, string name, decimal price, int categoryId, int? stationId, string? unit, string? description, bool isAvailable)
    {
        var res = await _http.PutAsJsonAsync($"/api/menu/{id}", new
        {
            name,
            price,
            categoryId,
            departmentId = stationId,
            unit,
            description,
            isAvailable
        });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task SetMenuItemAvailableAsync(int id, bool isAvailable)
    {
        var res = await _http.PutAsJsonAsync($"/api/menu/{id}/available", isAvailable);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteMenuItemAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/menu/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // ---------------------------------------------------------------------
    // ẢNH MÓN ĂN
    // ---------------------------------------------------------------------

    // Upload (hoặc thay) ảnh cho món. Trả về ImageUrl tương đối do Backend lưu.
    public async Task<string?> UploadMenuItemImageAsync(int menuItemId, string filePath)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg",
            });
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var res = await _http.PostAsync($"/api/menu/{menuItemId}/image", content);
        await EnsureSuccessOrThrowMessageAsync(res);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return body.TryGetProperty("imageUrl", out var url) ? url.GetString() : null;
    }

    public async Task DeleteMenuItemImageAsync(int menuItemId)
    {
        var res = await _http.DeleteAsync($"/api/menu/{menuItemId}/image");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // Cache byte[] theo ImageUrl (tên file có mã ngẫu nhiên nên đổi ảnh là đổi URL,
    // không lo ảnh cũ). Mỗi lần gọi tạo Bitmap MỚI để control nào dispose thì không ảnh hưởng chỗ khác.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> ImageCache = new();

    public async Task<Image?> LoadMenuImageAsync(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        try
        {
            if (!ImageCache.TryGetValue(imageUrl, out var bytes))
            {
                bytes = await _http.GetByteArrayAsync(imageUrl);
                ImageCache[imageUrl] = bytes;
            }
            using var ms = new MemoryStream(bytes);
            using var raw = Image.FromStream(ms);
            return new Bitmap(raw); // copy để không phụ thuộc MemoryStream
        }
        catch
        {
            return null; // ảnh lỗi/không tồn tại -> caller hiển thị placeholder
        }
    }

    public async Task UpdateTaskAsync(int id, string title, string? description, int assignedToUserId, DateTime? dueAt)
    {
        var res = await _http.PutAsJsonAsync($"/api/stafftasks/{id}", new { title, description, assignedToUserId, dueAt });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteTaskAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/stafftasks/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // =====================================================================
    // KHÁCH HÀNG & KHÁCH HÀNG THÂN THIẾT (BỔ SUNG)
    // =====================================================================

    public async Task<List<CustomerDto>> GetCustomersAsync(string? keyword = null)
    {
        var url = string.IsNullOrWhiteSpace(keyword) ? "/api/customers" : $"/api/customers?keyword={Uri.EscapeDataString(keyword)}";
        var res = await _http.GetFromJsonAsync<List<CustomerDto>>(url, JsonOpts);
        return res ?? new List<CustomerDto>();
    }

    public async Task<CustomerDetailDto?> GetCustomerAsync(int id)
    {
        return await _http.GetFromJsonAsync<CustomerDetailDto>($"/api/customers/{id}", JsonOpts);
    }

    // Tra cứu nhanh theo SĐT (gõ gần đúng cũng ra kết quả) - dùng ở màn Bán hàng
    // để gắn khách vào hóa đơn trước khi thanh toán.
    public async Task<List<CustomerSearchResultDto>> SearchCustomersByPhoneAsync(string phone)
    {
        var res = await _http.GetFromJsonAsync<List<CustomerSearchResultDto>>($"/api/customers/search?phone={Uri.EscapeDataString(phone)}", JsonOpts);
        return res ?? new List<CustomerSearchResultDto>();
    }

    public async Task<int> CreateCustomerAsync(string fullName, string phone, string? email, DateTime? dateOfBirth)
    {
        var res = await _http.PostAsJsonAsync("/api/customers", new { fullName, phone, email, dateOfBirth });
        await EnsureSuccessOrThrowMessageAsync(res);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        return json.GetProperty("id").GetInt32();
    }

    public async Task UpdateCustomerAsync(int id, string fullName, string phone, string? email, DateTime? dateOfBirth)
    {
        var res = await _http.PutAsJsonAsync($"/api/customers/{id}", new { fullName, phone, email, dateOfBirth });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task DeleteCustomerAsync(int id)
    {
        var res = await _http.DeleteAsync($"/api/customers/{id}");
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task SetCustomerWebAccountActiveAsync(int customerId, bool isActive)
    {
        var res = await _http.PutAsJsonAsync($"/api/customers/{customerId}/web-account/active", isActive);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task<List<LoyaltyHistoryDto>> GetLoyaltyHistoryAsync(int customerId)
    {
        var res = await _http.GetFromJsonAsync<List<LoyaltyHistoryDto>>($"/api/customers/{customerId}/loyalty-history", JsonOpts);
        return res ?? new List<LoyaltyHistoryDto>();
    }

    // points có thể âm (trừ điểm) hoặc dương (cộng điểm thủ công, vd tặng sinh nhật).
    public async Task AdjustCustomerPointsAsync(int customerId, int points)
    {
        var res = await _http.PostAsJsonAsync($"/api/customers/{customerId}/adjust-points", new { points });
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    // =====================================================================
    // THỐNG KÊ / BÁO CÁO (BỔ SUNG)
    // =====================================================================

    public async Task<RevenueSummaryDto?> GetReportSummaryAsync(DateTime from, DateTime to)
    {
        return await _http.GetFromJsonAsync<RevenueSummaryDto>($"/api/reports/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", JsonOpts);
    }

    public async Task<List<RevenuePointDto>> GetRevenueByPeriodAsync(DateTime from, DateTime to)
    {
        var res = await _http.GetFromJsonAsync<List<RevenuePointDto>>($"/api/reports/revenue-by-period?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", JsonOpts);
        return res ?? new List<RevenuePointDto>();
    }


    public async Task<List<RevenuePointDto>> GetRevenueByHourAsync(DateTime date)
    {
        var res = await _http.GetFromJsonAsync<List<RevenuePointDto>>($"/api/reports/revenue-by-hour?date={date:yyyy-MM-dd}", JsonOpts);
        return res ?? new List<RevenuePointDto>();
    }

    public async Task<List<RecentInvoiceReportDto>> GetRecentInvoicesReportAsync(DateTime from, DateTime to, int take = 10)
    {
        var res = await _http.GetFromJsonAsync<List<RecentInvoiceReportDto>>($"/api/reports/recent-invoices?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&take={take}", JsonOpts);
        return res ?? new List<RecentInvoiceReportDto>();
    }

    public async Task<List<TopItemDto>> GetTopItemsAsync(DateTime from, DateTime to, int take = 10)
    {
        var res = await _http.GetFromJsonAsync<List<TopItemDto>>($"/api/reports/top-items?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&take={take}", JsonOpts);
        return res ?? new List<TopItemDto>();
    }

    public async Task<List<EmployeeRevenueDto>> GetRevenueByEmployeeAsync(DateTime from, DateTime to)
    {
        var res = await _http.GetFromJsonAsync<List<EmployeeRevenueDto>>($"/api/reports/by-employee?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", JsonOpts);
        return res ?? new List<EmployeeRevenueDto>();
    }

    public async Task<List<CampaignReportDto>> GetCampaignReportAsync(DateTime from, DateTime to)
    {
        var res = await _http.GetFromJsonAsync<List<CampaignReportDto>>($"/api/reports/campaigns?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", JsonOpts);
        return res ?? new List<CampaignReportDto>();
    }
    // =====================================================================
    // THÔNG BÁO NỘI BỘ (BỔ SUNG)
    // =====================================================================

    public async Task<List<NotificationDto>> GetNotificationsAsync(int userId, string role, bool unreadOnly = false)
    {
        var url = $"/api/notifications?userId={userId}&role={Uri.EscapeDataString(role)}" + (unreadOnly ? "&status=unread" : "");
        var res = await _http.GetFromJsonAsync<List<NotificationDto>>(url, JsonOpts);
        return res ?? new List<NotificationDto>();
    }

    public async Task MarkNotificationReadAsync(int id)
    {
        var res = await _http.PutAsync($"/api/notifications/{id}/read", null);
        await EnsureSuccessOrThrowMessageAsync(res);
    }

    public async Task MarkAllNotificationsReadAsync(int userId, string role)
    {
        var res = await _http.PutAsync($"/api/notifications/read-all?userId={userId}&role={Uri.EscapeDataString(role)}", null);
        await EnsureSuccessOrThrowMessageAsync(res);
    }
    // =====================================================================
    // TÌM KIẾM TOÀN CỤC (BỔ SUNG)
    // =====================================================================

    public async Task<List<SearchResultDto>> SearchAsync(string keyword, string role)
    {
        var res = await _http.GetFromJsonAsync<List<SearchResultDto>>($"/api/search?keyword={Uri.EscapeDataString(keyword)}&role={Uri.EscapeDataString(role)}", JsonOpts);
        return res ?? new List<SearchResultDto>();
    }
}

public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? Description { get; set; }

    public override string ToString() => RoleName switch
    {
        "QuanLy" => "Quản lý",
        "ThuNgan" => "Thu ngân",
        "PhucVu" => "Phục vụ",
        "Bep" => "Bếp",
        _ => RoleName,
    };
}