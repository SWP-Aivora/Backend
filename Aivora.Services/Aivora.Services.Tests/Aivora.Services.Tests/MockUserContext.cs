using Aivora.Services.JwtService;

namespace Aivora.Services.VerificationService.Tests;

public class MockUserContext : IUserContext
{
    private Guid? _currentUserId = null;
    private string? _role = null;

    public MockUserContext(Guid? userId = null, string? role = null)
    {
        _currentUserId = userId;
        _role = role;
    }

    public Guid GetCurrentUserId()
    {
        if (_currentUserId == null)
            throw new InvalidOperationException("User ID not set");
        return _currentUserId.Value;
    }

    public string GetUserRole()
    {
        if (string.IsNullOrEmpty(_role))
            throw new InvalidOperationException("Role not set");
        return _role!;
    }

    public Guid GetExpertId()
    {
        return GetCurrentUserId();
    }

    public Guid GetClientId()
    {
        return GetCurrentUserId();
    }

    public Guid GetAdminId()
    {
        return GetCurrentUserId();
    }
}