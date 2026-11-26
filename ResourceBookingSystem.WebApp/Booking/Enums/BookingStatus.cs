using System;
using System.Collections.Generic;
using System.Text;

namespace ResourceBookingSystem.WebApp.Booking.Enums
{
    public enum BookingStatus
    {
        Confirmed,
        Cancelled
    }

    public enum BookingResultStatus
    {
        Success,
        InvalidTimeRange,
        ResourceUnavailable,
        SlotAlreadyBooked,
        BookingNotFound
    }

}
