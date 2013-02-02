using System;
using CSSMinifier.FileLoaders;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class MediaQueryGroupingCssLoaderTests
	{
		[Fact]
		public void NonMediaQueryWrappedContentWillAppearFirst()
		{
			var content = "@media screen{div.Header{background:white}}div.Header{color:black}";
			var expected = "div.Header{color:black}@media screen{div.Header{background:white}}";

			var contentLoader = new MediaQueryGroupingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2012, 2, 2, 20, 43, 17), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void TwoSingleStatementMediaQuerySectionsWithMatchingConditionsAreCombined()
		{
			var content = "@media screen{div.Header{background:white}}@media screen{div.Header{color:black}}";
			var expected = "@media screen{div.Header{background:white}div.Header{color:black}}";

			var contentLoader = new MediaQueryGroupingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2012, 2, 2, 20, 43, 17), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void TwoSingleStatementMediaQuerySectionsWithMatchingConditionsAreCombinedWhenSeparatedByNonMediaQueryContent()
		{
			var content = "@media screen{div.Header{background:white}}div.Header{width:100%}@media screen{div.Header{color:black}}";
			var expected = "div.Header{width:100%}@media screen{div.Header{background:white}div.Header{color:black}}";

			var contentLoader = new MediaQueryGroupingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2012, 2, 2, 20, 43, 17), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void DifferentMediaQueriesAreNotCombined()
		{
			var content = "@media screen{div.Header{background:white}}@media print{div.Header{color:black}}";
			var expected = content;

			var contentLoader = new MediaQueryGroupingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2012, 2, 2, 20, 43, 17), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}
	}
}
