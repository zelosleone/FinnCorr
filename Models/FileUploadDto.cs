using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace YourNamespace.Models
{
    public class FileUploadDto
    {
        [Required(ErrorMessage = "File1 is required.")]
        public IFormFile File1 { get; set; }

        [Required(ErrorMessage = "File2 is required.")]
        public IFormFile File2 { get; set; }

        public string File1FieldsJson { get; set; }

        public string File2FieldsJson { get; set; }

        // Deserialize JSON strings into lists
        public List<FieldDefinition> File1Fields => 
            string.IsNullOrEmpty(File1FieldsJson) ? new List<FieldDefinition>() : 
            JsonSerializer.Deserialize<List<FieldDefinition>>(File1FieldsJson);

        public List<FieldDefinition> File2Fields => 
            string.IsNullOrEmpty(File2FieldsJson) ? new List<FieldDefinition>() : 
            JsonSerializer.Deserialize<List<FieldDefinition>>(File2FieldsJson);

        public bool TryGetFile1Fields(out List<FieldDefinition> fields)
        {
            if (string.IsNullOrEmpty(File1FieldsJson))
            {
                fields = new List<FieldDefinition>();
                return true;
            }

            try
            {
                fields = JsonSerializer.Deserialize<List<FieldDefinition>>(File1FieldsJson);
                return true;
            }
            catch (JsonException)
            {
                fields = null;
                return false;
            }
        }

        public bool TryGetFile2Fields(out List<FieldDefinition> fields)
        {
            if (string.IsNullOrEmpty(File2FieldsJson))
            {
                fields = new List<FieldDefinition>();
                return true;
            }

            try
            {
                fields = JsonSerializer.Deserialize<List<FieldDefinition>>(File2FieldsJson);
                return true;
            }
            catch (JsonException)
            {
                fields = null;
                return false;
            }
        }

        public string ConfigurationJson { get; set; }

        public AnalysisConfiguration Configuration =>
            string.IsNullOrEmpty(ConfigurationJson) 
                ? new AnalysisConfiguration() 
                : JsonSerializer.Deserialize<AnalysisConfiguration>(ConfigurationJson);
    }

    public class FieldDefinition
    {
        [Required(ErrorMessage = "FieldName is required.")]
        public string FieldName { get; set; }

        [Required(ErrorMessage = "DataType is required.")]
        [RegularExpression("^(int|float|date|string)$", ErrorMessage = "DataType must be one of the following: int, float, date, string.")]
        public string DataType { get; set; }
    }
}