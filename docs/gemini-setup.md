# Gemini AI Setup Guide

## Overview

CivicOps integrates with Google's Gemini AI to provide intelligent incident classification, routing, and summarization. This guide covers setup, configuration, and best practices.

---

## Features

### AI-Powered Classification
- **Natural Language Understanding:** Process messy, unstructured incident reports
- **Category Detection:** Automatically identify incident categories
- **Department Routing:** Intelligent assignment to appropriate departments
- **Priority Assessment:** Determine urgency based on content
- **Summary Generation:** Create clear, concise incident summaries

### Hybrid Mode
- **Primary:** Gemini AI for intelligent processing
- **Fallback:** Deterministic keyword-based classification
- **Seamless:** Automatic fallback on errors or unavailability
- **Reliable:** System always works, with or without AI

---

## Prerequisites

- Google Cloud account (or Google AI Studio access)
- Gemini API key
- CivicOps application deployed

---

## Setup Steps

### Step 1: Get Gemini API Key

#### Option A: Google AI Studio (Recommended for Development)

1. **Visit Google AI Studio:**
   - Go to https://makersuite.google.com/app/apikey
   - Sign in with Google account

2. **Create API Key:**
   - Click "Create API Key"
   - Select or create a Google Cloud project
   - Copy the generated API key

3. **Note Limitations:**
   - Free tier available
   - Rate limits apply
   - For production, use Google Cloud

#### Option B: Google Cloud Console (Recommended for Production)

1. **Enable Gemini API:**
   - Go to https://console.cloud.google.com/
   - Select or create project
   - Enable "Generative Language API"

2. **Create API Key:**
   - Go to APIs & Services → Credentials
   - Create credentials → API key
   - Restrict key (recommended):
     - API restrictions: Generative Language API
     - Application restrictions: IP addresses or HTTP referrers

3. **Set Up Billing:**
   - Required for production use
   - Pay-as-you-go pricing
   - Set budget alerts

---

### Step 2: Configure Environment Variables

Create `.env` file or set environment variables:

```bash
# Gemini Configuration
GEMINI_API_KEY=your_api_key_here
GEMINI_MODEL=gemini-2.0-flash-exp
GEMINI_ENABLED=true
```

**Security Notes:**
- Never commit API keys to Git
- Use secure configuration management
- Rotate keys periodically
- Monitor usage and costs

---

### Step 3: Choose Model

CivicOps supports multiple Gemini models:

#### gemini-2.0-flash-exp (Recommended)
- **Best for:** Fast classification, low cost
- **Speed:** Very fast (~1-2 seconds)
- **Cost:** Low
- **Quality:** Excellent for classification tasks

#### gemini-1.5-pro
- **Best for:** Complex analysis, high accuracy
- **Speed:** Moderate (~2-4 seconds)
- **Cost:** Higher
- **Quality:** Best overall

#### gemini-1.5-flash
- **Best for:** Balance of speed and quality
- **Speed:** Fast (~1-2 seconds)
- **Cost:** Moderate
- **Quality:** Very good

**Configuration:**
```bash
GEMINI_MODEL=gemini-2.0-flash-exp
```

---

## Implementation Details

### Service Architecture

```csharp
public class GeminiService : IGeminiService
{
    private readonly IConfiguration _configuration;
    private readonly IClassificationService _fallbackService;
    private readonly HttpClient _httpClient;
    
    public bool IsEnabled { get; private set; }
    public string Status { get; private set; }
    
    public async Task<ClassificationResult> ClassifyWithGeminiAsync(
        string description, 
        string? category = null)
    {
        if (!IsEnabled)
        {
            return await _fallbackService.ClassifyIncidentAsync(description, category);
        }
        
        try
        {
            // Call Gemini API
            var result = await CallGeminiApiAsync(description, category);
            result.IsGeminiProcessed = true;
            result.Method = "Gemini AI";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API error, falling back");
            return await _fallbackService.ClassifyIncidentAsync(description, category);
        }
    }
}
```

### Prompt Engineering

CivicOps uses a carefully crafted prompt:

```
You are a civic operations AI assistant helping classify municipal incident reports.

Analyze this incident report and provide classification in JSON format:

Report: {description}
Suggested Category: {category}

Provide your response as a JSON object with these fields:
- category: A brief category name (e.g., "Water Infrastructure", "Electricity")
- department: One of: WaterAndSanitation, Electricity, RoadsAndStormwater, ...
- priority: One of: Low, Medium, High, Urgent
- summary: A clear one-sentence summary of the incident

Respond ONLY with valid JSON, no other text.
```

### Response Parsing

```csharp
private ClassificationResult ParseGeminiResponse(string responseJson)
{
    // Extract JSON from response
    var jsonStart = text.IndexOf('{');
    var jsonEnd = text.LastIndexOf('}');
    var jsonText = text.Substring(jsonStart, jsonEnd - jsonStart + 1);
    
    // Parse classification
    var classification = JsonDocument.Parse(jsonText).RootElement;
    
    return new ClassificationResult
    {
        Category = classification.GetProperty("category").GetString(),
        Department = ParseDepartment(classification.GetProperty("department").GetString()),
        Priority = ParsePriority(classification.GetProperty("priority").GetString()),
        Summary = classification.GetProperty("summary").GetString(),
        Method = "Gemini AI",
        IsGeminiProcessed = true
    };
}
```

---

## Testing

### Test with Demo Data

1. **Enable Gemini:**
   ```bash
   export GEMINI_API_KEY="your_key"
   export GEMINI_ENABLED=true
   ```

2. **Submit test report:**
   - Navigate to `/Home/Report`
   - Enter: "Burst water pipe on Main Road causing flooding"
   - Submit

3. **Verify AI processing:**
   - Check incident detail page
   - Look for "Classification Method: Gemini AI"
   - Verify accurate department assignment

### Test Fallback

1. **Disable Gemini:**
   ```bash
   export GEMINI_ENABLED=false
   ```

2. **Submit report:**
   - Same test as above

3. **Verify fallback:**
   - Check "Classification Method: Deterministic"
   - Verify system still works

---

## Monitoring

### Key Metrics

- **Success Rate:** % of successful Gemini calls
- **Fallback Rate:** % of requests using fallback
- **Response Time:** Average API response time
- **Cost:** API usage costs
- **Accuracy:** Classification accuracy (manual review)

### Logging

```csharp
_logger.LogInformation(
    "Gemini classification: Category={Category}, Department={Department}, Method={Method}",
    result.Category,
    result.Department,
    result.Method
);

_logger.LogWarning(
    "Gemini API error, using fallback: {Error}",
    ex.Message
);
```

### Dashboard Integration

Check connector status at `/Home/Connectors`:
- **Status:** Configured / Demo / Disabled
- **Mode:** Hybrid with deterministic fallback
- **Health:** Real-time status

---

## Cost Management

### Pricing (as of 2026)

**Gemini 2.0 Flash:**
- Input: $0.075 per 1M tokens
- Output: $0.30 per 1M tokens

**Gemini 1.5 Pro:**
- Input: $1.25 per 1M tokens
- Output: $5.00 per 1M tokens

### Cost Estimation

**Small Municipality (1,000 incidents/month):**
- Average tokens per request: ~500 input, ~100 output
- Monthly cost: ~$0.50-2.00

**Medium Municipality (10,000 incidents/month):**
- Monthly cost: ~$5-20

**Large Municipality (100,000 incidents/month):**
- Monthly cost: ~$50-200

### Optimization Tips

1. **Use Flash Model:** Faster and cheaper
2. **Optimize Prompts:** Reduce token usage
3. **Cache Results:** Store classifications
4. **Batch Processing:** Process multiple incidents
5. **Set Quotas:** Prevent unexpected costs

---

## Best Practices

### Prompt Design

✅ **Do:**
- Be specific about output format
- Provide clear examples
- Request JSON responses
- Include all required fields

❌ **Don't:**
- Use overly long prompts
- Request unnecessary information
- Use ambiguous language
- Forget error handling

### Error Handling

```csharp
try
{
    var result = await _geminiService.ClassifyWithGeminiAsync(description);
}
catch (HttpRequestException ex)
{
    // Network error - use fallback
    _logger.LogWarning("Network error, using fallback");
}
catch (JsonException ex)
{
    // Parse error - use fallback
    _logger.LogWarning("Parse error, using fallback");
}
catch (Exception ex)
{
    // Unknown error - use fallback
    _logger.LogError(ex, "Unexpected error, using fallback");
}
```

### Rate Limiting

```csharp
private readonly SemaphoreSlim _rateLimiter = new(10, 10); // 10 concurrent requests

public async Task<ClassificationResult> ClassifyWithGeminiAsync(string description)
{
    await _rateLimiter.WaitAsync();
    try
    {
        return await CallGeminiApiAsync(description);
    }
    finally
    {
        _rateLimiter.Release();
    }
}
```

---

## Troubleshooting

### Common Issues

**Issue:** API key invalid
- **Solution:** Verify key is correct
- **Solution:** Check key hasn't expired
- **Solution:** Ensure API is enabled in Google Cloud

**Issue:** Rate limit exceeded
- **Solution:** Implement rate limiting
- **Solution:** Upgrade quota
- **Solution:** Use fallback more aggressively

**Issue:** Slow responses
- **Solution:** Use Flash model instead of Pro
- **Solution:** Reduce prompt length
- **Solution:** Implement timeout

**Issue:** Inaccurate classifications
- **Solution:** Improve prompt engineering
- **Solution:** Provide more context
- **Solution:** Use Pro model for better accuracy

---

## Advanced Configuration

### Custom Temperature

```csharp
var requestBody = new
{
    contents = new[] { /* ... */ },
    generationConfig = new
    {
        temperature = 0.3,  // Lower = more deterministic
        maxOutputTokens = 500,
        topP = 0.8,
        topK = 40
    }
};
```

### Safety Settings

```csharp
var requestBody = new
{
    contents = new[] { /* ... */ },
    safetySettings = new[]
    {
        new
        {
            category = "HARM_CATEGORY_HARASSMENT",
            threshold = "BLOCK_MEDIUM_AND_ABOVE"
        }
    }
};
```

---

## Production Checklist

- [ ] API key configured securely
- [ ] Appropriate model selected
- [ ] Rate limiting implemented
- [ ] Error handling tested
- [ ] Fallback mechanism verified
- [ ] Monitoring and logging enabled
- [ ] Cost alerts configured
- [ ] Quota limits set
- [ ] Performance benchmarked
- [ ] Accuracy validated

---

## Resources

### Documentation
- [Gemini API Documentation](https://ai.google.dev/docs)
- [Google AI Studio](https://makersuite.google.com/)
- [Pricing Information](https://ai.google.dev/pricing)

### Tools
- [API Explorer](https://ai.google.dev/tutorials/rest_quickstart)
- [Prompt Gallery](https://ai.google.dev/examples)

---

## Support

For issues:
- Check Google AI documentation
- Review CivicOps logs
- Test with fallback disabled
- Verify API key and permissions

---

**Document Version:** 1.0  
**Last Updated:** May 15, 2026  
**Gemini API Version:** v1beta
