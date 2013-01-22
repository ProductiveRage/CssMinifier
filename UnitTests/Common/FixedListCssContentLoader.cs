using System;
using System.Collections.Generic;
using System.Linq;
using CSSMinifier.FileLoaders;

namespace UnitTests.Common
{
	public class FixedListCssContentLoader : ITextFileLoader
	{
		private List<TextFileContents> _files;
		public FixedListCssContentLoader(params TextFileContents[] files)
		{
			if (files == null)
				throw new ArgumentNullException("files");
			var filesTidied = new List<TextFileContents>();
			foreach (var file in files)
			{
				if (file == null)
					throw new ArgumentException("Null value encountered in files");
				filesTidied.Add(file);
			}
			_files = filesTidied;
		}

		public TextFileContents Load(string filename)
		{
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentException("Null/blank filename specified");
			var file = _files.FirstOrDefault(f => f.RelativePath.Replace("/", "\\").Equals(filename.Replace("/", "\\"), StringComparison.InvariantCultureIgnoreCase));
			if (file == null)
				throw new ArgumentException("Unsupported filename: " + filename);
			return file;
		}
	}
}
