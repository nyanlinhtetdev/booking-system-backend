using Microsoft.EntityFrameworkCore;
using ResourceBookingSystem.Database.AppDbContextModels;
using ResourceBookingSystem.WebApp.Booking.DTOs;
using ResourceBookingSystem.WebApp.Booking.Enums;
using ResourceBookingSystem.WebApp.Booking.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Resource.DTOs;
using ResourceBookingSystem.WebApp.Resource.Enums;
using ResourceBookingSystem.WebApp.Shared.DTOs;
using BookingEntity = ResourceBookingSystem.Database.AppDbContextModels.Booking;

namespace ResourceBookingSystem.WebApp.Booking.Services
{
    public class BookingService(AppDbContext dbContext): IBookingService
    {
        public async Task<BookingResult> CreateBookingAsync(
            Guid userId,
            BookingRequestDto request)
        {
            if (!BookingDatetimeIsAvailable(
                    request.StartDatetime,
                    request.EndDatetime))
            {
                return new(BookingResultStatus.InvalidTimeRange);
            }

            var startDatetimeUtc = ConvertToUtc(request.StartDatetime, request.Timezone);
            var endDatetimeUtc = ConvertToUtc(request.EndDatetime, request.Timezone);

            if (!await ResourceIsAvailable(request.ResourceId))
            {
                return new(BookingResultStatus.ResourceUnavailable);
            }

            if (await BookingExistsAsync(
                    request.ResourceId,
                    startDatetimeUtc,
                    endDatetimeUtc))
            {
                return new(BookingResultStatus.SlotAlreadyBooked);
            }

            var newBooking = new BookingEntity
            {
                UserId = userId,
                ResourceId = request.ResourceId,
                Status = BookingStatus.Confirmed.ToString(),
                StartDatetime = startDatetimeUtc,
                EndDatetime = endDatetimeUtc
            };

            await dbContext.Bookings.AddAsync(newBooking);
            await dbContext.SaveChangesAsync();

            return new(
                BookingResultStatus.Success,
                RespondBookingData(newBooking));
        }

        public async Task<BookingResult> UpdateBookingAsync(
            Guid bookingId,
            Guid userId,
            BookingUpdateRequestDto request)
        {
            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(existingBooking =>
                    existingBooking.BookingId == bookingId &&
                    existingBooking.UserId == userId &&
                    existingBooking.Status != BookingStatus.Cancelled.ToString() &&
                    existingBooking.StartDatetime > DateTime.UtcNow);

            if (booking is null)
            {
                return new(BookingResultStatus.BookingNotFound);
            }

            if (!BookingDatetimeIsAvailable(
                    request.StartDatetime,
                    request.EndDatetime))
            {
                return new(BookingResultStatus.InvalidTimeRange);
            }

            var startDatetimeUtc = ConvertToUtc(request.StartDatetime, request.Timezone);
            var endDatetimeUtc = ConvertToUtc(request.EndDatetime, request.Timezone);

            if (!await ResourceIsAvailable(booking.ResourceId))
            {
                return new(BookingResultStatus.ResourceUnavailable);
            }

            if (await BookingExistsAsync(
                    booking.ResourceId,
                    startDatetimeUtc,
                    endDatetimeUtc,
                    booking.BookingId))
            {
                return new(BookingResultStatus.SlotAlreadyBooked);
            }

            booking.StartDatetime = startDatetimeUtc;
            booking.EndDatetime = endDatetimeUtc;
            booking.ModifiedDatetime = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            return new(
                BookingResultStatus.Success,
                RespondBookingData(booking));
        }
        
        public async Task<bool> CancelBookingAsync(
            Guid bookingId,
            Guid userId)
        {
            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(booking =>
                    booking.BookingId == bookingId &&
                    booking.UserId == userId &&
                    booking.Status != BookingStatus.Cancelled.ToString() &&
                    booking.StartDatetime > DateTime.UtcNow); 

            if(booking is null)
            {
                return false;
            }

            booking.Status = BookingStatus.Cancelled.ToString();
            booking.ModifiedDatetime = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
            return true;
        }
        private static readonly TimeSpan MaxAdvanceBookingWindow = TimeSpan.FromDays(90);

        private static bool BookingDatetimeIsAvailable(
            DateTime startDatetime, 
            DateTime endDatetime)
        {
            var now = DateTime.UtcNow;
            if (startDatetime < now)
            {
                return false;
            }

            if (startDatetime > now.Add(MaxAdvanceBookingWindow))
            {
                return false;
            }

            if (startDatetime >= endDatetime)
            {
                return false;
            }

            if (startDatetime.Date != endDatetime.Date)
            {
                return false;
            }

            var duration = endDatetime - startDatetime;

            if (duration != TimeSpan.FromHours(1))
            {
                return false;
            }

            if (startDatetime.Hour < 9 || endDatetime.Hour > 18)
            {
                return false;
            }

            return startDatetime.Minute == 0 &&
                   startDatetime.Second == 0 &&
                   startDatetime.Millisecond == 0 &&
                   endDatetime.Minute == 0 &&
                   endDatetime.Second == 0 &&
                   endDatetime.Millisecond == 0;
        }

        private Task<bool> BookingExistsAsync(
            Guid resourceId,
            DateTime startDatetime,
            DateTime endDatetime,
            Guid? excludedBookingId = null)
        {
            return dbContext.Bookings.AnyAsync(booking =>
                booking.ResourceId == resourceId &&
                booking.Status != BookingStatus.Cancelled.ToString() &&
                (excludedBookingId == null ||
                 booking.BookingId != excludedBookingId) &&
                booking.StartDatetime < endDatetime &&
                booking.EndDatetime > startDatetime);
        }

        private Task<bool> ResourceIsAvailable(Guid resourceId)
        {
            return dbContext.Resources.AnyAsync(resource =>
                resource.ResourceId == resourceId &&
                resource.Status == ResourceStatus.Active.ToString());
        }
        private BookingResponseDto RespondBookingData(BookingEntity bookingEntity)
        {
            return new BookingResponseDto
            {
                BookingId = bookingEntity.BookingId,
                UserId = bookingEntity.UserId,
                ResourceId = bookingEntity.ResourceId,
                ResourceName = bookingEntity.Resource?.Name,
                ResourceLocation = bookingEntity.Resource?.Location,
                Status = bookingEntity.Status,
                CreatedDatetime = bookingEntity.CreatedDatetime,
                ModifiedDatetime = bookingEntity.ModifiedDatetime,
                StartDatetime = bookingEntity.StartDatetime,
                EndDatetime = bookingEntity.EndDatetime           
            };
        }

        private enum BookingPeriod
        {
            All,
            Upcoming,
            History
        }

        public Task<PaginatedResponse<BookingResponseDto>> GetMyBookingsAsync(
            Guid userId,
            int pageNumber,
            int pageSize,
            bool upcoming)
        {
            return GetBookingsAsync(
                userId,
                pageNumber,
                pageSize,
                upcoming == true? BookingPeriod.Upcoming : BookingPeriod.History);
        }

        public Task<PaginatedResponse<BookingResponseDto>> GetAllBookingsAsync(
            int pageNumber,
            int pageSize)
        {
            return GetBookingsAsync(
                null,
                pageNumber,
                pageSize,
                BookingPeriod.All,
                includeCancelled: true);
        }

        private async Task<PaginatedResponse<BookingResponseDto>> GetBookingsAsync(
            Guid? userId,
            int pageNumber,
            int pageSize,
            BookingPeriod period,
            bool includeCancelled = false)
        {
            var now = DateTime.UtcNow;

            var query = dbContext.Bookings.AsNoTracking().AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(booking => booking.UserId == userId.Value);
            }

            if (!includeCancelled)
            {
                query = query.Where(booking => booking.Status != BookingStatus.Cancelled.ToString());
            }

            query = period switch
            {
                BookingPeriod.Upcoming =>
                    query.Where(booking => booking.StartDatetime > now),

                BookingPeriod.History =>
                    query.Where(booking => booking.StartDatetime <= now),

                _ => query
            };

            var totalRecords = await query.CountAsync();

            var bookings = await query
                .Include(booking => booking.Resource)
                .OrderBy(booking => booking.StartDatetime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)           
                .ToListAsync();

            return new PaginatedResponse<BookingResponseDto>
            {
                Items = bookings.Select(RespondBookingData).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(
                    (double)totalRecords / pageSize)
            };
        }

        public async Task<BookingResponseDto?> GetByIdForAdminAsync(
            Guid bookingId)
        {
            var booking = await dbContext.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BookingId == bookingId);

            return booking is null
                ? null
                : RespondBookingData(booking);
        }

        public async Task<bool> CancelBookingByAdminAsync(Guid bookingId)
        {
            var booking = await dbContext.Bookings
                .FirstOrDefaultAsync(item =>
                    item.BookingId == bookingId &&
                    item.Status != BookingStatus.Cancelled.ToString());

            if (booking is null)
            {
                return false;
            }

            booking.Status = BookingStatus.Cancelled.ToString();
            booking.ModifiedDatetime = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<BookingResponseDto?> GetByIdAsync(
            Guid bookingId,
            Guid userId)
        {
            var booking = await dbContext.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(booking =>
                    booking.BookingId == bookingId &&
                    booking.UserId == userId);

            return booking is null
                ? null
                : RespondBookingData(booking);
        }
        private DateTime ConvertToUtc(
            DateTime localDateTime,
            string timeZoneId)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId
            );
            localDateTime = DateTime.SpecifyKind(
                localDateTime,
                DateTimeKind.Unspecified
            );

            return TimeZoneInfo.ConvertTimeToUtc(
                localDateTime,
                timeZone
            );
        }
    }
}
