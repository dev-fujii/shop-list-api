using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ShopListApi.Summaries;

/// <summary>
/// 集計の取得で受け取る条件
/// 月か、開始日と終了日を指定する
/// </summary>
public class SummaryQuery : IValidatableObject
{
    /// <summary>
    /// 集計する月。yyyy-MM の形で指定する
    /// </summary>
    public string? Month { get; set; }

    /// <summary>
    /// 集計の開始日。To と一緒に指定する
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// 集計の終了日。From と一緒に指定する
    /// </summary>
    public DateOnly? To { get; set; }

    /// <summary>
    /// 月と期間の指定方法、日付の前後関係を確認する
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Month is not null)
        {
            if (From.HasValue || To.HasValue)
            {
                yield return new ValidationResult
                (
                    "month と from・to は同時に指定できません。",
                    [nameof(Month), nameof(From), nameof(To)]
                );
            }

            if
            (
                !DateOnly.TryParseExact
                (
                    Month + "-01",
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _
                )
            )
            {
                yield return new ValidationResult
                (
                    "month は存在する年月を yyyy-MM で指定してください。",
                    [nameof(Month)]
                );
            }
        }
        else if (From.HasValue != To.HasValue)
        {
            yield return new ValidationResult
            (
                "from と to は両方指定してください。",
                [nameof(From), nameof(To)]
            );
        }

        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult
            (
                "from は to 以前の日付にしてください。",
                [nameof(From), nameof(To)]
            );
        }
    }
}
