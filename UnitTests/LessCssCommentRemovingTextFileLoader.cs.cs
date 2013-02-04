using System;
using CSSMinifier.FileLoaders;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class LessCssCommentRemovingTextFileLoaderTests
	{
		[Fact]
		public void BlankLinesAtTheStartOfTheFileShouldNotAffectNumbering()
		{
			var resultingContent = GetProcessedContent(
				"\n\n.ContentOnLine3 { }\n"
			);
			var expectedContent = "\n\n.ContentOnLine3 { }\n";

			Assert.Equal(expectedContent, resultingContent);
		}

		private string GetProcessedContent(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			var filename = "Test1.css";
			var textFileLoader = new FixedListCssContentLoader(
				new TextFileContents(filename, new DateTime(2013, 1, 10), content)
			);
			var processor = new LessCssCommentRemovingTextFileLoader(textFileLoader);
			return processor.Load(filename).Content;
		}
	}
}
