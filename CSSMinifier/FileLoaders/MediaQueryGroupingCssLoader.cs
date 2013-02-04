using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSSParser;
using CSSParser.ContentProcessors;
using CSSParser.ContentProcessors.StringProcessors;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will combine CSS style blocks that are wrapped in media queries, so long as the media queries are identical. The media query blocks will be rearranged
	/// so that they appear after any non-media-query-wrapped content. This may introduce side effects, depending upon how the source CSS has been written. It is
	/// recommedend that minified CSS is passed into the processor since the media query definition comparisons are not very aggressive (so if one media query
	/// has a comment between some of the terms and another doesn't then they won't be found to match, likewise if there are any variations in whitespace around
	/// the media query definitions). It will still operate if the provided content has not been minified, but there is a chance that the media queries will not
	/// be grouped as efficiently. The provided content must be valid CSS or the behaviour is not defined - content may get rearranged in an unexpected manner.
	/// This functionality may be required as there are thought to be performance implications for large numbers of media-query-wrapped style blocks such
	/// that performance would be improved by grouping those with the same criteria.
	/// </summary>
	public class MediaQueryGroupingCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		public MediaQueryGroupingCssLoader(ITextFileLoader contentLoader)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");

			_contentLoader = contentLoader;
		}

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank relativePath.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var content = _contentLoader.Load(relativePath);
			return new TextFileContents(
				content.RelativePath,
				content.LastModified,
				Process(content.Content)
			);
		}

		private string Process(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			var nonMediaQueryWrappedContentBuilder = new StringBuilder();
			var mediaQueryDefinitionBuffer = new StringBuilder();
			var mediaQueryWrappedContent = new Dictionary<string, List<CategorisedCharacterString>>();
			var processingStateDetails = new ProcessStateDetails(ProcessingStateOptions.InNonMediaQueryWrappedContent, null);
			foreach (var segment in Parser.ParseCSS(content))
			{
				// If we're in a media query header, have we reach the open brace for the media-query-wrapped content? If so, then update the
				// processingStateDetails and move on. If not, then add to the media query header content and move on.
				if (processingStateDetails.ProcessingState == ProcessingStateOptions.InMediaQueryDefinition)
				{
					if (segment.CharacterCategorisation == CharacterCategorisationOptions.OpenBrace)
					{
						var mediaQuery = mediaQueryDefinitionBuffer.ToString();
						mediaQueryDefinitionBuffer.Clear();

						List<CategorisedCharacterString> mediaQueryWrappedContentEntry;
						if (mediaQueryWrappedContent.ContainsKey(mediaQuery))
							mediaQueryWrappedContentEntry = mediaQueryWrappedContent[mediaQuery];
						else
						{
							mediaQueryWrappedContentEntry = new List<CategorisedCharacterString>();
							mediaQueryWrappedContent.Add(mediaQuery, mediaQueryWrappedContentEntry);
						}

						processingStateDetails = new ProcessStateDetails(
							ProcessingStateOptions.InMediaQueryWrappedSelector,
							mediaQueryWrappedContentEntry
						);

						// Note: Not storing the opening brace of the media query since braces will be inserted later when media query content
						// blocks are combined (where possible)
						continue;
					}
					
					mediaQueryDefinitionBuffer.Append(segment.Value);
					continue;
				}

				// If we're in media-query-wrapped selector content, have we reached the end of it yet by entering a style block or by reaching the
				// end of the media query content? If so, then update the processingStateDetails appropriately and move on. If not, then add to the
				// media-query-wrapped content and move on.
				if (processingStateDetails.ProcessingState == ProcessingStateOptions.InMediaQueryWrappedSelector)
				{
					if (segment.CharacterCategorisation == CharacterCategorisationOptions.OpenBrace)
					{
						processingStateDetails = new ProcessStateDetails(ProcessingStateOptions.InMediaQueryWrappedStyleBlock, processingStateDetails.MediaQueryContent);
						processingStateDetails.MediaQueryContent.Add(segment);
						continue;
					}

					if (segment.CharacterCategorisation == CharacterCategorisationOptions.CloseBrace)
					{
						processingStateDetails = new ProcessStateDetails(ProcessingStateOptions.InNonMediaQueryWrappedContent, null);

						// Note: Not storing the closing brace of the media query since braces will be inserted later when media query content
						// blocks are combined (where possible)
						continue;
					}

					processingStateDetails.MediaQueryContent.Add(segment);
					continue;
				}

				// If we're in a media-query-wrapped style block, have we reached the end of it yet by encountering a closing brace? If so, then
				// update the processingStateDetails appropriately. Either way, the segment should be added to the media-query-wrapped content
				// and we can then move on to the next segment.
				if (processingStateDetails.ProcessingState == ProcessingStateOptions.InMediaQueryWrappedStyleBlock)
				{
					if (segment.CharacterCategorisation == CharacterCategorisationOptions.CloseBrace)
						processingStateDetails = new ProcessStateDetails(ProcessingStateOptions.InMediaQueryWrappedSelector, processingStateDetails.MediaQueryContent);

					processingStateDetails.MediaQueryContent.Add(segment);
					continue;
				}

				// Are we entering a media query header? If so, then update the processingStateDetails and move on. If not, then add to the
				// non-media-query-wrapped content and move on.
				if (processingStateDetails.ProcessingState == ProcessingStateOptions.InNonMediaQueryWrappedContent)
				{
					if ((segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty) && (segment.Value == "@media"))
					{
						processingStateDetails = new ProcessStateDetails(ProcessingStateOptions.InMediaQueryDefinition, null);
						mediaQueryDefinitionBuffer.Append(segment.Value);
						continue;
					}

					nonMediaQueryWrappedContentBuilder.Append(segment.Value);
					continue;
				}

				throw new Exception("Unsupported ProcessingState: " + processingStateDetails.ProcessingState);
			}

			var restructuredContentBuilder = new StringBuilder();
			restructuredContentBuilder.Append(nonMediaQueryWrappedContentBuilder.ToString());
			foreach (var mediaQueryContent in mediaQueryWrappedContent)
			{
				restructuredContentBuilder.Append(mediaQueryContent.Key);
				restructuredContentBuilder.Append("{");
				foreach (var segment in mediaQueryContent.Value)
					restructuredContentBuilder.Append(segment.Value);
				restructuredContentBuilder.Append("}");
			}
			return restructuredContentBuilder.ToString();
		}

		private class ProcessStateDetails
		{
			public ProcessStateDetails(ProcessingStateOptions processingState, List<CategorisedCharacterString> mediaQueryContent)
			{
				if (!Enum.IsDefined(typeof(ProcessingStateOptions), processingState))
					throw new ArgumentOutOfRangeException("processingState");
				if ((processingState == ProcessingStateOptions.InMediaQueryWrappedSelector) || (processingState == ProcessingStateOptions.InMediaQueryWrappedStyleBlock))
				{
					if (mediaQueryContent == null)
						throw new ArgumentException("A mediaQueryContentBuilder must be specified for the InMediaQueryWrappedSelector and InMediaQueryWrappedStyleBlock processing states");
				}
				else
				{
					if (mediaQueryContent != null)
						throw new ArgumentException("A mediaQueryContentBuilder must not be specified for processing states other than InMediaQueryWrappedSelector and InMediaQueryWrappedStyleBlock");
				}

				ProcessingState = processingState;
				MediaQueryContent = mediaQueryContent;
			}

			public ProcessingStateOptions ProcessingState { get; private set; }
			
			/// <summary>
			/// This will be null for all states other than InNonMediaQueryWrappedContent and non-null-or-whitespace for InNonMediaQueryWrappedContent
			/// </summary>
			public List<CategorisedCharacterString> MediaQueryContent { get; private set; }
		}

		private enum ProcessingStateOptions
		{
			/// <summary>
			/// eg. within a "@media screen and (max-width:70em)" media query definition / header
			/// </summary>
			InMediaQueryDefinition,

			/// <summary>
			/// Selectors inside a media query - eg. "div.Header" within a media-query section
			/// </summary>
			InMediaQueryWrappedSelector,
			
			/// <summary>
			/// Style properties and values within a media-query-wrapped section
			/// </summary>
			InMediaQueryWrappedStyleBlock,
			
			/// <summary>
			/// Content that is nothing to do with media-query-wrapped styles
			/// </summary>
			InNonMediaQueryWrappedContent
		}
	}
}
