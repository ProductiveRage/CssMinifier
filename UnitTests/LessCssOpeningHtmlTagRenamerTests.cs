using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using CSSMinifier.FileLoaders;
using UnitTests.Common;

namespace UnitTests
{
	public class LessCssOpeningHtmlTagRenamerTests
	{
		[Fact]
		public void HtmlTagWithoutNestedSelectorsShouldNotBeReplaced()
		{
			var filename = "Test.css";
			var content = "html { }";
			var textFileLoader = new LessCssOpeningHtmlTagRenamer(
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
		public void HtmlTagWithNestedSelectorShouldBeReplaced()
		{
			var filename = "Test.css";
			var content = "html { h2 { font-weight: bold; } }";
			var textFileLoader = new LessCssOpeningHtmlTagRenamer(
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

		/// <summary>
		/// This test was written with the mistaken thinking that not only would the string "html" be removed but that the opening and closing braces
		/// of the wrapping html tag be identified and potentially altered so that the tag could be removed from the final output. This is incorrect
		/// as the aim is that the replacement string need be removed from the final content but no braces as nested style declarations is only
		/// supported by LESS and they are flattened down in the compiled CSS (so "html { div.Header { color: white; } }" becomes
		/// "html div.Header { color: white; } }", from which only "html " need removing, not any of the braces). However this
		/// test identified a whitespace-removal issue when added, so it's being left in.
		/// </summary>
		[Fact]
		public void TrailingLineReturnsShouldBeSupported()
		{
			var filename = "Test.css";
			var content = "html { h2 { font-weight: bold; } }\n";
			var textFileLoader = new LessCssOpeningHtmlTagRenamer(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 1, 31, 21, 55, 0), content)
				),
				"REPLACEDCONTENT"
			);

			Assert.Equal(
				"REPLACEDCONTENT { h2 { font-weight: bold; } }\n",
				textFileLoader.Load(filename).Content
			);
		}
	}
}
