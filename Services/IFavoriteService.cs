using SmartKitchen.API.DTOs;
namespace SmartKitchen.API.Services;
public interface IFavoriteService
{
    Task<bool> AddFavoriteAsync(string userId, int recipeId);
    Task<bool> RemoveFavoriteAsync(string userId, int recipeId);
    Task<PagedResult<RecipeListDto>> GetFavoritesAsync(string userId, int page, int pageSize);
    Task<bool> IsFavoriteAsync(string userId, int recipeId);
}
