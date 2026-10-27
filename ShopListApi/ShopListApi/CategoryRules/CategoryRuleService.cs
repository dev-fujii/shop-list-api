using Microsoft.EntityFrameworkCore;
using ShopListApi.Common;
using ShopListApi.Data;

namespace ShopListApi.CategoryRules;

/// <summary>
/// 分類ルールの一覧取得・1件取得・登録を行う。
/// </summary>
/// <remarks>
/// 今のユーザーが所有するルールだけを扱う。
/// </remarks>
public class CategoryRuleService
{
    /// <summary>
    /// 今のユーザーの分類ルールをページ単位で取得する。
    /// </summary>
    /// <remarks>
    /// ルールIDの昇順で取得する。
    /// 指定ページにデータが無い場合も、一覧を空にして全体件数を返す。
    /// </remarks>
    public async Task<PagedResponse<CategoryRuleResponse>> GetAllAsync(int page, int pageSize)
    {
        // 今のユーザーのルールだけを一覧に出す。
        var query = _db.CategoryRules.Where(r => r.UserId == _currentUser.Id);
        var totalCount = await query.CountAsync();

        // 大きなページ番号でも掛け算があふれないよう、先に long にする。
        var skip = ((long)page - 1) * pageSize;
        var items = new List<CategoryRuleResponse>();
        if (skip < totalCount)
        {
            items = await query
                .OrderBy(r => r.Id)
                .Skip((int)skip)
                .Take(pageSize)
                .Select(r => new CategoryRuleResponse
                {
                    Id = r.Id,
                    Keyword = r.Keyword,
                    CategoryId = r.CategoryId
                })
                .ToListAsync();
        }

        return new PagedResponse<CategoryRuleResponse>
        (
           items,
           page,
           pageSize,
           totalCount   
        );
    }

    /// <summary>
    /// 指定IDの分類ルールを、今のユーザーのものに限って取得する。
    /// </summary>
    public async Task<CategoryRuleResponse> GetByIdAsync(int id)
    {
        // IDが合っていても、他人のルールは取得させない。
        var rule = await _db.CategoryRules
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == _currentUser.Id);

        if (rule is null)
        {
            throw new AppException(
                StatusCodes.Status404NotFound,
                ErrorCodes.CategoryRules.NotFound,
                "分類ルールが見つかりません。");
        }

        return ToResponse(rule);
    }

    /// <summary>
    /// 今のユーザーの分類ルールを登録する。
    /// </summary>
    /// <remarks>
    /// 必須項目や文字数など、入力形式の検証が済んでいることを前提とする。
    /// 指定した分類の存在・所有者を先に確認し、その後でキーワードの重複を確認する。
    /// 両方に問題がある場合は、分類のエラーを優先する。
    /// </remarks>
    public async Task<CategoryRuleResponse> CreateAsync
    (
        CreateCategoryRuleRequest request
    )
    {
        // 自分の分類かを先に確認し、分類とキーワードが両方不正なら分類のエラーを返す。
        var ownsCategory = await _db.Categories.AnyAsync
        (
            c => c.Id == request.CategoryId && 
            c.UserId == _currentUser.Id
        );

        if (!ownsCategory)
        {
            throw CreateCategoryNotFoundException();
        }

        // 同じユーザーが登録した同じキーワードだけを重複とする。
        var duplicate = await _db.CategoryRules.AnyAsync
        (
            r => r.UserId == _currentUser.Id && 
            r.Keyword == request.Keyword
        );

        if (duplicate)
        {
            throw CreateDuplicateKeywordException();
        }

        var rule = new CategoryRule
        {
            UserId = _currentUser.Id,
            Keyword = request.Keyword,
            CategoryId = request.CategoryId!.Value
        };

        _db.CategoryRules.Add(rule);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            // 確かめた後に分類が削除された場合も、500にしない。
            throw CreateCategoryNotFoundException();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // 並行した登録で分類も消えていた場合は、先に分類のエラーを返す。
            if 
            (
                !await _db.Categories.AnyAsync
                (
                    c => c.Id == rule.CategoryId && 
                    c.UserId == _currentUser.Id
                )
            )
            {
                throw CreateCategoryNotFoundException();
            }

            throw CreateDuplicateKeywordException();
        }

        return ToResponse(rule);
    }

    #region // --- 内部処理 --- //

    /// <summary>
    /// 分類ルールを、APIで返すレスポンスに変換する。
    /// </summary>
    private static CategoryRuleResponse ToResponse(CategoryRule rule) => new()
    {
        Id = rule.Id,
        Keyword = rule.Keyword,
        CategoryId = rule.CategoryId
    };

    /// <summary>
    /// 指定した分類が使えない場合に返す例外を作る。
    /// </summary>
    /// <remarks>
    /// 分類が存在しない場合、他人の分類の場合、保存前に分類が削除された場合に使う。
    /// </remarks>
    private static AppException CreateCategoryNotFoundException()
    {
        // 指定した分類が無い・他人のものの場合は、入力内容のエラーとして422を返す。
        return new AppException
        (
            StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.Categories.NotFound,
            "分類が見つかりません。"   
        );
    }

    /// <summary>
    /// 同じユーザーが同じキーワードを登録しようとした場合に返す例外を作る。
    /// </summary>
    private static AppException CreateDuplicateKeywordException() => new
    (
        StatusCodes.Status409Conflict,
        ErrorCodes.CategoryRules.DuplicateKeyword,
        "同じキーワードの分類ルールが既に存在します。"
    );

    /// <summary>
    /// 分類ルールの処理に使うDBと、今のユーザー情報を受け取る。
    /// </summary>
    public CategoryRuleService(AppDbContext db, CurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private readonly AppDbContext _db;
    private readonly CurrentUser _currentUser;

    #endregion
}
