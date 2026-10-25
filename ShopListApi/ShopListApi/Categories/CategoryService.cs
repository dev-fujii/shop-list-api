using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.Categories;


/// <summary>
/// 分類の処理（一覧・1件・作成・更新・削除）
/// 今のユーザーの分類だけを扱う
/// </summary>
public class CategoryService
{

    /// <summary>
    /// 自分の作った分類の一覧を取得する
    /// </summary>
    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        return await _db.Categories
            .Where(c => c.UserId == _currentUser.Id)
            .OrderBy(c => c.Id)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name
            })
            .ToListAsync();
    }

    /// <summary>
    /// 自分の分類を1件。無い・他人の分類なら 404
    /// </summary>
    public async Task<CategoryResponse> GetByIdAsync(int id)
    {
        // 自身のユーザーIDと分類が一致してるか確認
        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUser.Id);

        // 無い・他人の分類なら 404
        if (category is null)
        {
            throw new AppException
            (
                StatusCodes.Status404NotFound,
                ErrorCodes.Categories.NotFound,
                "分類が見つかりません。"
            );
        }

        return new CategoryResponse
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    /// <summary>
    /// 分類を作る。
    /// 同じ名前がもうあれば 409
    /// </summary>
    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request)
    {
        // 自分の分類の中に、同じ名前があるか
        var exists = await _db.Categories
            .AnyAsync(c => c.UserId == _currentUser.Id && c.Name == request.Name);

        if (exists)
        {
            throw new AppException
            (
                StatusCodes.Status409Conflict,
                ErrorCodes.Categories.DuplicateName,
                "同じ名前の分類が既に存在します。"
            );
        }

        // 新しい分類を作成
        var newCategory = new Category
        {
            Name = request.Name,
            UserId = _currentUser.Id
        };

        // チェックが全部終わってから、最後に1回だけ書き込む
        _db.Categories.Add(newCategory);
        await SaveAsync();

        return new CategoryResponse
        {
            Id = newCategory.Id,
            Name = newCategory.Name
        };
    }

    /// <summary>
    /// 分類の名前を変える。
    /// 無い・他人の分類なら 404、ほかの分類に同じ名前があれば 409
    /// </summary>
    public async Task UpdateAsync(int id, UpdateCategoryRequest request)
    {
        var data = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUser.Id);

        if (data is null)
        {
            // 無い・他人の分類なら 404
            throw new AppException
            (
                StatusCodes.Status404NotFound,
                ErrorCodes.Categories.NotFound,
                "分類が見つかりません。"
            );
        }
        else
        {
            // 自分の分類の中に、同じ名前があるか
            var exists = await _db.Categories
                .AnyAsync(c => c.UserId == _currentUser.Id && c.Name == request.Name && c.Id != id);
            if (exists)
            {
                throw new AppException
                (
                    StatusCodes.Status409Conflict,
                    ErrorCodes.Categories.DuplicateName,
                    "同じ名前の分類が既に存在します。"
                );
            }
        }

        data.Name = request.Name;
        await SaveAsync();
    }

    /// <summary>
    /// 分類を消す。
    /// 無い・他人の分類なら404、取引に使われていれば409
    /// </summary>
    /// <remarks>
    /// その分類を指す分類ルールも一緒に消す所は、分類ルールを作るときに足す。
    /// </remarks>
    public async Task DeleteAsync(int id)
    {
        var data = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUser.Id);

        if (data is null)
        {
            // 無い・他人の分類なら 404
            throw new AppException
            (
                StatusCodes.Status404NotFound,
                ErrorCodes.Categories.NotFound,
                "分類が見つかりません。"
            );
        }

        if (await HasTransactionsForCategoryAsync(id))
        {
            throw CreateInUseException();
        }

        _db.Categories.Remove(data);

        // 確かめた直後に、別のリクエストがこの分類を取引に付けると、外部キーが削除を断る。
        // そのときも 500 ではなく 409 にする
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw CreateInUseException();
        }
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 今のユーザーの取引に、この分類を使っているものがあるか
    /// </summary>
    private async Task<bool> HasTransactionsForCategoryAsync(int categoryId)
    {
        return await _db.Transactions
            .AnyAsync(t => t.UserId == _currentUser.Id && t.CategoryId == categoryId);
    }

    /// <summary>
    /// 取引に使われているので消せない（409）
    /// </summary>
    private static AppException CreateInUseException()
    {
        return new AppException
        (
            StatusCodes.Status409Conflict,
            ErrorCodes.Categories.InUse,
            "取引に使われている分類は削除できません。"
        );
    }

    public CategoryService(AppDbContext db, CurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 書き込む。同じ名前で DB に断られたら 409
    /// </summary>
    /// <remarks>
    /// 先に AnyAsync で確かめていても、同じ名前が同時に2回送られると、両方が確かめを通ってしまう。
    /// そのときは DB の一意インデックス（UserId と Name）が2回目を断るので、それも 409 にする。
    /// </remarks>
    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw new AppException
            (
                StatusCodes.Status409Conflict,
                ErrorCodes.Categories.DuplicateName,
                "同じ名前の分類が既に存在します。"
            );
        }
    }

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    #endregion

}
