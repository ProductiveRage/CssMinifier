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
            );
            if (Request.FilePath.EndsWith(".less", StringComparison.InvariantCultureIgnoreCase))
            {
                cssLoader = new DotLessCssCssLoader(
                    cssLoader,
                    DotLessCssCssLoader.LessCssMinificationTypeOptions.Minify
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