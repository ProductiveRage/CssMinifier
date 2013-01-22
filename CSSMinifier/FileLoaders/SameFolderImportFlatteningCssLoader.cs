using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using CSSMinifier.Lists;
using CSSMinifier.Logging;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will "flatten" all of the imports in a CSS file by including the imported content inline (part of the process also involves removing comments from the content). The
	/// imports must all reside in the same folder as the initial file and all must be specified by filename only - relative and absolute paths are not supported and will result
	/// in an UnsupportedStylesheetImportException being raised, as will external urls (or a warning being logged, depending upon the unsupportedImportBehaviour value). If an
	/// import chain results in circular references, a CircularStylesheetImportException will be raised (or a warning being logged, depending upon the specified value for
	/// circularReferenceImportBehaviour).
	/// </summary>
	public class SameFolderImportFlatteningCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		private ErrorBehaviourOptions _circularReferenceImportBehaviour, _unsupportedImportBehaviour;
		private ILogEvents _logger;
		public SameFolderImportFlatteningCssLoader(
				ITextFileLoader contentLoader,
			ErrorBehaviourOptions circularReferenceImportBehaviour,
			ErrorBehaviourOptions unsupportedImportBehaviour,
			ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), circularReferenceImportBehaviour))
				throw new ArgumentOutOfRangeException("circularReferenceImportBehaviour");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), unsupportedImportBehaviour))
				throw new ArgumentOutOfRangeException("unsupportedImportBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_circularReferenceImportBehaviour = circularReferenceImportBehaviour;
			_unsupportedImportBehaviour = unsupportedImportBehaviour;
			_logger = logger;
		}

		public enum ErrorBehaviourOptions
		{
			DisplayWarningAndIgnore,
			RaiseException
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

			return GetCombinedContent(relativePath, new NonNullImmutableList<string>());
		}

		/// <summary>
		/// This will return a TextFileContents instance creating by removing all comments and flattening all of the import declarations in a stylesheet - nested
		/// imports are handled recursively. Only imports in the same folder are supported - the imports may not have relative or absolute paths specified, nor
		/// may they be external urls - breaking these conditions will result in an UnsupportedStylesheetImportException being raised. If there are any circular
		/// references defined by the imports, a CircularStylesheetImportException will be raised. The LastModified value on the returned data will be the most
		/// recent date taken from all of the considered files.
		/// </summary>
		private TextFileContents GetCombinedContent(string relativePath, NonNullImmutableList<string> importChain)
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
				var removeImport = false;
				if (importDeclaration.RelativePath.Contains("\\") || importDeclaration.RelativePath.Contains("/"))
				{
					if (_unsupportedImportBehaviour == ErrorBehaviourOptions.DisplayWarningAndIgnore)
					{
						_logger.LogIgnoringAnyError(LogLevel.Warning, () => "Unsupported import specified: " + importDeclaration.RelativePath + " (it has been removed)");
						removeImport = true;
					}
					else
						throw new UnsupportedStylesheetImportException("Imported stylesheets may not specify relative or absolute paths nor external urls: " + importDeclaration.RelativePath);
				}

				// If the original file has a relative path (eg. "styles/Test1.css") then we'll need to include that path in the import filename (eg. "Test2.css"
				// must be transformed for "styles/Test2.css") otherwise the circular reference detection won't work and the file won't be loaded from the right
				// location when GetCombinedContent is called recursively for the import. We can use this approach to include the path on the import filename
				// since we are only supporting imports in the same location as the containing stylesheet (see above; relative or absolute paths are not
				// allowed in imports)
				StylesheetImportDeclaration importDeclarationWithConsistentFilename;
				var breakPoint = relativePath.LastIndexOfAny(new[] { '\\', '/' });
				if (breakPoint == -1)
					importDeclarationWithConsistentFilename = importDeclaration;
				else
				{
					importDeclarationWithConsistentFilename = new StylesheetImportDeclaration(
						importDeclaration.Declaration,
						relativePath.Substring(0, breakPoint + 1) + importDeclaration.RelativePath,
						importDeclaration.MediaOverride
					);
				}

				// Ensure that the requested stylesheet has not been requested further up the chain - if so, throw a CircularStylesheetImportException rather than
				// waiting for a StackOverflowException to occur (or log a warning and remove the import, depending upon specified behaviour options)
				if (importChain.Any(f => f.Equals(importDeclarationWithConsistentFilename.RelativePath, StringComparison.InvariantCultureIgnoreCase)))
				{
					if (_circularReferenceImportBehaviour == ErrorBehaviourOptions.DisplayWarningAndIgnore)
					{
						_logger.LogIgnoringAnyError(
							LogLevel.Warning,
							() => string.Format(
								"Circular import encountered: {0} (it has been removed from {1})",
								importDeclarationWithConsistentFilename.RelativePath,
								relativePath
							)
						);
						removeImport = true;
					}
					else
						throw new CircularStylesheetImportException("Circular stylesheet import detected for file: " + importDeclarationWithConsistentFilename.RelativePath);
				}

				// Retrieve the content from imported file, wrap it in a media query if required and replace the import declaration with the content
				TextFileContents importedFileContent;
				if (removeImport)
				{
					// If we want to ignore this import (meaning it's invalid and DifferentFolderImportBehaviourOptions is to log and proceed instead of throw an
					// exception) then we just want to replace the dodgy import with blank content
					importedFileContent = new TextFileContents(importDeclarationWithConsistentFilename.RelativePath, DateTime.MinValue, "");
				}
				else
				{
					importedFileContent = GetCombinedContent(
						importDeclarationWithConsistentFilename.RelativePath,
						importChain.Add(combinedContentFile.RelativePath)
					);
				}
				if ((importDeclarationWithConsistentFilename.MediaOverride != null) && !removeImport) // Don't bother wrapping an import that will be ignored in any media query content
				{
					importedFileContent = new TextFileContents(
						importedFileContent.RelativePath,
						importedFileContent.LastModified,
						String.Format(
							"@media {0} {{{1}{2}{1}}}{1}",
							importDeclarationWithConsistentFilename.MediaOverride,
							Environment.NewLine,
							importedFileContent.Content
						)
					);
				}
				combinedContentFile = new TextFileContents(
					combinedContentFile.RelativePath,
					combinedContentFile.LastModified > importedFileContent.LastModified ? combinedContentFile.LastModified : importedFileContent.LastModified,
					combinedContentFile.Content.Replace(
						importDeclarationWithConsistentFilename.Declaration,
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
				content.RelativePath,
				content.LastModified,
				CommentRemover.Replace(content.Content + "/**/", "") // Ensure that any unclosed comments are handled
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
		/// to handle comments so the content has commented-out import declaration, they will be included in the returned list). This is only public to enable unit
		/// testing of this method.
		/// </summary>
		public static NonNullImmutableList<StylesheetImportDeclaration> GetImportDeclarations(string commentLessContent)
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

		/// <summary>
		/// This is only public to enable unit testing of the GetImportDeclarations methods
		/// </summary>
		public class StylesheetImportDeclaration
		{
			public StylesheetImportDeclaration(string declaration, string relativePath, string mediaOverride)
			{
				if (string.IsNullOrWhiteSpace(declaration))
					throw new ArgumentException("Null/blank importDeclaration specified");
				if (string.IsNullOrWhiteSpace(relativePath))
					throw new ArgumentException("Null/blank relativePath specified");

				Declaration = declaration;
				RelativePath = relativePath;
				MediaOverride = string.IsNullOrWhiteSpace(mediaOverride) ? null : mediaOverride.ToString();
			}

			/// <summary>
			/// This will never be null or empty
			/// </summary>
			public string Declaration { get; private set; }

			/// <summary>
			/// This will never be null or empty
			/// </summary>
			public string RelativePath { get; private set; }

			/// <summary>
			/// This may be null but it will never be empty
			/// </summary>
			public string MediaOverride { get; private set; }
		}
	}
}
