using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Services.Tickets;
using Microsoft.AspNetCore.Mvc;

namespace jett_exchange_backend.Controllers;

[ApiController]
[Route("api/ticket")]
public class TicketController(
    ITicketReader ticketReader,
    ITicketDeleter ticketDeleter,
    ITicketPoster ticketPoster,
    ITicketDeleteTokenService deleteTokenService)
    : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await ticketReader.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("by-pin/{pin}")]
    public async Task<IActionResult> GetByPin(string pin)
    {
        var response = await ticketReader.GetByPinAsync(pin);
        if (response.Data is not null)
        {
            Response.Headers["X-Delete-Token"] = deleteTokenService.GenerateToken(response.Data.Id);
        }

        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("date")]
    public async Task<IActionResult> GetForDate([FromQuery] DateOnly date, [FromQuery] int page = 1)
    {
        var response = await ticketReader.GetForDateAsync(date, page);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetForDateRange([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, [FromQuery] int page = 1)
    {
        var response = await ticketReader.GetForDateRangeAsync(startDate, endDate, page);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("")]
    public async Task<IActionResult> DeleteByToken()
    {
        var response = await ticketDeleter.DeleteByTokenAsync(ExtractBearerToken());
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("republish")]
    public async Task<IActionResult> RepublishByToken()
    {
        var response = await ticketDeleter.RepublishByTokenAsync(ExtractBearerToken());
        return StatusCode(response.StatusCode, response);
    }

    [HttpPatch("")]
    public async Task<IActionResult> ModifyTicket([FromBody] UpdateTicketRequest request)
    {
        var response = await ticketDeleter.ModifyTicketByTokenAsync(ExtractBearerToken(), request);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("file-url")]
    public async Task<IActionResult> GetFileUrl()
    {
        var response = await ticketDeleter.GetFileUrlByTokenAsync(ExtractBearerToken());
        return StatusCode(response.StatusCode, response);
    }

    private string ExtractBearerToken()
    {
        string? header = Request.Headers.Authorization;
        const string prefix = "Bearer ";

        return header is not null && header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..]
            : string.Empty;
    }

    [HttpPost("upload-url")]
    public async Task<IActionResult> CreateUploadUrl()
    {
        var response = await ticketPoster.CreateUploadUrlAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("")]
    public async Task<IActionResult> PostTicket([FromBody] PostTicketRequest request)
    {
        var response = await ticketPoster.PostTicketAsync(request);
        return StatusCode(response.StatusCode, response);
    }


}
