using GoCheaper.Identity.Api.Models;

namespace GoCheaper.Identity.Api.Features.Common;

public record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsDriver,
    bool IsPassenger,
    string? MobilePhone,
    bool IsEmailVerified,
    string? EmailVerificationToken,
    string? DriverPictureBase64,
    DateTime CreatedAt);

public static class UserMapper
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.IsDriver,
        user.IsPassenger,
        user.MobilePhone,
        user.IsEmailVerified,
        user.EmailVerificationToken,
        user.DriverPictureBase64,
        user.CreatedAt);
}
