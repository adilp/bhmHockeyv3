using FluentAssertions;
using Xunit;

namespace BHMHockey.Api.Tests;

/// <summary>
/// Sample test to verify the test framework is set up correctly.
/// Delete this file after adding real tests.
/// </summary>
public class SampleTest
{
    [Fact]
    public void TestFramework_IsConfiguredCorrectly()
    {
        // Arrange
        var expected = 4;

        // Act
        var result = 2 + 2;

        // Assert (using FluentAssertions)
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(-1, 1, 0)]
    public void TestFramework_TheoryTests_Work(int a, int b, int expected)
    {
        // Act
        var result = a + b;

        // Assert
        result.Should().Be(expected);
    }
}
