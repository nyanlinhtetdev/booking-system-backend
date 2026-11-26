using Microsoft.EntityFrameworkCore;
using ResourceBookingSystem.Database.AppDbContextModels;
using ResourceBookingSystem.WebApp.Booking.Enums;
using ResourceBookingSystem.WebApp.Resource.DTOs;
using ResourceBookingSystem.WebApp.Resource.Enums;
using ResourceBookingSystem.WebApp.Resource.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Shared.DTOs;
using ResourceEntity = ResourceBookingSystem.Database.AppDbContextModels.Resource;

namespace ResourceBookingSystem.WebApp.Resource.Services;

public sealed class ResourceService(AppDbContext dbContext) : IResourceService
{
    public async Task<PaginatedResponse<ResourceResponseDto>> GetAllAsync(
        int pageNumber,
        int pageSize,
        string search, 
        string categoryId,
        bool includeInactive)
    {
        var query = dbContext.Resources
            .AsNoTracking()
            .Include(resource => resource.ResourceCategory)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(resource =>
                resource.Status == ResourceStatus.Active.ToString());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(resource => resource.Name.ToLower().Contains(search.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            query = query.Where(resource => resource.ResourceCategoryId == Guid.Parse(categoryId));
        }

        var totalRecords = await query.CountAsync();

        var resources = await query
            .OrderBy(resource => resource.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<ResourceResponseDto>
        {
            Items = resources.Select(ToResponse).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling(
                    (double)totalRecords / pageSize)
        };
    }

    public async Task<ResourceResponseDto?> GetByIdAsync(
        Guid resourceId,
        bool includeInactive)
    {
        var query = dbContext.Resources
            .AsNoTracking()
            .Include(resource => resource.ResourceCategory)
            .Where(resource => resource.ResourceId == resourceId);

        if (!includeInactive)
        {
            query = query.Where(resource =>
                resource.Status == ResourceStatus.Active.ToString());
        }

        var resource = await query.FirstOrDefaultAsync();

        return resource is null ? null : ToResponse(resource);
    }


    public async Task<ResourceAvailabilityResponseDto?> GetAvailabilityAsync(
        Guid resourceId,
        Guid currentUserId,
        DateTime date)
    {
        var resourceExists = await dbContext.Resources
            .AnyAsync(resource =>
                resource.ResourceId == resourceId &&
                resource.Status == ResourceStatus.Active.ToString());

        if (!resourceExists)
        {
            return null;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");

        var localWeekStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var utcWeekStart = TimeZoneInfo.ConvertTimeToUtc(localWeekStart, timeZone);
        var utcWeekEnd = utcWeekStart.AddDays(7);

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.ResourceId == resourceId &&
                booking.Status != BookingStatus.Cancelled.ToString() &&
                booking.StartDatetime < utcWeekEnd &&
                booking.EndDatetime > utcWeekStart)
            .OrderBy(booking => booking.StartDatetime)
            .Select(booking => new BookedSlotDto
            {
                BookingId = booking.BookingId,
                StartDatetime = booking.StartDatetime,
                EndDatetime = booking.EndDatetime,
                IsOwnedByCurrentUser = booking.UserId == currentUserId
            })
            .ToListAsync();

        return new ResourceAvailabilityResponseDto
        {
            ResourceId = resourceId,
            WeekStartDate = utcWeekStart,
            WeekEndDate = utcWeekEnd.AddDays(-1),
            Bookings = bookings
        };
    }

    public async Task<ResourceResponseDto?> CreateAsync(
        ResourceRequestDto request)
    {
        if (!await CategoryExistsAsync(request.ResourceCategoryId))
        {
            return null;
        }

        var resource = new ResourceEntity
        {
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            Capacity = request.Capacity,
            ResourceCategoryId = request.ResourceCategoryId,
            Status = ResourceStatus.Active.ToString()
        };

        await dbContext.Resources.AddAsync(resource);
        await dbContext.SaveChangesAsync();

        await dbContext.Entry(resource)
            .Reference(item => item.ResourceCategory)
            .LoadAsync();

        return ToResponse(resource);
    }

    public Task<bool> CategoryExistsAsync(Guid categoryId)
    {
        return dbContext.ResourceCategories
            .AnyAsync(category => category.ResourceCategoryId == categoryId);
    }

    public async Task<ResourceResponseDto?> UpdateAsync(
        Guid resourceId,
        ResourceRequestDto request)
    {
        var resource = await dbContext.Resources
            .FirstOrDefaultAsync(item => item.ResourceId == resourceId);

        if (resource is null)
        {
            return null;
        }

        if (!await CategoryExistsAsync(request.ResourceCategoryId))
        {
            return null;
        }

        resource.Name = request.Name.Trim();
        resource.Location = request.Location.Trim();
        resource.Capacity = request.Capacity;
        resource.ResourceCategoryId = request.ResourceCategoryId;

        await dbContext.SaveChangesAsync();

        await dbContext.Entry(resource)
            .Reference(item => item.ResourceCategory)
            .LoadAsync();

        return ToResponse(resource);
    }

    public async Task<ResourceResponseDto?> UpdateStatusAsync(
        Guid resourceId,
        string status)
    {
        var normalizedStatus = status.Trim();

        if (!Enum.TryParse<ResourceStatus>(
                normalizedStatus,
                ignoreCase: true,
                out var resourceStatus))
        {
            return null;
        }

        var resource = await dbContext.Resources
            .Include(item => item.ResourceCategory)
            .FirstOrDefaultAsync(item => item.ResourceId == resourceId);

        if (resource is null)
        {
            return null;
        }

        resource.Status = resourceStatus.ToString();

        await dbContext.SaveChangesAsync();

        return ToResponse(resource);
    }

    private static ResourceResponseDto ToResponse(ResourceEntity resource)
    {
        return new ResourceResponseDto
        {
            ResourceId = resource.ResourceId,
            Name = resource.Name,
            Location = resource.Location,
            Capacity = resource.Capacity,
            Status = resource.Status,
            ResourceCategoryId = resource.ResourceCategoryId,
            ResourceCategoryName = resource.ResourceCategory.Name
        };
    }
}
