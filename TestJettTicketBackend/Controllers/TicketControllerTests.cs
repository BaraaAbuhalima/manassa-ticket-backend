using FluentAssertions;
using jett_exchange_backend.Controllers;
using jett_exchange_backend.Data;
using jett_exchange_backend.Models;
using jett_exchange_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace TestJettTicketBackend;

public class TicketControllerTests
{
    [Test]
    public async Task GetById_ReturnsOk_WhenTicketExists()
    {
        await using var factory = new TestApplicationFactory();
        var ticketId = Guid.NewGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        await SeedAsync(scope.ServiceProvider, ticketId);

        var controller = new TicketController(scope.ServiceProvider.GetRequiredService<ITicketService>());

        var result = await controller.GetById(ticketId);

        result.Should().BeOfType<OkObjectResult>();
        var ok = result.As<OkObjectResult>();
        ok.Value.Should().BeOfType<Ticket>();
        ok.Value.As<Ticket>().Id.Should().Be(ticketId);
    }

    [Test]
    public async Task GetById_ReturnsNotFound_WhenTicketDoesNotExist()
    {
        await using var factory = new TestApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var controller = new TicketController(scope.ServiceProvider.GetRequiredService<ITicketService>());

        var result = await controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    private static async Task SeedAsync(IServiceProvider serviceProvider, Guid ticketId)
    {
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        context.NormalJettTickets.Add(new Ticket
        {
            Id = ticketId,
            TicketId = "TCK-1001",
            OriginalOwnerName = "John Doe",
            OriginalOwnerPassportNumber = "P1234567",
            Date = new DateTime(2026, 6, 27),
            Price = 12.50m,
            NumberOfBags = 1,
            TotalPrice = 12.50m
        });

        await context.SaveChangesAsync();
    }
}
