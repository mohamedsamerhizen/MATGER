using System.Security.Claims;
using MATGER.Api.Data;
using MATGER.Api.DTOs.Addresses;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Entities;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
public sealed class AddressesController(
    ApplicationDbContext dbContext,
    IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddressResponse>>> GetAll()
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var addresses = await dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(address =>
                address.UserId == userId.Value &&
                !address.IsDeleted)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.CreatedAt)
            .Select(address => ToResponse(address))
            .ToListAsync();

        return Ok(addresses);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AddressResponse>> GetById(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var address = await dbContext.CustomerAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(address =>
                address.Id == id &&
                address.UserId == userId.Value &&
                !address.IsDeleted);

        if (address is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Address was not found."));
        }

        return Ok(ToResponse(address));
    }

    [HttpPost]
    public async Task<ActionResult<AddressResponse>> Create(CreateAddressRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var validationError = ValidateCreateRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var now = DateTime.UtcNow;
        var hasAnyAddress = await dbContext.CustomerAddresses
            .AnyAsync(address =>
                address.UserId == userId.Value &&
                !address.IsDeleted);

        var shouldBeDefault = request.IsDefault || !hasAnyAddress;

        if (shouldBeDefault)
        {
            await ClearDefaultAddressesAsync(userId.Value);
        }

        var address = new CustomerAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Label = request.Label.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            Country = request.Country.Trim(),
            City = request.City.Trim(),
            Area = NormalizeOptional(request.Area),
            Street = request.Street.Trim(),
            Building = NormalizeOptional(request.Building),
            Floor = NormalizeOptional(request.Floor),
            Apartment = NormalizeOptional(request.Apartment),
            PostalCode = NormalizeOptional(request.PostalCode),
            Notes = NormalizeOptional(request.Notes),
            IsDefault = shouldBeDefault,
            IsDeleted = false,
            CreatedAt = now
        };

        dbContext.CustomerAddresses.Add(address);

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "AddressCreated",
            entityName: nameof(CustomerAddress),
            entityId: address.Id.ToString(),
            oldValue: null,
            newValue: ToAuditSnapshot(address),
            reason: "Customer address was created.");

        await dbContext.SaveChangesAsync();

        var response = ToResponse(address);

        return CreatedAtAction(
            nameof(GetById),
            new { id = address.Id },
            response);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<AddressResponse>> Update(
        Guid id,
        UpdateAddressRequest request)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var address = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(address =>
                address.Id == id &&
                address.UserId == userId.Value &&
                !address.IsDeleted);

        if (address is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Address was not found."));
        }

        var oldValue = ToAuditSnapshot(address);

        if (request.Label is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Label))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address label is required."));
            }

            address.Label = request.Label.Trim();
        }

        if (request.FullName is not null)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address full name is required."));
            }

            address.FullName = request.FullName.Trim();
        }

        if (request.PhoneNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address phone number is required."));
            }

            address.PhoneNumber = request.PhoneNumber.Trim();
        }

        if (request.Country is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Country))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address country is required."));
            }

            address.Country = request.Country.Trim();
        }

        if (request.City is not null)
        {
            if (string.IsNullOrWhiteSpace(request.City))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address city is required."));
            }

            address.City = request.City.Trim();
        }

        if (request.Area is not null)
        {
            address.Area = NormalizeOptional(request.Area);
        }

        if (request.Street is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Street))
            {
                return BadRequest(Error(
                    StatusCodes.Status400BadRequest,
                    "Address street is required."));
            }

            address.Street = request.Street.Trim();
        }

        if (request.Building is not null)
        {
            address.Building = NormalizeOptional(request.Building);
        }

        if (request.Floor is not null)
        {
            address.Floor = NormalizeOptional(request.Floor);
        }

        if (request.Apartment is not null)
        {
            address.Apartment = NormalizeOptional(request.Apartment);
        }

        if (request.PostalCode is not null)
        {
            address.PostalCode = NormalizeOptional(request.PostalCode);
        }

        if (request.Notes is not null)
        {
            address.Notes = NormalizeOptional(request.Notes);
        }

        if (request.IsDefault == true)
        {
            await ClearDefaultAddressesAsync(userId.Value, address.Id);
            address.IsDefault = true;
        }
        else if (request.IsDefault == false && address.IsDefault)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Default address cannot be unset directly. Set another address as default or delete this address."));
        }

        address.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "AddressUpdated",
            entityName: nameof(CustomerAddress),
            entityId: address.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(address),
            reason: "Customer address was updated.");

        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(address));
    }

    [HttpPatch("{id:guid}/set-default")]
    public async Task<ActionResult<AddressResponse>> SetDefault(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var address = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(address =>
                address.Id == id &&
                address.UserId == userId.Value &&
                !address.IsDeleted);

        if (address is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Address was not found."));
        }

        if (address.IsDefault)
        {
            return Ok(ToResponse(address));
        }

        var oldValue = ToAuditSnapshot(address);

        await ClearDefaultAddressesAsync(userId.Value, address.Id);

        address.IsDefault = true;
        address.UpdatedAt = DateTime.UtcNow;

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "AddressSetDefault",
            entityName: nameof(CustomerAddress),
            entityId: address.Id.ToString(),
            oldValue: oldValue,
            newValue: ToAuditSnapshot(address),
            reason: "Customer default address was changed.");

        await dbContext.SaveChangesAsync();

        return Ok(ToResponse(address));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var address = await dbContext.CustomerAddresses
            .FirstOrDefaultAsync(address =>
                address.Id == id &&
                address.UserId == userId.Value &&
                !address.IsDeleted);

        if (address is null)
        {
            return NotFound(Error(
                StatusCodes.Status404NotFound,
                "Address was not found."));
        }

        var oldValue = ToAuditSnapshot(address);
        var wasDefault = address.IsDefault;
        var now = DateTime.UtcNow;

        address.IsDeleted = true;
        address.IsDefault = false;
        address.UpdatedAt = now;

        CustomerAddress? newestRemainingAddress = null;

        if (wasDefault)
        {
            newestRemainingAddress = await dbContext.CustomerAddresses
                .Where(otherAddress =>
                    otherAddress.UserId == userId.Value &&
                    otherAddress.Id != address.Id &&
                    !otherAddress.IsDeleted)
                .OrderByDescending(otherAddress => otherAddress.CreatedAt)
                .FirstOrDefaultAsync();

            if (newestRemainingAddress is not null)
            {
                newestRemainingAddress.IsDefault = true;
                newestRemainingAddress.UpdatedAt = now;
            }
        }

        await auditLogService.LogAsync(
            actorUserId: userId.Value,
            action: "AddressDeleted",
            entityName: nameof(CustomerAddress),
            entityId: address.Id.ToString(),
            oldValue: oldValue,
            newValue: new
            {
                DeletedAddress = ToAuditSnapshot(address),
                NewDefaultAddressId = newestRemainingAddress?.Id
            },
            reason: "Customer address was deleted.");

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        return userId;
    }

    private async Task ClearDefaultAddressesAsync(Guid userId, Guid? exceptAddressId = null)
    {
        var defaultAddresses = await dbContext.CustomerAddresses
            .Where(address =>
                address.UserId == userId &&
                address.IsDefault &&
                !address.IsDeleted &&
                (!exceptAddressId.HasValue || address.Id != exceptAddressId.Value))
            .ToListAsync();

        foreach (var address in defaultAddresses)
        {
            address.IsDefault = false;
            address.UpdatedAt = DateTime.UtcNow;
        }
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

    private static string? ValidateCreateRequest(CreateAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return "Address label is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return "Address full name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return "Address phone number is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Country))
        {
            return "Address country is required.";
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            return "Address city is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Street))
        {
            return "Address street is required.";
        }

        return null;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static AddressResponse ToResponse(CustomerAddress address)
    {
        return new AddressResponse
        {
            Id = address.Id,
            Label = address.Label,
            FullName = address.FullName,
            PhoneNumber = address.PhoneNumber,
            Country = address.Country,
            City = address.City,
            Area = address.Area,
            Street = address.Street,
            Building = address.Building,
            Floor = address.Floor,
            Apartment = address.Apartment,
            PostalCode = address.PostalCode,
            Notes = address.Notes,
            IsDefault = address.IsDefault,
            CreatedAt = address.CreatedAt,
            UpdatedAt = address.UpdatedAt
        };
    }

    private static object ToAuditSnapshot(CustomerAddress address)
    {
        return new
        {
            address.Id,
            address.UserId,
            address.Label,
            address.FullName,
            address.PhoneNumber,
            address.Country,
            address.City,
            address.Area,
            address.Street,
            address.Building,
            address.Floor,
            address.Apartment,
            address.PostalCode,
            address.Notes,
            address.IsDefault,
            address.IsDeleted,
            address.CreatedAt,
            address.UpdatedAt
        };
    }
}