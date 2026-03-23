using Microsoft.AspNetCore.Mvc;
using JoVisionBackend.Models;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class FileController : ControllerBase
{
    private readonly string _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

    public FileController()
    {
        if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create( IFormFile file, [FromForm] string owner)
    {
        if (file == null || string.IsNullOrEmpty(owner))
            return BadRequest("Missing file or owner information.");

        if (Path.GetExtension(file.FileName).ToLower() != ".jpg")
            return BadRequest("Only JPG files are allowed.");

        string filePath = Path.Combine(_storagePath, file.FileName);
        string jsonPath = filePath + ".json";

        if (System.IO.File.Exists(filePath))
            return BadRequest("File already exists.");

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var metadata = new FileMetadata
            {
                Owner = owner,
                CreationTime = DateTime.UtcNow,
                LastModifiedTime = DateTime.UtcNow
            };

            string jsonString = JsonSerializer.Serialize(metadata);
            await System.IO.File.WriteAllTextAsync(jsonPath, jsonString);

            return StatusCode(201, "Created");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error during upload.");
        }
    }

    [HttpGet("Delete")]
    public IActionResult Delete([FromQuery] string fileName, [FromQuery] string fileOwner)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileOwner))
            return BadRequest("FileName and FileOwner are required.");

        string filePath = Path.Combine(_storagePath, fileName);
        filePath+= ".jpg"; 
        string jsonPath = filePath + ".json";

        if (!System.IO.File.Exists(filePath) || !System.IO.File.Exists(jsonPath))
            return BadRequest("File not found.");

        try
        {
            string jsonContent = System.IO.File.ReadAllText(jsonPath);
            var metadata = JsonSerializer.Deserialize<FileMetadata>(jsonContent);

            if (metadata?.Owner != fileOwner)
                return StatusCode(403, "Forbidden: You are not the owner of this file.");

            System.IO.File.Delete(filePath);
            System.IO.File.Delete(jsonPath);

            return Ok("Deleted");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error during deletion.");
        }
    }
}