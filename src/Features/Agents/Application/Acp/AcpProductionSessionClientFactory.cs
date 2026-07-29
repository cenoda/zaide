using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure.Acp;

namespace Zaide.Features.Agents.Application.Acp;

/// <summary>
/// Production ACP session client factory that reads actor bindings and launches
/// the configured absolute executable profile.
/// </summary>
internal sealed class AcpProductionSessionClientFactory : IAcpSessionClientFactory
{
    private readonly IAgentActorBackendBindingStore _bindingStore;
    private readonly IAcpProcessLauncher _processLauncher;
    private readonly Func<string> _workingDirectoryProvider;

    public AcpProductionSessionClientFactory(
        IAgentActorBackendBindingStore bindingStore,
        IAcpProcessLauncher processLauncher,
        Func<string> workingDirectoryProvider)
    {
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _workingDirectoryProvider = workingDirectoryProvider
            ?? throw new ArgumentNullException(nameof(workingDirectoryProvider));
    }

    public async Task<IAcpSessionClient> CreateAsync(
        AgentBackendExecutionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_bindingStore.TryGetBinding(context.Request.TargetActorId, out var binding)
            || binding.BackendId != AgentBackendIds.Acp
            || binding.AcpRuntime is null)
        {
            throw new InvalidOperationException(
                "ACP runtime binding is required for the target actor.");
        }

        var runtime = binding.AcpRuntime;
        if (!File.Exists(runtime.ExecutablePath))
        {
            throw new AcpProcessLifecycleException(
                AcpProcessLifecycleFailureKind.ProtocolFailure,
                $"ACP executable was not found at '{runtime.ExecutablePath}'.");
        }

        var options = new AcpProcessLaunchOptions(runtime.ExecutablePath, runtime.Arguments)
        {
            WorkingDirectory = _workingDirectoryProvider(),
            AllowlistedEnvironment = AcpProcessEnvironmentPolicy.CreateAllowlistedEnvironment(),
        };

        var host = await AcpStdioProcessHost.StartAsync(options, _processLauncher, cancellationToken)
            .ConfigureAwait(false);
        return new AcpStdioProcessSessionClient(host);
    }
}
