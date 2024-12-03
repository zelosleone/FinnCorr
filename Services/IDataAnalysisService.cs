using YourNamespace.Models;
using System.Threading.Tasks;
using System.Collections.Generic; // Added this line

namespace YourNamespace.Services
{
    public interface IDataAnalysisService
    {
        Task<AnalysisResult> ProcessFilesAsync(FileUploadDto uploadDto, List<FieldDefinition> file1Fields, List<FieldDefinition> file2Fields, AnalysisConfiguration config);
    }
}