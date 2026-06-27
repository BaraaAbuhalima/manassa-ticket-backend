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
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream);
        }
        return Ok(new ApiResponse<string>()
        {
            Success = true,
            Message = "Ticket processed successfully",
            Data = "This is a placeholder response. Implement the actual processing logic."
        });

        // // 1. Save PDF temporarily
        // var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        //
        // await using (var stream = new FileStream(tempFile, FileMode.Create))
        // {
        //     await request.File.CopyToAsync(stream);
        // }
        //
        // try
        // {
        //     // 2. Call Python
        //     var result = await RunPythonScript(tempFile, request);
        //
        //     return Ok(result);
        // }
        // finally
        // {
        //     // 3. Cleanup
        //     if (System.IO.File.Exists(tempFile))
        //         System.IO.File.Delete(tempFile);
        // }
    }
}
