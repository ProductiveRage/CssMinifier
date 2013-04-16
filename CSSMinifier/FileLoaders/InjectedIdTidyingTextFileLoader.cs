using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This is intended as a complement to the LessCssLineNumberingStyleSheetProcessor when used with LESS-supported nested style selectors - in this scenario, too many
	/// line number markers will be present in the final content if markers are injected into nested selectors. This will take the list of HtmlId-esque marker insertions
	/// (of the form "#id," or "#filename.css_n,") that have been inserted into the content and will remove selectors that are generated that are not of benefit to the
	/// ginal content (the only remaining selectors should be those present in the pre-marker-injected content and the most specific marker for each selector). See the
	/// TidySelectorContent method's comment for an example (or refer to the unit tests).
	/// - The markers should always start with "#", end with "," and have alphanumeric content with optional use of "_", "-" or "."  (anything else can not be guaranteed
	///   to work with this process)
	/// </summary>
	public class InjectedIdTidyingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		private readonly InsertedMarkerRetriever _insertedMarkerRetriever;
		public InjectedIdTidyingTextFileLoader(ITextFileLoader fileLoader, InsertedMarkerRetriever insertedMarkerRetriever)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");
			if (insertedMarkerRetriever == null)
				throw new ArgumentNullException("insertedMarkerRetriever");

			_fileLoader = fileLoader;
			_insertedMarkerRetriever = insertedMarkerRetriever;
		}

		/// <summary>
		/// This may never return null, nor a set containing any null or blank entries. All markers must be of the format "#id," (leading or trailing whitespace is allowed
		/// and ignored). An exception will be raised if these conditions are not met.
		/// </summary>
		public delegate IEnumerable<string> InsertedMarkerRetriever();

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
				Process(unprocessedContent.Content)
			);
		}

		private string Process(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			// If there's no content to process then return it unaltered
			if (content.Trim() == "")
				return content;

			// Retrieve the inserted Ids that may need tidying up (if none were inserted then return the content unaltered)
			var insertedIds = GetInsertedIds();
			if (!insertedIds.Any())
				return content;

			var stringBuilder = new StringBuilder();
			var bufferBuilder = new StringBuilder();
			foreach (var cssSegment in CSSParser.Parser.ParseLESS(content))
			{
				// Ignore any comment content as it will only interfere with the processing we want to do (and we expect it to have been removed
				// by the time we get to this processing step anyway)
				if (cssSegment.CharacterCategorisation == CSSParser.ContentProcessors.CharacterCategorisationOptions.Comment)
					continue;

				// If we encounter whitespace and we're not part way through some "selector-or-style-property" content then just push it to the
				// output and move on
				if ((cssSegment.CharacterCategorisation == CSSParser.ContentProcessors.CharacterCategorisationOptions.Whitespace) && (bufferBuilder.Length == 0))
				{
					stringBuilder.Append(cssSegment.Value);
					continue;
				}

				// If we encounter SelectorOrStyleProperty content or Whitespace when we're already part-way through SelectorOrStyleProperty
				// content, then push the content to the buffer to be dealt with when we get to the end of the content
				if ((cssSegment.CharacterCategorisation == CSSParser.ContentProcessors.CharacterCategorisationOptions.SelectorOrStyleProperty)
				|| (cssSegment.CharacterCategorisation == CSSParser.ContentProcessors.CharacterCategorisationOptions.Whitespace))
				{
					bufferBuilder.Append(cssSegment.Value);
					continue;
				}

				// This indicates that the "selector-or-style-property" content (if we're in the middle of processing any) was a style property,
				// which we don't have to process
				if (cssSegment.CharacterCategorisation == CSSParser.ContentProcessors.CharacterCategorisationOptions.StylePropertyColon)
				{
					if (bufferBuilder.Length > 0)
					{
						stringBuilder.Append(bufferBuilder.ToString());
						bufferBuilder.Clear();
					}
					stringBuilder.Append(cssSegment.Value);
					continue;
				}

				// Any other content type indicates the end of "selector-or-style-property" (if we have any in the buffer), so it needs processing
				if (bufferBuilder.Length > 0)
				{
					stringBuilder.Append(
						TidySelectorContent(bufferBuilder.ToString(), insertedIds)
					);
					bufferBuilder.Clear();
				}
				stringBuilder.Append(cssSegment.Value);
			}
			if (bufferBuilder.Length > 0)
			{
				stringBuilder.Append(
					TidySelectorContent(bufferBuilder.ToString(), insertedIds)
				);
			}
			return stringBuilder.ToString();
		}

		/// <summary>
		/// Given a CSS selector (that may be a comma-separated string of multiple selectors), it will remove any selector content that relates to injected
		/// marker ids other than the most specific selector for that id. (Where ids are injected into nested LESS style declarations, less specific ids
		/// from earlier line numbers may appear with more specific ids for the nested properties - we only want the injected id that is closest to the
		/// style property, since that is the most descriptive).
		/// </summary>
		private string TidySelectorContent(string cssSelector, IEnumerable<HtmlId> insertedIds)
		{
			if (cssSelector == null)
				throw new ArgumentNullException("cssSelector");
			if (insertedIds == null)
				throw new ArgumentNullException("insertedIds");

			var insertedIdsArray = insertedIds.ToArray();
			if (insertedIdsArray.Any(id => id == null))
				throw new ArgumentException("Null reference encountered in insertedIds set");

			// Example content before marker injection:
			// 
			//   section {
			//     div.Whatever {
			//       color: black;
			//       h2 { font-weight: bold; }
			//     }
			//   }
			//
			// After marker injection (the line the markers would appear on has been tweaked for readability, but the content remains):
			//
			//   #test1.css_1, section {
			//     #test1.css_2, div.Whatever {
			//       color: black;
			//       #test1.css_4, h2 { font-weight: bold; }
			//     }
			//   }
			//
			// After LESS compilation has flattened the nested selectors:
			//
			//   #test1.css_1 #test1.css_2,
			//   #test1.css_1 div.Whatever,
			//   section #test1.css_2,
			//   section div.Whatever { color: black; }
			//
			//   #test1.css_1 #test1.css_2 #test1.css_4,
			//   #test1.css_1 div.Whatever #test1.css_4,
			//   section #test1.css_2 #test1.css_4,
			//   section div.Whatever #test1.css_4,
			//   #test1.css_1 #test1.css_2 h2,
			//   #test1.css_1 div.Whatever h2,
			//   section #test1.css_2 h2,
			//   section div.Whatever h2 { font-weight: bold; }
			//
			// The desirable selectors from these are:
			//
			//   section #test1.css_2,
			//   section div.Whatever { color: black; }
			//
			//   section div.Whatever #test1.css_4,
			//   section div.Whatever h2 { font-weight: bold; }
			//
			// as the first of each pair indicates where the style block for those properties started (lines 2 and 4, resp) whilst the other selectors
			// are only unwanted byproducts of the marker insertion and LESS' nested selector flattening, and should be removed

			// RULE:
			//  Maintain if selector contains no inserted markers (eg. section div.Whatever h2)
			//    eg. "section div.Whatever h2"
			//  - This will always be the case with the last entry since markers are always inserted into the start of selector chains

			// RULE:
			//  Maintain if the last selector segment is the ONLY inserted marker
			//    eg. "section #test1.css_2"
			//    eg. "section div.Whatever #test1.css_4"

			// Selectors we don't want may be of the form
			//    "#Test19.css_1 #Test19.css_10 h2"

			// Note: If the selector doesn't appear to be due to a flattened nested selector chain then we want to keep it (otherwise, the injected
			// markers against top-level / non-nested selectors will all be removed)
			//   eg. "#Test19.css_1"

			// Note: While "#" and "." may be used as part of the inserted marker, the child selector ">" may not and so we'll need to break on
			// any whitespace AND the ">" symbol (this is protected against by the HtmlId constructor validation)

			var tidiedSelectors = new List<string>();
			foreach (var selector in cssSelector.Split(',').Select(s => s.Trim()).Where(s => s != ""))
			{
				// Passing a null char[] to Split will break the string on all whitespace
				var selectorSegments = selector.Split(SelectorBreakCharacters, StringSplitOptions.RemoveEmptyEntries);
				if (selectorSegments.Length == 0)
					continue;

				// Always include non-nested segments (as they will either be genuine selectors which we need to maintain, or they will markers for
				// a top-level selector - eg. "body { background: white; }" becomes "#test.css_1, body { background: white; }" and we want to keept
				// the "#test.css_1" marker to indicate that the body style came from the indicated location
				if (selectorSegments.Length == 1)
				{
					tidiedSelectors.Add(selector);
					continue;
				}

				// The final segment is the only place in which an injected id may appear, if one appears elsewhere in the selector then the entire
				// selector should be removed from the output. If the final segment IS an injected id, then the entire selector should be replaced
				// with only that id (since the rest of the selector is irrelevant and effectively just noise around the injected id - eg. instead
				// "div.Wrapper div.Header #Test1.css_15" we should include on "#Test1.css_15").
				var shouldSelectorBeMaintained = true;
				string contentToReplaceSelectorWith = null;
				for (var index = 0; index < selectorSegments.Length; index++)
				{
					var selectorSegmentWithoutAnyPseudoClass = selectorSegments[index].Split(':')[0];
					if (insertedIds.Any(id => selectorSegmentWithoutAnyPseudoClass == ("#" + id.Value)))
					{
						if (index == (selectorSegments.Length - 1))
							contentToReplaceSelectorWith = selectorSegmentWithoutAnyPseudoClass;
						else
						{
							shouldSelectorBeMaintained = false;
							break;
						}
					}
				}
				if (!shouldSelectorBeMaintained)
					continue;

				tidiedSelectors.Add(contentToReplaceSelectorWith ?? selector);
			}
			
			// Exclude any duplicate selectors (most common where injected ids appear in multiple selectors which are then reduced down by the
			// above loop to be the injected id only)
			return string.Join(",", tidiedSelectors.Distinct());
		}

		/// <summary>
		/// We want to break selectors on any whitespace or on the child selector character (>)
		/// </summary>
		private readonly static char[] SelectorBreakCharacters =
			Enumerable.Range((int)char.MinValue, (int)char.MaxValue)
				.Select(c => (char)c)
				.Where(c => char.IsWhiteSpace(c) || (c == '>'))
				.ToArray();

		/// <summary>
		/// This will never return null, nor a set containing any nulls. It may return an empty set if no Ids have been reported as being inserted into the content.
		/// </summary>
		private IEnumerable<HtmlId> GetInsertedIds()
		{
			var insertedMarkers = _insertedMarkerRetriever();
			if (insertedMarkers == null)
				throw new Exception("InsertedMarkerRetriever returned null reference");

			var insertedMarkersArray = insertedMarkers.ToArray();
			if (insertedMarkersArray.Any(m => m == null))
				throw new Exception("InsertedMarkerRetriever returned a set containing a null reference");
			var ids = new List<HtmlId>();
			for (var index = 0; index < insertedMarkersArray.Length; index++)
			{
				var marker = insertedMarkersArray[index].Trim();
				if (!marker.StartsWith("#") || !marker.EndsWith(","))
					throw new Exception("InsertedMarkerRetriever returned a set containing an invalid value: " + marker);

				try
				{
					ids.Add(new HtmlId(marker.Substring(1, marker.Length - 2)));
				}
				catch (ArgumentException e)
				{
					throw new Exception("InsertedMarkerRetriever returned a set containing an invalid value: " + marker, e);
				}
			}
			return ids;
		}

		private class HtmlId
		{
			public HtmlId(string value)
			{
				if (string.IsNullOrWhiteSpace(value))
					throw new ArgumentNullException("Null/blank value specified");

				// Not a lot of point going to town over the validation here, ensuring it's not blank, doesn't contain any whitespace part-way through and
				// doesn't have a hash or child selector symbol in it should do the job fine
				value = value.Trim();
				if (value.Any(c => char.IsWhiteSpace(c)))
					throw new ArgumentException("Id values may not contain whitespace (other than leading and/or trailing whitespace, which will be stripped");
				if (value.Contains('#'))
					throw new ArgumentException("Id values may not contain the hash character");
				if (value.Contains('>'))
					throw new ArgumentException("Id values may not contain the child selector (\">\") character");

				Value = value;
			}

			/// <summary>
			/// This will never be null or blank
			/// </summary>
			public string Value { get; private set; }

			public override string ToString()
			{
				return base.ToString() + ":" + Value;
			}
		}
	}
}
