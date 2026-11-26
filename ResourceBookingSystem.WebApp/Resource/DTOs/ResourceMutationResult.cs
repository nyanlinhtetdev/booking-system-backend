namespace ResourceBookingSystem.WebApp.Resource.DTOs;

public sealed record ResourceMutationResult(
    ResourceResponseDto OldValue,
    ResourceResponseDto NewValue);
