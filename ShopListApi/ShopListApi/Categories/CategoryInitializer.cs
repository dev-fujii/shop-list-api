using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.Categories;

/// <summary>
/// 今のユーザーに初期分類を用意する
/// 同じ名前の分類があれば、そのまま使う
/// </summary>
/// <remarks>
/// 初期化コマンドから呼び出す。通常の API 起動時には実行しない。
/// もう一度実行すると、削除・改名によって足りなくなった初期名も追加する。
/// </remarks>
public class CategoryInitializer
{
    /// <summary>
    /// 自分の分類に無い初期名だけを追加する
    /// </summary>
    public async Task<int> InitializeAsync()
    {
        // 自分の分類の名前だけを取る
        var existingNames = await _db.Categories
            .Where(c => c.UserId == _currentUser.Id)
            .Select(c => c.Name)
            .ToListAsync();

        var addedCount = 0;
        foreach (var name in DefaultNames)
        {
            // 同じ名前があれば、重複して作らない
            if (existingNames.Contains(name))
            {
                continue;
            }

            _db.Categories.Add(new Category
            {
                // 自分の分類として作る。ID は DB が決める
                Name = name,
                UserId = _currentUser.Id
            });
            addedCount++;
        }

        // 追加する分類をそろえてから、最後に1回だけ書き込む
        await _db.SaveChangesAsync();
        return addedCount;
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 分類の追加に使う DB と、今のユーザー情報を受け取る
    /// </summary>
    public CategoryInitializer(AppDbContext db, CurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 最初に用意する分類の名前
    /// </summary>
    private static readonly string[] DefaultNames =
    [
        "食費",
        "日用品",
        "交通費",
        "趣味",
        "固定費",
        "給料"
    ];

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    #endregion
}
