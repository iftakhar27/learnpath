using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnPath.Api.Modules.Admin;

/// <summary>
/// Emails on a test server, where nothing is really sent (<c>Email:Provider = log</c>), e.g. password reset links.
/// Returns 404 wherever a real email service is configured.
/// </summary>
[ApiController]
[Route("api/v1/admin/test-emails")]
[Authorize(Policy = Policies.SupportOps)]
public class TestEmailsController(IServiceProvider services, TimeProvider clock) : ControllerBase
{
    /// <summary>Emails from the last hour, newest first (at most 20).</summary>
    [HttpGet]
    public ActionResult<List<TestEmailDto>> Recent()
    {
        var outbox = services.GetService<TestEmailOutbox>() ?? throw AppException.NotFound("Test emails");
        return outbox.Recent(clock.GetUtcNow());
    }
}
