using manassa_ticket_backend.DTOs.Requests;
using manassa_ticket_backend.Services.Contact;
using Microsoft.AspNetCore.Mvc;

namespace manassa_ticket_backend.Controllers;

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
