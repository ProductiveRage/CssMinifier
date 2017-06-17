using System;
using CSSMinifier.FileLoaders;
using UnitTests.Common;
using Xunit;

namespace UnitTests
{
	public sealed class LessCssKeyFrameScoperTests
	{
		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimation()
		{
			var content = "html { @keyframes my-animation { } .toBeAnimated { animation: my-animation 2s; } }";
			var expected = "html { @keyframes test1_my-animation { } .toBeAnimated { animation: test1_my-animation 2s; } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimationName()
		{
			var content = "html { @keyframes my-animation { } .toBeAnimated { animation-name: my-animation; } }";
			var expected = "html { @keyframes test1_my-animation { } .toBeAnimated { animation-name: test1_my-animation; } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingMultipleAnimationName()
		{
			var content = "html { @keyframes my-animation { } @keyframes my-animation2 { } .toBeAnimated { animation-name: my-animation my-animation2; } }";
			var expected = "html { @keyframes test1_my-animation { } @keyframes test1_my-animation2 { } .toBeAnimated { animation-name: test1_my-animation test1_my-animation2; } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}


		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimation_DurationThenAnimationName()
		{
			var content = "html { @keyframes my-animation { } .toBeAnimated { animation: 2s my-animation; } }";
			var expected = "html { @keyframes test1_my-animation { } .toBeAnimated { animation: 2s test1_my-animation; } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimation_KeyFramesComesAfterTargetSelector()
		{
			var content = "html { .toBeAnimated { animation: my-animation 2s; } @keyframes my-animation { } }";
			var expected = "html { .toBeAnimated { animation: test1_my-animation 2s; } @keyframes test1_my-animation { } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		/// <summary>
		/// If a prefix can't be generated from the filename then an An auto-generated prefix will be used of the form 'scope' + filename-with-css/less-extension.GetHashCode()
		/// </summary>
		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimation_FilenameUnsuitableForPrefix()
		{
			var content = "html { @keyframes my-animation { } .toBeAnimated { animation: my-animation 2s; } }";

			var prefix = "scope" + "1".GetHashCode();
			var expected = "html { @keyframes " + prefix + "_my-animation { } .toBeAnimated { animation: " + prefix + "_my-animation 2s; } }";

			var filename = "1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NonNestedContent()
		{
			var content = "@keyframes my-animation { } .toBeAnimated { animation: my-animation 2s; }";
			var expected = content;

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}

		[Fact]
		public void NestedKeyFramesAndSelectorSpecifyingAnimation_VendorSpecific()
		{
			var content = "html { @-webkit-keyframes my-animation { } .toBeAnimated { -webkit-animation: my-animation 2s; } }";
			var expected = "html { @-webkit-keyframes test1_my-animation { } .toBeAnimated { -webkit-animation: test1_my-animation 2s; } }";

			var filename = "test1.css";
			var contentLoader = new LessCssKeyFrameScoper(
				new FixedListCssContentLoader(
					new TextFileContents(filename, new DateTime(2017, 6, 17, 18, 24, 0), content)
				)
			);

			Assert.Equal(
				expected,
				contentLoader.Load(filename).Content
			);
		}
	}
}
