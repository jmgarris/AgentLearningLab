using AgentLearningLab.Application.Abstractions;
using AgentLearningLab.Application.Models;
using AgentLearningLab.Application.Tools;
using AgentLearningLab.Domain.Enums;
using AgentLearningLab.Tools.Models;
using System.Text.Json;

namespace AgentLearningLab.Tools.Tools;

public sealed class SearchClubKnowledgeTool(IKnowledgeSearchService knowledgeSearchService) : ITool
{
    public ToolDefinition Definition => new(
        "search_club_knowledge",
        "Search the seeded fictional club documents and return the most relevant chunks with citation identifiers.",
        """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "Search query describing the rule or procedure to find.",
              "minLength": 3,
              "maxLength": 200
            },
            "maximumResults": {
              "type": "integer",
              "description": "Maximum number of results to return.",
              "minimum": 1,
              "maximum": 5
            }
          },
          "required": ["query", "maximumResults"],
          "additionalProperties": false
        }
        """,
        ToolAccessMode.ReadOnly,
        false,
        ClubRole.Member);

    public string BuildActionSummary(JsonDocument arguments) => $"Search club knowledge for '{arguments.RootElement.GetProperty("query").GetString()}'.";

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        JsonDocument arguments,
        CancellationToken cancellationToken)
    {
        var input = arguments.RootElement.Deserialize<SearchClubKnowledgeInput>(ToolJson.SerializerOptions)!;
        var results = await knowledgeSearchService.SearchAsync(input.Query, input.MaximumResults, cancellationToken);

        var citations = results
            .Select(x => new AgentCitation(
                x.DocumentTitle,
                x.Section,
                x.CitationId,
                $"[{x.DocumentTitle} {x.Section} — {x.CitationId}]"))
            .ToList();

        return new ToolExecutionResult(
            true,
            ToolJson.Serialize(results),
            $"Retrieved {results.Count} knowledge result(s).",
            citations);
    }

    public ToolValidationResult Validate(string argumentsJson)
    {
        try
        {
            var input = JsonSerializer.Deserialize<SearchClubKnowledgeInput>(argumentsJson, ToolJson.SerializerOptions);
            var errors = new List<string>();
            if (input is null)
            {
                return new ToolValidationResult(false, null, ["Arguments are required."]);
            }

            if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Trim().Length is < 3 or > 200)
            {
                errors.Add("query must be between 3 and 200 characters.");
            }

            if (input.MaximumResults is < 1 or > 5)
            {
                errors.Add("maximumResults must be between 1 and 5.");
            }

            return errors.Count > 0
                ? new ToolValidationResult(false, null, errors)
                : new ToolValidationResult(true, ToolJson.Serialize(new
                {
                    query = input.Query.Trim(),
                    maximumResults = input.MaximumResults
                }), []);
        }
        catch (JsonException)
        {
            return new ToolValidationResult(false, null, ["Arguments must be valid JSON."]);
        }
    }
}
