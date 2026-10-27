using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LearnPath.Api.Infrastructure;

/// <summary>Expected business failure; rendered as RFC 7807 problem details with a stable <c>code</c>.</summary>
public class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static AppException NotFound(string what) => new(404, "not_found", $"{what} not found.");
    public static AppException Forbidden(string message = "You do not have access to this resource.") => new(403, "forbidden", message);
    public static AppException Conflict(string code, string message) => new(409, code, message);
    public static AppException BadRequest(string code, string message) => new(400, code, message);
    public static AppException PaymentRequired(string message = "Purchase this course to unlock this content.") => new(402, "entitlement_required", message);
}

public class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        ProblemDetails problem;
        if (ex is AppException app)
        {
            problem = new ProblemDetails { Status = app.Status, Title = app.Message };
            problem.Extensions["code"] = app.Code;
        }
        else
        {
            logger.LogError(ex, "Unhandled exception for {Path}", ctx.Request.Path);
            problem = new ProblemDetails { Status = 500, Title = "Something went wrong. Please try again." };
            problem.Extensions["code"] = "internal_error";
        }
        problem.Extensions["traceId"] = ctx.TraceIdentifier;
        ctx.Response.StatusCode = problem.Status!.Value;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new AppException(401, "unauthenticated", "Login required."));
}

/// <summary>Resolves "en"/"hi" from <c>?lang=</c> or <c>Accept-Language</c> and picks localized text with English fallback.</summary>
public class RequestLanguage(IHttpContextAccessor accessor)
{
    public string Code
    {
        get
        {
            var ctx = accessor.HttpContext;
            var q = ctx?.Request.Query["lang"].ToString();
            if (!string.IsNullOrEmpty(q)) return q.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
            var header = ctx?.Request.Headers.AcceptLanguage.ToString() ?? "";
            return header.StartsWith("hi", StringComparison.OrdinalIgnoreCase) ? "hi" : "en";
        }
    }

    public string Pick(string en, string? hi) => Code == "hi" && !string.IsNullOrWhiteSpace(hi) ? hi : en;
    public string? PickOpt(string? en, string? hi) => Code == "hi" && !string.IsNullOrWhiteSpace(hi) ? hi : en;
}

public static class India
{
    public static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
    public static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Tz).DateTime);
    public static DateOnly DateOf(DateTimeOffset t) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(t, Tz).DateTime);
}

public static class Hashing
{
    public static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string HmacSha256Hex(string secret, string message) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message))).ToLowerInvariant();

    public static bool FixedTimeEqualsHex(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a.ToLowerInvariant()), Encoding.ASCII.GetBytes(b.ToLowerInvariant()));
}

public class AuditService(AppDbContext db, TimeProvider clock)
{
    public void Record(Guid? actor, string action, string entityType, object? entityId, object? data = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            DataJson = data is null ? null : System.Text.Json.JsonSerializer.Serialize(data),
            CreatedAt = clock.GetUtcNow(),
        });
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class Paging
{
    public static (int page, int size) Normalize(int? page, int? size) => (Math.Max(1, page ?? 1), Math.Clamp(size ?? 20, 1, 100));
}

/// <summary>Turns expected <see cref="AppException"/>s into problem responses inside MVC so they are not logged as unhandled errors.</summary>
public class AppExceptionFilter : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
{
    public void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
    {
        if (context.Exception is not AppException app) return;
        var problem = new ProblemDetails { Status = app.Status, Title = app.Message };
        problem.Extensions["code"] = app.Code;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(problem) { StatusCode = app.Status };
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Marks non-nullable properties as required in the OpenAPI schema so generated clients get exact types.
/// Record parameters with default values stay optional.
/// </summary>
public class RequiredNonNullableSchemaFilter : Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiSchema schema, Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
    {
        if (schema.Properties is not { Count: > 0 } || !context.Type.IsClass) return;
        var optional = context.Type.GetConstructors().SelectMany(c => c.GetParameters())
            .Where(p => p.HasDefaultValue).Select(p => p.Name!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, prop) in schema.Properties)
            if (!prop.Nullable && !optional.Contains(name)) schema.Required.Add(name);
    }
}
