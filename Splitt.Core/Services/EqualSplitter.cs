namespace Splitt.Core.Services;

public static class EqualSplitter
{
    /// <summary>
    /// Splits <paramref name="total"/> (whole Toman) equally among <paramref name="count"/> people.
    /// The rounding remainder is distributed one unit at a time to the first participants,
    /// so the returned shares always sum exactly to the total.
    /// </summary>
    public static decimal[] Split(decimal total, int count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "تعداد نفرات باید بیشتر از صفر باشد.");
        if (total < 0)
            throw new ArgumentOutOfRangeException(nameof(total), "مبلغ نمی‌تواند منفی باشد.");

        decimal baseShare = Math.Floor(total / count);
        decimal remainder = total - baseShare * count;

        var shares = new decimal[count];
        for (int i = 0; i < count; i++)
        {
            shares[i] = baseShare;
            if (remainder >= 1)
            {
                shares[i] += 1;
                remainder -= 1;
            }
        }

        // Any sub-unit remainder (only possible for non-integer totals) goes to the first share.
        shares[0] += remainder;
        return shares;
    }
}
