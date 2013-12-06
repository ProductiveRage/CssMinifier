using System;
using System.Collections;
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
			var markerIdLookup = new MarkerIdLookup(_markerIdRetriever());
			engine.Plugins = new[] { 
                new SelectorRewriterVisitorPluginConfigurator(markerIdLookup, _optionalTagNameToRemove)
            };
			return new TextFileContents(
				initialFileContents.RelativePath,
				initialFileContents.LastModified,
				engine.TransformToCss(initialFileContents.Content, null)
			);
		}

		private class SelectorRewriterVisitorPluginConfigurator : IPluginConfigurator
		{
			private readonly MarkerIdLookup _markerIdLookup;
			private readonly string _optionalTagNameToRemove;
			public SelectorRewriterVisitorPluginConfigurator(MarkerIdLookup markerIdLookup, string optionalTagNameToRemove)
			{
				if (markerIdLookup == null)
					throw new ArgumentNullException("markerIdLookup");

				_markerIdLookup = markerIdLookup;
				_optionalTagNameToRemove = optionalTagNameToRemove;
			}

			public IPlugin CreatePlugin() { return new SelectorRewriterVisitorPlugin(_markerIdLookup, _optionalTagNameToRemove); }
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
			private readonly MarkerIdLookup _markerIdLookup;
			private readonly string _optionalTagNameToRemove;
			public SelectorRewriterVisitorPlugin(MarkerIdLookup markerIdLookup, string optionalTagNameToRemove)
			{
				if (markerIdLookup == null)
					throw new ArgumentNullException("markerIdLookup");

				_markerIdLookup = markerIdLookup;
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
						return new MarkerIdTidyingRuleset(ruleset.Selectors, ruleset.Rules, _markerIdLookup, _optionalTagNameToRemove) { Location = ruleset.Location };
				}
				return node;
			}
		}

		/// <summary>
		/// This clones a Ruleset's data and overrides its AppendCSS method to prevent rendering "un-useful mark-id-originating selectors"
		/// </summary>
		private class MarkerIdTidyingRuleset : Ruleset
		{
			private readonly MarkerIdLookup _markerIdLookup;
			private readonly string _optionalTagNameToRemove;
			public MarkerIdTidyingRuleset(NodeList<Selector> selectors, NodeList rules, MarkerIdLookup markerIdLookup, string optionalTagNameToRemove)
				: base(selectors, rules)
			{
				if (markerIdLookup == null)
					throw new ArgumentNullException("markerIdLookup");

				_markerIdLookup = markerIdLookup;
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

				var pathsToInclude = FilterPaths(paths, _markerIdLookup);
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

			private IEnumerable<IEnumerable<Selector>> FilterPaths(IEnumerable<IEnumerable<Selector>> paths, MarkerIdLookup markerIdLookup)
			{
				if (paths == null)
					throw new ArgumentNullException("paths");
				if (markerIdLookup == null)
					throw new ArgumentNullException("markerIdLookup");

				var currentElementContent = new CombinedElementContent(markerIdLookup);

				var markerIdsAccountedFor = new HashSet<string>();
				var combinedPaths = new List<Selector>();
				foreach (var path in paths)
				{
					if (path == null)
						throw new ArgumentException("Null reference encountered in paths set");

					currentElementContent.Clear();

					var allElementInSelectorBuffer = new List<Element>();
					Element[] markerIdElements = null;
					var selectorShouldBeIgnored = false;
					foreach (var selector in path)
					{
						if (selector == null)
							throw new ArgumentException("Null reference encountered in selector set in paths");

						var isFirstElementInSelector = true;
						foreach (var element in selector.Elements)
						{
							if (element == null)
								throw new ArgumentException("Null element reference encountered within selector set in paths");

							// The only time that a marker id should be present in a selector is if it's the last content (meaning that the current
							// selector is the one that the marker id is most specific for). If we have already identified marker id content in the
							// current selector and now we're encountering more content then this must be a selector to ignore (as the marker id
							// content was not right at the end).
							if (markerIdElements != null)
							{
								selectorShouldBeIgnored = true;
								break;
							}

							// We're potentially combining the elements from several distinct selectors into one long selector here, so ensure that
							// when elements from different selectors are combined that there is always some combinator character between them (a
							// blank string combinator will not work)
							Element elementToAdd;
							if (isFirstElementInSelector && (element.Combinator.Value == "") && allElementInSelectorBuffer.Any())
								elementToAdd = new Element(new Combinator(" "), element.Value);
							else
								elementToAdd = element;

							// As we process the elements, we need to join up elements that form a single selector segment (eg. "#test1" and ".css_1")
							// to see if together they form a marker id. As soon as a combinator with a space is encountered, this is reset as this
							// indicates a new segment.
							if (elementToAdd.Combinator.Value == " ")
								currentElementContent.Clear();
							currentElementContent.Add(element);

							// Test whether the current selector segment is a marker id
							var markerId = currentElementContent.TryToGetAsMarkerId();
							if (markerId != null)
							{
								if (markerIdsAccountedFor.Contains(markerId))
								{
									selectorShouldBeIgnored = true;
									break;
								}
								markerIdElements = currentElementContent.ToArray();
								markerIdsAccountedFor.Add(markerId);
							}

							// If we haven't determined that we've encountered a selector to skip yet then keep building up the combined set of elements
							// for the current selector
							allElementInSelectorBuffer.Add(elementToAdd);
							isFirstElementInSelector = false;
						}
						if (selectorShouldBeIgnored)
							break;
					}
					if (!selectorShouldBeIgnored)
					{
						// If the selector is one that we've decided that we want, there's still potentially more work to do. If it's a selector that
						// relates to a marker id, we need to ensure that we only include the marker id content (eg. if we have "div h2 #test1.css_12",
						// the only important information is the marker "#test1.css_12"). There may be multiple selectors for the same marker id, we
						// only want one of them to be included in the final output. If the selector does not relate to a marker id then we need to
						// ensure that any elements that relate to the "optionalTagNameToRemove" (where non-null) are removed.
						if (markerIdElements == null)
						{
							if (_optionalTagNameToRemove == null)
								combinedPaths.Add(new Selector(allElementInSelectorBuffer));
							else
								combinedPaths.Add(new Selector(allElementInSelectorBuffer.Where(e => e.Value != _optionalTagNameToRemove)));
						}
						else
							combinedPaths.Add(new Selector(markerIdElements));
					}
				}
				return combinedPaths.Select(selector => new[] { selector });
			}

			private class CombinedElementContent : IEnumerable<Element>
			{
				private readonly MarkerIdLookup _markerIdLookup;
				private readonly List<Element> _elements;
				private readonly StringBuilder _stringContentBuilder;
				private bool _contentIsTooLongToBeMarkerId;
				public CombinedElementContent(MarkerIdLookup markerIdLookup)
				{
					if (markerIdLookup == null)
						throw new ArgumentNullException("markerIdLookup");

					_markerIdLookup = markerIdLookup;
					_elements = new List<Element>();
					_stringContentBuilder = new StringBuilder();
					_contentIsTooLongToBeMarkerId = false;
				}

				public void Add(Element element)
				{
					if (element == null)
						throw new ArgumentNullException("element");

					_elements.Add(element);
					if (!_contentIsTooLongToBeMarkerId)
					{
						var elementCombinator = element.Combinator.Value;
						if ((elementCombinator != " ") || (_stringContentBuilder.Length == 0))
						{
							// If there is no content yet and the combinator for the current element is a space then ignore it, otherwise we may
							// end up building a selector segment with a leading space, which will prevent it from being matched to a marker id
							_stringContentBuilder.Append(elementCombinator);
						}
						_stringContentBuilder.Append(element.Value);

						if (_stringContentBuilder.Length > _markerIdLookup.GreatestMarkerIdLength)
							_contentIsTooLongToBeMarkerId = true;
					}
				}
				public void Clear()
				{
					_elements.Clear();
					_stringContentBuilder.Clear();
					_contentIsTooLongToBeMarkerId = false;
				}

				public string TryToGetAsMarkerId()
				{
					if ((_stringContentBuilder.Length == 0) || _contentIsTooLongToBeMarkerId)
						return null;

					var stringContent = _stringContentBuilder.ToString();
					return _markerIdLookup.Contains(stringContent) ? stringContent : null;
				}

				public IEnumerator<Element> GetEnumerator()
				{
					return _elements.GetEnumerator();
				}
				IEnumerator IEnumerable.GetEnumerator()
				{
					return GetEnumerator();
				}
			}
		}

		private class MarkerIdLookup
		{
			private readonly HashSet<string> _markerIds;
			public MarkerIdLookup(IEnumerable<string> markerIds)
			{
				if (markerIds == null)
					throw new ArgumentNullException("markerIds");

				_markerIds = new HashSet<string>(markerIds);
				GreatestMarkerIdLength = _markerIds.Any() ? markerIds.Max(id => (id == null) ? 0 : id.Length) : 0;
			}

			public bool Contains(string markerId)
			{
				if (markerId == null)
					throw new ArgumentNullException("markerId");
				return (markerId.Length <= GreatestMarkerIdLength) && _markerIds.Contains(markerId);
			}
			public int GreatestMarkerIdLength { get; private set; }
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
