using System;
using CSSMinifier.FileLoaders.Helpers;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.Factories
{
	/// <summary>
	/// This will generate a stylesheet loader that will flatten all import statements, so long as they reference imports in the same location as the source file, compile
	/// LESS to CSS and minify the results. It will apply further processing that may not always be desirable: 1. Pseudo id selectors will be injected that indicate which
	/// source file and line number that each style block originated (eg. "#test.css_123"), 2. Any files that are fully wrapped in an html tag will have the html tag removed
	/// from the final selectors (the assumption being that the wrapping body tag is to restrict the scope of any LESS values or mixins declared in the file and that it is
	/// not required in the compiled output), 3. Media-query-wrapped sections will be moved to the end of the final content and sections combined into a single media query
	/// for cases where multiple sections exist with the same media query criteria (this may introduce side effects if the styling was not written to withstand rearrangement).
	/// This generator is appropriate if the rules outlined at www.productiverage.com/Read/42 are followed.
	/// </summary>
	public class EnhancedNonCachedLessCssLoaderFactory : IGenerateCssLoaders
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public EnhancedNonCachedLessCssLoaderFactory(ITextFileLoader contentLoader, ErrorBehaviourOptions errorBehaviour, ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), errorBehaviour))
				throw new ArgumentOutOfRangeException("lineNumberInjectionBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_errorBehaviour = errorBehaviour;
			_logger = logger;
		}
		public EnhancedNonCachedLessCssLoaderFactory(IRelativePathMapper pathMapper, ErrorBehaviourOptions errorBehaviour, ILogEvents logger)
			: this(new SimpleTextFileContentLoader(pathMapper), errorBehaviour, logger) { }

		/// <summary>
		/// This will never return null, it will raise an exception if unable to satisfy the request
		/// </summary>
		public ITextFileLoader Get()
		{
			var scopingHtmlTagReplaceString = "REPLACEME";
			var sourceMappingMarkerIdGenerator = new SourceMappingMarkerIdGenerator();
			return new MediaQueryGroupingCssLoader(
				new DotLessCssCssLoader(
					new SameFolderImportFlatteningCssLoader(
						new LessCssLineNumberingTextFileLoader(
							new LessCssCommentRemovingTextFileLoader(
								new LessCssOpeningHtmlTagRenamer(
									_contentLoader,
									scopingHtmlTagReplaceString
								)
							),
							sourceMappingMarkerIdGenerator.MarkerGenerator,
							selector => selector != scopingHtmlTagReplaceString // Don't insert marker ids on wrapper selectors that will be removed
						),
						SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.CommentsAreAlreadyRemoved,
						_errorBehaviour,
						_errorBehaviour,
						_logger
					),
					() => sourceMappingMarkerIdGenerator.GetInsertedMarkerIds(),
					scopingHtmlTagReplaceString,
					_errorBehaviour,
					_logger
				)
			);
		}
	}
}
