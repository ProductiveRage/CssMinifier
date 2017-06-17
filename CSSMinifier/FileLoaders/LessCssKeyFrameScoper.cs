using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSSMinifier.FileLoaders.Helpers;
using CSSParser;
using CSSParser.ContentProcessors;
using CSSParser.ContentProcessors.StringProcessors;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// LESS does not scoping of @keyframe declarations in the same way as it does variables - if a @keyframe declaration appears within a block then it will effectively
	/// be lifted straight into the global scope of final CSS content. With the variable scoping rules, two different style blocks may declare variables with the same name
	/// but there will be no conflict, which is not possible with @keyframe. This content loader enables an approximation; any @keyframe declarations that exist within a
	/// style block will be given a prefix relating to the relative path of the file and any references in the current file to the animation names will be changed accordingly.
	/// This makes it possible to define animations in a style sheet file and know that it will not conflict with any identically-named animations in other style sheets. (It
	/// only applies to nested declarations so that it remains possible to define animations to be shared across all style sheets, they just need to appear unnested).
	public sealed class LessCssKeyFrameScoper : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		public LessCssKeyFrameScoper(ITextFileLoader fileLoader)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");

			_fileLoader = fileLoader;
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
				Process(relativePath, unprocessedContent.Content)
			);
		}

		/// <summary>
		/// This will throw an exception for null content or if otherwise unable to satisfy the request, it will never return null
		/// </summary>
		private string Process(string relativePath, string content)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException($"Null/blank {nameof(relativePath)} specified");
			if (content == null)
				throw new ArgumentNullException("content");

			relativePath = relativePath.Trim();
			if (relativePath.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
				relativePath = relativePath.Substring(0, relativePath.Length - 4);
			else if (relativePath.EndsWith(".less", StringComparison.OrdinalIgnoreCase))
				relativePath = relativePath.Substring(0, relativePath.Length - 5);

			var prefixToInsert = SourceMappingMarkerIdGenerator.TryToGetHtmlIdFriendlyVersion(relativePath);
			if (prefixToInsert == null)
			{
				// If the path string doesn't contain any content that may be used as a "custom ident" then make something up because we need to use something as
				// the prefix, otherwise the animation won't be scoped to the current files
				prefixToInsert = "scope" + relativePath.GetHashCode();
			}

			// Note: Since we have enumerate the segments set twice (if any animation names are found to be renamed) then I considered calling ToArray on it so that the parsing
			// work would only be done once - however, this means that the content has to be full loaded into an array in memory and I don't have any information that indicates
			// that the additional memory use would be a worthwhile tradeoff for the double-parsing work (the parsing isn't very expensive and so doing it twice isn't necessarily
			// the worst thing in the world). If no animation names are found that need to be renamed then the parsing work will only be performed once. Similar logic is applied
			// to the result of the RewriteKeyFrameNames method - it needs to be executed once to determine whether any animation names need to be changed and (if any are found)
			// then it will be executed a second time when RewriteAnimationNames is called (but the RewriteKeyFrameNames work isn't very expensive and so there isn't necessarily
			// any harm in doing it twice).
			var segments = Parser.ParseLESS(content);
			var rewrittenNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Case insensitive (see https://developer.mozilla.org/en-US/docs/Web/CSS/custom-ident)
			segments = RewriteKeyFrameNames(
				segments,
				nestedAnimationName =>
				{
					var rewrittedName = prefixToInsert + "_" + nestedAnimationName;
					rewrittenNames[nestedAnimationName] = rewrittedName;
					return rewrittedName;
				}
			);
			ForceEvaluation(segments);
			if (!rewrittenNames.Any())
				return content;
			segments = RewriteAnimationNames(
				segments,
				animationName => rewrittenNames.ContainsKey(animationName) ? rewrittenNames[animationName] : animationName
			);
			var rewrittenContentBuilder = new StringBuilder();
			foreach (var segment in segments)
				rewrittenContentBuilder.Append(segment.Value);
			return rewrittenContentBuilder.ToString();
		}

		private static IEnumerable<CategorisedCharacterString> RewriteKeyFrameNames(IEnumerable<CategorisedCharacterString> segments, Func<string, string> nestedAnimationNameRewriteLookup)
		{
			if (segments == null)
				throw new ArgumentNullException(nameof(segments));
			if (nestedAnimationNameRewriteLookup == null)
				throw new ArgumentNullException(nameof(nestedAnimationNameRewriteLookup));

			var bracketCount = 0;
			var withinKeyFrameContent = false;
			foreach (var segment in segments)
			{
				if (segment == null)
					throw new ArgumentException($"Null reference encountered in {nameof(segments)} set");

				if (segment.CharacterCategorisation == CharacterCategorisationOptions.OpenBrace)
				{
					bracketCount++;
					withinKeyFrameContent = false;
				}
				else if (segment.CharacterCategorisation == CharacterCategorisationOptions.CloseBrace)
					bracketCount--;
				else if (segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty)
				{
					if (withinKeyFrameContent && (bracketCount > 0))
					{
						var rewrittenName = nestedAnimationNameRewriteLookup(segment.Value);
						if (rewrittenName != segment.Value)
						{
							yield return new CategorisedCharacterString(rewrittenName, segment.IndexInSource, segment.CharacterCategorisation);
							continue;
						}
					}
					else if (IsKeyFrameProperty(segment.Value))
						withinKeyFrameContent = true;
				}

				yield return segment;
			}
		}

		private static bool IsKeyFrameProperty(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException($"Null/blank {nameof(value)} specified");

			return value.StartsWith("@") && value.EndsWith("keyframes", StringComparison.OrdinalIgnoreCase);
		}

		private static IEnumerable<CategorisedCharacterString> RewriteAnimationNames(IEnumerable<CategorisedCharacterString> segments, Func<string, string> animationNameRewriteLookup)
		{
			if (segments == null)
				throw new ArgumentNullException(nameof(segments));
			if (animationNameRewriteLookup == null)
				throw new ArgumentNullException(nameof(animationNameRewriteLookup));

			var lastSelectorOrStylePropertyWasAnimation = false;
			foreach (var segment in segments)
			{
				if (segment == null)
					throw new ArgumentException($"Null reference encountered in {nameof(segments)} set");

				if (segment.CharacterCategorisation == CharacterCategorisationOptions.SelectorOrStyleProperty)
					lastSelectorOrStylePropertyWasAnimation = IsAnimationPropertyName(segment.Value);
				else if ((segment.CharacterCategorisation == CharacterCategorisationOptions.Value) && lastSelectorOrStylePropertyWasAnimation)
				{
					var rewrittenName = animationNameRewriteLookup(segment.Value);
					if (rewrittenName != segment.Value)
					{
						yield return new CategorisedCharacterString(rewrittenName, segment.IndexInSource, segment.CharacterCategorisation);
						continue;
					}
				}

				yield return segment;
			}
		}

		private static bool IsAnimationPropertyName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentException($"Null/blank {nameof(value)} specified");

			return
				value.Equals("animation", StringComparison.OrdinalIgnoreCase) ||
				value.Equals("animation-name", StringComparison.OrdinalIgnoreCase) ||
				value.EndsWith("-animation", StringComparison.OrdinalIgnoreCase) ||
				value.EndsWith("-animation-name", StringComparison.OrdinalIgnoreCase);
		}

		private static void ForceEvaluation<T>(IEnumerable<T> source)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			foreach (var item in source)
			{
				// This body is deliberately empty, we just want to enumerate the set
			}
		}
	}
}