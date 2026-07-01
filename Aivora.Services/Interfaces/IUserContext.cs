namespace Aivora.Services.JwtService;

public interface IUserContext
{
    Guid GetCurrentUserId();
    string GetUserRole();
    Guid GetExpertId();
    Guid GetClientId();
    Guid GetAdminId();
}