using System;
using System.Collections.Generic;
using System.Text;
using CSSParser;
using CSSParser.ContentProcessors;
using CSSParser.ContentProcessors.StringProcessors;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// If a LESS stylesheet wraps all of its styles in a html tag (eg. "html { /* All content here */ }") to restrict the scope of any LESS values and mixins, then it
	/// may be desirable to remove the html tags that will consequently prepend all of the generated CSS selectors. This can be achieved by wrapping the ITextFileLoader
	/// instances that load individual files in a LessCssOpeningHtmlTagRenamer and replacing the html tag with a particular string that can be removed by a
	/// ContentReplacingTextFileLoader when the processing and content minification is otherwise complete.
	/// 
	/// Note: The string that is inserted in place of the html tag should appear to be a valid selector otherwise the LESS processor may be unable to process the content.
	/// 
	/// Note: If this is to be used in conjunction with a LessCssLineNumberingTextFileLoader, then ensure that this appears first in the file loader chain otherwise a
	/// marker may be injected at the start of the content, preventing the opening "html" tag from being detected. Also note that this will not not work with files that
	/// have imports before the "html" tag, but this is acceptable for the intended use cases (where html-wrapped content is for standalone files, they wouldn't be expected
	/// to import anthing).
	/// </summary>
	public class LessCssOpeningHtmlTagRenamer : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		private readonly string _replaceOpeningHtmlTagWith;
		public LessCssOpeningHtmlTagRenamer(ITextFileLoader fileLoader, string replaceOpeningHtmlTagWith)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");
			if (string.IsNullOrWhiteSpace(replaceOpeningHtmlTagWith))
				throw new ArgumentException("Null/blank replaceOpeningHtmlTagWith specified");

			_fileLoader = fileLoader;
			_replaceOpeningHtmlTagWith = replaceOpeningHtmlTagWith;
		}

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

		/// <summary>
		/// This will throw an exception for null content or if otherwise unable to satisfy the request, it will never return null
		/// </summary>
		private string Process(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			var processedSegments = new List<CategorisedCharacterString>();
			var searchProgress = ScopeRestrictingHtmlTagSearchProgressOptions.LookingForHtmlTag;
			foreach (var segment in Parser.ParseLESS(content))
			{
				// No examination of Comment or Whitespace segments is required, just add them to the processedSegments list so that they don't
				// get lost and move on
				if ((segment.CharacterCategorisation == CharacterCategorisationOptions.Comment)
				|| (segment.CharacterCategorisation == CharacterCategorisationOptions.Whitespace))
				{
					processedSegments.Add(segment);
					continue;
				}

				switch (searchProgress)
				{
					// If we're yet to locate the opening html tag, and this isn't Comment or Whitespace content, then this segment has to be the
					// opening html tag or we'll have to return the content unprocessed (as it appears to not be html-scoped LESS content)
					case ScopeRestrictingHtmlTagSearchProgressOptions.LookingForHtmlTag:
						if ((segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty) && (segment.Value == "html"))
						{
							processedSegments.Add(segment);
							searchProgress = ScopeRestrictingHtmlTagSearchProgressOptions.LookingForHtmlTagOpeningBrace;
							continue;
						}
						return content;

					// If we HAVE located the opening html tag, the next non-Comment-or-Whitespace content must be an opening brace, otherwise
					// we'll have to return the content unprocessed (as it appears to not be html-scoped LESS content)
					case ScopeRestrictingHtmlTagSearchProgressOptions.LookingForHtmlTagOpeningBrace:
						if (segment.CharacterCategorisation == CharacterCategorisationOptions.OpenBrace)
						{
							processedSegments.Add(segment);
							searchProgress = ScopeRestrictingHtmlTagSearchProgressOptions.LookingForFirstSelectorOrStyle;
							continue;
						}
						return content;

					// If we've located the opening html tag's opening brace, we need to try to find the first selector-or-style-property and
					// try to confirm that it's a nested selector and not a style property
					case ScopeRestrictingHtmlTagSearchProgressOptions.LookingForFirstSelectorOrStyle:
						if (segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty)
						{
							processedSegments.Add(segment);
							searchProgress = ScopeRestrictingHtmlTagSearchProgressOptions.LookingToConfirmItsNotStyleProperty;
							continue;
						}
						return content;

					// Once we've located selector-or-style-property content within the the html block, we'll know it was a style property if
					// it's followed by a colon rather than another selector-or-style-property or an opening brace (if it's a style property
					// then we've not identified a scope-restricting html tag and so return the content unprocessed)
					case ScopeRestrictingHtmlTagSearchProgressOptions.LookingToConfirmItsNotStyleProperty:
						if (segment.CharacterCategorisation != CharacterCategorisationOptions.StylePropertyColon)
						{
							processedSegments.Add(segment);
							searchProgress = ScopeRestrictingHtmlTagSearchProgressOptions.SuccessfullyIdentifiedHtmlTagToReplace;
							break;
						}
						return content;

					default:
						throw new Exception("Encountered unexpected searchProgress value: " + searchProgress.ToString());
				}
				if (searchProgress == ScopeRestrictingHtmlTagSearchProgressOptions.SuccessfullyIdentifiedHtmlTagToReplace)
					break;
			}
			if (searchProgress != ScopeRestrictingHtmlTagSearchProgressOptions.SuccessfullyIdentifiedHtmlTagToReplace)
				return content;

			// If we HAVE identified an opening html tag then we'll want to rewrite the content
			// - Write out the segments we had to process, renaming the html tag
			var rewrittenContentBuilder = new StringBuilder();
			foreach (var segment in processedSegments)
			{
				if ((segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty) && (segment.Value == "html"))
					rewrittenContentBuilder.Append(_replaceOpeningHtmlTagWith);
				else
					rewrittenContentBuilder.Append(segment.Value);
			}
			// - Write out the rest of the content, after the last segment we DID process
			var finalProcessedSegment = processedSegments[processedSegments.Count - 1];
			rewrittenContentBuilder.Append(
				content.Substring(finalProcessedSegment.IndexInSource + finalProcessedSegment.Value.Length)
			);
			return rewrittenContentBuilder.ToString();
		}

		private enum ScopeRestrictingHtmlTagSearchProgressOptions
		{
			LookingForHtmlTag,
			LookingForHtmlTagOpeningBrace,
			LookingForFirstSelectorOrStyle,
			LookingToConfirmItsNotStyleProperty,
			SuccessfullyIdentifiedHtmlTagToReplace
		}
	}
}
