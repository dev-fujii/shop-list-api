using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ShopListApi.Data;

/// <summary>
/// DB に書き込めなかったときの例外（DbUpdateException）を見分ける
/// </summary>
public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// 一意インデックス（同じ値は1つだけ）に断られたか
    /// </summary>
    /// <remarks>
    /// PostgreSQL は、一意インデックスにぶつかるとエラーコード 23505（unique_violation）を返す。
    /// EF Core は、それを DbUpdateException の InnerException に入れて投げる。
    /// </remarks>
    public static bool IsUniqueViolation(this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
