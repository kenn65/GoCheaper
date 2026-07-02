namespace GoCheaper.Identity.Api.Features.VerifyAuthCode;

public record VerifyAuthCodeRequest(string Email, string Code);
