using System;
using Zaide.Features.Agents.Presentation.Transparency;

namespace Zaide.App.Composition;

/// <summary>
/// Composition-owned registration for transparency commands. The feature view
/// model owns the command behavior and does not depend on the command registry.
/// </summary>
internal static class AgentTransparencyCommandRegistration
{
    internal static void Register(
        ICommandRegistry registry,
        AgentTransparencyManagementViewModel transparencyManagement)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(transparencyManagement);

        registry.Register(new CommandDescriptor(
            "agent.trace.open",
            "Open Agent Trace",
            "Agent",
            Array.Empty<string>(),
            transparencyManagement.OpenTraceCommand));

        registry.Register(new CommandDescriptor(
            "agent.memory.open",
            "Open Agent Memory",
            "Agent",
            Array.Empty<string>(),
            transparencyManagement.OpenMemoryCommand));

        registry.Register(new CommandDescriptor(
            "agent.usage.open",
            "Open Agent Usage",
            "Agent",
            Array.Empty<string>(),
            transparencyManagement.OpenUsageCommand));
    }
}
