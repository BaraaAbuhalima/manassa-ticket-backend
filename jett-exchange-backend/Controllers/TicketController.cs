using jett_exchange_backend.Common;
using jett_exchange_backend.DTOs.Requests;
using jett_exchange_backend.Models;
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
        if (ticket == null)
        {
            return NotFound(new ApiResponse<string>()
            {
                Success = false,
                Message = "Ticket not found",
                Errors = new List<string> { "Ticket not found" }


            });
        }

        return Ok(new ApiResponse<Ticket>()
        {
            Success = true,
            Message = "Ticket retrieved successfully",
            Data = ticket,

        });
    }

    [HttpDelete("{ticketReference}")]
    public async Task<IActionResult> DeleteById(string ticketReference)
    {

        var deleted = await _ticketService.DeleteByRefAsync(ticketReference);
        if (!deleted)
        {
            return NotFound(new ApiResponse<string>()
            {
                Success = false,
                Message = "Ticket not found",
                Errors = new List<string> { "Ticket not found" }
            });
        }

        return Ok(new ApiResponse<string>()
        {
            Success = true,
            Message = "Ticket deleted successfully",
        });
    }
    [HttpPost("")]
    public async Task<IActionResult> ProcessTicket([FromForm] PostTicketRequest request)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileId = Guid.NewGuid();
        var fileName = $"{fileId}.pdf";
        var fullPath = Path.Combine(uploadsFolder, fileName);

        // 1. Save file locally
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }

        // 2. Call Python API
        using var client = new HttpClient();

        var response = await client.PostAsync(
            $"http://python-service:8000/extract-ticket-pdf-info?file_path={fullPath}",
            null
        );

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new ApiResponse<string>
            {
                Success = false,
                Message = "Python service failed",
                Data = null
            });
        }

        // 3. Read Python response
        var pythonResult = await response.Content.ReadAsStringAsync();

        // 4. Return Python result to frontend
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Ticket processed successfully",
            Data = pythonResult
        });
    }
}
