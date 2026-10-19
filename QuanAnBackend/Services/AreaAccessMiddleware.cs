using System.Text.Json;

namespace QuanAnBackend.Services;

/// <summary>
/// Phân công khu vực là thông tin điều phối, KHÔNG phải khóa nghiệp vụ.
/// Nhân viên vẫn thao tác khu khác nếu capability cho phép; middleware chỉ xác
/// định thao tác ngoài phân công để lưu audit trail.
/// </summary>
public class AreaAccessMiddleware
{
    private readonly RequestDelegate _next;

    public AreaAccessMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AreaAccessService areaAccess,
        MobileOperationAuditService audit)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !IsWriteMethod(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var method = context.Request.Method;
        var tableIds = new List<int>();
        int? orderItemId = null;

        if (HttpMethods.IsPut(method))
        {
            tableIds.AddIfPositive(ParseTableRoute(
                path,
                "/api/tables/",
                new[] { "/receive", "/release" }
            ));

            tableIds.AddIfPositive(ParseTableRoute(
                path,
                "/api/mobile/dining/tables/",
                new[] { "/guest-count" }
            ));

            orderItemId = ParseOrderItemRoute(path);
        }

        if (HttpMethods.IsPost(method))
        {
            if (path.Equals("/api/orders/add-items", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/orders/checkin", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/payments/request", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/mobile/order-actions/clear-table", StringComparison.OrdinalIgnoreCase))
            {
                var body = await ReadJsonBodyAsync(context.Request);
                tableIds.AddIfPositive(GetInt(body, "tableId"));
            }
            else if (path.Equals("/api/tables/transfer", StringComparison.OrdinalIgnoreCase) ||
                     path.Equals("/api/tables/merge", StringComparison.OrdinalIgnoreCase) ||
                     path.Equals("/api/tables/link", StringComparison.OrdinalIgnoreCase) ||
                     path.Equals("/api/tables/split", StringComparison.OrdinalIgnoreCase))
            {
                var body = await ReadJsonBodyAsync(context.Request);
                tableIds.AddIfPositive(GetInt(body, "sourceTableId"));
                tableIds.AddIfPositive(GetInt(body, "targetTableId"));
                tableIds.AddIfPositive(GetInt(body, "primaryTableId"));

                if (body.HasValue && body.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in body.Value.EnumerateObject())
                    {
                        if (!property.Name.Equals("tableIds", StringComparison.OrdinalIgnoreCase) ||
                            property.Value.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var value in property.Value.EnumerateArray())
                        {
                            if (value.ValueKind == JsonValueKind.Number &&
                                value.TryGetInt32(out var id))
                                tableIds.AddIfPositive(id);
                        }
                    }
                }
            }
        }

        var outsideAssignedArea = false;
        if (await areaAccess.RequiresAreaRestrictionAsync(context.User))
        {
            foreach (var tableId in tableIds.Distinct())
            {
                if (!await areaAccess.CanOperateTableAsync(context.User, tableId))
                {
                    outsideAssignedArea = true;
                    break;
                }
            }

            if (!outsideAssignedArea &&
                orderItemId.HasValue &&
                !await areaAccess.CanOperateOrderItemAsync(
                    context.User,
                    orderItemId.Value))
            {
                outsideAssignedArea = true;
            }
        }

        var statusCode = StatusCodes.Status200OK;
        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            statusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            try
            {
                await audit.LogAsync(
                    context,
                    tableIds.Distinct().ToList(),
                    outsideAssignedArea,
                    statusCode
                );
            }
            catch
            {
                // Audit không được phép làm hỏng nghiệp vụ chính.
            }
        }
    }

    private static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);

    private static int? ParseTableRoute(
        string path,
        string prefix,
        IReadOnlyList<string> suffixes)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var suffix in suffixes)
        {
            if (!path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var raw = path[prefix.Length..^suffix.Length].Trim('/');
            return int.TryParse(raw, out var id) ? id : null;
        }

        return null;
    }

    private static int? ParseOrderItemRoute(string path)
    {
        const string prefix = "/api/orders/items/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string? suffix = null;
        if (path.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
            suffix = "/status";
        else if (path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            suffix = "/cancel";

        if (suffix is null) return null;

        var raw = path[prefix.Length..^suffix.Length].Trim('/');
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static async Task<JsonElement?> ReadJsonBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        try
        {
            using var document = await JsonDocument.ParseAsync(
                request.Body,
                cancellationToken: request.HttpContext.RequestAborted
            );
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static int? GetInt(JsonElement? body, string name)
    {
        if (!body.HasValue || body.Value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var property in body.Value.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind == JsonValueKind.Number &&
                property.Value.TryGetInt32(out var number))
                return number;

            if (property.Value.ValueKind == JsonValueKind.String &&
                int.TryParse(property.Value.GetString(), out number))
                return number;
        }

        return null;
    }
}

file static class AreaAccessListExtensions
{
    public static void AddIfPositive(this List<int> target, int? value)
    {
        if (value.HasValue && value.Value > 0)
            target.Add(value.Value);
    }
}
