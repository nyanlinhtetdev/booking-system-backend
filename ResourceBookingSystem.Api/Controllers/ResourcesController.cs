using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceBookingSystem.WebApp.ResourceCategory.ServiceInterfaces;
using ResourceBookingSystem.WebApp.Resource.ServiceInterfaces;

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

        private bool CanManageResources()
        {
            return User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        }
    }
}
