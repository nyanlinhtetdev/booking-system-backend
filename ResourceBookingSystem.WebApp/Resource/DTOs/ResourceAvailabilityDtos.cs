namespace ResourceBookingSystem.WebApp.Resource.DTOs;

public sealed class ResourceAvailabilityResponseDto
{
    public Guid ResourceId { get; init; }

    public DateTime WeekStartDate { get; init; }

    public DateTime WeekEndDate { get; init; }

    public IReadOnlyList<BookedSlotDto> Bookings { get; init; } = [];
}

public sealed class BookedSlotDto
{
    public Guid BookingId { get; init; }

    public DateTime StartDatetime { get; init; }

    public DateTime EndDatetime { get; init; }

    public bool IsOwnedByCurrentUser { get; init; }
}
