using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopListApi.Summaries;

namespace ShopListApi.Tests;

/// <summary>
/// 集計する期間と、条件の組み合わせのテスト
/// DB を使わず、月末・年またぎ・不正な条件を確認する
/// </summary>
[TestClass]
public class SummaryTests
{
    /// <summary>
    /// 月を指定したとき、1日から末日までになることを確認する
    /// うるう年の2月と、次の月を作れない9999年12月も対象にする
    /// </summary>
    [TestMethod]
    [DataRow("2024-02", "2024-02-29")]
    [DataRow("2026-02", "2026-02-28")]
    [DataRow("2026-04", "2026-04-30")]
    [DataRow("9999-12", "9999-12-31")]
    public void MonthIncludesItsFirstAndLastDay(string month, string lastDay)
    {
        var actual = SummaryPeriod.Resolve
        (
            new SummaryQuery { Month = month },
            new DateOnly(2026, 10, 28),
            3
        );

        Assert.AreEqual
        (
            DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture),
            actual.From
        );
        Assert.AreEqual
        (
            DateOnly.ParseExact(lastDay, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            actual.To
        );
    }

    /// <summary>
    /// 1月に3か月分を集計するとき、前年11月1日から1月末までになることを確認する
    /// </summary>
    [TestMethod]
    public void DefaultPeriodCrossesTheYearAndIncludesTheCurrentMonth()
    {
        var actual = SummaryPeriod.Resolve(new SummaryQuery(), new DateOnly(2026, 1, 15), 3);
        Assert.AreEqual(new DateOnly(2025, 11, 1), actual.From);
        Assert.AreEqual(new DateOnly(2026, 1, 31), actual.To);
    }

    /// <summary>
    /// 既定の月数を変えると、集計の開始月も変わることを確認する
    /// </summary>
    [TestMethod]
    [DataRow(1, 10)]
    [DataRow(6, 5)]
    public void DefaultPeriodUsesTheConfiguredMonthCount(int monthCount, int expectedFirstMonth)
    {
        var actual = SummaryPeriod.Resolve
        (
            new SummaryQuery(),
            new DateOnly(2026, 10, 28),
            monthCount
        );
        Assert.AreEqual(new DateOnly(2026, expectedFirstMonth, 1), actual.From);
        Assert.AreEqual(new DateOnly(2026, 10, 31), actual.To);
    }

    /// <summary>
    /// 開始日と終了日が同じ場合も、その1日を集計できることを確認する
    /// </summary>
    [TestMethod]
    public void ExplicitPeriodCanBeASingleDay()
    {
        var day = new DateOnly(2004, 2, 29);
        var actual = SummaryPeriod.Resolve
        (
            new SummaryQuery { From = day, To = day },
            new DateOnly(2026, 10, 28),
            3
        );
        Assert.AreEqual(day, actual.From);
        Assert.AreEqual(day, actual.To);
    }

    /// <summary>
    /// 不正な年月、月と期間の併用、期間の片方だけ、開始日と終了日の逆転を拒否する
    /// </summary>
    [TestMethod]
    public void InvalidMonthsAndConflictingOrIncompletePeriodsAreRejected()
    {
        var day = new DateOnly(2026, 10, 28);
        SummaryQuery[] invalid =
        [
            new() { Month = "2026-13" },
            new() { Month = "2026-2" },
            new() { Month = "0000-01" },
            new() { Month = "2026-10", From = day, To = day },
            new() { Month = "2026-10", From = day },
            new() { From = day },
            new() { To = day },
            new() { From = day, To = day.AddDays(-1) }
        ];

        foreach (var query in invalid)
        {
            var errors = new List<ValidationResult>();
            Assert.IsFalse
            (
                Validator.TryValidateObject(query, new ValidationContext(query), errors, true),
                $"不正な条件を受け付けた: month={query.Month}, from={query.From}, to={query.To}"
            );
            Assert.IsNotEmpty(errors);
        }
    }

    /// <summary>
    /// 条件なし、正しい月、開始日と終了日の組み合わせを受け付けることを確認する
    /// </summary>
    [TestMethod]
    public void DefaultMonthAndCompleteRangeAreValid()
    {
        var day = new DateOnly(2026, 10, 28);
        SummaryQuery[] valid =
        [
            new(),
            new() { Month = "2024-02" },
            new() { From = day, To = day }
        ];

        foreach (var query in valid)
        {
            var errors = new List<ValidationResult>();
            Assert.IsTrue(Validator.TryValidateObject(query, new ValidationContext(query), errors, true));
        }
    }
}
