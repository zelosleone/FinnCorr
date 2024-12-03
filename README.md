# Financial Data Correlation Analysis Engine

## Overview
A REST API and web-based analysis tool for financial time series correlation analysis, featuring automated data processing and visualization capabilities.

## Features

### Data Processing
- CSV and JSON file support
- Automated price data detection
- Multiple data format handling
- Time series data support

### Analysis Capabilities
- Percentage change correlation calculations
- Pearson correlation coefficient analysis
- Correlation classification:
  - Strong positive (> 70%)
  - Moderate positive (30-70%)
  - Little/no correlation (-30% to 30%)
  - Moderate negative (-30% to -70%)
  - Strong negative (< -70%)

### Visualization
- Interactive Plotly charts
- Dual-axis correlation visualization
- HTML-based output

## Technical Stack
- ASP.NET Core REST API
- ML.NET for data processing
- SoftCircuits.CsvParser
- System.Text.Json
- Plotly.js

## API Reference

### Upload Endpoint
```http
POST /api/upload
Content-Type: multipart/form-data
```

### Response Structure
```typescript
interface AnalysisResult {
    insights: string;
    graphUrl: string;
    totalCorrelations: number;
    strongPositive: number;
    moderatePositive: number;
    moderateNegative: number;
    strongNegative: number;
    noCorrelation: number;
    positivePercentage: number;
    negativePercentage: number;
}
```

## Implementation Details

### Core Components
- `DataAnalysisService`: Analysis logic
- `UploadController`: API handling
- `FileUploadDto`: Upload model
- `AnalysisResult`: Output model
- `AnalysisConfiguration`: Settings model

### Mathematical Foundation

#### Percentage Change
```
Δ% = ((pₙ - pₙ₋₁) / pₙ₋₁) × 100
```

#### Pearson Correlation
```
r = Σ((x - μₓ)(y - μᵧ)) / √(Σ(x - μₓ)² × Σ(y - μᵧ)²)
```

#### Quality Scoring
```
Q = (C × 10) + V + R + S
```

## Setup
1. Install .NET runtime
2. Clone repository
3. Run `dotnet restore`
4. Execute `dotnet run`
5. Access Swagger UI at root URL

## Documentation
Full API documentation available via Swagger UI
