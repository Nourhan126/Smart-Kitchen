using System;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public class SearchService : ISearchService
{
    private readonly ApplicationDbContext _db;
    private readonly IRecipeService _recipeService;

    public SearchService(
        ApplicationDbContext db,
        IRecipeService recipeService)
    {
        _db = db;
        _recipeService = recipeService;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEARCH
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<RecipeListDto>> SearchAsync(
        string query,
        int page,
        int pageSize,
        string? userId)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResult<RecipeListDto>
            {
                Page = Math.Max(1, page),
                PageSize = 7
            };
        }

        var search =
            query.Trim().ToLower();

        return await _recipeService.GetRecipesAsync(
            new RecipeFilterRequest
            {
                Search = search,
                Page = page,
                PageSize = 7
            },
            userId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTOCOMPLETE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<string>>
        GetAutocompleteSuggestionsAsync(
            string prefix,
            int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return new List<string>();
        }

        prefix =
            prefix.Trim().ToLower();

        // Recipe names startsWith
        var nameStartsWith =
            await _db.Recipes
                .Where(r =>
                    r.Name.ToLower().StartsWith(prefix))
                .Select(r => r.Name)
                .Distinct()
                .OrderBy(n => n)
                .Take(limit)
                .ToListAsync();

        var results =
            nameStartsWith.ToList();

        var seen =
            results.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        if (results.Count < limit)
        {
            var nameContains =
                await _db.Recipes

                    .Where(r =>
                        !r.Name.ToLower().StartsWith(prefix)
                        &&
                        EF.Functions.Like(
                            r.Name.ToLower(),
                            $"%{prefix}%"))

                    .Select(r => r.Name)

                    .Distinct()

                    .OrderBy(n => n)

                    .Take(limit - results.Count)

                    .ToListAsync();

            foreach (var n in nameContains)
            {
                if (seen.Add(n))
                {
                    results.Add(n);
                }
            }
        }

        return results
            .Take(limit)
            .ToList();
    }

    public async Task SaveSearchAsync(
        string userId,
        string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        var normalized =
            term.Trim().ToLower();

        var existing =
            await _db.UserSearchHistories

                .FirstOrDefaultAsync(ush =>

                    ush.UserId == userId

                    &&

                    ush.SearchTerm.ToLower() == normalized
                );

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        if (existing is not null)
        {
            existing.SearchedAt =
                egyptNow;
        }
        else
        {
            _db.UserSearchHistories.Add(
                new UserSearchHistory
                {
                    UserId = userId,
                    SearchTerm = term.Trim(),
                    SearchedAt = egyptNow
                });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<string>>
        GetRecentSearchesAsync(
            string userId,
            int limit = 10)
    {
        var raw =
            await _db.UserSearchHistories

                .Where(ush => ush.UserId == userId)

                .OrderByDescending(ush => ush.SearchedAt)

                .Select(ush => new
                {
                    ush.SearchTerm,
                    ush.SearchedAt
                })

                .ToListAsync();

        return raw

            .GroupBy(x => x.SearchTerm.ToLower())

            .Select(g =>
                g.OrderByDescending(x => x.SearchedAt)
                 .First()
                 .SearchTerm)

            .Take(limit)

            .ToList();
    }

    public async Task ClearRecentSearchesAsync(
        string userId)
    {
        var searches =
            await _db.UserSearchHistories

                .Where(ush => ush.UserId == userId)

                .ToListAsync();

        _db.UserSearchHistories
            .RemoveRange(searches);

        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEARCH + SAVE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<RecipeListDto>>
        SearchAndSaveAsync(
            string query,
            int page,
            int pageSize,
            string? userId)
    {
        var result =
            await SearchAsync(
                query,
                page,
                pageSize,
                userId);

        if (!string.IsNullOrEmpty(userId)
            &&
            !string.IsNullOrWhiteSpace(query))
        {
            await SaveSearchAsync(
                userId,
                query);
        }

        return result;
    }

    private static string GetDifficulty(Recipe recipe)
    {
        if (recipe.Minutes > 90)
            return "Advanced";

        if (recipe.Minutes > 45)
            return "Medium";

        if (recipe.NSteps <= 8 &&
            recipe.NIngredients <= 8)
            return "Easy";

        if (recipe.NSteps <= 15 &&
            recipe.NIngredients <= 12)
            return "Medium";

        return "Advanced";
    }

    private static string GetSeasonFromRecipe(Recipe recipe)
    {
        var text = $"{recipe.Name} {recipe.Description}".ToLowerInvariant();

        if (text.Contains("summer"))
            return "Summer";

        if (text.Contains("winter"))
            return "Winter";

        if (text.Contains("spring"))
            return "Spring";

        if (text.Contains("autumn"))
            return "Autumn";

        return DateTime.Now.Month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            _ => "Autumn"
        };
    }
}
