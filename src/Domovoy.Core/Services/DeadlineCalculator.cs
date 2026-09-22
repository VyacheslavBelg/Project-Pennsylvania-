using Domovoy.Core.Domain;
using Domovoy.Core.Reference;

namespace Domovoy.Core.Services;

/// <summary>
/// Расчёт срока ответа по нормативу.
///
/// Рабочие дни считаются без учёта переносов и праздников: производственный календарь
/// в MVP не подключён. Это заявленное упрощение — оно может сдвинуть срок на день-два
/// в новогодние и майские периоды, и на границе таких дат вывод нужно перепроверять.
/// </summary>
public static class DeadlineCalculator
{
    public static DateTimeOffset Compute(DateTimeOffset from, NormativeDeadline deadline) =>
        deadline.Unit switch
        {
            DeadlineUnit.Minutes => from.AddMinutes(deadline.Amount),
            DeadlineUnit.CalendarDays => from.AddDays(deadline.Amount),
            DeadlineUnit.BusinessDays => AddBusinessDays(from, deadline.Amount),
            _ => from.AddDays(deadline.Amount)
        };

    private static DateTimeOffset AddBusinessDays(DateTimeOffset from, int days)
    {
        var result = from;

        while (days > 0)
        {
            result = result.AddDays(1);

            if (result.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                days--;
            }
        }

        return result;
    }

    /// <summary>Человеческое описание остатка времени для карточки и напоминаний.</summary>
    public static string DescribeRemaining(DateTimeOffset deadline, DateTimeOffset now)
    {
        var left = deadline - now;

        if (left <= TimeSpan.Zero)
        {
            var over = now - deadline;
            return over.TotalDays >= 1
                ? $"срок истёк {(int)over.TotalDays} дн. назад"
                : "срок истёк";
        }

        if (left.TotalHours < 1)
        {
            return $"осталось {(int)left.TotalMinutes} мин.";
        }

        return left.TotalDays < 1
            ? $"осталось {(int)left.TotalHours} ч."
            : $"осталось {(int)left.TotalDays} дн.";
    }
}
