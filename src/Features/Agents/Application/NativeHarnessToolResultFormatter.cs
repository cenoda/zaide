using System;
using System.Text;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Bounded, sanitized tool-result summaries for model re-prompting.
/// </summary>
internal static class NativeHarnessToolResultFormatter
{
    public static string Format(AgentActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();
        builder.Append("result_kind=").Append(result.ResultKind);
        if (result.FailureKind is not null)
        {
            builder.Append("; failure_kind=").Append(result.FailureKind);
        }

        builder.Append("; summary=").Append(result.Summary);

        if (result.Content is not null)
        {
            builder.Append("; content=").Append(result.Content);
        }

        if (result.Revision != default)
        {
            builder.Append("; revision=").Append(result.Revision.Value);
        }

        if (result.ByteLength > 0)
        {
            builder.Append("; byte_length=").Append(result.ByteLength);
        }

        if (result.CommandExecution is not null)
        {
            var command = result.CommandExecution;
            builder.Append("; command_outcome=").Append(command.Outcome);
            if (command.ExitCode is not null)
            {
                builder.Append("; exit_code=").Append(command.ExitCode);
            }

            builder.Append("; stdout=").Append(command.StandardOutput.Text);
            builder.Append("; stderr=").Append(command.StandardError.Text);
        }

        return BoundAndSanitize(builder.ToString());
    }

    public static string FormatValidationError(string error) =>
        BoundAndSanitize($"result_kind=Failed; summary=Tool validation failed: {error}");

    public static string FormatCancellation() =>
        "result_kind=Cancelled; summary=Tool execution was cancelled.";

    public static string FormatBrokerUnavailable() =>
        "result_kind=Denied; failure_kind=BrokerUnavailable; summary=Action broker is unavailable.";

    private static string BoundAndSanitize(string text)
    {
        var summary = new AgentActionAuditSummary(text);
        return summary.Text;
    }
}
