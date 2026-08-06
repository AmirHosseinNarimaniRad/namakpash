using Splitt.Core.Services;

namespace Splitt.Tests;

public class EqualSplitterTests
{
    [Theory]
    [InlineData(100_000, 4, 25_000)]
    [InlineData(90_000, 3, 30_000)]
    [InlineData(1, 1, 1)]
    public void EvenTotals_SplitExactly(decimal total, int count, decimal expectedEach)
    {
        var shares = EqualSplitter.Split(total, count);

        Assert.Equal(count, shares.Length);
        Assert.All(shares, s => Assert.Equal(expectedEach, s));
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(100_001, 2)]
    [InlineData(999_999, 7)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(123_457, 11)]
    public void UnevenTotals_SharesSumExactlyToTotal(decimal total, int count)
    {
        var shares = EqualSplitter.Split(total, count);

        Assert.Equal(total, shares.Sum());
    }

    [Fact]
    public void Remainder_GoesOneUnitEachToFirstParticipants()
    {
        // 100 / 3 => base 33, remainder 1: first person gets 34.
        var shares = EqualSplitter.Split(100, 3);
        Assert.Equal(new decimal[] { 34, 33, 33 }, shares);

        // 11 / 4 => base 2, remainder 3: first three get 3.
        shares = EqualSplitter.Split(11, 4);
        Assert.Equal(new decimal[] { 3, 3, 3, 2 }, shares);
    }

    [Fact]
    public void SharesNeverDifferByMoreThanOneUnit()
    {
        for (decimal total = 0; total < 500; total++)
        {
            for (int count = 1; count <= 9; count++)
            {
                var shares = EqualSplitter.Split(total, count);
                Assert.Equal(total, shares.Sum());
                Assert.True(shares.Max() - shares.Min() <= 1);
            }
        }
    }

    [Fact]
    public void InvalidInputs_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualSplitter.Split(100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualSplitter.Split(100, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => EqualSplitter.Split(-5, 2));
    }
}
