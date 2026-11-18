using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platera.Application.Common;
using Platera.Application.Images;
using Platera.Application.Lookups;
using Platera.Application.Recipes;

namespace Platera.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipes;
    public RecipesController(IRecipeService recipes) { _recipes = recipes; }

    [HttpGet]
    public async Task<ActionResult<PagedResult<RecipeSummaryDto>>> List([FromQuery] RecipeQuery query, CancellationToken ct) =>
        Ok(await _recipes.ListAsync(query, ct));

    [HttpGet("featured")]
    public async Task<ActionResult<IReadOnlyList<RecipeSummaryDto>>> Featured([FromQuery] int take = 6, CancellationToken ct = default) =>
        Ok(await _recipes.FeaturedAsync(take, ct));

    [HttpGet("trending")]
    public async Task<ActionResult<IReadOnlyList<RecipeSummaryDto>>> Trending([FromQuery] int take = 6, CancellationToken ct = default) =>
        Ok(await _recipes.TrendingAsync(take, ct));

    [Authorize, HttpGet("mine")]
    public async Task<ActionResult<PagedResult<RecipeSummaryDto>>> Mine([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await _recipes.MineAsync(query, ct));

    [Authorize, HttpGet("liked")]
    public async Task<ActionResult<PagedResult<RecipeSummaryDto>>> Liked([FromQuery] PageQuery query, CancellationToken ct) =>
        Ok(await _recipes.LikedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecipeDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _recipes.GetAsync(id, ct));

    [Authorize, HttpPost]
    public async Task<ActionResult<RecipeDetailDto>> Create(SaveRecipeRequest request, CancellationToken ct)
    {
        var created = await _recipes.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize, HttpPut("{id:guid}")]
    public async Task<ActionResult<RecipeDetailDto>> Update(Guid id, SaveRecipeRequest request, CancellationToken ct) =>
        Ok(await _recipes.UpdateAsync(id, request, ct));

    [Authorize, HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _recipes.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/lookups")]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookups;
    public LookupsController(ILookupService lookups) { _lookups = lookups; }

    [HttpGet("categories")] public async Task<ActionResult<IReadOnlyList<LookupDto>>> Categories(CancellationToken ct) => Ok(await _lookups.CategoriesAsync(ct));
    [HttpGet("cuisines")] public async Task<ActionResult<IReadOnlyList<LookupDto>>> Cuisines(CancellationToken ct) => Ok(await _lookups.CuisinesAsync(ct));
}

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IImageUploadService _images;
    public UploadsController(IImageUploadService images) { _images = images; }

    [Authorize, HttpPost("images")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ImageUploadResult>> Upload(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await _images.UploadAsync(stream, file.Length, ct));
    }
}
