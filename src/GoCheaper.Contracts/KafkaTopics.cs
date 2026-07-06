namespace GoCheaper.Contracts;

public static class KafkaTopics
{
    public const string UserRegistered          = "user-registered";
    public const string ForgotPasswordRequested = "forgot-password-requested";
    public const string AuthCodeRequested       = "auth-code-requested";
    public const string UserProfileUpdated      = "user-profile-updated";
    public const string TripCreated             = "trip-created";
    public const string TripUpdated             = "trip-updated";
    public const string TripDeleted             = "trip-deleted";
    public const string TripBooked              = "trip-booked";
    public const string BookingCancelled              = "booking-cancelled";
    public const string TripCancelledForPassenger     = "trip-cancelled-for-passenger";
    public const string UserDeleted                   = "user-deleted";
}
