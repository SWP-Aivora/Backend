using System.Text.Json;

namespace Aivora.Services.AIJobAssistantService.Prompting;

public class AIServiceDescriptionPromptBuilder
{
    public string Build(Request.GenerateServiceDescriptionRequest request)
    {
        return $$"""
            You are an AI Expert Service Profile Writer.
            Generate a high-converting service profile from this expert input:
            {{JsonSerializer.Serialize(request)}}

            Return ONLY one JSON object with this schema:
            {
              "suggestedTitle": "service title",
              "suggestedDescription": "service description",
              "packages": [
                {
                  "name": "Basic",
                  "title": "Basic package title",
                  "description": "what is included",
                  "price": 100,
                  "deliveryDays": 3,
                  "features": ["feature"]
                },
                {
                  "name": "Standard",
                  "title": "Standard package title",
                  "description": "what is included",
                  "price": 200,
                  "deliveryDays": 7,
                  "features": ["feature"]
                },
                {
                  "name": "Premium",
                  "title": "Premium package title",
                  "description": "what is included",
                  "price": 400,
                  "deliveryDays": 14,
                  "features": ["feature"]
                }
              ],
              "faqs": [
                {
                  "question": "common question",
                  "answer": "answer"
                }
              ]
            }
            """;
    }
}
