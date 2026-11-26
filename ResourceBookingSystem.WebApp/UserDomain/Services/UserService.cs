using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResourceBookingSystem.Database.AppDbContextModels;
using ResourceBookingSystem.WebApp.Auth.DTOs;
using ResourceBookingSystem.WebApp.Shared.DTOs;
using ResourceBookingSystem.WebApp.UserDomain.DTOs;
using ResourceBookingSystem.WebApp.UserDomain.ServiceInterfaces;

namespace ResourceBookingSystem.WebApp.UserDomain.Services;

public class UserService(AppDbContext dbContext) : IUserService
{
    public async Task<bool> UpdateProfile(
        Guid userId,
        UserUpdateProfileDto request)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user is null)
        {
            return false;
        }

        var hasChanges = false;

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(request.ContactNumber))
        {
            user.ContactNumber = request.ContactNumber.Trim();
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return false;
        }

        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<PaginatedResponse<UserDto>> GetAllAsync(
        int pageNumber,
        int pageSize,
        bool includeInactive)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }

        var totalRecords = await query.CountAsync();

        var users = (await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync())
            .Select(ToDto)
            .ToList();

        return new PaginatedResponse<UserDto>
        {
            Items = users,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize)
        };
    }

    public async Task<UserDto?> DeactivateAsync(Guid userId)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(item => item.UserId == userId);

        if (user is null || !user.IsActive || user.Role == "SuperAdmin")
        {
            return null;
        }

        user.IsActive = false;
        await dbContext.SaveChangesAsync();

        return ToDto(user);
    }

    public async Task<UserDto?> ReactivateAsync(Guid userId)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(item => item.UserId == userId);

        if (user is null || user.IsActive || user.Role == "SuperAdmin")
        {
            return null;
        }

        user.IsActive = true;
        await dbContext.SaveChangesAsync();

        return ToDto(user);
    }

    public async Task<UserDto?> CreateAdminAsync(CreateAdminRequestDto request)
    {
        var email = request.Email.Trim();

        if (await dbContext.Users.AnyAsync(user => user.Email == email))
        {
            return null;
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            ContactNumber = string.IsNullOrWhiteSpace(request.ContactNumber)
                ? null
                : request.ContactNumber.Trim(),
            Role = "Admin",
            IsActive = true
        };

        user.PasswordHash = new PasswordHasher<User>()
            .HashPassword(user, request.Password);

        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        return ToDto(user);
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            ContactNumber = user.ContactNumber,
            StudentId = user.StudentId,
            Role = user.Role,
            IsActive = user.IsActive
        };
    }
}
