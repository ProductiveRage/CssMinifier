using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSSMinifier.Logging;
using dotless.Core;
using dotless.Core.Loggers;
using dotless.Core.Parser;
using dotless.Core.Parser.Infrastructure;
using dotless.Core.Parser.Infrastructure.Nodes;
using dotless.Core.Parser.Tree;
using dotless.Core.Plugins;
using dotless.Core.Utils;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will LESS-compile and minify style content. If the constructor that takes markerIdRetriever and optionalTagNameToRemove arguments is used, then work may be done
	/// to efficiently process the output of the LessCssLineNumberingTextFileLoader, if that class was configured to insert source mapping marker ids; the selectors that are
	/// transformed from nested LESS selectors into their flattened vanilla CSS form will be trimmed down to ensure that no selectors are present in the final data that are
	/// combinations of marker ids, since these are of no use in the final content and may bloat the output considerably. (Source mapping marker ids may be of the form
	/// "test.css_12" to indicate which file and line number that any rules were found in the original source content). The "optionalTagNameToRemove" may be used in
	/// conjunction with the LessCssOpeningHtmlTagRenamer, which will rename html selectors that wrap an entire file's content as these are generally used as a best
	/// practice to limit the scope of any LESS values or mixins to that file. The LessCssOpeningHtmlTagRenamer will rename html selectors that match that criteria
	/// to a known-good-to-remove name which this class may trim out of the returned content. This allows the selector to be included to restrict scope without
	/// bloating the final output.
	/// </summary>
	public class DotLessCssCssLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly InsertedMarkerRetriever _markerIdRetriever;
		private readonly ErrorBehaviourOptions _reportedErrorBehaviour;
		private readonly string _optionalTagNameToRemove;
		private readonly ILogEvents _logger;
		public DotLessCssCssLoader(
			ITextFileLoader contentLoader,
			InsertedMarkerRetriever markerIdRetriever,
			string optionalTagNameToRemove,
			ErrorBehaviourOptions reportedErrorBehaviour,
			ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (markerIdRetriever == null)
				throw new ArgumentNullException("markerIdRetriever");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), reportedErrorBehaviour))
				throw new ArgumentOutOfRangeException("reportedErrorBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_markerIdRetriever = markerIdRetriever;
			_optionalTagNameToRemove = optionalTagNameToRemove;
			_reportedErrorBehaviour = reportedErrorBehaviour;
			_logger = logger;
		}

		/// <summary>
		/// This constructor is used if you just want a straight LESS compilation-and-minification without any handling of source mapping marker ids or scope-restricting
		/// selector removals
		/// </summary>
		public DotLessCssCssLoader(ITextFileLoader contentLoader, ErrorBehaviourOptions reportedErrorBehaviour, ILogEvents logger)
			: this(contentLoader, () => new string[0], null, reportedErrorBehaviour, logger) { }

		/// <summary>
		/// This may never return null, nor a set containing any null or blank entries. All markers must be of the format "#id".
		/// </summary>
		public delegate IEnumerable<string> InsertedMarkerRetriever();

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank relativePath.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var initialFileContents = _contentLoader.Load(relativePath);
			var engine = new LessEngine(
				new Parser(),
				new DotLessCssPassThroughLogger(_logger, _reportedErrorBehaviour),
				true, // Compress
				false // Debug
			);
			engine.Plugins = new[] { 
                new SelectorRewriterVisitorPluginConfigurator(_markerIdRetriever, _optionalTagNameToRemove)
            };
			return new TextFileContents(
				initialFileContents.RelativePath,
				initialFileContents.LastModified,
				engine.TransformToCss(initialFileContents.Content, null)
			);
		}

		private class SelectorRewriterVisitorPluginConfigurator : IPluginConfigurator
		{
			private readonly InsertedMarkerRetriever _markerIdRetriever;
			private readonly string _optionalTagNameToRemove;
			public SelectorRewriterVisitorPluginConfigurator(InsertedMarkerRetriever markerIdRetriever, string optionalTagNameToRemove)
			{
				if (markerIdRetriever == null)
					throw new ArgumentNullException("markerIdRetriever");

				_markerIdRetriever = markerIdRetriever;
				_optionalTagNameToRemove = optionalTagNameToRemove;
			}

			public IPlugin CreatePlugin() { return new SelectorRewriterVisitorPlugin(_markerIdRetriever, _optionalTagNameToRemove); }
			public IEnumerable<IPluginParameter> GetParameters() { return new IPluginParameter[0]; }
			public void SetParameterValues(IEnumerable<IPluginParameter> parameters) { }
			public string Name { get { return "SelectorRewriterVisitorPluginConfigurator"; } }
			public string Description { get { return Name; } }
			public Type Configurates { get { return typeof(SelectorRewriterVisitorPlugin); } }
		}

		/// <summary>
		/// This will replace any Ruleset with a MarkerIdTidyingRuleset instance with the same data, the difference is that the MarkerIdTidyingRuleset
		/// will avoid rendering redundant selectors that contains marker ids that are of no use (each marker ids should only appear once in the final
		/// content, at the point at which it is most specific and most useful - marker ids will appear multiple times if nested selectors exist in
		/// the source, we want to ignore these)
		/// </summary>
		private class SelectorRewriterVisitorPlugin : VisitorPlugin
		{
			private readonly InsertedMarkerRetriever _markerIdRetriever;
			private readonly string _optionalTagNameToRemove;
			public SelectorRewriterVisitorPlugin(InsertedMarkerRetriever markerIdRetriever, string optionalTagNameToRemove)
			{
				if (markerIdRetriever == null)
					throw new ArgumentNullException("markerIdRetriever");

				_markerIdRetriever = markerIdRetriever;
				_optionalTagNameToRemove = optionalTagNameToRemove;
			}

			public override VisitorPluginType AppliesTo
			{
				get { return VisitorPluginType.AfterEvaluation; }
			}

			public override Node Execute(Node node, out bool visitDeeper)
			{
				visitDeeper = true;
				if (node.GetType() == typeof(Ruleset))
				{
					var ruleset = (Ruleset)node;
					if (ruleset != null)
						return new MarkerIdTidyingRuleset(ruleset.Selectors, ruleset.Rules, _markerIdRetriever, _optionalTagNameToRemove) { Location = ruleset.Location };
				}
				return node;
			}
		}

		/// <summary>
		/// This clones a Ruleset's data and overrides its AppendCSS method to prevent rendering "un-useful mark-id-originating selectors"
		/// </summary>
		private class MarkerIdTidyingRuleset : Ruleset
		{
			private readonly InsertedMarkerRetriever _markerIdRetriever;
			private readonly string _optionalTagNameToRemove;
			public MarkerIdTidyingRuleset(NodeList<Selector> selectors, NodeList rules, InsertedMarkerRetriever markerIdRetriever, string optionalTagNameToRemove)
				: base(selectors, rules)
			{
				if (markerIdRetriever == null)
					throw new ArgumentNullException("markerIdRetriever");

				_markerIdRetriever = markerIdRetriever;
				_optionalTagNameToRemove = optionalTagNameToRemove;
			}

			public override void AppendCSS(Env env, Context context)
			{
				// This entire method is lifted straight from Ruleset's AppendCSS implementation, the only difference being the tidying of the paths done
				// before pushing that content to the output
				var paths = new Context();
				if (!IsRoot)
					paths.AppendSelectors(context, Selectors);

				env.Output.Push();

				var rules = new List<StringBuilder>();
				var nonCommentRules = 0;
				foreach (var node in Rules)
				{
					if (node.IgnoreOutput())
						continue;

					var comment = node as Comment;
					if (comment != null && !comment.IsValidCss)
						continue;

					var ruleset = node as Ruleset;
					if (ruleset != null)
						ruleset.AppendCSS(env, paths);
					else
					{
						var rule = node as Rule;
						if (rule && rule.Variable)
							continue;

						if (!IsRoot)
						{
							if (!comment)
								nonCommentRules++;
							env.Output.Push().Append(node);
							rules.Add(env.Output.Pop());
						}
						else
							env.Output.Append(node);
					}
				}

				var rulesetOutput = env.Output.Pop();

				var pathsToInclude = FilterPaths(paths).ToArray(); // TODO: Remove ToArray
				if (!IsRoot && pathsToInclude.Any())
				{
					if (nonCommentRules > 0)
					{
						// This line is taken from of Context's AppendCSS, it allows us to only render the selectors we're interested in (which
						// we can't do it we use that method directly, and I can't see a way to create a new instance with just the filtered
						// path data)
						env.Output.AppendMany(
							pathsToInclude,
							path => path.Select(p => p.ToCSS(env)).JoinStrings("").Trim(),
							","
						);
						env.Output.Append("{");
						env.Output.AppendMany(rules.ConvertAll(stringBuilder => stringBuilder.ToString()).Distinct(), "");
						env.Output.TrimRight(';');
						env.Output.Append("}");
					}
				}
				env.Output.Append(rulesetOutput);
			}

			private IEnumerable<IEnumerable<Selector>> FilterPaths(IEnumerable<IEnumerable<Selector>> paths)
			{
				if (paths == null)
					throw new ArgumentNullException("paths");

				var markerIds = new HashSet<string>(_markerIdRetriever());
				var returnedMarkerIdSelectors = new HashSet<string>();
				foreach (var path in paths)
				{
					// IsMarkerId indicates that a selector segment is a complete match with a marker id (eg. "test1.css_12"), ContainsMarkerId indicates that
					// a selector segment is a partial match (eg. "test1.css_12:hover") which most likely means that it was the result of an injected marker
					// being combined with a parent selector (eg. "&:hover")
					var pathSegmentMarkerIdStates = path
						.Select(s =>
						{
							var combinedElementValue = string.Join("", s.Elements.Select(e => e.Value));
							var isMarkerId = markerIds.Contains(combinedElementValue);
							return new
							{
								Selector = s,
								CombinedElementValue = combinedElementValue,
								ContainsMarkerId = GetCombinedElementValues(s.Elements).Any(v => markerIds.Contains(v)),
								IsMarkerId = isMarkerId
							};
						});

					// Ignore any selectors with partial marker id segments (we're interested in selectors with zero marker id content since these are "real
					// selectors" from the source and we're interested in selectors where the last segment is a marker id and no others are, since this will
					// be the most specific marker id for the rules, but if there is a partial marker id match then a parent selector has affected and there
					// should be a marker id with that parent selector that will be more specific and should be used in preference)
					if (pathSegmentMarkerIdStates.Any(s => s.ContainsMarkerId && !s.IsMarkerId))
						continue;

					// If there are no marker ids in the selector then this is a "real selector" and hasn't been polluted by marker ids at all
					var numberOfMarkerIds = pathSegmentMarkerIdStates.Count(s => s.IsMarkerId || s.ContainsMarkerId);
					if (numberOfMarkerIds == 0)
					{
						if (_optionalTagNameToRemove == null)
							yield return path;
						else
						{
							// If there was an "optionalTagNameToRemove" value specified then exclude any elements from the selector that match this value
							var segmentsWithoutTagNameToRemove = path
								.Select(s =>
								{
									var elementsToKeep = s.Elements.Where(e => e.Value != _optionalTagNameToRemove);
									return elementsToKeep.Any() ? new Selector(elementsToKeep) : null;
								})
								.Where(s => s != null);
							if (segmentsWithoutTagNameToRemove.Any())
								yield return segmentsWithoutTagNameToRemove;
						}
						continue;
					}

					// If there are multiple marker ids, then we're not interested - we only want to keep the single most-specific marker id selector,
					// this should be a selector where the last segment is a marker id
					if (numberOfMarkerIds == 1)
					{
						var lastSegment = pathSegmentMarkerIdStates.Last();
						if (lastSegment.IsMarkerId)
						{
							if (!returnedMarkerIdSelectors.Contains(lastSegment.CombinedElementValue))
							{
								yield return new[] { lastSegment.Selector };
								returnedMarkerIdSelectors.Add(lastSegment.CombinedElementValue);
							}
						}
					}
				}
			}

			private IEnumerable<string> GetCombinedElementValues(IEnumerable<Element> elements)
			{
				if (elements == null)
					throw new ArgumentNullException("elements");
				if (elements.Any(e => e == null))
					throw new ArgumentException("Null reference encountered in element set");

				var elementsArray = elements.ToArray();
				for (var index = 0; index < elementsArray.Length; index++)
				{
					var combinedValue = new StringBuilder();
					for (var indexInner = index; indexInner < elementsArray.Length; indexInner++)
					{
						combinedValue.Append(elementsArray[indexInner].Value);
						yield return combinedValue.ToString();
					}
				}
			}
		}

		private class DotLessCssPassThroughLogger : ILogger
		{
			private readonly ILogEvents _logger;
			private readonly ErrorBehaviourOptions _reportedErrorBehaviour;
			public DotLessCssPassThroughLogger(ILogEvents logger, ErrorBehaviourOptions reportedErrorBehaviour)
			{
				if (logger == null)
					throw new ArgumentNullException("logger");
				if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), reportedErrorBehaviour))
					throw new ArgumentOutOfRangeException("reportedErrorBehaviour");

				_logger = logger;
				_reportedErrorBehaviour = reportedErrorBehaviour;
			}

			public void Debug(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(CSSMinifier.Logging.LogLevel.Debug, DateTime.Now, () => message, null);
			}

			public void Error(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(CSSMinifier.Logging.LogLevel.Error, DateTime.Now, () => message, null);

				if (_reportedErrorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw new Exception("dotLess parsing error: " + message);
			}

			public void Info(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(CSSMinifier.Logging.LogLevel.Info, DateTime.Now, () => message, null);
			}

			public void Log(dotless.Core.Loggers.LogLevel level, string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				switch (level)
				{
					case dotless.Core.Loggers.LogLevel.Debug:
						Debug(message);
						return;

					case dotless.Core.Loggers.LogLevel.Error:
						Error(message);
						return;

					case dotless.Core.Loggers.LogLevel.Info:
						Info(message);
						return;

					case dotless.Core.Loggers.LogLevel.Warn:
						Warn(message);
						return;

					default:
						_logger.Log(CSSMinifier.Logging.LogLevel.Warning, DateTime.Now, () => "DotLess logged message with unsupported LogLeve [" + level.ToString() + "]: " + message, null);
						return;
				}
			}

			public void Warn(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(CSSMinifier.Logging.LogLevel.Warning, DateTime.Now, () => message, null);
			}

			public void Log(dotless.Core.Loggers.LogLevel level, string message) { Log(level, message, new object[0]); }
			public void Debug(string message) { Debug(message, new object[0]); }
			public void Error(string message) { Error(message, new object[0]); }
			public void Info(string message) { Info(message, new object[0]); }
			public void Warn(string message) { Warn(message, new object[0]); }
		}
	}
}
