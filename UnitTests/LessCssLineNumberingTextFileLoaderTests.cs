using System;
using CSSMinifier.FileLoaders;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class LessCssLineNumberingTextFileLoaderTests
	{
		[Fact]
		public void NestedSelectorsShouldGetMarkersAsShouldTheTopLevelSelector()
		{
			var content = "body\n{\n  div.Header\n  {\n    color: black;\n  }\n}\n";
			var expected = "#test.css_1,body\n{#test.css_3,\n  div.Header\n  {\n    color: black;\n  }\n}\n";

			var filename = "test.css";
			var lineNumberInjectingContentLoader = new LessCssLineNumberingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 21, 18, 0), content)
				),
				(relativePath, lineNumber) => "#test.css_" + lineNumber + ","
			);

			Assert.Equal(
				expected,
				lineNumberInjectingContentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void StylePropertyNamesShouldNotGetMarkers()
		{
			var content = "body\n{\n  color: black;\n}\n";
			var expected = "#test.css_1,body\n{\n  color: black;\n}\n";

			var filename = "test.css";
			var lineNumberInjectingContentLoader = new LessCssLineNumberingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 21, 18, 0), content)
				),
				(relativePath, lineNumber) => "#test.css_" + lineNumber + ","
			);

			Assert.Equal(
				expected,
				lineNumberInjectingContentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void StylePropertyNamesShouldNotGetMarkersWhenFollowedByNestedSelectorWhichShould()
		{
			var content = "body\n{\n  color: black;\n  div.Header { background: white; }\n}\n";
			var expected = "#test.css_1,body\n{\n  color: black;#test.css_4,\n  div.Header { background: white; }\n}\n";

			var filename = "test.css";
			var lineNumberInjectingContentLoader = new LessCssLineNumberingTextFileLoader(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2013, 2, 5, 21, 18, 0), content)
				),
				(relativePath, lineNumber) => "#test.css_" + lineNumber + ","
			);

			Assert.Equal(
				expected,
				lineNumberInjectingContentLoader.Load(filename).Content
			);
		}
	}
}
