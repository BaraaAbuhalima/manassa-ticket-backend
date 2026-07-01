using jett_exchange_backend.Helpers;
using FluentAssertions;

namespace TestJettTicketBackend.Helpers;

public class RandomPinGeneratorTests
{
    private RandomPinGenerator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new RandomPinGenerator();

    [TestCase(1)]
    [TestCase(16)]
    [TestCase(32)]
    public void Generate_ReturnsStringOfRequestedLength(int length)
    {
        var pin = _sut.Generate(length);

        pin.Should().HaveLength(length);
    }

    [Test]
    public void Generate_OnlyUsesAllowedCharacters()
    {
        var pin = _sut.Generate(200);

        pin.Should().MatchRegex("^[A-Za-z0-9]+$");
    }

    [Test]
    public void Generate_ProducesDifferentValues_AcrossCalls()
    {
        var pins = Enumerable.Range(0, 20).Select(_ => _sut.Generate(16)).ToList();

        pins.Distinct().Should().HaveCount(pins.Count);
    }
}