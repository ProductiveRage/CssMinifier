using System;
using System.Collections.Generic;
using System.Linq;

namespace CSSMinifier.FileLoaders.Helpers
{
	/// <summary>
	/// This class may be used with the LessCssLineNumberingTextFileLoader, which enables the insertion of arbitrary markers at the starts of selectors - this implementation
	/// will generate marker ids based on the filename and line number in the source in which the selector that is being added to was found
	/// </summary>
	public sealed class SourceMappingMarkerIdGenerator
	{
		private static char[] _allowedNonAlphaNumericCharacters;
		private static char[] _allowedNonAlphaNumericCharactersPlusPeriod;
		static SourceMappingMarkerIdGenerator()
		{
			_allowedNonAlphaNumericCharacters = new[] { '_', '-', '.' };
			_allowedNonAlphaNumericCharactersPlusPeriod = _allowedNonAlphaNumericCharacters.Concat(new[] { '.' }).ToArray();
		}

		private readonly List<string> _insertedMarkers;
		public SourceMappingMarkerIdGenerator()
		{
			_insertedMarkers = new List<string>();
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

			// We'll leave in any "." characters since we want it to appear like "#ProductDetail.css_123"
			var relativePathHtmlIdFriendly = TryToGetHtmlIdFriendlyVersion(relativePath, allowPeriod: true);
			if (relativePathHtmlIdFriendly == null)
				return "";

			// Generate the insertion token such that a new id is added and separated from the real declaration header with a comma (we keep track of the ids
			// that have been inserted (eg. "test.css_12") but have to return CSS content that may be injected into the start of a selector and so append a
			// comma to it (eg. "test.css_12,")
			var markerId = $"#{relativePathHtmlIdFriendly}_{lineNumber}";
			_insertedMarkers.Add(markerId);
			return markerId + ",";
		}

		/// <summary>
		/// Try to manipulate a string such that it may be used for an html id (stripping or replacing invalid characters, ensuring that it doesn't start with
		/// a number, etc..) - it may not be possible, in which case null will be returned (this will never return a blank string)
		/// </summary>
		public static string TryToGetHtmlIdFriendlyVersion(string value)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			return TryToGetHtmlIdFriendlyVersion(value, allowPeriod: false);
		}

		private static string TryToGetHtmlIdFriendlyVersion(string value, bool allowPeriod)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			// Make into a html-id-valid form
			var relativePathHtmlIdFriendly = "";
			for (var index = 0; index < value.Length; index++)
			{
				if (!_allowedNonAlphaNumericCharacters.Contains(value[index]) && !char.IsLetter(value[index]) && !char.IsNumber(value[index]))
					relativePathHtmlIdFriendly += "_";
				else
					relativePathHtmlIdFriendly += value[index];
			}

			// Remove any runs of "_" that we've are (presumablY) a result of the above manipulation
			while (relativePathHtmlIdFriendly.IndexOf("__") != -1)
				relativePathHtmlIdFriendly = value.Replace("__", "_");

			// Ids must start with a letter, so try to find the first letter in the content (if none, then return "" to indicate no insertion required)
			var startIndexOfLetterContent = relativePathHtmlIdFriendly
				.Select((character, index) => new { Character = character, Index = index })
				.FirstOrDefault(c => char.IsLetter(c.Character));
			if (startIndexOfLetterContent == null)
				return null;

			relativePathHtmlIdFriendly = relativePathHtmlIdFriendly.Substring(startIndexOfLetterContent.Index).Trim();
			return (relativePathHtmlIdFriendly == "") ? null : relativePathHtmlIdFriendly;
		}

		public IEnumerable<string> GetInsertedMarkerIds()
		{
			return _insertedMarkers.AsReadOnly();
		}
	}
}
