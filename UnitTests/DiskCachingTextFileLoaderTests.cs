using System;
using System.Collections.Generic;
using System.IO;
using CSSMinifier.FileLoaders;
using Xunit;

namespace UnitTests
{
	public class DiskCachingTextFileLoaderTests
	{
		/// <summary>
		/// This only tests the most basic case, edge cases will need additional tests
		/// </summary>
		[Fact]
		public void StraightDownTheRoadCopyShouldSucceed()
		{
			var source = new TextFileContents(
				"styles.css",
				new DateTime(2013, 2, 6, 18, 19, 0),
				".Woo{color:black}"
			);

			var contentRepresentation = DiskCachingTextFileLoader.GetFileContentRepresentation(source);
			
			TextFileContents copy;
			using (var reader = new StringReader(contentRepresentation))
			{
				copy = DiskCachingTextFileLoader.GetFileContents(reader);
			}

			Assert.Equal<TextFileContents>(
				source,
				copy,
				new TextFileContentsComparer()
			);
		}

		private class TextFileContentsComparer : IEqualityComparer<TextFileContents>
		{
			public bool Equals(TextFileContents x, TextFileContents y)
			{
				if (x == null)
					throw new ArgumentNullException("x");
				if (y == null)
					throw new ArgumentNullException("y");

				return (
					(x.Content == y.Content) &&
					(x.LastModified == y.LastModified) &&
					(x.RelativePath == y.RelativePath)
				);
			}

			public int GetHashCode(TextFileContents obj)
			{
				// Doesn't matter what we return here for comparison purposes, it's fine if all instances get the same hash code, it's the
				// Equals method that's important
				return 0; 
			}
		}
	}
}
