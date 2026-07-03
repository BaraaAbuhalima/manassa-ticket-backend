using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Services.Tickets;
using Microsoft.AspNetCore.Mvc;

namespace jett_exchange_backend.Controllers;

[ApiController]
[Route("api/ticket")]
public class TicketController(ITicketReader ticketReader, ITicketDeleter ticketDeleter, ITicketPoster ticketPoster)
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

    [HttpDelete("{token}")]
    public async Task<IActionResult> DeleteByToken(string token)
    {
        var response = await ticketDeleter.DeleteByTokenAsync(token);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("")]
    public async Task<IActionResult> PostTicket([FromForm] PostTicketRequest request)
    {
        var response = await ticketPoster.PostTicketAsync(request);
        return StatusCode(response.StatusCode, response);
    }
    
    
}
