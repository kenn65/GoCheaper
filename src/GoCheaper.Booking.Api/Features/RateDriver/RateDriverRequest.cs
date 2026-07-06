namespace GoCheaper.Booking.Api.Features.RateDriver;

public record RateDriverRequest(Guid Token, int Stars, string? Comment);
