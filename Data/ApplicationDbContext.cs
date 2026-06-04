using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeStepImage> RecipeStepImages => Set<RecipeStepImage>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<RecipeRecommendation> RecipeRecommendations => Set<RecipeRecommendation>();
    public DbSet<UserIngredientActivity> UserIngredientActivities => Set<UserIngredientActivity>();
    public DbSet<UserDietPreference> UserDietPreferences => Set<UserDietPreference>();
    public DbSet<UserAllergenPreference> UserAllergenPreferences => Set<UserAllergenPreference>();
    public DbSet<RecipeDietClassification> RecipeDietClassifications => Set<RecipeDietClassification>();
    public DbSet<RecipeAllergenClassification> RecipeAllergenClassifications => Set<RecipeAllergenClassification>();

    // ✅ إضافة ContactMessages
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<UserSearchHistory> UserSearchHistories => Set<UserSearchHistory>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<SafetyAlert> SafetyAlerts => Set<SafetyAlert>();
    public DbSet<ActivityLogEntry> ActivityLogEntries => Set<ActivityLogEntry>();
    public DbSet<GasControlState> GasControlStates => Set<GasControlState>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.AddHours(3), // Save
            v => v              // Read
        );

        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? v.Value.AddHours(3) : v,
            v => v
        );

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(dateTimeConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableDateTimeConverter);
                }
            }
        }

        // Recipe
        builder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                  .IsRequired()
                  .HasMaxLength(500);

            
        });

        // RecipeIngredient (Many-to-Many)
        builder.Entity<RecipeIngredient>()
            .HasKey(ri => new { ri.RecipeId, ri.IngredientId });

        builder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Recipe)
            .WithMany(r => r.RecipeIngredients)
            .HasForeignKey(ri => ri.RecipeId);

        builder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Ingredient)
            .WithMany(i => i.RecipeIngredients)
            .HasForeignKey(ri => ri.IngredientId);

        // RecipeTag (Many-to-Many)
        builder.Entity<RecipeTag>()
            .HasKey(rt => new { rt.RecipeId, rt.TagId });

        builder.Entity<RecipeTag>()
            .HasOne(rt => rt.Recipe)
            .WithMany(r => r.RecipeTags)
            .HasForeignKey(rt => rt.RecipeId);

        builder.Entity<RecipeTag>()
            .HasOne(rt => rt.Tag)
            .WithMany(t => t.RecipeTags)
            .HasForeignKey(rt => rt.TagId);

        // RecipeStep
        builder.Entity<RecipeStep>()
            .HasOne(rs => rs.Recipe)
            .WithMany(r => r.Steps)
            .HasForeignKey(rs => rs.RecipeId);

        builder.Entity<RecipeStepImage>()
            .HasOne(i => i.RecipeStep)
            .WithMany(s => s.Images)
            .HasForeignKey(i => i.RecipeStepId)
            .OnDelete(DeleteBehavior.Cascade);

        // Favorite
        builder.Entity<Favorite>()
            .HasOne(f => f.Recipe)
            .WithMany(r => r.Favorites)
            .HasForeignKey(f => f.RecipeId);

        builder.Entity<SensorReading>()
            .Property(s => s.State)
            .HasMaxLength(30);

        builder.Entity<SafetyAlert>()
            .Property(a => a.Message)
            .HasMaxLength(200);

        builder.Entity<SafetyAlert>()
            .Property(a => a.State)
            .HasMaxLength(30);

        builder.Entity<ActivityLogEntry>()
            .Property(a => a.Title)
            .HasMaxLength(100);

        builder.Entity<ActivityLogEntry>()
            .Property(a => a.Description)
            .HasMaxLength(500);

        builder.Entity<ActivityLogEntry>()
            .Property(a => a.SeverityIconType)
            .HasMaxLength(30);

        builder.Entity<MediaAsset>()
            .Property(m => m.MediaType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Entity<MediaAsset>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RecipeRecommendation>()
            .HasOne(r => r.Recipe)
            .WithMany()
            .HasForeignKey(r => r.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecipeRecommendation>()
            .HasOne(r => r.RecommendedRecipe)
            .WithMany()
            .HasForeignKey(r => r.RecommendedRecipeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecipeRecommendation>()
            .HasIndex(r => new
            {
                r.RecipeId,
                r.RecommendedRecipeId,
                r.Source,
                r.Season
            })
            .IsUnique();

        builder.Entity<Ingredient>()
            .Property(i => i.ImageUrl)
            .HasMaxLength(1000);

       

        builder.Entity<UserIngredientActivity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<UserIngredientActivity>()
            .HasOne(a => a.Recipe)
            .WithMany()
            .HasForeignKey(a => a.RecipeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<UserIngredientActivity>()
            .HasIndex(a => new { a.UserId, a.CreatedAt });

        builder.Entity<UserDietPreference>()
            .HasIndex(p => new { p.UserId, p.DietName })
            .IsUnique();

        builder.Entity<UserDietPreference>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserAllergenPreference>()
            .HasIndex(p => new { p.UserId, p.AllergenName })
            .IsUnique();

        builder.Entity<UserAllergenPreference>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecipeDietClassification>()
            .HasIndex(p => new { p.RecipeId, p.DietName })
            .IsUnique();

        builder.Entity<RecipeDietClassification>()
            .HasOne(p => p.Recipe)
            .WithMany(r => r.DietClassifications)
            .HasForeignKey(p => p.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecipeAllergenClassification>()
            .HasIndex(p => new { p.RecipeId, p.AllergenName })
            .IsUnique();

        builder.Entity<RecipeAllergenClassification>()
            .HasOne(p => p.Recipe)
            .WithMany(r => r.AllergenClassifications)
            .HasForeignKey(p => p.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
