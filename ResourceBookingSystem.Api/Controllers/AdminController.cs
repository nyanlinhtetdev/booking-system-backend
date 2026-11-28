using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceBookingSystem.WebApp.AuditLog.DTOs;
using ResourceBookingSystem.WebApp.AuditLog.ServiceInterfaces;
using ResourceBookingSystem.WebApp.UserDomain.DTOs;
using ResourceBookingSystem.WebApp.UserDomain.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Resource.DTOs;
using ResourceBookingSystem.WebApp.Resource.ServiceInterfaces;
using ResourceBookingSystem.WebApp.ResourceCategory.DTOs;
using ResourceBookingSystem.WebApp.ResourceCategory.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Booking.ServiceInterfaces;
using System.Security.Claims;

namespace ResourceBookingSystem.API.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController(
        IResourceService resourceService,
        IResourceCategoryService resourceCategoryService,
        IBookingService bookingService,
        IUserService userService,
        IAuditLogService auditLogService) : ControllerBase
    {
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool includeInactive = true)
        {
            if (!IsValidPagination(pageNumber, pageSize))
            {
                return InvalidPagination();
            }

            var users = await userService.GetAllAsync(
                pageNumber,
                pageSize,
                includeInactive);

            return Ok(users);
        }

        [HttpPatch("users/{id:guid}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid id)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            if (id == actorUserId)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid user operation",
                    detail: "You cannot deactivate your own account.");
            }

            var user = await userService.DeactivateAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "UserDeactivated",
                "User",
                user.UserId,
                new { IsActive = true },
                new { IsActive = false });

            return Ok(user);
        }

        [HttpPatch("users/{id:guid}/reactivate")]
        public async Task<IActionResult> ReactivateUser(Guid id)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var user = await userService.ReactivateAsync(id);

            if (user is null)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "UserReactivated",
                "User",
                user.UserId,
                new { IsActive = false },
                new { IsActive = true });

            return Ok(user);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("admins")]
        public async Task<IActionResult> CreateAdmin(CreateAdminRequestDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var user = await userService.CreateAdminAsync(request);

            if (user is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Email already exists",
                    detail: "A user with this email already exists.");
            }

            await RecordAuditAsync(
                actorUserId,
                "AdminCreated",
                "User",
                user.UserId,
                null,
                user);

            return Ok(user);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!IsValidPagination(pageNumber, pageSize))
            {
                return InvalidPagination();
            }

            var auditLogs = await auditLogService.GetAllAsync(
                pageNumber,
                pageSize);

            return Ok(auditLogs);
        }

        [HttpGet("bookings")]
        public async Task<IActionResult> GetAllBookings(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!IsValidPagination(pageNumber, pageSize))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid pagination",
                    detail: "Page number must be at least 1 and page size must be between 1 and 100.");
            }

            var bookings = await bookingService.GetAllBookingsAsync(
                pageNumber,
                pageSize);

            return Ok(bookings);
        }

        [HttpGet("bookings/{id:guid}")]
        public async Task<IActionResult> GetBooking(Guid id)
        {
            var booking = await bookingService.GetByIdForAdminAsync(id);

            return booking is null
                ? NotFound()
                : Ok(booking);
        }

        [HttpPatch("bookings/{id:guid}/cancel")]
        public async Task<IActionResult> CancelBooking(Guid id)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var booking = await bookingService.GetByIdForAdminAsync(id);
            var cancelled = await bookingService.CancelBookingByAdminAsync(id);

            if (!cancelled)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "AdminCancelledBooking",
                "Booking",
                id,
                booking,
                booking is null ? null : new
                {
                    booking.BookingId,
                    booking.UserId,
                    booking.ResourceId,
                    Status = "Cancelled",
                    booking.StartDatetime,
                    booking.EndDatetime
                });

            return NoContent();
        }

        [HttpPost("resource-categories")]
        public async Task<IActionResult> CreateResourceCategory(
            ResourceCategoryRequestDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var result = await resourceCategoryService.CreateAsync(request);

            if (result.Status == ResourceCategoryMutationStatus.Success)
            {
                await RecordAuditAsync(
                    actorUserId,
                    "ResourceCategoryCreated",
                    "ResourceCategory",
                    result.Category!.ResourceCategoryId,
                    null,
                    result.Category);

                return Created("/api/resource-categories", result.Category);
            }

            return result.Status == ResourceCategoryMutationStatus.NameAlreadyExists
                ? Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Category already exists",
                    detail: "A resource category with this name already exists.")
                : Problem();
        }

        [HttpPut("resource-categories/{id:guid}")]
        public async Task<IActionResult> UpdateResourceCategory(
            Guid id,
            ResourceCategoryRequestDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var result = await resourceCategoryService.UpdateAsync(id, request);

            if (result.Status == ResourceCategoryMutationStatus.Success)
            {
                await RecordAuditAsync(
                    actorUserId,
                    "ResourceCategoryUpdated",
                    "ResourceCategory",
                    id,
                    result.OldCategory,
                    result.Category);

                return Ok(result.Category);
            }

            return result.Status switch
            {
                ResourceCategoryMutationStatus.NotFound => NotFound(),
                ResourceCategoryMutationStatus.NameAlreadyExists => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Category already exists",
                    detail: "A resource category with this name already exists."),
                _ => Problem()
            };
        }

        [HttpDelete("resource-categories/{id:guid}")]
        public async Task<IActionResult> DeleteResourceCategory(Guid id)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            var result = await resourceCategoryService.DeleteAsync(id);

            if (result.Status == ResourceCategoryMutationStatus.Success)
            {
                await RecordAuditAsync(
                    actorUserId,
                    "ResourceCategoryDeleted",
                    "ResourceCategory",
                    id,
                    null,
                    null);

                return NoContent();
            }

            return result.Status switch
            {
                ResourceCategoryMutationStatus.NotFound => NotFound(),
                ResourceCategoryMutationStatus.HasResources => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Category is in use",
                    detail: "A category cannot be deleted while resources use it."),
                _ => Problem()
            };
        }

        [HttpPost("resources")]
        public async Task<IActionResult> CreateResource(
            ResourceRequestDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            if (!await resourceService.CategoryExistsAsync(
                    request.ResourceCategoryId))
            {
                return BadRequest("The resource category does not exist.");
            }

            var resource = await resourceService.CreateAsync(request);

            if (resource is null)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "ResourceCreated",
                "Resource",
                resource.ResourceId,
                null,
                resource);

            return Created($"/api/resources/{resource.ResourceId}", resource);
        }

        [HttpPatch("resources/{id:guid}")]
        public async Task<IActionResult> UpdateResource(
            Guid id,
            ResourceRequestDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            if (!await resourceService.CategoryExistsAsync(
                    request.ResourceCategoryId))
            {
                return BadRequest("The resource category does not exist.");
            }

            var result = await resourceService.UpdateAsync(id, request);

            if (result is null)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "ResourceUpdated",
                "Resource",
                result.NewValue.ResourceId,
                result.OldValue,
                result.NewValue);

            return Ok(result.NewValue);
        }

        [HttpPatch("resources/{id:guid}/status")]
        public async Task<IActionResult> UpdateResourceStatus(
            Guid id,
            ResourceStatusUpdateDto request)
        {
            if (!TryGetCurrentUserId(out var actorUserId))
            {
                return Unauthorized();
            }

            if (!string.Equals(request.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(request.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Status must be Active or Inactive.");
            }

            var resource = await resourceService.UpdateStatusAsync(
                id,
                request.Status);

            if (resource is null)
            {
                return NotFound();
            }

            await RecordAuditAsync(
                actorUserId,
                "ResourceStatusUpdated",
                "Resource",
                resource.ResourceId,
                null,
                new { resource.Status });

            return Ok(resource);
        }

        private async Task RecordAuditAsync(
            Guid actorUserId,
            string action,
            string entityType,
            Guid? entityId,
            object? oldValues,
            object? newValues)
        {
            await auditLogService.RecordAsync(new AuditLogRecordRequest(
                actorUserId,
                action,
                entityType,
                entityId,
                oldValues,
                newValues,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                HttpContext.TraceIdentifier));
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        private IActionResult InvalidPagination()
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid pagination",
                detail: "Page number must be at least 1 and page size must be between 1 and 100.");
        }

        private static bool IsValidPagination(
            int pageNumber,
            int pageSize)
        {
            return pageNumber >= 1 &&
                   pageSize >= 1 &&
                   pageSize <= 100;
        }
    }
}
