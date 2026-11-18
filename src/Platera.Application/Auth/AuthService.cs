using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Application.Common;
using Platera.Domain.Entities;

namespace Platera.Application.Auth;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokens;

    public AuthService(UserManager<AppUser> users, IApplicationDbContext db, ITokenService tokens)
    {
        _users = users; _db = db; _tokens = tokens;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _users.FindByEmailAsync(request.Email) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new AppUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
        };
        var result = await _users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));
        await _users.AddToRoleAsync(user, Roles.User);
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await _users.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAppException();
        if (!user.IsActive) throw new ForbiddenException("This account has been deactivated.");
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = _tokens.Hash(request.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAppException("Refresh token is invalid or expired.");

        var user = await _users.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Account is unavailable.");

        stored.RevokedAt = DateTime.UtcNow; // rotate
        return await IssueAsync(user, ct);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = _tokens.Hash(request.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User not found.");
        return new UserDto(user.Id, user.Email!, user.DisplayName, (await _users.GetRolesAsync(user)).ToArray());
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken ct)
    {
        var roles = (await _users.GetRolesAsync(user)).ToArray();
        var (access, expires) = _tokens.CreateAccessToken(user, roles);
        var refresh = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.Hash(refresh),
            ExpiresAt = DateTime.UtcNow.Add(_tokens.RefreshTokenLifetime),
        });
        await _db.SaveChangesAsync(ct);
        return new AuthResponse(access, refresh, expires, new UserDto(user.Id, user.Email!, user.DisplayName, roles));
    }
}
