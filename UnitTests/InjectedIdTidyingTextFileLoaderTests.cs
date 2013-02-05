using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using CSSMinifier.FileLoaders;
using UnitTests.Common;

namespace UnitTests
{
	public class InjectedIdTidyingTextFileLoaderTests
	{
		[Fact]
		public void NonNestedMarkersShouldNotBeRemoved()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1,"
			};
			var content = "#test.css_1,.Woo{color: blue;}";
			var tidiedContentLoader = new InjectedIdTidyingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content)
				),
				() => insertedMarkers
			);

			Assert.Equal(
				content, // We're expecting that the content not be altered
				tidiedContentLoader.Load(filename).Content
			);
		}

		/// <summary>
		/// eg.
		///   #test.css_1, .Woo { 
		///     #test.css_2, h2 { font-weight: bold; }
		///   }
		/// is translated into
		///   #test.css_1 #test.css_2, #test.css_1 h2, .Woo #test.css_2, .Woo h2 { font-weight: bold; }
		/// but we want it to be
		///   .Woo #test.css_2, .Woo h2 { font-weight: bold; }
		/// since we want to keep the real selector (".Woo h2") but also keep the most specific marker (".Woo #test.css_2")
		/// </summary>
		[Fact]
		public void OnceNestedSelectorShouldLoseTopLevelMarker()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1,",
				"#test.css_2,"
			};
			var content = "#test.css_1 #test.css_2,#test.css_1 h2,.Woo #test.css_2,.Woo h2{font-weight:bold;}";
			var tidiedContentLoader = new InjectedIdTidyingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content)
				),
				() => insertedMarkers
			);

			Assert.Equal(
				".Woo #test.css_2,.Woo h2{font-weight:bold;}",
				tidiedContentLoader.Load(filename).Content
			);
		}

		/// <summary>
		/// This is the same principle as OnceNestedSelectorShouldLoseTopLevelMarker but it ensures that child selectors are handled correctly
		/// (we won't be able to rely on breaking selectors on whitespace if there are child selectors in minified content)
		/// </summary>
		[Fact]
		public void OnceNestedChildSelectorShouldLoseTopLevelMarker()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1,",
				"#test.css_2,"
			};
			var content = "#test.css_1 #test.css_2,#test.css_1>h2,.Woo #test.css_2,.Woo>h2{font-weight:bold;}";
			var tidiedContentLoader = new InjectedIdTidyingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content)
				),
				() => insertedMarkers
			);

			Assert.Equal(
				".Woo #test.css_2,.Woo>h2{font-weight:bold;}",
				tidiedContentLoader.Load(filename).Content
			);
		}
	}
}
