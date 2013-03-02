using System;
using System.Collections.Generic;
using System.Linq;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.Factories
{
	/// <summary>
	/// This will generate a stylesheet loader that will flatten all import statements, so long as they reference imports in the same location as the source file, compile
	/// LESS to CSS and minify the results. It will apply further processing that may not always be desirable: 1. Pseudo id selectors will be injected that indicate which
	/// source file and line number that each style block originated (eg. "#test.css_123"), 2. Any files that are fully wrapped in a body tag will have the body tag removed
	/// from the final selectors (the assumption being that the wrapping body tag is to restrict the scope of any LESS values or mixins declared in the file and that it is
	/// not required in the compiled output), 3. Media-query-wrapped sections will be moved to the end of the final content and sections combined into a single media query
	/// for cases where multiple sections exist with the same media query criteria (this may introduce side effects if the styling was not written to withstand rearrangement).
	/// This generator is appropriate if the rules outlined at www.productiverage.com/Read/42 are followed.
	/// </summary>
	public class EnhancedNonCachedLessCssLoaderFactory : IGenerateCssLoaders
	{
		private readonly IRelativePathMapper _pathMapper;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public EnhancedNonCachedLessCssLoaderFactory(IRelativePathMapper pathMapper, ErrorBehaviourOptions errorBehaviour, ILogEvents logger)
		{
			if (pathMapper == null)
				throw new ArgumentNullException("pathMapper");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), errorBehaviour))
				throw new ArgumentOutOfRangeException("lineNumberInjectionBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_pathMapper = pathMapper;
			_errorBehaviour = errorBehaviour;
			_logger = logger;
		}

		/// <summary>
		/// This will never return null, it will raise an exception if unable to satisfy the request
		/// </summary>
		public ITextFileLoader Get()
		{
			var importFlatteningErrorBehaviour = (_errorBehaviour == ErrorBehaviourOptions.LogAndContinue)
				? SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.DisplayWarningAndIgnore
				: SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.RaiseException;

			var dotLessCompilerErrorBehaviour = (_errorBehaviour == ErrorBehaviourOptions.LogAndContinue)
				? DotLessCssCssLoader.ReportedErrorBehaviourOptions.LogAndContinue
				: DotLessCssCssLoader.ReportedErrorBehaviourOptions.LogAndRaiseException;

			var scopingBodyTagReplaceString = "REPLACEME";

			var recordingMarkerGenerator = new LessCssLineNumberMarkerGenerator.RecordingMarkerGenerator(
				LessCssLineNumberMarkerGenerator.GetPseudoHtmlIdMarkerGeneratorForSingleFolderCombiningProcess()
			);

			return new MediaQueryGroupingCssLoader(
				new InjectedIdTidyingTextFileLoader(
					new ContentReplacingTextFileLoader(
						new DotLessCssCssLoader(
							new SameFolderImportFlatteningCssLoader(
								new LessCssLineNumberingTextFileLoader(
									new LessCssCommentRemovingTextFileLoader(
										new LessCssOpeningBodyTagRenamer(
											new SimpleTextFileContentLoader(_pathMapper),
											scopingBodyTagReplaceString
										)
									),
									recordingMarkerGenerator.MarkerGenerator
								),
								SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.CommentsAreAlreadyRemoved,
								importFlatteningErrorBehaviour,
								importFlatteningErrorBehaviour,
								_logger
							),
							DotLessCssCssLoader.LessCssMinificationTypeOptions.Minify,
							dotLessCompilerErrorBehaviour,
							_logger
						),
						scopingBodyTagReplaceString + " ",
						""
					),
					() => recordingMarkerGenerator.InsertedMarkers
				)
			);
		}

		private static class LessCssLineNumberMarkerGenerator
		{
			// We'll leave in any "." characters since we want it to appear like "#Test.css_123"
			private static char[] AllowedNonAlphaNumericCharacters = new[] { '_', '-', '.' };

			/// <summary>
			/// This will generate a html-id-type string to insert into the markup, based on the filename and line number - eg. "#Test.css_1418," (the trailing
			/// comma is required for it to be inserted into the start of existing declaration header)
			/// </summary>
			public static LessCssLineNumberingTextFileLoader.MarkerGenerator GetPseudoHtmlIdMarkerGeneratorForSingleFolderCombiningProcess()
			{
				return (relativePath, lineNumber) =>
				{
					if (string.IsNullOrWhiteSpace(relativePath))
						throw new ArgumentException("Null/blank relativePath specified");
					if (lineNumber <= 0)
						throw new ArgumentOutOfRangeException("lineNumber", "must be greater than zero");

					// Working on the assumpton that all files are located in the same folder, if there was a relative path to the first file (which may then include others),
					// we may as well remove that relative path and consider the filename only - this is assuming that the SameFolderImportFlatteningCssLoader is being used
					// in the stylesheet compilation process
					relativePath = relativePath.Replace("\\", "/").Split('/').Last();

					// Make into a html-id-valid form
					var relativePathHtmlIdFriendly = "";
					for (var index = 0; index < relativePath.Length; index++)
					{
						if (!AllowedNonAlphaNumericCharacters.Contains(relativePath[index]) && !char.IsLetter(relativePath[index]) && !char.IsNumber(relativePath[index]))
							relativePathHtmlIdFriendly += "_";
						else
							relativePathHtmlIdFriendly += relativePath[index];
					}

					// Remove any runs of "_" that we've are (presumablY) a result of the above manipulation
					while (relativePathHtmlIdFriendly.IndexOf("__") != -1)
						relativePathHtmlIdFriendly = relativePath.Replace("__", "_");

					// Ids must start with a letter, so try to find the first letter in the content (if none, then return "" to indicate no insertion required)
					var startIndexOfLetterContent = relativePathHtmlIdFriendly
						.Select((character, index) => new { Character = character, Index = index })
						.FirstOrDefault(c => char.IsLetter(c.Character));
					if (startIndexOfLetterContent == null)
						return "";

					// Generate the insertion token such that a new id is added and separated from the real declaration header with a comma
					return string.Format(
						"#{0}_{1}, ",
						relativePathHtmlIdFriendly.Substring(startIndexOfLetterContent.Index),
						lineNumber
					);
				};
			}

			public class RecordingMarkerGenerator
			{
				private readonly List<string> _insertedMarkers;
				public RecordingMarkerGenerator(LessCssLineNumberingTextFileLoader.MarkerGenerator markerGenerator)
				{
					if (markerGenerator == null)
						throw new ArgumentNullException("markerGenerator");

					_insertedMarkers = new List<string>();
					MarkerGenerator = (relativePath, lineNumber) =>
					{
						var marker = markerGenerator(relativePath, lineNumber);
						if (!string.IsNullOrWhiteSpace(marker))
						{
							lock (_insertedMarkers)
							{
								_insertedMarkers.Add(marker);
							}
						}
						return marker;
					};
				}

				/// <summary>
				/// This will never return null
				/// </summary>
				public LessCssLineNumberingTextFileLoader.MarkerGenerator MarkerGenerator { get; private set; }

				/// <summary>
				/// This will never return null, nor a set containing any null or blank entries. It will return an empty set if no insertions were made.
				/// </summary>
				public IEnumerable<string> InsertedMarkers
				{
					get
					{
						lock (_insertedMarkers)
						{
							return _insertedMarkers.AsReadOnly();
						}
					}
				}
			}
		}
	}
}
