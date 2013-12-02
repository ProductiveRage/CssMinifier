using System;
using CSSMinifier.FileLoaders;
using CSSMinifier.Logging;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class DotLessCssCssLoaderTests
	{
		[Fact]
		public void TagToRemoveShouldBeRemovedEvenIfNotFirstSelectorSegment()
		{
			var filename = "test.css";
			var content = "REPLACEME { div.whatever { body.stage2 & { background: red; } } }";
			var contentLoader = new DotLessCssCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content)
				),
				() => new string[0],
				"REPLACEME",
				ErrorBehaviourOptions.LogAndRaiseException,
				new NullLogger()
			);

			Assert.Equal(
				"body.stage2 div.whatever{background:red}",
				contentLoader.Load(filename).Content
			);
		}
	}
}
