using System.Security.Claims;
using MATGER.Api.Authentication;
using MATGER.Api.DTOs.Auth;
using MATGER.Api.DTOs.Common;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MATGER.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<JwtSettings> jwtOptions) : ControllerBase
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var validationError = ValidateRegisterRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var normalizedEmail = request.Email.Trim();

        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            return Conflict(Error(
                StatusCodes.Status409Conflict,
                "Email is already registered."));
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(IdentityErrorResponse(
                StatusCodes.Status400BadRequest,
                "Registration failed.",
                createResult.Errors));
        }

        var roleResult = await userManager.AddToRoleAsync(user, ApplicationRoles.Customer);

        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return BadRequest(IdentityErrorResponse(
                StatusCodes.Status400BadRequest,
                "Registration failed while assigning the customer role.",
                roleResult.Errors));
        }

        var roles = await userManager.GetRolesAsync(user);

        var response = await CreateAuthResponseAsync(user, roles);

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var validationError = ValidateLoginRequest(request);

        if (validationError is not null)
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                validationError));
        }

        var normalizedEmail = request.Email.Trim();

        var user = await userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid email or password."));
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);

        if (!passwordValid || !user.IsActive)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid email or password."));
        }

        var roles = await userManager.GetRolesAsync(user);

        var response = await CreateAuthResponseAsync(user, roles);

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Refresh token is required."));
        }

        var rotationResult = await refreshTokenService.RotateRefreshTokenAsync(
            request.RefreshToken.Trim());

        if (!rotationResult.IsValid)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token."));
        }

        var user = await userManager.FindByIdAsync(rotationResult.UserId.ToString());

        if (user is null || !user.IsActive)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token."));
        }

        var roles = await userManager.GetRolesAsync(user);

        var accessToken = jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            roles);

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rotationResult.NewRefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            UserId = user.Id.ToString(),
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToList()
        };

        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(Error(
                StatusCodes.Status400BadRequest,
                "Refresh token is required."));
        }

        var revoked = await refreshTokenService.RevokeRefreshTokenAsync(
            request.RefreshToken.Trim());

        if (!revoked)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token."));
        }

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var user = await userManager.FindByIdAsync(userId);

        if (user is null || !user.IsActive)
        {
            return Unauthorized(Error(
                StatusCodes.Status401Unauthorized,
                "Invalid access token."));
        }

        var roles = await userManager.GetRolesAsync(user);

        var response = new CurrentUserResponse
        {
            UserId = user.Id.ToString(),
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToList()
        };

        return Ok(response);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user,
        IEnumerable<string> roles)
    {
        var accessToken = jwtTokenService.GenerateAccessToken(
            user.Id,
            user.Email!,
            user.FullName,
            roles);

        var refreshToken = await refreshTokenService.CreateRefreshTokenAsync(user.Id);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            UserId = user.Id.ToString(),
            Email = user.Email!,
            FullName = user.FullName,
            Roles = roles.ToList()
        };
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

    private ApiErrorResponse IdentityErrorResponse(
        int statusCode,
        string message,
        IEnumerable<IdentityError> identityErrors)
    {
        var errors = identityErrors
            .GroupBy(error => string.IsNullOrWhiteSpace(error.Code)
                ? "Identity"
                : error.Code)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.Description)
                    .Where(description => !string.IsNullOrWhiteSpace(description))
                    .ToArray());

        return new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            TraceId = HttpContext.TraceIdentifier,
            Errors = errors
        };
    }

    private static string? ValidateRegisterRequest(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return "Full name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return "Password is required.";
        }

        return null;
    }

    private static string? ValidateLoginRequest(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return "Password is required.";
        }

        return null;
    }
}