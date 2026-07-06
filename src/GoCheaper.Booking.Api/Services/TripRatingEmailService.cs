using System.Text.Json;
using Confluent.Kafka;
using GoCheaper.Booking.Api.Data;
using GoCheaper.Contracts;
using GoCheaper.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace GoCheaper.Booking.Api.Services;

public class TripRatingEmailService(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<TripRatingEmailService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
                { logger.LogError(ex, "Error in TripRatingEmailService"); }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        // Completed = DepartureTime + 2 days has passed (matches TripStatus.Compute logic)
        var cutoff = DateTime.Now.AddDays(-2);

        var bookings = await db.Bookings
            .Include(b => b.Trip)
            .Where(b => b.Trip.DepartureTime != null
                     && b.Trip.DepartureTime.Value <= cutoff
                     && b.RatingEmailSentAt == null
                     && b.PassengerEmail != "")
            .ToListAsync(ct);

        foreach (var booking in bookings)
        {
            try
            {
                var token = Guid.NewGuid();
                booking.RatingToken       = token;
                booking.RatingEmailSentAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                var @event = new TripRatingRequestedEvent(
                    BookingId:         booking.Id,
                    PassengerEmail:    booking.PassengerEmail,
                    PassengerFullName: booking.PassengerFullName,
                    DriverFullName:    booking.Trip.DriverFullName,
                    DriverId:          booking.Trip.DriverId,
                    From:              booking.Trip.From,
                    To:                booking.Trip.To,
                    DepartureTime:     booking.Trip.DepartureTime,
                    RatingToken:       token);

                await producer.ProduceAsync(KafkaTopics.TripRatingRequested,
                    new Message<string, string>
                    {
                        Key   = booking.Id.ToString(),
                        Value = JsonSerializer.Serialize(@event)
                    }, ct);

                logger.LogInformation("Published rating request for booking {BookingId}", booking.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish rating request for booking {BookingId}", booking.Id);
            }
        }
    }
}
