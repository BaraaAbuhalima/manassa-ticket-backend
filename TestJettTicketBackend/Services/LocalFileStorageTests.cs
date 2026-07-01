using jett_exchange_backend.Services.FileStorage;
using FluentAssertions;
using TestJettTicketBackend.TestHelpers;

namespace TestJettTicketBackend.Services;

public class LocalFileStorageTests
{
    private string _tempDir = null!;
    private LocalFileStorage _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "jett-tests-" + Guid.NewGuid());
        _sut = new LocalFileStorage();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public async Task SavePdfAsync_CreatesDirectory_WhenMissing()
    {
        Directory.Exists(_tempDir).Should().BeFalse();

        await _sut.SavePdfAsync(FakeFormFile.CreatePdf(), _tempDir);

        Directory.Exists(_tempDir).Should().BeTrue();
    }

    [Test]
    public async Task SavePdfAsync_WritesFileContent_AndReturnsPath()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };

        var path = await _sut.SavePdfAsync(FakeFormFile.CreatePdf(content: content), _tempDir);

        File.Exists(path).Should().BeTrue();
        (await File.ReadAllBytesAsync(path)).Should().Equal(content);
        Path.GetExtension(path).Should().Be(".pdf");
    }

    [Test]
    public async Task SavePdfAsync_GeneratesUniqueFileNames_ForEachCall()
    {
        var path1 = await _sut.SavePdfAsync(FakeFormFile.CreatePdf(), _tempDir);
        var path2 = await _sut.SavePdfAsync(FakeFormFile.CreatePdf(), _tempDir);

        path1.Should().NotBe(path2);
    }

    [Test]
    public async Task DeleteAsync_RemovesExistingFile_AndReturnsTrue()
    {
        var path = await _sut.SavePdfAsync(FakeFormFile.CreatePdf(), _tempDir);

        var result = await _sut.DeleteAsync(path);

        result.Should().BeTrue();
        File.Exists(path).Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_ReturnsFalse_WhenFileDoesNotExist()
    {
        var result = await _sut.DeleteAsync(Path.Combine(_tempDir, "does-not-exist.pdf"));

        result.Should().BeFalse();
    }
}