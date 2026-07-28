using System;
namespace Zaide.Features.Agents.Infrastructure.Acp;

/// <summary>
/// Stable ACP v1 method names from schema-v1.20.0 meta.json.
/// </summary>
internal static class AcpMethodNames
{
    public const string Initialize = "initialize";

    public const string Authenticate = "authenticate";

    public const string SessionNew = "session/new";

    public const string SessionLoad = "session/load";

    public const string SessionSetMode = "session/set_mode";

    public const string SessionSetConfigOption = "session/set_config_option";

    public const string SessionPrompt = "session/prompt";

    public const string SessionCancel = "session/cancel";

    public const string SessionList = "session/list";

    public const string SessionDelete = "session/delete";

    public const string SessionResume = "session/resume";

    public const string SessionClose = "session/close";

    public const string Logout = "logout";

    public const string SessionRequestPermission = "session/request_permission";

    public const string SessionUpdate = "session/update";

    public const string FsWriteTextFile = "fs/write_text_file";

    public const string FsReadTextFile = "fs/read_text_file";

    public const string TerminalCreate = "terminal/create";

    public const string TerminalOutput = "terminal/output";

    public const string TerminalRelease = "terminal/release";

    public const string TerminalWaitForExit = "terminal/wait_for_exit";

    public const string TerminalKill = "terminal/kill";

    public const string CancelRequest = "$/cancel_request";
}
