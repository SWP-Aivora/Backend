using Aivora.api.Extensions;
using Aivora.Services.CategoryService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[EnableRateLimiting("General")]
public class CategoryController : ControllerBase
{
    private readonly IService _categoryService;

    public CategoryController(IService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _categoryService.GetCategoriesAsync();
        return Ok(ApiResponseFactory.SuccessResponse(result, "Categories retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategory(Guid id)
    {
        var result = await _categoryService.GetCategoryByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Category retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> CreateCategory([FromBody] Request.CreateCategoryRequest request)
    {
        var result = await _categoryService.CreateCategoryAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Category created successfully", HttpContext.TraceIdentifier));
    }
}
