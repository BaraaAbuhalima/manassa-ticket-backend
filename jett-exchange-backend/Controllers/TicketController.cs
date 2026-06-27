using jett_exchange_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace jett_exchange_backend.Controllers;

[ApiController]
[Route("ticket")]
public class TicketController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var ticket = await _ticketService.GetByIdAsync(id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpDelete("id/{id:guid}")]
    public async Task<IActionResult> DeleteById(Guid id)
    {
        var deleted = await _ticketService.DeleteByIdAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
