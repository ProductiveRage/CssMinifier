using System;
using CSSMinifier.FileLoaders;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class MinifyingCssLoaderTests
	{
		[Fact]
		public void EnsureUnclosedCommentsAreRemoved()
		{
			var content = "/* Test 1 */\r\np { color: blue; }\r\n/*\r\n";
			var expected = "p{color:blue}";
			
			var contentLoader = new MinifyingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void EnsureSquareBracketsAreSupported()
		{
			var content = "h2#Header a[title~=\"value\"] { font-size: 120%; }\r\n";
			var expected = "h2#Header a[title~=\"value\"]{font-size:120%}";

			var contentLoader = new MinifyingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void EnsureHyphenatedPseudoClassesAreSupported()
		{
			var content = "p.Highlight a:last-child { font-weight: bold; }\r\n";
			var expected = "p.Highlight a:last-child{font-weight:bold}";

			var contentLoader = new MinifyingCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content)
				)
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}
	}
}
