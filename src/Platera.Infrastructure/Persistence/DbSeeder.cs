using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platera.Domain.Entities;

namespace Platera.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlateraDbContext>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<PlateraDbContext>>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        if (db.Database.IsRelational()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);

        foreach (var r in new[] { Roles.Admin, Roles.User })
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new AppRole(r));

        var adminEmail = cfg["Seed:AdminEmail"] ?? "admin@platera.local";
        var adminPassword = cfg["Seed:AdminPassword"];
        var admin = await users.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                log.LogWarning("Seed:AdminPassword not configured; admin user not seeded.");
            }
            else
            {
                admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, DisplayName = "Platera Admin" };
                var res = await users.CreateAsync(admin, adminPassword);
                if (res.Succeeded) await users.AddToRoleAsync(admin, Roles.Admin);
                else log.LogError("Admin seed failed: {Errors}", string.Join(", ", res.Errors.Select(e => e.Description)));
            }
        }

        if (await db.Recipes.AnyAsync(ct)) return;
        if (cfg.GetValue("Seed:SampleData", true) is false) return;

        // Sample author
        var chef = await users.FindByEmailAsync("chef@platera.local");
        if (chef is null)
        {
            chef = new AppUser { UserName = "chef@platera.local", Email = "chef@platera.local", EmailConfirmed = true, DisplayName = "Chef Maria" };
            var pwd = cfg["Seed:AdminPassword"] ?? Guid.NewGuid().ToString("N") + "aA1!";
            var res = await users.CreateAsync(chef, pwd);
            if (res.Succeeded) await users.AddToRoleAsync(chef, Roles.User);
        }

        var categories = new[] { "Breakfast", "Starter", "Main course", "Dessert", "Snack", "Soup", "Salad" }
            .Select(n => new Category { Name = n }).ToList();
        var cuisines = new[] { "Italian", "Indian", "Mexican", "Japanese", "Thai", "French", "Greek", "Lebanese", "Sri Lankan", "American" }
            .Select(n => new Cuisine { Name = n }).ToList();
        db.Categories.AddRange(categories);
        db.Cuisines.AddRange(cuisines);
        await db.SaveChangesAsync(ct);

        int C(string n) => categories.First(c => c.Name == n).Id;
        int U(string n) => cuisines.First(c => c.Name == n).Id;

        var tagCache = new Dictionary<string, Tag>();
        Tag T(string n) => tagCache.TryGetValue(n, out var t) ? t : tagCache[n] = new Tag { Name = n };

        Recipe R(int n, string title, string desc, string cuisine, string cat, int prep, int cook, int serves, Difficulty d,
                 bool featured, string[] tags, (string q, string n)[] ing, string[] steps, string alt)
        {
            var r = new Recipe
            {
                Title = title, Description = desc, CuisineId = U(cuisine), CategoryId = C(cat),
                PrepMinutes = prep, CookMinutes = cook, Servings = serves, Difficulty = d,
                AuthorId = chef!.Id, IsFeatured = featured, CreatedAt = DateTime.UtcNow.AddDays(-n),
                UpdatedAt = DateTime.UtcNow.AddDays(-n),
            };
            r.Tags = tags.Select(T).ToList();
            r.Ingredients = ing.Select((x, i) => new Ingredient { Quantity = x.q, Name = x.n, Order = i }).ToList();
            r.Steps = steps.Select((s, i) => new RecipeStep { Instruction = s, Order = i + 1 }).ToList();
            r.Images = new() { new RecipeImage { Url = $"/seed/recipe-{n}.svg", AltText = alt, Order = 0 } };
            return r;
        }

        var recipes = new List<Recipe>
        {
            R(1, "Classic Margherita Pizza", "Blistered crust, sweet tomato, creamy mozzarella and fresh basil.", "Italian", "Main course", 20, 12, 2, Difficulty.Medium, true,
              new[]{"pizza","vegetarian"}, new[]{("300 g","pizza dough"),("120 g","crushed tomatoes"),("125 g","fresh mozzarella"),("1 handful","basil leaves"),("1 tbsp","olive oil")},
              new[]{"Heat the oven to its highest setting with a tray inside.","Stretch the dough, top with tomatoes and torn mozzarella.","Bake 10-12 minutes until charred.","Finish with basil and olive oil."}, "Illustration of a pizza with basil leaves"),
            R(2, "Butter Chicken", "Tender chicken in a silky, gently spiced tomato-butter sauce.", "Indian", "Main course", 25, 35, 4, Difficulty.Medium, true,
              new[]{"curry","comfort-food"}, new[]{("600 g","chicken thigh"),("200 g","tomato puree"),("50 g","butter"),("100 ml","cream"),("2 tbsp","garam masala")},
              new[]{"Marinate the chicken in yogurt and spices for 30 minutes.","Sear the chicken until golden.","Simmer tomato puree, butter and spices, then add chicken.","Stir in cream and serve with rice or naan."}, "Illustration of a bowl of butter chicken curry"),
            R(3, "Street-Style Tacos al Pastor", "Smoky marinated pork with pineapple on warm corn tortillas.", "Mexican", "Main course", 30, 15, 4, Difficulty.Medium, false,
              new[]{"tacos","street-food"}, new[]{("500 g","pork shoulder, sliced"),("3","dried guajillo chiles"),("1 cup","pineapple chunks"),("12","corn tortillas"),("1","white onion")},
              new[]{"Blend chiles with vinegar and spices into a marinade.","Marinate pork for at least 2 hours.","Grill pork and pineapple until charred.","Serve on warm tortillas with onion and cilantro."}, "Illustration of three tacos with pineapple"),
            R(4, "Tonkotsu-Style Ramen", "Rich pork broth, springy noodles, soft egg and scallions.", "Japanese", "Soup", 30, 120, 3, Difficulty.Hard, false,
              new[]{"noodles","soup"}, new[]{("1 kg","pork bones"),("3 portions","ramen noodles"),("3","soft-boiled eggs"),("2","scallions"),("2 tbsp","miso paste")},
              new[]{"Blanch and simmer bones for two hours.","Whisk miso into the broth.","Cook noodles and assemble bowls.","Top with egg and scallions."}, "Illustration of a bowl of ramen with egg"),
            R(5, "Green Thai Curry", "Fragrant coconut curry with vegetables and fresh basil.", "Thai", "Main course", 15, 20, 4, Difficulty.Easy, true,
              new[]{"curry","vegan","coconut"}, new[]{("3 tbsp","green curry paste"),("400 ml","coconut milk"),("1","zucchini"),("200 g","firm tofu"),("1 handful","Thai basil")},
              new[]{"Fry the curry paste in a splash of coconut cream.","Add remaining coconut milk and vegetables.","Simmer 12 minutes with tofu.","Finish with Thai basil and serve over rice."}, "Illustration of a bowl of green curry"),
            R(6, "Buttery Croissants", "Flaky, golden laminated pastry worth the weekend.", "French", "Breakfast", 90, 20, 8, Difficulty.Hard, false,
              new[]{"baking","pastry"}, new[]{("500 g","bread flour"),("280 g","cold butter"),("10 g","salt"),("60 g","sugar"),("7 g","instant yeast")},
              new[]{"Make the dough and chill overnight.","Laminate with the butter block in three folds.","Shape, proof, and egg wash.","Bake at 200C until deep golden."}, "Illustration of golden croissants"),
            R(7, "Greek Salad with Feta", "Crisp vegetables, briny olives and a thick slab of feta.", "Greek", "Salad", 10, 0, 2, Difficulty.Easy, false,
              new[]{"salad","vegetarian","quick"}, new[]{("3","ripe tomatoes"),("1","cucumber"),("100 g","feta"),("1/2","red onion"),("1 handful","kalamata olives")},
              new[]{"Chop vegetables into chunky pieces.","Dress with olive oil, oregano and lemon.","Top with feta and olives."}, "Illustration of a salad with feta cheese"),
            R(8, "Creamy Hummus", "Silky chickpea dip with tahini, lemon and garlic.", "Lebanese", "Starter", 10, 0, 6, Difficulty.Easy, false,
              new[]{"dip","vegan","quick"}, new[]{("400 g","cooked chickpeas"),("3 tbsp","tahini"),("1","lemon, juiced"),("1 clove","garlic"),("3 tbsp","ice water")},
              new[]{"Blend tahini and lemon until pale.","Add chickpeas and garlic and blend until smooth.","Loosen with ice water.","Serve with olive oil and paprika."}, "Illustration of a bowl of hummus with olive oil"),
            R(9, "Kottu Roti", "Sri Lankan chopped flatbread stir-fried with vegetables and egg.", "Sri Lankan", "Main course", 20, 15, 2, Difficulty.Medium, true,
              new[]{"street-food","spicy"}, new[]{("4","roti, shredded"),("2","eggs"),("1 cup","shredded cabbage"),("1","carrot"),("1 tbsp","curry powder")},
              new[]{"Stir-fry onion, carrot and cabbage with curry powder.","Push aside, scramble the eggs.","Add shredded roti and chop with two spatulas.","Serve hot with curry sauce."}, "Illustration of a plate of kottu roti"),
            R(10, "Fudgy Chocolate Brownies", "Crackly top, dense fudgy centre.", "American", "Dessert", 15, 25, 12, Difficulty.Easy, false,
              new[]{"chocolate","baking"}, new[]{("200 g","dark chocolate"),("150 g","butter"),("200 g","sugar"),("3","eggs"),("80 g","flour")},
              new[]{"Melt chocolate with butter.","Whisk in sugar and eggs until glossy.","Fold in flour.","Bake at 175C for 25 minutes."}, "Illustration of chocolate brownie squares"),
            R(11, "Spaghetti alla Carbonara", "Eggs, pecorino, guanciale and black pepper — no cream.", "Italian", "Main course", 10, 15, 2, Difficulty.Medium, false,
              new[]{"pasta","quick"}, new[]{("200 g","spaghetti"),("100 g","guanciale"),("3","egg yolks"),("50 g","pecorino romano"),("1 tsp","black pepper")},
              new[]{"Render guanciale until crisp.","Boil pasta, reserving a cup of water.","Mix yolks and pecorino into a paste.","Toss pasta off the heat with guanciale and egg mixture, loosening with water."}, "Illustration of a plate of spaghetti carbonara"),
            R(12, "Avocado Toast with Poached Egg", "A simple, satisfying breakfast.", "American", "Breakfast", 5, 5, 1, Difficulty.Easy, false,
              new[]{"breakfast","quick","vegetarian"}, new[]{("2 slices","sourdough"),("1","ripe avocado"),("1","egg"),("1 pinch","chili flakes"),("1 tsp","lemon juice")},
              new[]{"Toast the bread.","Mash avocado with lemon and salt.","Poach the egg for 3 minutes.","Assemble and finish with chili flakes."}, "Illustration of avocado toast topped with an egg"),
        };

        db.Recipes.AddRange(recipes);
        await db.SaveChangesAsync(ct);

        // a few sample likes/ratings from the admin and chef
        if (admin is not null)
        {
            foreach (var r in recipes.Take(6))
            {
                db.Likes.Add(new Like { RecipeId = r.Id, UserId = admin.Id });
                db.Ratings.Add(new Rating { RecipeId = r.Id, UserId = admin.Id, Stars = 4 + (r.Title.Length % 2) });
            }
            db.Comments.Add(new Comment { RecipeId = recipes[0].Id, UserId = admin.Id, Text = "Tried this tonight, the crust was perfect!" });
            await db.SaveChangesAsync(ct);
        }
        log.LogInformation("Seeded {Count} sample recipes", recipes.Count);
    }
}
