using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent200.Host.Services;

/// <summary>
/// A delegating chat client that implements a "Human-in-the-Loop" (HITL) governance gate.
/// It intercepts assistant responses that contain tool calls and checks if any are marked as "sensitive".
/// If a sensitive tool is detected, it pauses execution and waits for manual approval via the terminal.
/// </summary>
public class HumanInTheLoopChatClient : DelegatingChatClient
{
    private readonly HashSet<string> _sensitiveTools;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HumanInTheLoopChatClient"/> class.
    /// </summary>
    /// <param name="innerClient">The inner IChatClient to wrap.</param>
    /// <param name="sensitiveTools">A collection of tool names that require human approval before execution.</param>
    /// <param name="logger">Logger for tracing HITL activity.</param>
    public HumanInTheLoopChatClient(IChatClient innerClient, IEnumerable<string> sensitiveTools, ILogger logger)
        : base(innerClient)
    {
        _sensitiveTools = new HashSet<string>(sensitiveTools, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>
    /// Overrides the base GetResponseAsync to intercept and vet tool calls.
    /// </summary>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Execute the inner client's base logic to get the assistant's proposed response.
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // 2. Scan the response messages for any function (tool) calls.
        var toolCalls = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .ToList();

        _logger.LogInformation("HITL: Detected {Count} tool calls.", toolCalls.Count);

        // If no tool calls are present, we can return the response immediately.
        if (toolCalls.Count == 0)
        {
            return response;
        }

        // 3. Check if any detected tool calls are in our "sensitive" list.
        var sensitiveCall = toolCalls.FirstOrDefault(tc => _sensitiveTools.Contains(tc.Name));

        if (sensitiveCall != null)
        {
            // A sensitive action was proposed. Pause and request human intervention.
            _logger.LogInformation("HITL: Detected SENSITIVE tool call: {ToolName}", sensitiveCall.Name);
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[HITL] HUMAN-IN-THE-LOOP APPROVAL REQUIRED");
            Console.ResetColor();
            Console.WriteLine($"Agent 200 wants to execute a sensitive operation:");
            Console.WriteLine($"- Tool: {sensitiveCall.Name}");
            
            // Print the arguments proposed for the tool call so the human has context.
            if (sensitiveCall.Arguments != null)
            {
                foreach (var arg in sensitiveCall.Arguments)
                {
                    Console.WriteLine($"  - {arg.Key}: {arg.Value}");
                }
            }

            // Wait for user input in the console.
            Console.Write("\nApprove this action? (y/n): ");
            var input = Console.ReadLine()?.Trim().ToLower();

            // 4. Handle rejection.
            if (input != "y" && input != "yes")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[HITL] Action REJECTED by user. Stopping remediation.");
                Console.ResetColor();
                
                // We return a "soft failure" message to the agent so it can update its own internal state
                // without crashing or retrying indefinitely.
                var rejectedResponse = new ChatResponse(new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.Assistant, "I attempted to perform a sensitive action but it was REJECTED by the human operator. I will stop my remediation efforts here.")
                });
                
                return rejectedResponse;
            }

            // 5. Handle approval.
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[HITL] Action APPROVED. Proceeding...");
            Console.ResetColor();
        }

        // Return the original response (which contains the tool calls) if approved or if no sensitive tools were found.
        return response;
    }
}
