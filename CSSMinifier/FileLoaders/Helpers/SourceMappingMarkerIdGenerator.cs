using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSSMinifier.FileLoaders.Helpers
{
	/// <summary>
	/// TODO
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

			// Generate the insertion token such that a new id is added and separated from the real declaration header with a comma
			var markerContent = string.Format(
				"#{0}_{1}, ",
				relativePathHtmlIdFriendly.Substring(startIndexOfLetterContent.Index),
				lineNumber
			);
			string alias;
			if (_markerIdsAndAliases.TryGetValue(markerContent, out alias))
				return alias;
			alias = string.Format(
				"#1{0},",
				NumberEncoder.Encode(_markerIdsAndAliases.Count)
			);
			_markerIdsAndAliases.Add(markerContent, alias);
			return alias;
		}

		public IEnumerable<string> GetInsertedMarkers()
		{
			return _markerIdsAndAliases.Values.ToList().AsReadOnly();
		}

		public IEnumerable<KeyValuePair<string, string>> GetAbbreviatedMarkerExtensions()
		{
			return _markerIdsAndAliases.ToDictionary(
				entry => entry.Value,
				entry => entry.Key
			);
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
