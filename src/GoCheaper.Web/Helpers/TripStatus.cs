namespace GoCheaper.Web.Helpers;

public static class TripStatus
{
    public static string Compute(DateTime? departureTime)
    {
        if (departureTime is null) return "Pending";
        var now = DateTime.Now;
        if (departureTime.Value > now)            return "Pending";
        if (departureTime.Value.AddDays(2) > now) return "Active";
        return "Completed";
    }

    public static string BadgeClass(DateTime? departureTime) => Compute(departureTime) switch
    {
        "Active"    => "bg-success",
        "Completed" => "bg-secondary",
        _           => "bg-primary",
    };
}
