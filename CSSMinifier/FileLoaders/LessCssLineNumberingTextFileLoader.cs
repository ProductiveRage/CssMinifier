using System;
using System.Linq;
using System.Text;
using CSSParser;
using CSSParser.ContentProcessors;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will insert markers into style declaration headers that may indicate which line number the declaration header ended on. If a marker format is specified that injects a new
	/// html id then minified / combined style declarations can be mapped back to their source file and location (in this case the marker format would specify a fixed filename per
	/// file and include the line number). The markerFormat will have the string "{0}" replaced with the relativePath (altered to try to generate html-id-friendly strings) and
	/// "{1}" with the appropriate line number. This will work best on content that has has comments removed since there is no provision in this code to ignore comments
	/// (if comments are not removed then feasibly some markers will not be inserted correctly or at all).
	/// WARNING: This will not insert markers if the declaration header is wrapped in a media query. If LessCSS is used then media queries may be nested inside declarations,
	/// this WILL still insert markers as expected.
	/// </summary>
	public class LessCssLineNumberingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		private readonly MarkerGenerator _markerGenerator;
		private readonly Predicate<string> _optionalSelectorMarkerInsertionCondition;
		public LessCssLineNumberingTextFileLoader(ITextFileLoader fileLoader, MarkerGenerator markerGenerator, Predicate<string> optionalSelectorMarkerInsertionCondition)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");
			if (markerGenerator == null)
				throw new ArgumentNullException("markerGenerator");
			if (optionalSelectorMarkerInsertionCondition == null)
				throw new ArgumentNullException("optionalSelectorMarkerInsertionCondition");

			_fileLoader = fileLoader;
			_markerGenerator = markerGenerator;
			_optionalSelectorMarkerInsertionCondition = optionalSelectorMarkerInsertionCondition;
		}

		/// <summary>
		/// This will never be called with a null or blank relativePath nor a lineNumber of zero or less. It may return null or blank, meaning no insertion will be made.
		/// The returned content (if any) is not escaped so may alter the markup if required.
		/// </summary>
		public delegate string MarkerGenerator(string relativePath, int lineNumber);

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty filename - it is up to the particular implementation whether or not to throw an
		/// exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var unprocessedContent = _fileLoader.Load(relativePath);
			return new TextFileContents(
				unprocessedContent.RelativePath,
				unprocessedContent.LastModified,
				Process(unprocessedContent.Content, relativePath)
			);
		}

		/// <summary>
		/// This will throw an exception for null content or if otherwise unable to satisfy the request, it will never return null
		/// </summary>
		private string Process(string content, string relativePath)
		{
			if (content == null)
				throw new ArgumentNullException("content");
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			// Standardise line breaks to newline characters only
			content = content.Replace("\r\n", "\n").Replace('\r', '\n');

			// We're going to process backwards through the data so we need the total number of lines there are so that we know the number line we're starting on
			var lineNumber = content.Split('\n').Length;
			IAnalyseCharacters contentAnalyser = new StandardContentAnalyser();
			var stringBuilder = new StringBuilder();
			var selectorBuilder = new StringBuilder();
			for (var index = content.Length - 1; index >= 0; index--)
			{
				var currentCharacter = content[index];
				var analysisResult = contentAnalyser.Analyse(
					currentCharacter,
					(index > 0) ? (char?)content[index - 1] : null
				);
				if (analysisResult.MarkerInsertionType == MarkerInsertionTypeOptions.InsertAfterCurrentCharacter)
				{
					if ((_optionalSelectorMarkerInsertionCondition == null) || IsAcceptableToInsertHere(selectorBuilder.ToString()))
						stringBuilder.Insert(0, _markerGenerator(relativePath, lineNumber + analysisResult.MarkerLineNumberOffset) ?? "");
					selectorBuilder.Clear();
				}
				stringBuilder.Insert(0, currentCharacter);
				selectorBuilder.Insert(0, currentCharacter);
				if (analysisResult.MarkerInsertionType == MarkerInsertionTypeOptions.InsertBeforeCurrentCharacter)
				{
					if ((_optionalSelectorMarkerInsertionCondition == null) || IsAcceptableToInsertHere(selectorBuilder.ToString()))
						stringBuilder.Insert(0, _markerGenerator(relativePath, lineNumber + analysisResult.MarkerLineNumberOffset) ?? "");
					selectorBuilder.Clear();
				}
				contentAnalyser = analysisResult.NextAnalyser;

				if (currentCharacter == '\n')
					lineNumber--;
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// If an optionalSelectorMarkerInsertionCondition has been specified, then pass it the selector content from the point at which a marker insertion is
		/// possible (an optionalSelectorMarkerInsertionCondition may be passed in that prevents markers from being insert into scope-restricting html selectors
		/// in order to reduce the nesting of marker ids, for example)
		/// </summary>
		private bool IsAcceptableToInsertHere(string styleContentAtInsertionPoint)
		{
			if (styleContentAtInsertionPoint == null)
				throw new ArgumentNullException("styleContentAtInsertionPoint");

			if (_optionalSelectorMarkerInsertionCondition == null)
				return true;

			return _optionalSelectorMarkerInsertionCondition(
				string.Join(
					"",
					Parser.ParseLESS(styleContentAtInsertionPoint)
						.TakeWhile(c => c.CharacterCategorisation != CharacterCategorisationOptions.OpenBrace)
						.Where(c => c.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty)
						.Select(c => c.Value)
				)
			);
		}

		private interface IAnalyseCharacters
		{
			AnalysisResult Analyse(char currentCharacter, char? previousCharacter);
		}

		private class AnalysisResult
		{
			public static AnalysisResult InsertAfterCurrentCharacter(int markerLineNumberOffset, IAnalyseCharacters nextProcessor)
			{
				return new AnalysisResult(MarkerInsertionTypeOptions.InsertAfterCurrentCharacter, markerLineNumberOffset, nextProcessor);
			}
			public static AnalysisResult InsertBeforeCurrentCharacter(int markerLineNumberOffset, IAnalyseCharacters nextProcessor)
			{
				return new AnalysisResult(MarkerInsertionTypeOptions.InsertBeforeCurrentCharacter, markerLineNumberOffset, nextProcessor);
			}
			public static AnalysisResult NoInsertion(IAnalyseCharacters nextProcessor)
			{
				return new AnalysisResult(MarkerInsertionTypeOptions.NoInsertion, 0, nextProcessor);
			}

			private AnalysisResult(MarkerInsertionTypeOptions markerInsertionType, int markerLineNumberOffset, IAnalyseCharacters nextProcessor)
			{
				if (!Enum.IsDefined(typeof(MarkerInsertionTypeOptions), markerInsertionType))
					throw new ArgumentOutOfRangeException("markerInsertionType");
				if (markerLineNumberOffset < 0)
					throw new ArgumentOutOfRangeException("markerLineNumberOffset", "must be zero or greater");
				if (nextProcessor == null)
					throw new ArgumentNullException("nextProcessor");

				MarkerInsertionType = markerInsertionType;
				MarkerLineNumberOffset = markerLineNumberOffset;
				NextAnalyser = nextProcessor;
			}

			public MarkerInsertionTypeOptions MarkerInsertionType { get; private set; }

			public int MarkerLineNumberOffset { get; private set; }

			/// <summary>
			/// This will never be null
			/// </summary>
			public IAnalyseCharacters NextAnalyser { get; private set; }
		}

		private enum MarkerInsertionTypeOptions
		{
			InsertAfterCurrentCharacter,
			InsertBeforeCurrentCharacter,
			NoInsertion
		}

		private class StandardContentAnalyser : IAnalyseCharacters
		{
			public AnalysisResult Analyse(char currentCharacter, char? previousCharacter)
			{
				if (currentCharacter == '{')
					return AnalysisResult.NoInsertion(new DeclarationHeaderAnalyser());

				return AnalysisResult.NoInsertion(this);
			}
		}

		private class DeclarationHeaderAnalyser : IAnalyseCharacters
		{
			// What about media queries???? The latter is supported, but not the former
			//
			// @media (width: 400px) {
			//   .test {
			//     color: red;
			//   }
			// }
			//
			// or
			//
			// .test {
			//   @media (width: 400px) {
			//     color: red;
			//   }
			// }

			private static char[] _declarationHeaderTerminators_MarkerInserting = new[]
			{
				'}', // Expect this to be the end of another style block, the current style declaration header has terminated
				';'  // This could be the end of a LessCSS variable declaration, the current style declaration header has terminated
			};
			private static char[] _declarationHeaderTerminators_NonMarkerInserting = new[]
			{
				')', // Indicates that the declaration header is a LessCSS parameterised mixin or a media query, we don't want mark these
				'@'  // Also indicates a media query (without parameters - eg. "@media print" - otherwise the close bracket would have got it)
			};
			private static char[] _declarationHeaderTerminators_Reset = new char[]
			{
				'{'  // Presumably part of a nested LessCSS style block, reset to mark the current header and potentially track the next
			};

			private readonly int _declarationHeaderLineNumberOffset;
			private readonly bool _encounteredNonWhiteSpaceDeclarationContent;
			public DeclarationHeaderAnalyser() : this(0, false) { }
			private DeclarationHeaderAnalyser(int declarationHeaderLineNumberOffset, bool encounteredNonWhiteSpaceDeclarationContent)
			{
				if (declarationHeaderLineNumberOffset < 0)
					throw new ArgumentOutOfRangeException("declarationHeaderLineNumberOffset", "must be zero or greater");

				_declarationHeaderLineNumberOffset = declarationHeaderLineNumberOffset;
				_encounteredNonWhiteSpaceDeclarationContent = encounteredNonWhiteSpaceDeclarationContent;
			}

			public AnalysisResult Analyse(char currentCharacter, char? previousCharacter)
			{
				// If there is no more content to process then this must be a marker insertion point since we're in a declaration header
				if (previousCharacter == null)
					return AnalysisResult.InsertBeforeCurrentCharacter(_declarationHeaderLineNumberOffset, new StandardContentAnalyser());

				if (_declarationHeaderTerminators_MarkerInserting.Contains(currentCharacter))
				{
					// This terminator indicate the end of the header and suggests a return back to standard content (ie. not a header / css selector)
					return AnalysisResult.InsertAfterCurrentCharacter(_declarationHeaderLineNumberOffset, new StandardContentAnalyser());
				}
				else if (_declarationHeaderTerminators_NonMarkerInserting.Contains(currentCharacter))
				{
					// This terminator indicates that we weren't in a css selector at all (so no marker will be generated)
					return AnalysisResult.NoInsertion(new StandardContentAnalyser());
				}
				else if (_declarationHeaderTerminators_Reset.Contains(currentCharacter))
				{
					// If we hit a reset terminator then the current content (if any) should result in a marker being generated, as may the following content (eg. this
					// marker could be for a selector nested inside another)
					return AnalysisResult.InsertAfterCurrentCharacter(_declarationHeaderLineNumberOffset, new DeclarationHeaderAnalyser());
				}

				// If we've encountered a line break then we'll need to add one the marker line number offset - if there's loads of new lines between a declaration header
				// and the preceding content, the line number indicated in the marker should point to a line that includes part of the declaration header rather than
				// being the end of the new line / whitespace content from the content before it. This offset is only incremented if some non-whitespace content has
				// been encountered in the declaration header, so we indicate a class name rather than open bracket (when they're not on the same line), for example
				if ((currentCharacter == '\n') && _encounteredNonWhiteSpaceDeclarationContent)
					return AnalysisResult.NoInsertion(new DeclarationHeaderAnalyser(_declarationHeaderLineNumberOffset + 1, true));

				return AnalysisResult.NoInsertion(
					new DeclarationHeaderAnalyser(
						_declarationHeaderLineNumberOffset,
						_encounteredNonWhiteSpaceDeclarationContent || !char.IsWhiteSpace(currentCharacter)
					)
				);
			}
		}
	}
}
