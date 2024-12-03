namespace YourNamespace.Models
{
    public class AnalysisResult
    {
        public string Insights { get; set; }
        public string GraphUrl { get; set; }
        public int TotalCorrelations { get; set; }
        public int StrongPositive { get; set; }
        public int ModeratePositive { get; set; }
        public int ModerateNegative { get; set; }
        public int StrongNegative { get; set; }
        public int NoCorrelation { get; set; }
        public float PositivePercentage { get; set; }
        public float NegativePercentage { get; set; }
    }
}