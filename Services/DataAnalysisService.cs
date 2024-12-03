using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using YourNamespace.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text.Json;
using System.Text.Json.Serialization;  // Add this line
using System.Text;
using SoftCircuits.CsvParser;

namespace YourNamespace.Services
{
    public class DataAnalysisService : IDataAnalysisService
    {
        private readonly MLContext _mlContext;
        private readonly Dictionary<IDataView, List<DynamicRow>> _originalRows;

        public DataAnalysisService()
        {
            _mlContext = new MLContext();
            _originalRows = new Dictionary<IDataView, List<DynamicRow>>();
        }

        public async Task<AnalysisResult> ProcessFilesAsync(FileUploadDto uploadDto, List<FieldDefinition> file1Fields, List<FieldDefinition> file2Fields, AnalysisConfiguration config)
        {
            var data1Task = LoadFileAsync(uploadDto.File1, file1Fields);
            var data2Task = LoadFileAsync(uploadDto.File2, file2Fields);

            var data1 = await data1Task;
            var data2 = await data2Task;

            var correlations = CalculatePercentageIncreaseCorrelations(data1, data2, config);
            if (correlations == null || correlations.Count == 0)
            {
                return new AnalysisResult
                {
                    Insights = "No price columns detected. Please ensure your data contains relevant pricing fields.",
                    GraphUrl = string.Empty,
                    TotalCorrelations = 0,
                    StrongPositive = 0,
                    ModeratePositive = 0,
                    ModerateNegative = 0,
                    StrongNegative = 0,
                    NoCorrelation = 0,
                    PositivePercentage = 0,
                    NegativePercentage = 0
                };
            }

            var graphUrl = await GenerateGraphAsync(correlations);
            var insights = GenerateInsights(correlations);

            int positiveCount = correlations.Count(kvp => kvp.Value > 0);
            int negativeCount = correlations.Count(kvp => kvp.Value < 0);
            float positivePercentage = correlations.Count > 0 ? ((float)positiveCount / correlations.Count) * 100 : 0;
            float negativePercentage = correlations.Count > 0 ? ((float)negativeCount / correlations.Count) * 100 : 0;

            return new AnalysisResult
            {
                Insights = insights,
                GraphUrl = graphUrl,
                TotalCorrelations = correlations.Count,
                StrongPositive = correlations.Count(kvp => kvp.Value > 70),
                ModeratePositive = correlations.Count(kvp => kvp.Value > 30 && kvp.Value <= 70),
                ModerateNegative = correlations.Count(kvp => kvp.Value < -30 && kvp.Value >= -70),
                StrongNegative = correlations.Count(kvp => kvp.Value < -70),
                NoCorrelation = correlations.Count(kvp => Math.Abs(kvp.Value) <= 30),
                PositivePercentage = positivePercentage,
                NegativePercentage = negativePercentage
            };
        }

        private async Task<IDataView> LoadFileAsync(IFormFile file, List<FieldDefinition> fieldDefinitions)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.");

            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            var tempPath = Path.GetTempFileName();

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                if (fileExtension == ".json")
                {
                    var jsonString = await File.ReadAllTextAsync(tempPath);
                    var jsonData = JsonSerializer.Deserialize<List<TimeSeriesData>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    Console.WriteLine($"DEBUG: Loaded JSON with {jsonData.Count} records");
                    
                    var rows = new List<DynamicRow>();
                    foreach (var item in jsonData)
                    {
                        var row = new DynamicRow();
                        row.Add("Start", item.Start);
                        row.Add("End", item.End);
                        row.Add("Open", item.Open);
                        row.Add("High", item.High);
                        row.Add("Low", item.Low);
                        row.Add("Close", item.Close);
                        row.Add("Volume", item.Volume);
                        row.Add("Market Cap", item.MarketCap);
                        rows.Add(row);
                    }

                    if (rows.Any())
                    {
                        Console.WriteLine("DEBUG: First row after processing:");
                        var firstRow = rows[0];
                        foreach (var key in firstRow.GetAllKeys())
                        {
                            var value = firstRow.GetValue(key);
                            Console.WriteLine($"  {key}: {value} (Type: {value?.GetType().Name ?? "null"})");
                        }
                    }

                    var dataView = _mlContext.Data.LoadFromEnumerable(rows);
                    _originalRows[dataView] = rows;
                    return dataView;
                }
                else if (fileExtension == ".csv")
                {
                    var rows = new List<DynamicRow>();
                    
                    using CsvReader reader = new(tempPath);
                    string[] headers = null;

                    if (reader.Read())
                    {
                        headers = reader.Columns.Select(h => h.Trim()).ToArray();
                        
                        while (reader.Read())
                        {
                            var dynamicRow = new DynamicRow();
                            for (int i = 0; i < headers.Length && i < reader.Columns.Length; i++)
                            {
                                var header = headers[i];
                                var value = reader.Columns[i]?.Trim();
                                var dataType = DetermineDataType(header);
                                dynamicRow.Add(header, ConvertField(value, dataType));
                            }
                            rows.Add(dynamicRow);
                        }
                    }

                    return _mlContext.Data.LoadFromEnumerable(rows);
                }
                else
                {
                    throw new NotSupportedException("Unsupported file type. Please upload a CSV or JSON file.");
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private string DetermineDataType(string header)
        {
            if (string.IsNullOrEmpty(header))
                return "string";

            var lower = header.ToLower().Trim();  // Fixed: trim -> Trim
            
            // First, check for exact matches
            if (lower == "date" || lower == "time" || lower == "timestamp")
                return "date";
            if (lower == "close" || lower == "price" || lower == "high" || 
                lower == "low" || lower == "open")
                return "float";
            
            // Then check for partial matches
            if (lower.Contains("date") || lower.Contains("time"))
                return "date";
            if (lower.Contains("volume") || lower.Contains("price") || 
                lower.Contains("high") || lower.Contains("low") || 
                lower.Contains("open") || lower.Contains("close") || 
                lower.Contains("cap"))
                return "float";

            return "string";
        }

        private bool HasHeaders(string firstLine)
        {
            if (string.IsNullOrEmpty(firstLine)) return false;

            var values = firstLine.Split(',');
            return values.Any() && values.All(v => !float.TryParse(v, out _));
        }

        private string InferDataType(string[] values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                    continue;

                if (int.TryParse(value, out _))
                    return "int";
                if (float.TryParse(value, out _))
                    return "float";
                if (DateTime.TryParse(value, out _))
                    return "date";
                return "string";
            }
            return "string";
        }

        private object GetDefaultValue(string dataType)
        {
            return dataType.ToLower() switch
            {
                "int" => 0,
                "float" => 0f,
                "date" => DateTime.MinValue,
                _ => string.Empty,
            };
        }

        private Dictionary<string, float> CalculatePercentageIncreaseCorrelations(IDataView data1, IDataView data2, AnalysisConfiguration config)
        {
            var correlations = new Dictionary<string, float>();

            // Use cached original rows
            if (!_originalRows.TryGetValue(data1, out var rows1) || !_originalRows.TryGetValue(data2, out var rows2))
            {
                return correlations;
            }

            if (rows1.Count < 2 || rows2.Count < 2)
                return correlations;

            // Debug output
            Console.WriteLine("DEBUG: Using cached rows");
            if (rows1.Any())
            {
                Console.WriteLine("First dataset sample:");
                var firstRow = rows1[0];
                foreach (var key in firstRow.GetAllKeys())
                {
                    var value = firstRow.GetValue(key);
                    Console.WriteLine($"  {key}: {value} (Type: {value?.GetType().Name ?? "null"})");
                }
            }

            var priceColumn1 = DetectPriceColumn(rows1, config);
            var priceColumn2 = DetectPriceColumn(rows2, config);

            if (string.IsNullOrEmpty(priceColumn1) || string.IsNullOrEmpty(priceColumn2))
                return correlations;

            var prices1 = ExtractPrices(rows1, priceColumn1);
            var prices2 = ExtractPrices(rows2, priceColumn2);

            // Calculate percentage changes
            var changes1 = CalculatePriceChanges(prices1);
            var changes2 = CalculatePriceChanges(prices2);

            int minLength = Math.Min(changes1.Count, changes2.Count);
            
            if (minLength > 0)
            {
                var correlation = CalculatePearsonCorrelation(
                    changes1.Take(minLength).ToArray(),
                    changes2.Take(minLength).ToArray()
                );

                var key1 = DetectSymbol(rows1) ?? Path.GetFileNameWithoutExtension(rows1.First().GetValue<string>("fileName") ?? "Pair1");
                var key2 = DetectSymbol(rows2) ?? Path.GetFileNameWithoutExtension(rows2.First().GetValue<string>("fileName") ?? "Pair2");
                correlations.Add($"{key1}-{key2}", correlation * 100);
            }

            return correlations;
        }

        private string DetectPriceColumn(List<DynamicRow> rows, AnalysisConfiguration config)
        {
            if (rows == null || !rows.Any())
            {
                Console.WriteLine("DEBUG: No rows provided to DetectPriceColumn");
                return null;
            }

            var sample = rows.First();
            var availableColumns = sample.GetAllKeys().ToList();
            
            Console.WriteLine($"DEBUG: DetectPriceColumn called");
            Console.WriteLine($"DEBUG: Row count: {rows.Count}");
            Console.WriteLine($"DEBUG: Available columns: {string.Join(", ", availableColumns)}");

            var closeColumn = availableColumns.FirstOrDefault(col => 
                col.Equals("Close", StringComparison.OrdinalIgnoreCase));
            
            if (closeColumn != null)
            {
                var values = ExtractNumericValues(rows, closeColumn);
                if (values.Any())
                {
                    Console.WriteLine($"Found Close column with values: {string.Join(", ", values.Take(5))}");
                    return closeColumn;
                }
            }

            // Backup check for other price columns
            var priceColumns = new[] { "High", "Low", "Open", "Price", "Last" };
            
            foreach (var priceCol in priceColumns)
            {
                var match = availableColumns.FirstOrDefault(col => 
                    col.Equals(priceCol, StringComparison.OrdinalIgnoreCase));
                
                if (match != null)
                {
                    var values = ExtractNumericValues(rows, match);
                    if (values.Any())
                    {
                        Console.WriteLine($"Found {match} column with values: {string.Join(", ", values.Take(5))}");  // Debug line
                        return match;
                    }
                }
            }

            return null;
        }

        private double EvaluateDataQuality(List<float> values, int totalRows)
        {
            double score = 0;

            // Check data coverage
            var coverage = (double)values.Count / totalRows;
            score += coverage * 10;

            // Check for positive values
            if (values.All(v => v > 0))
                score += 5;

            // Check for reasonable price range
            if (values.All(v => v > 0.000001 && v < 1000000))
                score += 5;

            // Check for variance
            var variance = values.Average(v => Math.Pow(v - values.Average(), 2));
            if (variance > 0.00001 && variance < 1000000)
                score += 5;

            // Penalize extreme outliers
            var mean = values.Average();
            var stdDev = Math.Sqrt(variance);
            if (values.Any(v => Math.Abs(v - mean) > stdDev * 5))
                score -= 5;

            return score;
        }

        private List<float> ExtractPrices(List<DynamicRow> rows, string priceColumn)
        {
            return rows.Select(row =>
            {
                var value = row.GetValue(priceColumn)?.ToString()
                    ?.Replace(",", ".")
                    ?.Trim('$', ' ', '"');

                if (string.IsNullOrEmpty(value)) return 0f;

                // Try parsing with invariant culture
                if (float.TryParse(value, 
                    NumberStyles.Any, 
                    CultureInfo.InvariantCulture, 
                    out float price))
                {
                    return price;
                }

                // Try cleaning the value
                var cleanValue = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
                return float.TryParse(cleanValue, 
                    NumberStyles.Any, 
                    CultureInfo.InvariantCulture, 
                    out float cleanPrice) ? cleanPrice : 0f;
            }).ToList();
        }

        private List<float> ExtractNumericValues(List<DynamicRow> rows, string column)
        {
            var values = new List<float>();
            
            foreach (var row in rows)
            {
                var rawValue = row.GetValue(column);
                if (rawValue == null) continue;

                // Handle both string and numeric types
                if (rawValue is double doubleValue)
                {
                    values.Add((float)doubleValue);
                }
                else if (rawValue is float floatValue)
                {
                    values.Add(floatValue);
                }
                else if (float.TryParse(rawValue.ToString(), 
                    NumberStyles.Any, 
                    CultureInfo.InvariantCulture, 
                    out float parsed))
                {
                    values.Add(parsed);
                }
            }
            
            return values.Where(v => v != 0).ToList();
        }

        private double EvaluateValues(List<float> values)
        {
            if (!values.Any()) return 0;

            double score = 5;

            // Check if values are in a reasonable range for prices
            if (values.All(v => v > 0 && v < 1000000))
                score += 5;

            // Check for reasonable variance
            var variance = values.Average(v => Math.Pow(v - values.Average(), 2));
            if (variance > 0.01 && variance < 1000000)
                score += 3;

            // Prefer columns with more non-zero values
            score += Math.Min(5, values.Count / 10.0);

            return score;
        }

        private string DetectSymbol(List<DynamicRow> rows)
        {
            var sample = rows.First();
            foreach (var key in sample.GetAllKeys())
            {
                var columnLower = key.ToLower();
                if (columnLower.IndexOf("symbol", StringComparison.OrdinalIgnoreCase) >= 0 || 
                    columnLower.IndexOf("pair", StringComparison.OrdinalIgnoreCase) >= 0)  // Fixed Contains
                {
                    var symbol = rows.First().GetValue<string>(key);
                    if (!string.IsNullOrEmpty(symbol))
                        return symbol;
                }
            }
            return null;
        }

        private List<float> CalculatePriceChanges(List<float> prices)
        {
            var changes = new List<float>();
            for (int i = 1; i < prices.Count; i++)
            {
                if (prices[i-1] != 0)
                {
                    float change = ((prices[i] - prices[i-1]) / prices[i-1]) * 100;
                    changes.Add(change);
                }
                else
                {
                    changes.Add(0);
                }
            }
            return changes;
        }

        private float CalculatePearsonCorrelation(float[] xs, float[] ys)
        {
            if (xs.Length != ys.Length || xs.Length == 0)
                return 0;

            float meanX = xs.Average();
            float meanY = ys.Average();

            float sumXY = 0;
            float sumXX = 0;
            float sumYY = 0;

            for (int i = 0; i < xs.Length; i++)
            {
                float deltaX = xs[i] - meanX;
                float deltaY = ys[i] - meanY;
                sumXY += deltaX * deltaY;
                sumXX += deltaX * deltaX;
                sumYY += deltaY * deltaY;
            }

            if (sumXX == 0 || sumYY == 0)
                return 0;

            return (float)(sumXY / Math.Sqrt(sumXX * sumYY));
        }

        private async Task<string> GenerateGraphAsync(Dictionary<string, float> correlations)
        {
            var fields = correlations.Keys.ToList();
            var correlationValues = correlations.Values.ToList();

            // Prepare data for dual-axis bar chart
            var positiveCorrelations = correlationValues.Select(v => v > 0 ? v : 0).ToList();
            var negativeCorrelations = correlationValues.Select(v => v < 0 ? Math.Abs(v) : 0).ToList();

            var data = new List<object>
            {
                new
                {
                    x = fields,
                    y = positiveCorrelations,
                    name = "Positive Correlation",
                    type = "bar",
                    marker = new { color = "green" }
                },
                new
                {
                    x = fields,
                    y = negativeCorrelations,
                    name = "Negative Correlation",
                    type = "bar",
                    marker = new { color = "red" }
                }
            };

            var layout = new
            {
                title = "Correlation Percentage",
                barmode = "group",
                xaxis = new { title = "Fields" },
                yaxis = new { title = "Correlation Percentage (%)" }
            };

            var graphObject = new
            {
                data = data,
                layout = layout
            };

            var graphJson = JsonSerializer.Serialize(graphObject);

            var html = $@"
                <html>
                <head>
                    <script src='https://cdn.plot.ly/plotly-latest.min.js'></script>
                </head>
                <body>
                    <div id='chart'></div>
                    <script>
                        var graphData = {graphJson};
                        Plotly.newPlot('chart', graphData.data, graphData.layout);
                    </script>
                </body>
                </html>";

            var graphsPath = Path.Combine("wwwroot", "graphs");
            Directory.CreateDirectory(graphsPath);

            var filePath = Path.Combine(graphsPath, "correlations.html");
            await File.WriteAllTextAsync(filePath, html);

            return "/graphs/correlations.html";
        }

        private string GenerateInsights(Dictionary<string, float> correlations)
        {
            var insights = new StringBuilder();
            insights.AppendLine("**Correlation Insights:**\n");

            if (correlations.Count == 0)
            {
                insights.AppendLine("No common numeric fields found for correlation analysis.");
                return insights.ToString();
            }

            int positiveCount = correlations.Count(kvp => kvp.Value > 0);
            int negativeCount = correlations.Count(kvp => kvp.Value < 0);
            float positivePercentage = ((float)positiveCount / correlations.Count) * 100;
            float negativePercentage = ((float)negativeCount / correlations.Count) * 100;

            insights.AppendLine($"- **Positive Correlations**: {positiveCount} fields ({positivePercentage:F2}%)");
            insights.AppendLine($"- **Negative Correlations**: {negativeCount} fields ({negativePercentage:F2}%)\n");

            foreach (var kvp in correlations)
            {
                string interpretation = kvp.Value switch
                {
                    var v when v > 70 => "Strong positive correlation.",
                    var v when v > 30 => "Moderate positive correlation.",
                    var v when v > -30 => "Little to no correlation.",
                    var v when v > -70 => "Moderate negative correlation.",
                    _ => "Strong negative correlation."
                };
                insights.AppendLine($"- **{kvp.Key}**: {kvp.Value:F2}% ({interpretation})");
            }

            insights.AppendLine("\n**Summary:**");
            insights.AppendLine($"- Total Correlations: {correlations.Count}");
            insights.AppendLine($"- Strong Positive Correlations: {correlations.Count(kvp => kvp.Value > 70)}");
            insights.AppendLine($"- Moderate Positive Correlations: {correlations.Count(kvp => kvp.Value > 30 && kvp.Value <= 70)}");
            insights.AppendLine($"- Moderate Negative Correlations: {correlations.Count(kvp => kvp.Value < -30 && kvp.Value >= -70)}");
            insights.AppendLine($"- Strong Negative Correlations: {correlations.Count(kvp => kvp.Value < -70)}");
            insights.AppendLine($"- Little to No Correlations: {correlations.Count(kvp => Math.Abs(kvp.Value) <= 30)}");

            return insights.ToString();
        }

        public class DynamicRow
        {
            private readonly Dictionary<string, object> _row;
            private bool _keysInitialized;

            public DynamicRow()
            {
                _row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _keysInitialized = false;
            }

            public void Add(string key, object value)
            {
                if (string.IsNullOrEmpty(key)) return;
                
                if (value != null)
                {
                    if (value is double doubleVal)
                    {
                        _row[key] = (float)doubleVal;
                    }
                    else if (value is float floatVal)
                    {
                        _row[key] = floatVal;
                    }
                    else if (value is decimal decimalVal)
                    {
                        _row[key] = (float)decimalVal;
                    }
                    else if (float.TryParse(value.ToString(), 
                        NumberStyles.Any, 
                        CultureInfo.InvariantCulture, 
                        out float parsedValue))
                    {
                        _row[key] = parsedValue;
                    }
                    else
                    {
                        _row[key] = value;
                    }
                }
                _keysInitialized = true;
            }

            public IEnumerable<string> GetAllKeys()
            {
                if (!_keysInitialized)
                {
                    Console.WriteLine("DEBUG: Warning - GetAllKeys called before keys were initialized");
                    return Enumerable.Empty<string>();
                }
                return _row.Keys;
            }

            public T GetValue<T>(string key)
            {
                if (!_row.ContainsKey(key)) return default;
                
                var value = _row[key];
                if (value == null) return default;

                if (typeof(T) == typeof(float) && value is double doubleVal)
                {
                    return (T)(object)(float)doubleVal;
                }

                return value is T typedValue ? typedValue : default;
            }

            public object GetValue(string key)
            {
                return _row.ContainsKey(key) ? _row[key] : null;
            }
        }

        private object ConvertField(object value, string dataType)
        {
            if (value == null) return GetDefaultValue(dataType);

            string strValue = value.ToString().Replace(",", ".");
            
            return dataType.ToLower() switch
            {
                "int" or "float" => float.TryParse(strValue, 
                    NumberStyles.Any, 
                    CultureInfo.InvariantCulture, 
                    out float floatValue) ? floatValue : 0f,
                "date" => DateTime.TryParse(strValue, 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, 
                    out DateTime dateValue) ? dateValue : DateTime.MinValue,
                _ => strValue
            };
        }

        private List<string> GetNumericFields(DynamicRow row)
        {
            var numericFields = new List<string>();
            foreach (var key in row.GetAllKeys())
            {
                var value = row.GetValue(key);
                if (value == null) continue;

                // Check if the value is numeric
                if (value is float || value is double || value is int ||
                    value is decimal || value is long || value is short)
                {
                    numericFields.Add(key);
                    continue;
                }

                // Try parsing as float if it's a string
                if (value is string strValue && 
                    float.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    numericFields.Add(key);
                }
            }
            return numericFields;
        }
    }
}

public class TimeSeriesData
{
    public string Start { get; set; }
    public string End { get; set; }
    public float Open { get; set; }
    public float High { get; set; }
    public float Low { get; set; }
    public float Close { get; set; }
    public float Volume { get; set; }
    [JsonPropertyName("Market Cap")]
    public float MarketCap { get; set; }
}