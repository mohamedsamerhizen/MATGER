using System.Text.RegularExpressions;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Brands;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/brands")]
public sealed partial class BrandsController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandResponse>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Brands
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(brand => brand.IsActive);
        }

        var brands = await query
            .OrderBy(brand => brand.Name)
            .Select(brand => new BrandResponse
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                IsActive = brand.IsActive,
                CreatedAt = brand.CreatedAt,
                UpdatedAt = brand.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(brands);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands
            .AsNoTracking()
            .Where(brand => brand.Id == id)
            .Select(brand => new BrandResponse
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                IsActive = brand.IsActive,
                CreatedAt = brand.CreatedAt,
                UpdatedAt = brand.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Brand was not found."));
        }

        return Ok(brand);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<BrandResponse>> Create(
        CreateBrandRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Brand name is required."));
        }

        var name = request.Name.Trim();
        var slug = CreateSlug(name);

        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Brand name must contain at least one letter or number."));
        }

        var slugExists = await dbContext.Brands
            .AnyAsync(brand => brand.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Brand already exists."));
        }

        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(brand);

        return CreatedAtAction(
            nameof(GetById),
            new { id = brand.Id },
            response);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<BrandResponse>> Update(
        Guid id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands
            .FirstOrDefaultAsync(brand => brand.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Brand was not found."));
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Brand name is required."));
            }

            var name = request.Name.Trim();
            var slug = CreateSlug(name);

            if (string.IsNullOrWhiteSpace(slug))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Brand name must contain at least one letter or number."));
            }

            var slugExists = await dbContext.Brands
                .AnyAsync(otherBrand =>
                    otherBrand.Id != id &&
                    otherBrand.Slug == slug,
                    cancellationToken);

            if (slugExists)
            {
                return Conflict(Error(
                    StatusCodes.Status409Conflict,
                    "Brand already exists."));
            }

            brand.Name = name;
            brand.Slug = slug;
        }

        if (request.IsActive.HasValue)
        {
            brand.IsActive = request.IsActive.Value;
        }

        brand.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(brand));
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPatch("{id:guid}/disable")]
    public async Task<IActionResult> Disable(
        Guid id,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands
            .FirstOrDefaultAsync(brand => brand.Id == id, cancellationToken);

        if (brand is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Brand was not found."));
        }

        if (!brand.IsActive)
        {
            return NoContent();
        }

        brand.IsActive = false;
        brand.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

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

    private static BrandResponse ToResponse(Brand brand)
    {
        return new BrandResponse
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            IsActive = brand.IsActive,
            CreatedAt = brand.CreatedAt,
            UpdatedAt = brand.UpdatedAt
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
