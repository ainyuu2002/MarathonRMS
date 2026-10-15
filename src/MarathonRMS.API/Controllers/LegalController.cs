using MarathonRMS.DTOs.Legal;
using MarathonRMS.Services.Legal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.API.Controllers;

[ApiController]
[Route("api/legal")]
public class LegalController : ControllerBase
{
    private readonly ILegalService _legal;
    public LegalController(ILegalService legal) => _legal = legal;

    /// <summary>UC-84: văn bản hiện hành – docType = terms | privacy.</summary>
    [HttpGet("{docType}")]
    [AllowAnonymous]
    public async Task<ActionResult<LegalDocumentDto>> GetCurrent(string docType, CancellationToken ct) =>
        Ok(await _legal.GetCurrentAsync(docType, ct));
}
