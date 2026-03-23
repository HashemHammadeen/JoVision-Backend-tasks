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
    public async Task<IActionResult> Create(IFormFile file, [FromForm] string owner)
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
    [HttpPost("Update")]
    public async Task<IActionResult> Update(IFormFile file, [FromForm] string owner)
    {
        if (file == null || string.IsNullOrEmpty(owner))
            return BadRequest("Missing file or owner information.");

        string filePath = Path.Combine(_storagePath, file.FileName);
        string jsonPath = filePath + ".json";

        if (!System.IO.File.Exists(filePath) || !System.IO.File.Exists(jsonPath))
            return BadRequest("File does not exist. Use Create instead.");

        try
        {
            string jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
            var metadata = JsonSerializer.Deserialize<FileMetadata>(jsonContent);

            if (metadata?.Owner != owner)
                return StatusCode(403, "Forbidden: You do not own this file.");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            metadata.LastModifiedTime = DateTime.UtcNow;

            string jsonString = JsonSerializer.Serialize(metadata);
            await System.IO.File.WriteAllTextAsync(jsonPath, jsonString);

            return Ok("Updated");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error during update.");
        }
    }

    [HttpGet("Retrieve")]
    public async Task<IActionResult> Retrieve([FromQuery] string fileName, [FromQuery] string fileOwner)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(fileOwner))
            return BadRequest("FileName and FileOwner are required.");

        string filePath = Path.Combine(_storagePath, fileName);
        string jsonPath = filePath + ".json";

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found.");

        try
        {
            string jsonContent = await System.IO.File.ReadAllTextAsync(jsonPath);
            var metadata = JsonSerializer.Deserialize<FileMetadata>(jsonContent);

            if (metadata?.Owner != fileOwner)
                return StatusCode(403, "Forbidden: Access denied.");

            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "image/jpeg");
        }
        catch (Exception)
        {
            return StatusCode(500, "Internal server error during retrieval.");
        }
    }
    [HttpPost("Filter")]
    public async Task<IActionResult> Filter([FromForm] FilterRequest request)
    {
        try
        {
            var jsonFiles = Directory.GetFiles(_storagePath, "*.json");
            var allMetadata = new List<(string FileName, FileMetadata Meta)>();

            foreach (var file in jsonFiles)
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                var meta = JsonSerializer.Deserialize<FileMetadata>(content);
                if (meta != null)
                {
                var fileName = Path.GetFileNameWithoutExtension(file);
                allMetadata.Add((fileName, meta));
                }
            }

            IEnumerable<(string FileName, FileMetadata Meta)> filtered = request.FilterType switch
            {
                FilterType.ByModificationDate => allMetadata
                    .Where(x => x.Meta.LastModifiedTime < request.ModificationDate),
                
                FilterType.ByCreationDateDescending => allMetadata
                    .Where(x => x.Meta.CreationTime > request.CreationDate)
                    .OrderByDescending(x => x.Meta.CreationTime),
                
                FilterType.ByCreationDateAscending => allMetadata
                    .Where(x => x.Meta.CreationTime > request.CreationDate)
                    .OrderBy(x => x.Meta.CreationTime),
                
                FilterType.ByOwner => allMetadata
                    .Where(x => x.Meta.Owner == request.Owner),
                
                _ => throw new ArgumentException("Invalid FilterType")
            };

            var result = filtered.Select(x => new FileSummary 
            { 
                FileName = x.FileName, 
                OwnerName = x.Meta.Owner 
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("TransferOwnership")]
    public async Task<IActionResult> TransferOwnership([FromQuery] string oldOwner, [FromQuery] string newOwner)
    {
        if (string.IsNullOrEmpty(oldOwner) || string.IsNullOrEmpty(newOwner))
            return BadRequest("Both OldOwner and NewOwner are required.");

        try
        {
            var jsonFiles = Directory.GetFiles(_storagePath, "*.json");
            
            foreach (var file in jsonFiles)
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                var meta = JsonSerializer.Deserialize<FileMetadata>(content);

                if (meta?.Owner == oldOwner)
                {
                    meta.Owner = newOwner;
                    meta.LastModifiedTime = DateTime.UtcNow;
                    await System.IO.File.WriteAllTextAsync(file, JsonSerializer.Serialize(meta));
                }
            }

            var updatedFiles = Directory.GetFiles(_storagePath, "*.json")
                .Select(f => JsonSerializer.Deserialize<FileMetadata>(System.IO.File.ReadAllText(f)))
                .Where(m => m?.Owner == newOwner)
                .Select(m => m!.Owner) 
                .ToList();

            var finalQuery = Directory.GetFiles(_storagePath, "*.json")
                .Select(f => new { 
                    Name = Path.GetFileNameWithoutExtension(f), 
                    Meta = JsonSerializer.Deserialize<FileMetadata>(System.IO.File.ReadAllText(f)) 
                })
                .Where(x => x.Meta!.Owner == newOwner)
                .Select(x => new FileSummary { FileName = x.Name, OwnerName = x.Meta!.Owner })
                .ToList();

            return Ok(finalQuery);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }
}