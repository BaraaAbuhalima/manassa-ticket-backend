using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Services.Contact;
using Microsoft.AspNetCore.Mvc;

namespace jett_exchange_backend.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController(IContactUsService contactUsService) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> ContactUs([FromBody] ContactUsRequest request, CancellationToken cancellationToken)
    {
        var response = await contactUsService.SubmitAsync(request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
