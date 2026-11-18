using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Application.Common;
using Platera.Domain.Entities;

namespace Platera.Application.Admin;

public record ActivityDto(string Type, string Description, string Actor, DateTime At);
public record DashboardDto(int Users, int Recipes, int Comments, int Likes, int HiddenComments, List<ActivityDto> LatestActivity);
public record AdminUserDto(Guid Id, string Email, string DisplayName, bool IsActive, string[] Roles, DateTime CreatedAt, int RecipeCount);
public record SetActiveRequest(bool IsActive);
public record SetRoleRequest(string Role);
public record SetFeaturedRequest(bool IsFeatured);
public record SetHiddenRequest(bool IsHidden);
public record AdminCommentDto(Guid Id, Guid RecipeId, string RecipeTitle, string UserName, string Text, string? PhotoUrl, bool IsHidden, DateTime CreatedAt);

public class SetRoleRequestValidator : AbstractValidator<SetRoleRequest>
{
    public SetRoleRequestValidator()
    {
        RuleFor(x => x.Role).Must(r => r == Roles.Admin || r == Roles.User).WithMessage("Role must be Admin or User.");
    }
}

public interface IAdminService
{
    Task<DashboardDto> DashboardAsync(CancellationToken ct = default);
    Task<PagedResult<AdminUserDto>> UsersAsync(string? search, PageQuery query, CancellationToken ct = default);
    Task SetActiveAsync(Guid userId, SetActiveRequest request, CancellationToken ct = default);
    Task SetRoleAsync(Guid userId, SetRoleRequest request, CancellationToken ct = default);
    Task SetFeaturedAsync(Guid recipeId, SetFeaturedRequest request, CancellationToken ct = default);
    Task<PagedResult<AdminCommentDto>> CommentsAsync(bool hiddenOnly, PageQuery query, CancellationToken ct = default);
    Task SetCommentHiddenAsync(Guid commentId, SetHiddenRequest request, CancellationToken ct = default);
}

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly ICurrentUser _me;
    public AdminService(IApplicationDbContext db, UserManager<AppUser> users, ICurrentUser me) { _db = db; _users = users; _me = me; }

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        RequireAdmin();
        var recipes = await _db.Recipes.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(5)
            .Select(r => new ActivityDto("recipe", "Published \"" + r.Title + "\"", r.Author!.DisplayName, r.CreatedAt)).ToListAsync(ct);
        var comments = await _db.Comments.AsNoTracking().OrderByDescending(c => c.CreatedAt).Take(5)
            .Select(c => new ActivityDto("comment", "Commented on \"" + c.Recipe!.Title + "\"", c.User!.DisplayName, c.CreatedAt)).ToListAsync(ct);
        var users = await _db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).Take(5)
            .Select(u => new ActivityDto("user", "Joined Platera", u.DisplayName, u.CreatedAt)).ToListAsync(ct);
        var latest = recipes.Concat(comments).Concat(users).OrderByDescending(a => a.At).Take(10).ToList();

        return new DashboardDto(
            await _db.Users.CountAsync(ct), await _db.Recipes.CountAsync(ct), await _db.Comments.CountAsync(ct),
            await _db.Likes.CountAsync(ct), await _db.Comments.CountAsync(c => c.IsHidden, ct), latest);
    }

    public async Task<PagedResult<AdminUserDto>> UsersAsync(string? search, PageQuery q, CancellationToken ct = default)
    {
        RequireAdmin();
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.Email!.Contains(s) || u.DisplayName.Contains(s));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(u => u.Email).Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.IsActive, u.CreatedAt, Count = _db.Recipes.Count(r => r.AuthorId == u.Id) })
            .ToListAsync(ct);

        var items = new List<AdminUserDto>();
        foreach (var r in rows)
        {
            var user = await _users.FindByIdAsync(r.Id.ToString());
            var roles = user is null ? Array.Empty<string>() : (await _users.GetRolesAsync(user)).ToArray();
            items.Add(new AdminUserDto(r.Id, r.Email!, r.DisplayName, r.IsActive, roles, r.CreatedAt, r.Count));
        }
        return new PagedResult<AdminUserDto>(items, q.SafePage, q.SafePageSize, total);
    }

    public async Task SetActiveAsync(Guid userId, SetActiveRequest request, CancellationToken ct = default)
    {
        RequireAdmin();
        if (userId == _me.UserId) throw new BadRequestException("You cannot change your own active state.");
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User not found.");
        user.IsActive = request.IsActive;
        await _users.UpdateAsync(user);
        if (!request.IsActive)
        {
            var tokens = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
            foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task SetRoleAsync(Guid userId, SetRoleRequest request, CancellationToken ct = default)
    {
        RequireAdmin();
        if (userId == _me.UserId) throw new BadRequestException("You cannot change your own role.");
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User not found.");
        var current = await _users.GetRolesAsync(user);
        if (current.Count > 0) await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, request.Role);
    }

    public async Task SetFeaturedAsync(Guid recipeId, SetFeaturedRequest request, CancellationToken ct = default)
    {
        RequireAdmin();
        var r = await _db.Recipes.FirstOrDefaultAsync(x => x.Id == recipeId, ct) ?? throw new NotFoundException("Recipe not found.");
        r.IsFeatured = request.IsFeatured;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AdminCommentDto>> CommentsAsync(bool hiddenOnly, PageQuery q, CancellationToken ct = default)
    {
        RequireAdmin();
        var query = _db.Comments.AsNoTracking().AsQueryable();
        if (hiddenOnly) query = query.Where(c => c.IsHidden);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.CreatedAt).Skip((q.SafePage - 1) * q.SafePageSize).Take(q.SafePageSize)
            .Select(c => new AdminCommentDto(c.Id, c.RecipeId, c.Recipe!.Title, c.User!.DisplayName, c.Text, c.PhotoUrl, c.IsHidden, c.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<AdminCommentDto>(items, q.SafePage, q.SafePageSize, total);
    }

    public async Task SetCommentHiddenAsync(Guid commentId, SetHiddenRequest request, CancellationToken ct = default)
    {
        RequireAdmin();
        var c = await _db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, ct) ?? throw new NotFoundException("Comment not found.");
        c.IsHidden = request.IsHidden;
        await _db.SaveChangesAsync(ct);
    }

    private void RequireAdmin() { if (!_me.IsAdmin) throw new ForbiddenException("Admin only."); }
}
