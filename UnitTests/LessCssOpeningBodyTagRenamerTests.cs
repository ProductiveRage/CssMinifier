using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using CSSMinifier.FileLoaders;
using UnitTests.Common;

namespace UnitTests
{
	public class LessCssOpeningBodyTagRenamerTests
	{
		[Fact]
		public void BodyTagWithoutNestedSelectorsShouldNotBeReplaced()
		{
			var filename = "Test.css";
			var content = "body { }";
			var textFileLoader = new LessCssOpeningBodyTagRenamer(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 1, 31, 21, 55, 0), content)
				),
				"REPLACEDCONTENT"
			);

			Assert.Equal(
				content,
				textFileLoader.Load(filename).Content
			);
		}

		[Fact]
		public void BodyTagWithNestedSelectorShouldBeReplaced()
		{
			var filename = "Test.css";
			var content = "body { h2 { font-weight: bold; } }";
			var textFileLoader = new LessCssOpeningBodyTagRenamer(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 1, 31, 21, 55, 0), content)
				),
				"REPLACEDCONTENT"
			);

			Assert.Equal(
				"REPLACEDCONTENT { h2 { font-weight: bold; } }",
				textFileLoader.Load(filename).Content
			);
		}
	}
}
