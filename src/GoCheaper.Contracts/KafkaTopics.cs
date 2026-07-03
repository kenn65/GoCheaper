namespace GoCheaper.Contracts;

public static class KafkaTopics
{
    public const string UserRegistered          = "user-registered";
    public const string ForgotPasswordRequested = "forgot-password-requested";
    public const string AuthCodeRequested       = "auth-code-requested";
    public const string UserProfileUpdated      = "user-profile-updated";
}
