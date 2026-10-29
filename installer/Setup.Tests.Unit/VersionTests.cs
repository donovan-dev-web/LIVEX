// SPDX-License-Identifier: GPL-3.0-or-later
using Setup.Core.Distribution;
using Xunit;

namespace Setup.Tests.Unit;

public class ProductVersionTests
{
    [Theory]
    [InlineData("0.2.0", "0.1.0", 1)]
    [InlineData("0.1.0", "0.2.0", -1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("v1.2.3", "1.2.3", 0)]
    [InlineData("1.0.0+build.7", "1.0.0", 0)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    [InlineData("1.0.0-rc.1", "1.0.0", -1)]
    [InlineData("10.0.0", "9.9.9", 1)]
    public void Compare_Respecte_SemVer(string left, string right, int expected)
    {
        Assert.Equal(expected, Math.Sign(ProductVersion.Compare(left, right)));
    }

    [Fact]
    public void Une_Version_Illisible_N_Est_Jamais_Un_Downgrade()
    {
        Assert.False(ProductVersion.IsDowngrade("inconnue", "0.1.0"));
        Assert.False(ProductVersion.IsDowngrade("0.1.0", "inconnue"));
    }

    [Fact]
    public void Downgrade_Est_Detecte()
    {
        Assert.True(ProductVersion.IsDowngrade("0.2.0", "0.1.9"));
        Assert.False(ProductVersion.IsDowngrade("0.1.0", "0.2.0"));
        Assert.False(ProductVersion.IsDowngrade("0.2.0", "0.2.0"));
    }
}
