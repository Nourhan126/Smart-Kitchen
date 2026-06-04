using SmartKitchen.API.DTOs;

namespace SmartKitchen.API.Services;

public interface ISearchService
{
    // 🔍 search الرئيسي
    Task<PagedResult<RecipeListDto>> SearchAsync(string query, int page, int pageSize, string? userId);

    // 🔥 autocomplete
    Task<List<string>> GetAutocompleteSuggestionsAsync(string prefix, int limit = 10);

    // 💾 حفظ البحث
    Task SaveSearchAsync(string userId, string term);

    // 🕓 آخر عمليات البحث
    Task<List<string>> GetRecentSearchesAsync(string userId, int limit = 10);

    // 🗑️ مسح التاريخ
    Task ClearRecentSearchesAsync(string userId);

    // 🔥 تنفيذ search + حفظه
    Task<PagedResult<RecipeListDto>> SearchAndSaveAsync(string query, int page, int pageSize, string? userId);
}