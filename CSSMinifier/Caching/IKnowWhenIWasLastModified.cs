using System;

namespace CSSMinifier.Caching
{
	public interface IKnowWhenIWasLastModified
	{
		DateTime LastModified { get;  }
	}
}
