using System.Collections.Generic;

namespace YourNamespace.Models
{
    public class AnalysisConfiguration
    {
        public List<string> TargetColumns { get; set; } = new();
        public bool AutoDetectColumns { get; set; } = true;
    }
}