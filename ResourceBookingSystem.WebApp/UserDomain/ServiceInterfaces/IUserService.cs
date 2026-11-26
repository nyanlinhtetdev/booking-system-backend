using ResourceBookingSystem.WebApp.Auth.DTOs;
using ResourceBookingSystem.WebApp.Shared.DTOs;
using ResourceBookingSystem.WebApp.UserDomain.DTOs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ResourceBookingSystem.WebApp.UserDomain.ServiceInterfaces
{
    public interface IUserService
    {
        Task<bool> UpdateProfile(
            Guid userId,
            UserUpdateProfileDto request);

        Task<PaginatedResponse<UserDto>> GetAllAsync(
            int pageNumber,
            int pageSize,
            bool includeInactive);

        Task<UserDto?> DeactivateAsync(Guid userId);

        Task<UserDto?> ReactivateAsync(Guid userId);

        Task<UserDto?> CreateAdminAsync(CreateAdminRequestDto request);
    }
}
