namespace DocumentStorage.UnitTests;

using AwesomeAssertions;
using DocumentStorage.Api.Documents;

public sealed class DocumentIdTests
{
	[Fact]
	public void Should_ParseTheSameId_When_ValueIsAGeneratedId()
	{
		// Arrange
		var expected = DocumentId.New();

		// Act
		var parsed = DocumentId.TryParse(expected.ToString(), out var actual);

		// Assert
		parsed.Should().BeTrue();
		actual.Should().Be(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("not-a-guid")]
	[InlineData("../../etc/passwd")]
	[InlineData("00000000-0000-0000-0000-000000000000")]
	public void Should_Reject_When_ValueIsNotAValidId(string? value)
	{
		// Act
		var actual = DocumentId.TryParse(value, out _);

		// Assert
		actual.Should().BeFalse();
	}

	[Fact]
	public void Should_GenerateDistinctIds_When_CalledTwice()
	{
		// Arrange
		var first = DocumentId.New();

		// Act
		var actual = DocumentId.New();

		// Assert
		actual.Should().NotBe(first);
	}
}
