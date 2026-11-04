using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnPath.Api.Modules.Admin;

/// <summary>
/// Sign-in codes on a test server, where codes aren't sent by SMS (<c>Otp:Provider = log</c>). Lets support staff help
/// testers sign in with a mobile number. Returns 404 wherever a real SMS provider is configured.
/// </summary>
[ApiController]
[Route("api/v1/admin/test-otp-codes")]
[Authorize(Policy = Policies.SupportOps)]
public class TestOtpController(IServiceProvider services, TimeProvider clock) : ControllerBase
{
    /// <summary>Codes sent in the last hour, newest first (at most 20). Phone numbers show only their last four digits.</summary>
    [HttpGet]
    public ActionResult<List<TestOtpCodeDto>> Recent()
    {
        var outbox = services.GetService<TestOtpOutbox>() ?? throw AppException.NotFound("Test sign-in codes");
        return outbox.Recent(clock.GetUtcNow());
    }
}
