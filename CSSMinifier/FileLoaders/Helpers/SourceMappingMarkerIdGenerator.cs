using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSSMinifier.FileLoaders.Helpers
{
	/// <summary>
	/// This class may be used with the LessCssLineNumberingTextFileLoader, which enables the insertion of arbitrary markers at the starts of selectors - this implementation
	/// will generate marker ids based on the filename and line number in the source in which the selector that is being added to was found. It will insert "abbreviated" ids
	/// so that the content doesn't balloon as much when nested LESS selectors are used. After the content has been LESS-compiled, the MultiContentReplacingTextFileLoader
	/// may be used to replace all of the abbreviated ids with the full versions. The abbreviated ids all have a "1" prefix which were not valid ids before html5, this
	/// is to try to reduce the chance that an abbreviated id (that will be replaced out) will be the name of a real id in use in the stylesheets.
	/// </summary>
	public class SourceMappingMarkerIdGenerator
	{
		// We'll leave in any "." characters since we want it to appear like "#ProductDetail.css_123"
		private static char[] AllowedNonAlphaNumericCharacters = new[] { '_', '-', '.' };

		private readonly Dictionary<string, string> _markerIdsAndAliases;
		public SourceMappingMarkerIdGenerator()
		{
			_markerIdsAndAliases = new Dictionary<string, string>();
		}

		/// <summary>
		/// This will generate a html-id-type string to insert into the markup, based on the filename and line number - eg. "#ProductDetail.css_1418," (the trailing
		/// comma is required for it to be inserted into the start of existing declaration header)
		/// </summary>
		public string MarkerGenerator(string relativePath, int lineNumber)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");
			if (lineNumber <= 0)
				throw new ArgumentOutOfRangeException("lineNumber", "must be greater than zero");

			// Since we're requiring all files to be in the same folder, if there was a relative path to the first file (which may then include others),
			// we may as well remove that relative path and consider the filename only
			relativePath = relativePath.Replace("\\", "/").Split('/').Last();

			// Make into a html-id-valid form
			var relativePathHtmlIdFriendly = "";
			for (var index = 0; index < relativePath.Length; index++)
			{
				if (!AllowedNonAlphaNumericCharacters.Contains(relativePath[index]) && !char.IsLetter(relativePath[index]) && !char.IsNumber(relativePath[index]))
					relativePathHtmlIdFriendly += "_";
				else
					relativePathHtmlIdFriendly += relativePath[index];
			}

			// Remove any runs of "_" that we've are (presumablY) a result of the above manipulation
			while (relativePathHtmlIdFriendly.IndexOf("__") != -1)
				relativePathHtmlIdFriendly = relativePath.Replace("__", "_");

			// Ids must start with a letter, so try to find the first letter in the content (if none, then return "" to indicate no insertion required)
			var startIndexOfLetterContent = relativePathHtmlIdFriendly
				.Select((character, index) => new { Character = character, Index = index })
				.FirstOrDefault(c => char.IsLetter(c.Character));
			if (startIndexOfLetterContent == null)
				return "";

			// Generate the insertion token such that a new id is added and separated from the real declaration header with a comma (we'll keep actual id
			// content in the markerIdsAndAliases dictionary but we need to return a blob of CSS that can be inserted into the front of a selector and so
			// we'll need a comma there otherwise we'll break the selector)
			var markerId = string.Format(
				"#{0}_{1}",
				relativePathHtmlIdFriendly.Substring(startIndexOfLetterContent.Index),
				lineNumber
			);
			string aliasId;
			if (!_markerIdsAndAliases.TryGetValue(markerId, out aliasId))
			{
				aliasId = string.Format(
					"#1{0}",
					NumberEncoder.Encode(_markerIdsAndAliases.Count)
				);
				_markerIdsAndAliases.Add(markerId, aliasId);
			}
			return aliasId + ",";
		}

		public IEnumerable<string> GetInsertedMarkers()
		{
			// The actual markers that we inserted had commas at the end, so we need to add them in here too for consistency
			return _markerIdsAndAliases.Values.Select(id => id + ",");
		}

		public IEnumerable<KeyValuePair<string, string>> GetAbbreviatedMarkerExtensions()
		{
			// When requesting the abbreviated-to-full lookup data, we return the ids only (no trailing commas). The replacements are returned in descending
			// length order to ensure (if the replacements are performed in the order specified here) that incorrect partial replacements aren't made (eg.
			// if there are ids "#1A" an "#1A1" then we need to replace all instances of "#1A1" first otherwise the "#1A" replacement will overwrite parts
			// of the "#1A1" abbreviated ids, which will make the output appear corrupted)
			return _markerIdsAndAliases
				.Select(entry => new KeyValuePair<string, string>(entry.Value, entry.Key))
				.OrderByDescending(entry => entry.Key.Length);
		}

		private static class NumberEncoder
		{
			private const string CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";

			/// <summary>
			/// This will throw an exception for a negative number, it will never return null or an empty string
			/// </summary>
			public static string Encode(int value)
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException("value", "must be zero or greater");

				if (value == 0)
					return CHARS.Substring(0, 1);
				var stringBuilder = new StringBuilder();
				while (value > 0)
				{
					stringBuilder.Append(CHARS[value % CHARS.Length]);
					value = value / CHARS.Length;
				}
				return stringBuilder.ToString();
			}
		}
	}
}
