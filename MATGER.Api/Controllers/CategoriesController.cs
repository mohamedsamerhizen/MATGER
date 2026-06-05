using System.Text.RegularExpressions;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Categories;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed partial class CategoriesController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll()
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                IsActive = category.IsActive
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                IsActive = category.IsActive
            })
            .FirstOrDefaultAsync();

        if (category is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Category was not found."));
        }

        return Ok(category);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Category name is required."));
        }

        var name = request.Name.Trim();
        var slug = CreateSlug(name);

        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Category name must contain at least one letter or number."));
        }

        var slugExists = await dbContext.Categories
            .AnyAsync(category => category.Slug == slug);

        if (slugExists)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Category already exists."));
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true
        };

        dbContext.Categories.Add(category);

        await dbContext.SaveChangesAsync();

        var response = ToResponse(category);

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.Id },
            response);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> Update(
        Guid id,
        UpdateCategoryRequest request)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(category => category.Id == id);

        if (category is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Category was not found."));
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Category name is required."));
            }

            var newName = request.Name.Trim();
            var newSlug = CreateSlug(newName);

            if (string.IsNullOrWhiteSpace(newSlug))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Category name must contain at least one letter or number."));
            }

            var slugExists = await dbContext.Categories
                .AnyAsync(otherCategory =>
                    otherCategory.Id != id &&
                    otherCategory.Slug == newSlug);

            if (slugExists)
            {
                return Conflict(Error(
                    StatusCodes.Status409Conflict,
                    "Category with the same name already exists."));
            }

            category.Name = newName;
            category.Slug = newSlug;
        }

        if (request.IsActive.HasValue)
        {
            category.IsActive = request.IsActive.Value;
        }

        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(category));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id)
    {
        var category = await dbContext.Categories
            .FirstOrDefaultAsync(category => category.Id == id);

        if (category is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Category was not found."));
        }

        if (!category.IsActive)
        {
            return NoContent();
        }

        category.IsActive = false;

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private ApiErrorResponse Error(int statusCode, string message)
    {
        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private static CategoryResponse ToResponse(Category category)
    {
        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            IsActive = category.IsActive
        };
    }

    private static string CreateSlug(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = NonAlphaNumericRegex().Replace(slug, "-");
        slug = DuplicateDashesRegex().Replace(slug, "-");

        return slug.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateDashesRegex();
}