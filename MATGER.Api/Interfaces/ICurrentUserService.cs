namespace MATGER.Api.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }

    bool IsInRole(string role);
}