using System;
using System.Web;
using CSSMinifier.FileLoaders.Helpers;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.Factories
{
	/// <summary>
	/// This will generate a stylesheet loader that will flatten all import statements, so long as they reference imports in the same location as the source file, compile
	/// LESS to CSS, minify the results, and inject pseudo id selectors that indicate which source file and line number that each style block originated (eg. "#test.css_123").
	/// It will apply further processing that may not always be desirable: 1. Any files that are fully wrapped in an html tag will have the html tag removed from the final
	/// selectors (the assumption being that the wrapping body tag is to restrict the scope of any LESS values or mixins declared in the file and that it is not required in
	/// the compiled output), 2. Media-query-wrapped sections will be moved to the end of the final content and sections combined into a single media query for cases where
	/// multiple sections exist with the same media query criteria (this may introduce side effects if the styles were not written to withstand rearrangement). This factory
	/// is appropriate if the rules outlined at www.productiverage.com/Read/42 are followed.
	/// </summary>
	public class EnhancedNonCachedLessCssLoaderFactory : IGenerateCssLoaders
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly SourceMappingMarkerInjectionOptions _sourceMappingMarkerInjection;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public EnhancedNonCachedLessCssLoaderFactory(
			ITextFileLoader contentLoader,
			SourceMappingMarkerInjectionOptions sourceMappingMarkerInjection,
			ErrorBehaviourOptions errorBehaviour,
			ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(SourceMappingMarkerInjectionOptions), sourceMappingMarkerInjection))
				throw new ArgumentOutOfRangeException("sourceMappingMarkerInjection");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), errorBehaviour))
				throw new ArgumentOutOfRangeException("lineNumberInjectionBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_sourceMappingMarkerInjection = sourceMappingMarkerInjection;
			_errorBehaviour = errorBehaviour;
			_logger = logger;
		}
		public EnhancedNonCachedLessCssLoaderFactory(
			IRelativePathMapper pathMapper,
			SourceMappingMarkerInjectionOptions sourceMappingMarkerInjection,
			ErrorBehaviourOptions errorBehaviour,
			ILogEvents logger)
			: this(new SimpleTextFileContentLoader(pathMapper), sourceMappingMarkerInjection, errorBehaviour, logger) { }
		public EnhancedNonCachedLessCssLoaderFactory(IRelativePathMapper pathMapper)
			: this(new SimpleTextFileContentLoader(pathMapper), SourceMappingMarkerInjectionOptions.Inject, ErrorBehaviourOptions.LogAndRaiseException, new NullLogger()) { }
		public EnhancedNonCachedLessCssLoaderFactory(HttpServerUtilityBase server)
			: this(new ServerUtilityPathMapper(server)) { }

		/// <summary>
		/// This will never return null, it will raise an exception if unable to satisfy the request
		/// </summary>
		public ITextFileLoader Get()
		{
			var scopingHtmlTagReplaceString = "REPLACEME";
			ITextFileLoader singleFileLoader = new LessCssOpeningHtmlTagRenamer(
				_contentLoader,
				scopingHtmlTagReplaceString
			);
			var sourceMappingMarkerIdGenerator = new SourceMappingMarkerIdGenerator();
			if (_sourceMappingMarkerInjection == SourceMappingMarkerInjectionOptions.Inject)
			{
				singleFileLoader = new LessCssLineNumberingTextFileLoader(
					new LessCssCommentRemovingTextFileLoader(singleFileLoader),
					sourceMappingMarkerIdGenerator.MarkerGenerator,
					selector => selector != scopingHtmlTagReplaceString // Don't insert marker ids on wrapper selectors that will be removed
				);
			}
			return new MediaQueryGroupingCssLoader(
				new DotLessCssCssLoader(
					new SameFolderImportFlatteningCssLoader(
						singleFileLoader,
					_sourceMappingMarkerInjection == SourceMappingMarkerInjectionOptions.Inject
						? SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.CommentsAreAlreadyRemoved
						: SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.ContentIsUnprocessed,
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
