using Microsoft.AspNetCore.Mvc;
using YourNamespace.Services;
using YourNamespace.Models;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IDataAnalysisService _dataAnalysisService;

        public UploadController(IDataAnalysisService dataAnalysisService)
        {
            _dataAnalysisService = dataAnalysisService;
        }

        [HttpPost]
        public async Task<IActionResult> UploadFiles([FromForm] FileUploadDto uploadDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                return BadRequest(new { Errors = errors });
            }

            var allowedExtensions = new[] { ".csv", ".json" };
            var file1Extension = Path.GetExtension(uploadDto.File1.FileName).ToLower();
            var file2Extension = Path.GetExtension(uploadDto.File2.FileName).ToLower();

            if (!allowedExtensions.Contains(file1Extension) || !allowedExtensions.Contains(file2Extension))
            {
                return BadRequest("Only CSV and JSON files are allowed.");
            }

            List<FieldDefinition> file1Fields = new();
            List<FieldDefinition> file2Fields = new();
            var config = new AnalysisConfiguration { AutoDetectColumns = true };

            try
            {
                var result = await _dataAnalysisService.ProcessFilesAsync(uploadDto, file1Fields, file2Fields, config);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = $"An error occurred: {ex.Message}" });
            }
        }
    }
}