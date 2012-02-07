using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using CSSMinifier.Lists;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will "flatten" all of the imports in a CSS file by including the imported content inline (part of the process also involves removing comments from the content). The
	/// imports must all reside in the same folder as the initial file and all must be specified by filename only - relative and absolute paths are not supported and will result
	/// in an UnsupportedStylesheetImportException being raised, as will external urls. If an import chain results in circular references, a CircularStylesheetImportException
	/// will be raised.
	/// </summary>
	public class SameFolderImportFlatteningCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		public SameFolderImportFlatteningCssLoader(ITextFileLoader contentLoader)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("cssContentLoader");

			_contentLoader = contentLoader;
		}

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty relativePath - it is up to the particular implementation whether or not to throw
		/// an exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			return GetCombinedContent(relativePath, new NonNullImmutableList<TextFileContents>());
		}

		/// <summary>
		/// This will return a TextFileContents instance creating by removing all comments and flattening all of the import declarations in a stylesheet - nested
		/// imports are handled recursively. Only imports in the same folder are supported - the imports may not have relative or absolute paths specified, nor
		/// may they be external urls - breaking these conditions will result in an UnsupportedStylesheetImportException being raised. If there are any circular
		/// references defined by the imports, a CircularStylesheetImportException will be raised. The LastModified value on the returned data will be the most
		/// recent date taken from all of the considered files.
		/// </summary>
		private TextFileContents GetCombinedContent(string relativePath, NonNullImmutableList<TextFileContents> importChain)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");
			if (importChain == null)
				throw new ArgumentNullException("importChain");

			var combinedContentFile = RemoveComments(
				_contentLoader.Load(relativePath)
			);
			foreach (var importDeclaration in GetImportDeclarations(combinedContentFile.Content))
			{
				// Ensure that the imported stylesheet is not a relative or absolute path or an external url
				if (importDeclaration.Filename.Contains("\\") || importDeclaration.Filename.Contains("/"))
					throw new UnsupportedStylesheetImportException("Imported stylesheets may not specify relative or absolute paths nor external urls: " + importDeclaration.Filename);

				// Ensure that the requested stylesheet has not been requested further up the chain - if so, throw a CircularStylesheetImportException rather than
				// waiting for a StackOverflowException to occur
				var importDeclarationFullPath = Path.Combine(
					new FileInfo(combinedContentFile.Filename).DirectoryName,
					importDeclaration.Filename
				);
				if (importChain.Any(f => f.Filename.Equals(importDeclarationFullPath, StringComparison.InvariantCultureIgnoreCase)))
					throw new CircularStylesheetImportException("Circular stylesheet import detected for file: " + importDeclaration.Filename);

				// Retrieve the content from imported file, wrap it in a media query if required and replace the import declaration with the content
				var importedFileContent = GetCombinedContent(
					importDeclaration.Filename,
					importChain.Add(combinedContentFile)
				);
				if (importDeclaration.MediaOverride != null)
				{
					importedFileContent = new TextFileContents(
						importedFileContent.Filename,
						importedFileContent.LastModified,
						String.Format(
							"@media {0} {{{1}{2}{1}}}{1}",
							importDeclaration.MediaOverride,
							Environment.NewLine,
							importedFileContent.Content
						)
					);
				}
				combinedContentFile = new TextFileContents(
					combinedContentFile.Filename,
					combinedContentFile.LastModified > importedFileContent.LastModified ? combinedContentFile.LastModified : importedFileContent.LastModified,
					combinedContentFile.Content.Replace(
						importDeclaration.Declaration,
						importedFileContent.Content
					)
				);
			}
			return combinedContentFile;
		}

		private static readonly Regex CommentRemover = new Regex(@"/\*[\d\D]*?\*/", RegexOptions.Compiled);
		private TextFileContents RemoveComments(TextFileContents content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			return new TextFileContents(
				content.Filename,
				content.LastModified,
				CommentRemover.Replace(content.Content, "")
			);
		}

		// Valid @import formats (all should be terminated with either semi-colon, line return or end of file) -
		//  @import url("test.css")
		//  @import url("test.css")
		//  @import url(test.css)
		//  @import "test.css"
		//  @import 'test.css'
		// There may be additional spaces around punctuation any may optionally have a media type/query specified after the filename.
		private static readonly Regex ImportDeclarationsMatcher = new Regex(
			String.Join("|", new[]
			{
				// @import url("test.css") screen;
				"@import\\s+url\\(\"(?<filename>.*?)\"\\)\\s*(?<media>.*?)\\s*(?:;|\r|\n)",

				// @import url("test.css") screen;
				"@import\\s+url\\('(?<filename>.*?)'\\)\\s*(?<media>.*?)\\s*(?:;|\r|\n)",

				// @import url(test.css) screen;
				"@import\\s+url\\((?<filename>.*?)\\)\\s*(?<media>.*?)\\s*(?:;|\r|\n)",

				// @import "test.css" screen;
				"@import\\s+\"(?<filename>.*?)\"\\s*(?<media>.*?)\\s*(?:;|\r|\n)",

				// @import 'test.css' screen;
				"@import\\s+'(?<filename>.*?)'\\s*(?<media>.*?)\\s*(?:;|\r|\n)"
			}),
			RegexOptions.Compiled | RegexOptions.IgnoreCase
		);

		/// <summary>
		/// This will return a list of the import declarations within a valid css file's contents that has been stripped of comments (this method apples no processing
		/// to handle comments so the content has commented-out import declaration, they will be included in the returned list).
		/// </summary>
		private static NonNullImmutableList<StylesheetImportDeclaration> GetImportDeclarations(string commentLessContent)
		{
			if (commentLessContent == null)
				throw new ArgumentNullException("minifiedContent");

			commentLessContent = commentLessContent.Trim();
			if (commentLessContent == "")
				return new NonNullImmutableList<StylesheetImportDeclaration>();

			// Note: The content needs a line return appending to the end just in case the last line is an import doesn't have a trailing semi-colon or line
			// return of its own (the Regex won't pick it up otherwise)
			var importDeclarations = new List<StylesheetImportDeclaration>();
			foreach (var match in ImportDeclarationsMatcher.Matches(commentLessContent + "\n").Cast<Match>().Where(m => m.Success))
			{
				importDeclarations.Add(new StylesheetImportDeclaration(
					match.Value,
					match.Groups["filename"].Value,
					match.Groups["media"].Value
				));
			}
			return importDeclarations.ToNonNullImmutableList();
		}

		[Serializable]
		public class UnsupportedStylesheetImportException : Exception
		{
			public UnsupportedStylesheetImportException(string message) : base(message)
			{
				if (string.IsNullOrWhiteSpace(message))
					throw new ArgumentException("Null/blank message specified");
			}
			protected UnsupportedStylesheetImportException(SerializationInfo info, StreamingContext context) : base(info, context) { }
		}

		[Serializable]
		public class CircularStylesheetImportException : Exception
		{
			public CircularStylesheetImportException(string message) : base(message)
			{
				if (string.IsNullOrWhiteSpace(message))
					throw new ArgumentException("Null/blank message specified");
			}
			protected CircularStylesheetImportException(SerializationInfo info, StreamingContext context) : base(info, context) { }
		}

		private class StylesheetImportDeclaration
		{
			public StylesheetImportDeclaration(string declaration, string filename, string mediaOverride)
			{
				if (string.IsNullOrWhiteSpace(declaration))
					throw new ArgumentException("Null/blank importDeclaration specified");
				if (string.IsNullOrWhiteSpace(filename))
					throw new ArgumentException("Null/blank filename specified");

				Declaration = declaration;
				Filename = filename;
				MediaOverride = string.IsNullOrWhiteSpace(mediaOverride) ? null : mediaOverride.ToString();
			}

			/// <summary>
			/// This will never be null or empty
			/// </summary>
			public string Declaration { get; private set; }

			/// <summary>
			/// This will never be null or empty
			/// </summary>
			public string Filename { get; private set; }

			/// <summary>
			/// This may be null but it will never be empty
			/// </summary>
			public string MediaOverride { get; private set; }
		}
	}
}
