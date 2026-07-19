#pragma warning disable OPENAI001

using AgentLearningLab.Agent;
using AgentLearningLab.Application.AI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;
using System.ClientModel.Primitives;
using System.Reflection;

namespace AgentLearningLab.AgentTests;

[TestFixture]
public sealed class ResponseResultMapperTests
{
    private readonly ResponseResultMapper mapper = new(NullLogger<ResponseResultMapper>.Instance);

    [Test]
    public void DirectText_ShouldMapFinalText()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_text",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    { "type": "output_text", "text": "Hello from OpenAI." }
                  ]
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.FinalText.Should().Be("Hello from OpenAI.");
        result.ToolCalls.Should().BeEmpty();
    }

    [Test]
    public void MultipleTextParts_ShouldPreserveOrder()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_multi_text",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    { "type": "output_text", "text": "First line.\n\n" },
                    { "type": "output_text", "text": "Second line." }
                  ]
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.FinalText.Should().Be("First line.\n\nSecond line.");
    }

    [Test]
    public void FunctionCall_ShouldMapToolCall()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_tool",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "function_call",
                  "call_id": "call_123",
                  "name": "get_aircraft_status",
                  "arguments": "{\"tailNumber\":\"N123AB\"}",
                  "status": "completed"
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.FinalText.Should().BeNull();
        result.ToolCalls.Should().ContainSingle();
        result.ToolCalls[0].CallId.Should().Be("call_123");
        result.ToolCalls[0].ToolName.Should().Be("get_aircraft_status");
        result.ToolCalls[0].ArgumentsJson.Should().Be("{\"tailNumber\":\"N123AB\"}");
    }

    [Test]
    public void MultipleFunctionCalls_ShouldRetainOrder()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_multi_tool",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "function_call",
                  "call_id": "call_1",
                  "name": "first_tool",
                  "arguments": "{\"value\":1}",
                  "status": "completed"
                },
                {
                  "type": "function_call",
                  "call_id": "call_2",
                  "name": "second_tool",
                  "arguments": "{\"value\":2}",
                  "status": "completed"
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.ToolCalls.Select(call => call.ToolName).Should().Equal("first_tool", "second_tool");
    }

    [Test]
    public void ReasoningPlusText_ShouldIgnoreReasoningAndReturnAssistantText()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_reasoning_text",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "reasoning",
                  "id": "rs_1",
                  "summary": [
                    { "type": "summary_text", "text": "redacted" }
                  ]
                },
                {
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    { "type": "output_text", "text": "Visible answer." }
                  ]
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.FinalText.Should().Be("Visible answer.");
        result.ToolCalls.Should().BeEmpty();
    }

    [Test]
    public void ReasoningPlusToolCall_ShouldIgnoreReasoningAndReturnToolCall()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_reasoning_tool",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "reasoning",
                  "id": "rs_2",
                  "summary": []
                },
                {
                  "type": "function_call",
                  "call_id": "call_reasoning",
                  "name": "get_club_contact",
                  "arguments": "{\"role\":\"MaintenanceOfficer\"}",
                  "status": "completed"
                }
              ]
            }
            """);

        var result = mapper.Map(response);

        result.FinalText.Should().BeNull();
        result.ToolCalls.Should().ContainSingle(call => call.ToolName == "get_club_contact");
    }

    [Test]
    public void UnsupportedOutputItem_ShouldThrowMappingException()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_unsupported",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "reference",
                  "id": "ref_123"
                }
              ]
            }
            """);

        var action = () => mapper.Map(response);

        action.Should().Throw<OpenAIResponseMappingException>()
            .WithMessage("OpenAI returned a completed response*");
    }

    [Test]
    public void IncompleteStatus_ShouldThrowIncompleteException()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_incomplete",
              "model": "gpt-test",
              "status": "incomplete",
              "incomplete_details": {
                "reason": "max_output_tokens"
              },
              "output": []
            }
            """);

        var action = () => mapper.Map(response);

        action.Should().Throw<OpenAIResponseIncompleteException>()
            .WithMessage("*max_output_tokens*");
    }

    [Test]
    public void FailedStatus_ShouldThrowFailedException()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_failed",
              "model": "gpt-test",
              "status": "failed",
              "error": {
                "code": "server_error",
                "message": "Upstream failure.",
                "param": null
              },
              "output": []
            }
            """);

        var action = () => mapper.Map(response);

        action.Should().Throw<OpenAIResponseFailedException>()
            .WithMessage("*Upstream failure.*");
    }

    [Test]
    public void Usage_ShouldMapTokenCounts()
    {
        var response = ReadResponseResult(
            """
            {
              "id": "resp_usage",
              "model": "gpt-test",
              "status": "completed",
              "output": [
                {
                  "type": "message",
                  "role": "assistant",
                  "status": "completed",
                  "content": [
                    { "type": "output_text", "text": "Usage test." }
                  ]
                }
              ],
              "usage": {
                "input_tokens": 12,
                "output_tokens": 8,
                "total_tokens": 20
              }
            }
            """);

        var result = mapper.Map(response);

        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(12);
        result.Usage.CompletionTokens.Should().Be(8);
        result.Usage.TotalTokens.Should().Be(20);
    }

    [Test]
    public void CreateResponseAsync_ShouldReturnClientResultWrapper()
    {
        var method = typeof(ResponsesClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == "CreateResponseAsync"
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(CreateResponseOptions));

        method.ReturnType.FullName.Should().Contain("System.Threading.Tasks.Task");
        method.ReturnType.GetGenericArguments().Single().FullName.Should().Contain("System.ClientModel.ClientResult`1");
    }

    private static ResponseResult ReadResponseResult(string json)
        => ModelReaderWriter.Read<ResponseResult>(BinaryData.FromString(json))!;
}
