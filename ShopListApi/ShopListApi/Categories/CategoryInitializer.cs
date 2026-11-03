using Microsoft.EntityFrameworkCore;
using ShopListApi.Data;

namespace ShopListApi.Categories;

/// <summary>
/// 指定したユーザーに初期分類を用意する
/// 同じ名前の分類があれば、そのまま使う
/// </summary>
/// <remarks>
/// 初期化コマンドから呼び出す。通常の API 起動時には実行しない。
/// もう一度実行すると、削除・改名によって足りなくなった初期名も追加する。
/// 誰の分類かは引数で受け取る。コマンドはリクエストの外で動き、名札が無いため。
/// アカウント作成を足したら、新しいユーザーの ID を渡して使える
/// </remarks>
public class CategoryInitializer
{
    /// <summary>
    /// そのユーザーの分類に無い初期名だけを追加する
    /// </summary>
    public async Task<int> InitializeAsync(Guid userId)
    {
        // そのユーザーの分類の名前だけを取る
        var existingNames = await _db.Categories
            .Where(c => c.UserId == userId)
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
                // そのユーザーの分類として作る。ID は DB が決める
                Name = name,
                UserId = userId
            });
            addedCount++;
        }

        // 追加する分類をそろえてから、最後に1回だけ書き込む
        await _db.SaveChangesAsync();
        return addedCount;
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 分類の追加に使う DB を受け取る
    /// </summary>
    public CategoryInitializer(AppDbContext db)
    {
        _db = db;
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

    #endregion
}
