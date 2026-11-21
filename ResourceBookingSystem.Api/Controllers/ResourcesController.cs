using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceBookingSystem.WebApp.ResourceCategory.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Resource.ServiceInterfaces;
using System.Security.Claims;

namespace ResourceBookingSystem.API.Controllers
{
    [Route("api/resources")]
    [ApiController]
    public class ResourcesController(
        IResourceService resourceService,
        IResourceCategoryService resourceCategoryService) : ControllerBase
    {
        [HttpGet("~/api/resource-categories")]
        public async Task<IActionResult> GetResourceCategories()
        {
            var categories = await resourceCategoryService.GetAllAsync();

            return Ok(categories);
        }

        [HttpGet]
        public async Task<IActionResult> GetResources(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string search = "",
            [FromQuery] string categoryId = "")
        {
            if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid pagination",
                    detail: "Page number must be at least 1 and page size must be between 1 and 100.");
            }

            var resources = await resourceService.GetAllAsync(pageNumber, pageSize, search, categoryId, CanManageResources());

            return Ok(resources);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetResource(Guid id)
        {
            var resource = await resourceService.GetByIdAsync(
                id,
                CanManageResources());

            return resource is null ? NotFound() : Ok(resource);
        }

        [HttpGet("{id:guid}/availability")]
        public async Task<IActionResult> GetResourceAvailability(
            Guid id,
            [FromQuery] DateTime? date)
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Unauthorized();
            }

            var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
            var todayInMalaysia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
            
            // If date is provided, take just the calendar Date part (local date in Malaysia)
            var selectedDate = date.HasValue
                ? date.Value.Date
                : todayInMalaysia;

            var earliestAllowed = todayInMalaysia.AddDays(-7);
            var latestAllowed = todayInMalaysia.AddMonths(3).AddDays(7);

            // Reject out-of-range date requests

            if (selectedDate < earliestAllowed || selectedDate > latestAllowed)
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid date",
                    detail: "Availability date must be within 3 months of the current date.");
            }

            var availability = await resourceService.GetAvailabilityAsync(
                id,
                currentUserId,
                selectedDate);

            return availability is null
                ? NotFound()
                : Ok(availability);
        }

        private bool CanManageResources()
        {
            return User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userIdClaim, out userId);
        }
    }
}
