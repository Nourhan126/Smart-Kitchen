using System;
using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.Models;
using Twilio.TwiML.Fax;

namespace SmartKitchen.API.Services;

public class DataSeeder
{
    private readonly ApplicationDbContext _db;

    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        ApplicationDbContext db,
        ILogger<DataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(string csvFilePath)
    {
        if (!File.Exists(csvFilePath))
        {
            _logger.LogWarning(
                "CSV file not found: {Path}",
                csvFilePath
            );

            return;
        }

        _logger.LogInformation(
            "Starting dataset seeding..."
        );

        var config = new CsvConfiguration(
            CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var stream = new FileStream(
            csvFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite
        );

        using var reader =
            new StreamReader(stream);

        using var csv =
            new CsvReader(reader, config);

        await csv.ReadAsync();

        csv.ReadHeader();

        Console.WriteLine(
            "============== CSV HEADERS =============="
        );

        foreach (var h in csv.HeaderRecord)
        {
            Console.WriteLine(h);
        }

        // ================= CACHE =================

        var tagCache =
            await _db.Tags
                .ToDictionaryAsync(
                    t => t.Name,
                    StringComparer.OrdinalIgnoreCase);

        var ingredientCache =
            await _db.Ingredients
                .ToDictionaryAsync(
                    i => i.Name,
                    StringComparer.OrdinalIgnoreCase);

        // =========================================

        const int batchSize = 100;

        int count = 0;

        var recipesBatch =
            new List<Recipe>();

        while (await csv.ReadAsync())
        {
            try
            {
                // ================= TAGS =================

                var tagsList =
                    ParseList(
                        GetFieldSafe(
                            csv,
                            "tags"
                        ));

                var category =
                    tagsList.FirstOrDefault()
                    ?? "General";

                // ================= RECIPE =================

                var recipeName =
                    GetFieldSafe(
                        csv,
                        "name"
                    ).Trim();

                // ================= SKIP DUPLICATES =================

                var exists =
                    await _db.Recipes
                        .AnyAsync(r =>
                            r.Name == recipeName);

                if (exists)
                {
                    continue;
                }

                
                  
var recipe = new Recipe
{
    Name = recipeName,

    Description =
        GetFieldSafe(
            csv,
            "description"
        ),

    Minutes =
        GetIntOrDefault(
            csv,
            "minutes"
        ),

    NSteps =
        GetIntOrDefault(
            csv,
            "n_steps"
        ),

    NIngredients =
        GetIntOrDefault(
            csv,
            "n_ingredients"
        ),

    Category = category,

    Calories =
        GetDoubleOrDefault(
            csv,
            "calories"
        ),

    Fat =
        GetDoubleOrDefault(
            csv,
            "fat"
        ),

    Carbs =
        GetDoubleOrDefault(
            csv,
            "carbs"
        ),

    Fiber =
        GetDoubleOrDefault(
            csv,
            "fiber"
        ),

    ImageUrl = null
};


                Console.WriteLine(
                    $"Recipe: {recipe.Name}"
                );

                // ================= FIX CALORIES =================

                
if (recipe.Calories <= 0)
                {
                    recipe.Calories =
                        Math.Round(
                            recipe.Carbs * 4 +
                            recipe.Fat * 9 +
                            recipe.Fiber * 2,
                            1
                        );
                }
                // ================= TAGS =================

                foreach (var tagName in tagsList)
                {
                    if (string.IsNullOrWhiteSpace(
                        tagName))
                    {
                        continue;
                    }

                    if (!tagCache.TryGetValue(
                        tagName,
                        out var tag))
                    {
                        tag = new Tag
                        {
                            Name =
                                tagName.Trim()
                        };

                        _db.Tags.Add(tag);

                        tagCache[tagName] =
                            tag;
                    }

                    recipe.RecipeTags.Add(
                        new RecipeTag
                        {
                            Recipe = recipe,
                            Tag = tag
                        });
                }

                // ================= INGREDIENTS =================

                var ingredientNames =
                    ParseList(
                        GetFieldSafe(
                            csv,
                            "ingredients"
                        ));

                foreach (var ingredientName
                         in ingredientNames)
                {
                    if (string.IsNullOrWhiteSpace(
                        ingredientName))
                    {
                        continue;
                    }

                    var cleanName =
                        ingredientName.Trim();

                    if (!ingredientCache.TryGetValue(
                        cleanName,
                        out var ingredient))
                    {
                        ingredient =
                            new Ingredient
                            {
                                Name = cleanName
                            };

                        _db.Ingredients.Add(
                            ingredient
                        );

                        ingredientCache[
                            cleanName] =
                            ingredient;
                    }

                    recipe.RecipeIngredients.Add(
                        new RecipeIngredient
                        {
                            Recipe = recipe,
                            Ingredient = ingredient
                        });
                }

                // ================= STEPS =================

                var rawSteps =
                    GetFieldSafe(
                        csv,
                        "steps"
                    );

                var steps =
                    ParseList(rawSteps);

                if (steps.Count == 0)
                {
                    recipe.Steps.Add(
                        new RecipeStep
                        {
                            StepNumber = 1,

                            StepDescription =
                                "No preparation steps provided."
                        });
                }
                else
                {
                    int validStepNumber = 1;

                    foreach (var step in steps)
                    {
                        var cleanStep =
                            step?
                                .Trim()
                                .Replace("\r", "")
                                .Replace("\n", " ");

                        if (string.IsNullOrWhiteSpace(
                            cleanStep))
                        {
                            continue;
                        }

                        recipe.Steps.Add(
                            new RecipeStep
                            {
                                StepNumber =
                                    validStepNumber++,

                                StepDescription =
                                    cleanStep
                            });
                    }

                    if (!recipe.Steps.Any())
                    {
                        recipe.Steps.Add(
                            new RecipeStep
                            {
                                StepNumber = 1,

                                StepDescription =
                                    "No preparation steps provided."
                            });
                    }
                }

                recipesBatch.Add(recipe);

                count++;

                // ================= SAVE BATCH =================

                if (recipesBatch.Count >=
                    batchSize)
                {
                    await _db.Recipes.AddRangeAsync(
                        recipesBatch
                    );

                    await _db.SaveChangesAsync();

                    recipesBatch.Clear();

                    _logger.LogInformation(
                        "Seeded {Count} recipes",
                        count
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while seeding row {Row}",
                    count
                );
            }
        }

        // ================= FINAL SAVE =================

        if (recipesBatch.Any())
        {
            await _db.Recipes.AddRangeAsync(
                recipesBatch
            );

            await _db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Seeding completed successfully. Total recipes: {Count}",
            count
        );
    }

    // ================= HELPERS =================

    private static string GetFieldSafe(
        CsvReader csv,
        string field)
    {
        try
        {
            return csv.GetField(field)
                   ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<string> ParseList(
        string raw)
    {
        var matches =
            Regex.Matches(
                raw ?? "",
                @"'([^']*)'|""([^""]*)"""
            );

        return matches
            .Select(m =>
                m.Groups[1].Success
                    ? m.Groups[1].Value
                    : m.Groups[2].Value)
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x))
            .Distinct()
            .ToList();
    }

    private static int GetIntOrDefault(
        CsvReader csv,
        string field)
    {
        return int.TryParse(
            GetFieldSafe(csv, field)
                .Replace(" ", ""),
            out var value)
            ? value
            : 0;
    }

    private static double
        GetDoubleOrDefault(
            CsvReader csv,
            string field)
    {
        return double.TryParse(
            GetFieldSafe(csv, field)
                .Replace(" ", ""),
            out var value)
            ? value
            : 0;
    }
}