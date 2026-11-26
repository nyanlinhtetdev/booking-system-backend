using ResourceBookingSystem.WebApp.Resource.DTOs;
using ResourceBookingSystem.WebApp.Shared.DTOs;

namespace ResourceBookingSystem.WebApp.Resource.ServiceInterfaces;

public interface IResourceService
{
    Task<PaginatedResponse<ResourceResponseDto>> GetAllAsync(
        int pageNumber,
        int pageSize,
        string search,
        string categoryId,
        bool includeInactive);

    Task<ResourceResponseDto?> GetByIdAsync(
        Guid resourceId,
        bool includeInactive);

    Task<ResourceAvailabilityResponseDto?> GetAvailabilityAsync(
        Guid resourceId,
        Guid currentUserId,
        DateTime date);

    Task<ResourceResponseDto?> CreateAsync(ResourceRequestDto request);

    Task<bool> CategoryExistsAsync(Guid categoryId);

    Task<ResourceMutationResult?> UpdateAsync(
        Guid resourceId,
        ResourceRequestDto request);

    Task<ResourceResponseDto?> UpdateStatusAsync(
        Guid resourceId,
        string status);
}