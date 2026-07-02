namespace GoCheaper.Identity.Api.Features.UpdateUser;

public class UpdateUserRequest
{
    public string? MobilePhone        { get; set; }
    public string? DriverPictureBase64 { get; set; }
    public bool?   IsDriver           { get; set; }
    public bool?   IsPassenger        { get; set; }
}
