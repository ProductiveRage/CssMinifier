using System;
using System.Collections.Generic;
using System.Linq;
using CSSMinifier.FileLoaders;
using CSSMinifier.Logging;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public class SameFolderImportFlatteningCssLoaderTests
	{
		[Fact]
		public void SingleImport()
		{
			var content = "@import url(\"Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentImport = "p { color: red; }\r\n\r\n";
			var expected = "p { color: red; }\r\np { color: blue; }";

			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content),
					new TextFileContents("Test1.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void SingleImportWithSingleNestedImport()
		{
			var content = "@import url(\"Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentImport1 = "@import url(\"Test2.css\");\r\np { color: red; }\r\n\r\n";
			var contentImport2 = "p { color: yellow; }\r\n\r\n";
			var expected = "p { color: yellow; }\r\np { color: red; }\r\np { color: blue; }";

			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content),
					new TextFileContents("Test1.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport1),
					new TextFileContents("Test2.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport2)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		[Fact]
		public void DuplicateImportWithinSingleFile()
		{
			var content = "@import url(\"Test1.css\");\r\n@import url(\"Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentImport = "p { color: red; }\r\n\r\n";
			var expected = "p { color: red; }\r\np { color: red; }\r\np { color: blue; }";

			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content),
					new TextFileContents("Test1.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);
			Assert.Equal(expected, contentLoader.Load("Test.css").Content);
		}

		/// <summary>
		/// If an import declaration is encountered which specifies a relative path, a UnsupportedStylesheetImportException should be raised (all stylesheets have to
		/// be in the same folder)
		/// </summary>
		[Fact]
		public void RelativePathImportShouldRaiseException()
		{
			var content = "@import url(\"AnotherFolder/Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);
			Assert.Throws<SameFolderImportFlatteningCssLoader.UnsupportedStylesheetImportException>(() =>
			{
				contentLoader.Load("Test.css");
			});
		}

		/// <summary>
		/// If an imported file then tries to import itself this should result in a CircularStylesheetImportException
		/// </summary>
		[Fact]
		public void SelfImportingNestedFileShouldRaiseException()
		{
			var content = "@import url(\"Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentImport = "@import url(\"Test1.css\");\r\np { color: red; }\r\n\r\n";

			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content),
					new TextFileContents("Test1.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);
			Assert.Throws<SameFolderImportFlatteningCssLoader.CircularStylesheetImportException>(() =>
			{
				contentLoader.Load("Test.css");
			});
		}


		/// <summary>
		/// If an imported file then tries to import itself this should result in a CircularStylesheetImportException
		/// </summary>
		[Fact]
		public void SelfImportingNestedFileWithRelativePathShouldRaiseException()
		{
			var content = "@import url(\"Test1.css\");\r\np { color: blue; }\r\n\r\n";
			var contentImport = "@import url(\"Test1.css\");\r\np { color: red; }\r\n\r\n";

			var contentLoader = new SameFolderImportFlatteningCssLoader(
				new FixedListCssContentLoader(
					new TextFileContents("/Styles/Test.css", new DateTime(2011, 11, 26, 14, 07, 29), content),
					new TextFileContents("/Styles/Test1.css", new DateTime(2011, 11, 26, 14, 07, 29), contentImport)
				),
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException,
				new NullLogger()
			);

			Assert.Throws<SameFolderImportFlatteningCssLoader.CircularStylesheetImportException>(() =>
			{
				contentLoader.Load("/Styles/Test.css");
			});
		}

		public class ImportDeclarationRetrieverDirect
		{
			[Fact]
			public void EnsureThatImportDeclarationMatcherSupportsDoubleQuoteSemiColonTerminationAndNoMediaQuery()
			{
				var content = "@import url(\"test.css\");";
				var expected = new[]
				{
					new SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration(content, "test.css", null)
				};

				Assert.Equal<IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration>>(
					expected,
					SameFolderImportFlatteningCssLoader.GetImportDeclarations(content),
					new StylesheetImportDeclarationSetComparer()
				);
			}

			[Fact]
			public void EnsureThatImportDeclarationMatcherSupportsDoubleQuoteSemiColonTerminationAndScreenMediaQuery()
			{
				var content = "@import url(\"test.css\") screen;";
				var expected = new[]
				{
					new SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration(content, "test.css", "screen")
				};

				Assert.Equal<IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration>>(
					expected,
					SameFolderImportFlatteningCssLoader.GetImportDeclarations(content),
					new StylesheetImportDeclarationSetComparer()
				);
			}
		}

		private class StylesheetImportDeclarationSetComparer : IEqualityComparer<IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration>>
		{
			public bool Equals(
				IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration> x,
				IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration> y)
			{
				if (x == null)
					throw new ArgumentNullException("x");
				if (y == null)
					throw new ArgumentNullException("y");

				var arrX = x.ToArray();
				var arrY = y.ToArray();
				if (arrX.Length != arrY.Length)
					return false;

				for (var index = 0; index < arrX.Length; index++)
				{
					var xi = arrX[index];
					var yi = arrY[index];
					if ((xi == null) && (yi == null))
						continue;
					if ((xi == null) || (yi == null))
						return false;
					if (!xi.Declaration.Equals(yi.Declaration, StringComparison.InvariantCultureIgnoreCase)
					|| !xi.RelativePath.Equals(yi.RelativePath, StringComparison.InvariantCultureIgnoreCase)
					|| !(xi.MediaOverride ?? "").Equals((yi.MediaOverride ?? ""), StringComparison.InvariantCultureIgnoreCase))
						return false;
				}
				return true;
			}

			public int GetHashCode(IEnumerable<SameFolderImportFlatteningCssLoader.StylesheetImportDeclaration> obj)
			{
				if (obj == null)
					throw new ArgumentNullException("obj");

				// It doesn't matter what we return here, it's the Equals implementation that's important - so returning zero here means that Equals will always
				// be consulted which saves any messing about
				return 0;
			}
		}
	}
}
