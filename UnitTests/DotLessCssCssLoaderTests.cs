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
			var contentLoader = GetDefaultLoader(
				new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content),
				new string[0]
			);

			Assert.Equal(
				"body.stage2 div.whatever{background:red}",
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NonNestedMarkersShouldNotBeRemoved()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1"
			};
			var content = "#test.css_1,.Woo{color:blue}";
			var tidiedContentLoader = GetDefaultLoader(
				new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content),
				insertedMarkers
			);

			Assert.Equal(
				content, // We're expecting that the content not be altered
				tidiedContentLoader.Load(filename).Content
			);
		}

		/// <summary>
		/// This test was added to address a bug that was introduced into the InjectedIdTidyingTextFileLoader where whitespace around pseudo class colons was being
		/// removed ("h2 a:hover" is NOT the same as "h2 a :hover")
		/// </summary>
		[Fact]
		public void NonNestedMarkersShouldNotBeRemovedIncludingSignificantWhitespaceAroundPseudoClassColons()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1"
			};
			var content = "#test.css_1,.Woo :hover{color:blue}";
			var tidiedContentLoader = GetDefaultLoader(
				new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content),
				insertedMarkers
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
		///   #test.css_2, .Woo h2 { font-weight: bold; }
		/// since we want to keep the real selector (".Woo h2") and also keep the most specific inserted marker ("#test.css_2")
		/// </summary>
		[Fact]
		public void OnceNestedSelectorShouldLoseTopLevelMarker()
		{
			var filename = "test.css";
			var insertedMarkers = new[]
			{
				"#test.css_1",
				"#test.css_2"
			};
			var content = "#test.css_1 #test.css_2,#test.css_1 h2,.Woo #test.css_2,.Woo h2{font-weight:bold}";
			var tidiedContentLoader = GetDefaultLoader(
				new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content),
				insertedMarkers
			);

			Assert.Equal(
				"#test.css_2,.Woo h2{font-weight:bold}",
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
				"#test.css_1",
				"#test.css_2"
			};
			var content = "#test.css_1 #test.css_2,#test.css_1>h2,.Woo #test.css_2,.Woo>h2{font-weight:bold}";
			var tidiedContentLoader = GetDefaultLoader(
				new TextFileContents(filename, new DateTime(2013, 2, 5, 18, 38, 0), content),
				insertedMarkers
			);

			Assert.Equal(
				"#test.css_2,.Woo>h2{font-weight:bold}",
				tidiedContentLoader.Load(filename).Content
			);
		}

		private ITextFileLoader GetDefaultLoader(TextFileContents content, string[] injectedMarkers)
		{
			if (content == null)
				throw new ArgumentNullException("content");
			if (injectedMarkers == null)
				throw new ArgumentNullException("injectedMarkers");

			return new DotLessCssCssLoader(
				new FixedListCssContentLoader(new[] { content }),
				() => injectedMarkers,
				"REPLACEME",
				ErrorBehaviourOptions.LogAndRaiseException,
				new NullLogger()
			);
		}
	}
}
