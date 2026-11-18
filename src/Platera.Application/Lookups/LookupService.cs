using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platera.Application.Abstractions;
using Platera.Application.Common;
using Platera.Domain.Entities;

namespace Platera.Application.Lookups;

public record LookupDto(int Id, string Name);
public record SaveLookupRequest(string Name);

public class SaveLookupRequestValidator : AbstractValidator<SaveLookupRequest>
{
    public SaveLookupRequestValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(60); }
}

public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> CategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> CuisinesAsync(CancellationToken ct = default);
    Task<LookupDto> SaveCategoryAsync(int? id, SaveLookupRequest request, CancellationToken ct = default);
    Task<LookupDto> SaveCuisineAsync(int? id, SaveLookupRequest request, CancellationToken ct = default);
    Task DeleteCategoryAsync(int id, CancellationToken ct = default);
    Task DeleteCuisineAsync(int id, CancellationToken ct = default);
}

public class LookupService : ILookupService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _me;
    public LookupService(IApplicationDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task<IReadOnlyList<LookupDto>> CategoriesAsync(CancellationToken ct = default) =>
        await _db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new LookupDto(c.Id, c.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> CuisinesAsync(CancellationToken ct = default) =>
        await _db.Cuisines.AsNoTracking().OrderBy(c => c.Name).Select(c => new LookupDto(c.Id, c.Name)).ToListAsync(ct);

    public async Task<LookupDto> SaveCategoryAsync(int? id, SaveLookupRequest req, CancellationToken ct = default)
    {
        RequireAdmin();
        var name = req.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != id, ct)) throw new ConflictException("That category already exists.");
        Category entity;
        if (id is null) { entity = new Category { Name = name }; _db.Categories.Add(entity); }
        else { entity = await _db.Categories.FindAsync(new object[] { id.Value }, ct) ?? throw new NotFoundException("Category not found."); entity.Name = name; }
        await _db.SaveChangesAsync(ct);
        return new LookupDto(entity.Id, entity.Name);
    }

    public async Task<LookupDto> SaveCuisineAsync(int? id, SaveLookupRequest req, CancellationToken ct = default)
    {
        RequireAdmin();
        var name = req.Name.Trim();
        if (await _db.Cuisines.AnyAsync(c => c.Name == name && c.Id != id, ct)) throw new ConflictException("That cuisine already exists.");
        Cuisine entity;
        if (id is null) { entity = new Cuisine { Name = name }; _db.Cuisines.Add(entity); }
        else { entity = await _db.Cuisines.FindAsync(new object[] { id.Value }, ct) ?? throw new NotFoundException("Cuisine not found."); entity.Name = name; }
        await _db.SaveChangesAsync(ct);
        return new LookupDto(entity.Id, entity.Name);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var e = await _db.Categories.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Category not found.");
        if (await _db.Recipes.AnyAsync(r => r.CategoryId == id, ct)) throw new ConflictException("Category is used by recipes and cannot be deleted.");
        _db.Categories.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteCuisineAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var e = await _db.Cuisines.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Cuisine not found.");
        if (await _db.Recipes.AnyAsync(r => r.CuisineId == id, ct)) throw new ConflictException("Cuisine is used by recipes and cannot be deleted.");
        _db.Cuisines.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    private void RequireAdmin() { if (!_me.IsAdmin) throw new ForbiddenException("Admin only."); }
}
