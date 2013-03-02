using System;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.Factories
{
	/// <summary>
	/// This will generate a stylesheet loader that will flatten all import statements, so long as they reference imports in the same location as the source file, compile
	/// LESS to CSS and minify the results
	/// </summary>
	public class DefaultNonCachedLessCssLoaderFactory : IGenerateCssLoaders
	{
		private readonly IRelativePathMapper _pathMapper;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public DefaultNonCachedLessCssLoaderFactory(IRelativePathMapper pathMapper, ErrorBehaviourOptions errorBehaviour, ILogEvents logger)
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
			
			return new DotLessCssCssLoader(
				new SameFolderImportFlatteningCssLoader(
					new SimpleTextFileContentLoader(_pathMapper),
					SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.ContentIsUnprocessed,
					importFlatteningErrorBehaviour,
					importFlatteningErrorBehaviour,
					_logger
				),
				DotLessCssCssLoader.LessCssMinificationTypeOptions.Minify,
				dotLessCompilerErrorBehaviour,
				_logger
			);
		}
	}
}
