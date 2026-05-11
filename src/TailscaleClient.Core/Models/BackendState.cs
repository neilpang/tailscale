namespace TailscaleClient.Core.Models;

/// <summary>
/// Mirrors the string values of <c>ipn.State</c> on the Go side.
/// Sent as a plain string in the <see cref="Status.BackendState"/> field.
/// </summary>
public static class BackendState
{
    public const string NoState = "NoState";
    public const string InUseOtherUser = "InUseOtherUser";
    public const string NeedsLogin = "NeedsLogin";
    public const string NeedsMachineAuth = "NeedsMachineAuth";
    public const string Stopped = "Stopped";
    public const string Starting = "Starting";
    public const string Running = "Running";
}
