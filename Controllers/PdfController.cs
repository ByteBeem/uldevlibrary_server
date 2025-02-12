using Microsoft.AspNetCore.Mvc;
using PdfiumViewer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

[Route("api/courses")]
[ApiController]
public class CoursesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly string _examplePdfUrl = "https://mypdfstorage1.blob.core.windows.net/pdf-files/sample.pdf";

    public CoursesController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourseMaterials([FromQuery] string name)
    {
        if (string.IsNullOrEmpty(name))
            return BadRequest("Course name is required.");

        try
        {
            // 📌 Call the private method to convert the first page of the example PDF
            var imageResult = await ConvertPdfToImage(_examplePdfUrl);

            return Ok(new
            {
                course = name,
                pdfImageBase64 = imageResult.base64,
                pdfImageUrl = imageResult.imageUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Private method to convert the first page of a PDF into an image.
    /// </summary>
    private async Task<(string base64, string imageUrl)> ConvertPdfToImage(string pdfUrl)
    {
        byte[] pdfBytes;
        using (HttpClient client = new HttpClient())
        {
            pdfBytes = await client.GetByteArrayAsync(pdfUrl);
        }

        using var pdfStream = new MemoryStream(pdfBytes);
        using var pdfDocument = PdfDocument.Load(pdfStream);

        using Bitmap bitmap = new Bitmap(pdfDocument.Render(0, 500, 700, true)); 

        using var memoryStream = new MemoryStream();
        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        using SixLabors.ImageSharp.Image<Rgba32> img = SixLabors.ImageSharp.Image.Load<Rgba32>(memoryStream);
        img.Mutate(x => x.Resize(300, 400)); // Resize

        // Save image to server
        string uploadsFolder = Path.Combine(_env.WebRootPath, "pdf-images");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        string fileName = $"{Guid.NewGuid()}.png";
        string filePath = Path.Combine(uploadsFolder, fileName);

        img.Save(filePath, new PngEncoder());

        // Convert to Base64
        byte[] imgBytes = System.IO.File.ReadAllBytes(filePath);
        string base64String = Convert.ToBase64String(imgBytes);

        string imageUrl = $"{Request.Scheme}://{Request.Host}/pdf-images/{fileName}";

        return (base64String, imageUrl);
    }
}
