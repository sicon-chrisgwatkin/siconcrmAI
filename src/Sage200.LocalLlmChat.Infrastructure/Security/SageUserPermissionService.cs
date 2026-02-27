using Sage200.LocalLlmChat.Core.Abstractions;

namespace Sage200.LocalLlmChat.Infrastructure.Security;

public interface ISagePermissionGateway
{
    Task<bool> HasPermissionAsync(string action, CancellationToken cancellationToken);
}

public sealed class SageUserPermissionService : IUserPermissionService
{
    private readonly ISagePermissionGateway _permissionGateway;

    public SageUserPermissionService(ISagePermissionGateway permissionGateway)
    {
        _permissionGateway = permissionGateway;
    }

    public Task<bool> HasPermissionAsync(string action, CancellationToken cancellationToken) =>
        _permissionGateway.HasPermissionAsync(action, cancellationToken);
}
