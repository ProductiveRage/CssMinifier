# On-the-fly CSS Minification and Combination

The classes here are intended to enable the easy construction of ASP.Net MVC stylesheet controllers that will recursively combine @import statements into a single file (respecting any media queries that are part of the import) and minifying the content, processing as [DotLessCss](http://http://www.dotlesscss.org) if a stylesheet with extension ".less" is requested. For example -

    public class CSSController : Controller
    {
        public ActionResult Process()
        {
            ITextFileLoader cssLoader = new SameFolderImportFlatteningCssLoader(
                new SimpleTextFileContentLoader(
                    new ServerUtilityPathMapper(Server)
                )
                SameFolderImportFlatteningCssLoader.ContentLoaderCommentRemovalBehaviourOptions.ContentIsUnprocessed,
                SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.DisplayWarningAndIgnore,
                SameFolderImportFlatteningCssLoader.ErrorBehaviourOptions.DisplayWarningAndIgnore,
                new NullLogger()
            );
            if (Request.FilePath.EndsWith(".less", StringComparison.InvariantCultureIgnoreCase))
            {
                cssLoader = new DotLessCssCssLoader(
                    cssLoader,
                    DotLessCssCssLoader.LessCssMinificationTypeOptions.Minify
                    DotLessCssCssLoader.ReportedErrorBehaviourOptions.LogAndContinue,
                    new NullLogger()
                );
            }
            else
                cssLoader = new MinifyingCssLoader(cssLoader);
            var content = cssLoader.Load(Request.FilePath);
            return Content(content.Content, "text/css");
        }
    }

accessed by the following routes -

    routes.RouteExistingFiles = true; // Have to set this to true so that stylesheets get processed rather than returned direct
    routes.MapRoute(
        "StandardStylesheets",
        "{*allwithextension}",
        new { controller = "CSS", action = "Process" },
        new { allwithextension = @".*\.css(/.*)?" }
    );
    routes.MapRoute(
        "LessCssStylesheets",
        "{*allwithextension}",
        new { controller = "CSS", action = "Process" },
        new { allwithextension = @".*\.less(/.*)?" }
    );

The CSSController in the full implementation here only supports combination of stylesheets that are in the same folder but it incorporates caching based on the last-modified-date of files such that there is no delay between updating files and the minified content being re-generated and returned in subsequent requests. It also has full support for returning 304 responses when files have not changed as well as support for applying gzip or deflate compression to returned data.

## More advanced features

There is support for many additional features that aren't in the demo project yet but which are documented in the code and, to some extent, by unit tests.

The **LessCssLineNumberingTextFileLoader** and **InjectedIdTidyingTextFileLoader** classes can inject additional "pseudo html ids" into the generated content to describe where in the non-combined-compiled-and-minified content particular style blocks came from - eg.

    // Source tracing example (test1.css)
    section {
        div.Whatever {
            h2 { font-weight: bold; }
        }
    }

may result in the following selector

    section div.Whatever #test1.css_4, section div.Whatever h2 { font-weight: bold; }

In lieu of the kind of source mapping that can be done with javascript this can act as a way to track the compiled output back to source, if you consider the overhead of the additional markup a reasonable trade-off.

The **MediaQueryGroupingCssLoader** will take all style blocks that are wrapped in media queries and move them below the non-media-query-wrapped content, but any media query blocks with the same criteria will be combined. This can improve performance on some devices, specifically mobiles, at the cost of manipulation of the content - if the ordering of your styles is significant then this could cause problems! (If this is the case then I recommend a look at my blog post [Non-cascading CSS](http://www.productiverage.com/Read/42)! :)

The **LessCssOpeningBodyTagRenamer** and **ContentReplacingTextFileLoader** classes can be used to remove the overhead of wrapping files in a body tag to restrict the scope of any LESS values or mixins defined in them, to prevent them over-writing any pre-existing values or mixins with the same name. This is also covered in the post linked above - essentially if you have source content

    body
    {
        @color: #4D926F;
        div.Header
        {
            color: @color;
        }
    }

then the value "@color" will will not be overwritten in the parent scope, if it exists, but this is compiled to

    body div.header{color:#4D926F}

But use of **LessCssOpeningBodyTagRenamer** and **ContentReplacingTextFileLoader** can allow the body tag to exist in the source and for the scope to be restricted, but for the output to be

    div.header{color:#4D926F}
