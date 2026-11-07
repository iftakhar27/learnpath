using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Admin;
using LearnPath.Api.Modules.Analytics;
using LearnPath.Api.Modules.Catalog;
using LearnPath.Api.Modules.Commerce;
using LearnPath.Api.Modules.Exams;
using LearnPath.Api.Modules.Identity;
using LearnPath.Api.Modules.Planner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Configuration ----------
var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes. Set it via the Jwt__SigningKey environment variable.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(config.GetSection("Exams").Get<ExamOptions>() ?? new ExamOptions());
builder.Services.AddSingleton(config.GetSection("Analytics").Get<AnalyticsOptions>() ?? new AnalyticsOptions());
builder.Services.AddSingleton(TimeProvider.System);

// ---------- Data ----------
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(DatabaseUrl.ToNpgsql(config.GetConnectionString("Default"))));

// ---------- Integrations ----------
var useFakePayments = config.GetValue<bool>("Razorpay:UseFakeGateway");
// OTP: "log" writes codes to the log (development/staging only); "disabled" turns mobile sign-in off.
// No SMS provider (e.g. MSG91) is implemented yet, so Production must use "disabled" until one is added.
var otpProvider = config.GetValue("Otp:Provider", "log")!.ToLowerInvariant();
if (otpProvider is not ("log" or "disabled" or "msg91"))
    throw new InvalidOperationException($"Otp:Provider '{otpProvider}' is not supported. Use 'msg91', 'disabled', or 'log' outside Production.");
if (otpProvider == "msg91" && (string.IsNullOrWhiteSpace(config["Otp:Msg91:AuthKey"]) || string.IsNullOrWhiteSpace(config["Otp:Msg91:TemplateId"])))
    throw new InvalidOperationException("Otp:Provider is 'msg91' but Otp:Msg91:AuthKey or Otp:Msg91:TemplateId is not set.");
if (builder.Environment.IsProduction() && (useFakePayments || otpProvider == "log"))
    throw new InvalidOperationException("Fake payment gateway / logging OTP sender are not allowed in Production.");
if (!useFakePayments)
{
    var missing = new[] { "Razorpay:KeyId", "Razorpay:KeySecret", "Razorpay:WebhookSecret" }.Where(k => string.IsNullOrWhiteSpace(config[k])).ToList();
    if (missing.Count > 0) throw new InvalidOperationException($"Razorpay is enabled but {string.Join(", ", missing)} is not set (or set Razorpay:UseFakeGateway=true).");
    if (!config["Razorpay:KeyId"]!.StartsWith("rzp_", StringComparison.Ordinal))
        throw new InvalidOperationException("Razorpay:KeyId should be the Key Id from Razorpay (it starts with rzp_test_ or rzp_live_).");
}
if (useFakePayments) builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
else builder.Services.AddHttpClient<IPaymentGateway, RazorpayGateway>(c => c.Timeout = TimeSpan.FromSeconds(15));
if (otpProvider == "disabled") builder.Services.AddSingleton<IOtpSender, DisabledOtpSender>();
else if (otpProvider == "msg91")
{
    builder.Services.Configure<Msg91Options>(config.GetSection("Otp:Msg91"));
    // No request logging: the code is part of the request URL and must never reach the logs.
    builder.Services.AddHttpClient<IOtpSender, Msg91OtpSender>(c => c.Timeout = TimeSpan.FromSeconds(10)).RemoveAllLoggers();
}
else
{
    builder.Services.AddSingleton<TestOtpOutbox>(); // read by GET /api/v1/admin/test-otp-codes
    builder.Services.AddSingleton<IOtpSender, LoggingOtpSender>();
}
builder.Services.AddSingleton<IVideoUrlSigner, BunnyVideoUrlSigner>();
builder.Services.AddSingleton<IDocumentUrlSigner, HmacDocumentUrlSigner>();

// ---------- Module services ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestLanguage>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<EntitlementService>();
builder.Services.AddScoped<ExamService>();
builder.Services.AddScoped<ResultsService>();
builder.Services.AddScoped<LearnerStatsService>();
builder.Services.AddScoped<StudyPlanService>();
builder.Services.AddScoped<CommerceService>();

// ---------- Auth ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = true;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ClockSkew = TimeSpan.FromSeconds(30),
    };
    o.Events = new JwtBearerEvents
    {
        // A device whose session ended (replaced by another device, signed out, password changed) is refused at once,
        // not when its access token expires.
        OnTokenValidated = async ctx =>
        {
            var sid = ctx.Principal?.FindFirst(SessionClaims.SessionId)?.Value;
            if (!Guid.TryParse(sid, out var sessionId)) return; // tokens from before sessions existed (expire within minutes)
            var state = await ctx.HttpContext.RequestServices.GetRequiredService<SessionService>().GetStateAsync(sessionId, ctx.HttpContext.RequestAborted);
            if (state == SessionState.Active) return;
            ctx.HttpContext.Items[nameof(SessionState)] = state;
            ctx.Fail(state == SessionState.Replaced ? "session_replaced" : "session_ended");
        },
        OnChallenge = async ctx =>
        {
            if (ctx.HttpContext.Items[nameof(SessionState)] is not SessionState state) return;
            ctx.HandleResponse();
            await SessionResponses.WriteEndedAsync(ctx.HttpContext, state);
        },
    };
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SessionService>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.ContentEdit, p => p.RequireRole(Roles.Teacher, Roles.ContentAdmin, Roles.SuperAdmin))
    .AddPolicy(Policies.ContentPublish, p => p.RequireRole(Roles.ContentAdmin, Roles.SuperAdmin))
    .AddPolicy(Policies.SupportOps, p => p.RequireRole(Roles.Support, Roles.SuperAdmin))
    .AddPolicy(Policies.Finance, p => p.RequireRole(Roles.Finance, Roles.SuperAdmin))
    .AddPolicy(Policies.StaffRead, p => p.RequireRole(Roles.Staff))
    .AddPolicy(Policies.SuperAdmin, p => p.RequireRole(Roles.SuperAdmin));

var clientAddress = new ClientAddress(config);
builder.Services.AddSingleton(clientAddress);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(clientAddress.Resolve(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("RateLimit:AuthPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
});

// ---------- HTTP ----------
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers(o => o.Filters.Add<AppExceptionFilter>()).AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = $"{config["Branding:ProductName"] ?? "LearnPath"} API", Version = "v1" });
    c.CustomSchemaIds(t => t.IsGenericType ? $"{t.Name[..t.Name.IndexOf('`')]}Of{string.Join("And", t.GetGenericArguments().Select(a => a.Name))}" : t.Name);
    c.SupportNonNullableReferenceTypes();
    c.UseAllOfToExtendReferenceSchemas();
    c.SchemaFilter<RequiredNonNullableSchemaFilter>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
// A request carrying the token of an ended session is refused even on public pages (the course list), so the device
// learns at once that it was signed out instead of quietly continuing as a visitor.
app.Use(async (ctx, next) =>
{
    if (ctx.Items[nameof(SessionState)] is SessionState state) await SessionResponses.WriteEndedAsync(ctx, state);
    else await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
// Someone opening the bare API address in a browser should see that it is running, not a 404.
app.MapGet("/", (IConfiguration c, IWebHostEnvironment env) => Results.Ok(new
{
    name = $"{c["Branding:ProductName"] ?? "LearnPath"} API",
    status = "running",
    message = "This address serves the mobile app and the admin portal; it has no web pages of its own.",
    health = "/health",
    docs = env.IsProduction() ? null : "/swagger",
})).ExcludeFromDescription();
app.MapGet("/api/v1/config", (IConfiguration c) => new PublicConfigDto(c["Branding:ProductName"] ?? "LearnPath", "INR", "Asia/Kolkata", ["en", "hi"]));

if (config.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db, config, TimeProvider.System, app.Logger);
}

app.Run();

public partial class Program;

public record PublicConfigDto(string ProductName, string Currency, string TimeZone, string[] Languages);
