using System;
using System.Web;
using CSSMinifier.FileLoaders.Helpers;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.Factories
{
	/// <summary>
	/// This will generate a stylesheet loader that will flatten all import statements, so long as they reference imports in the same location as the source file, compile
	/// LESS to CSS, minify the results, and inject pseudo id selectors that indicate which source file and line number that each style block originated (eg. "#test.css_123")
	/// </summary>
	public class DefaultNonCachedLessCssLoaderFactory : IGenerateCssLoaders
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly SourceMappingMarkerInjectionOptions _sourceMappingMarkerInjection;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public DefaultNonCachedLessCssLoaderFactory(
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
		public DefaultNonCachedLessCssLoaderFactory(
			IRelativePathMapper pathMapper,
			SourceMappingMarkerInjectionOptions sourceMappingMarkerInjection,
			ErrorBehaviourOptions errorBehaviour,
			ILogEvents logger)
			: this(new SimpleTextFileContentLoader(pathMapper), sourceMappingMarkerInjection, errorBehaviour, logger) { }
		public DefaultNonCachedLessCssLoaderFactory(IRelativePathMapper pathMapper)
			: this(new SimpleTextFileContentLoader(pathMapper), SourceMappingMarkerInjectionOptions.Inject, ErrorBehaviourOptions.LogAndRaiseException, new NullLogger()) { }
		public DefaultNonCachedLessCssLoaderFactory(HttpServerUtilityBase server)
			: this(new ServerUtilityPathMapper(server)) { }

		/// <summary>
		/// This will never return null, it will raise an exception if unable to satisfy the request
		/// </summary>
		public ITextFileLoader Get()
		{
			ITextFileLoader singleFileLoader;
			var sourceMappingMarkerIdGenerator = new SourceMappingMarkerIdGenerator();
			if (_sourceMappingMarkerInjection == SourceMappingMarkerInjectionOptions.Inject)
			{
				singleFileLoader = new LessCssLineNumberingTextFileLoader(
					new LessCssCommentRemovingTextFileLoader(_contentLoader),
					sourceMappingMarkerIdGenerator.MarkerGenerator,
					null // optionalSelectorMarkerInsertionCondition (null => insert markers for all selectors)
				);
			}
			else
				singleFileLoader = _contentLoader;
			return new DotLessCssCssLoader(
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
				null, // optionalTagNameToRemove (null => no selectors need removing)
				_errorBehaviour,
				_logger
			);
		}
	}
}
