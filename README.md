# On-the-fly CSS Minification and Combination

The classes here are intended to enable the easy construction of ASP.Net MVC stylesheet controllers that will recursively combine @import statements into a single file (respecting any media queries that are part of the import) and minifying the content, processing as [DotLessCss](http://http://www.dotlesscss.org) if a stylesheet with extension ".less" is requested. For example -

    public class CSSController : Controller
    {
        public ActionResult Process()
        {
            // This example code doesn't handle any caching but supports import-
            // flattening, LESS compilation, minification and psuedo source
            // mapping markers (see below)
            var cssLoader = (new DefaultNonCachedLessCssLoaderFactory(Server)).Get();
            var content = cssLoader.Load(Request.FilePath);
            return Content(content.Content, "text/css");
        }
    }

accessed by the following routes -

    // Have to set this to true so that stylesheets get processed rather get
    // processed rather than returned direct as static files
    routes.RouteExistingFiles = true;
    routes.MapRoute(
        "Stylesheets",
        "{*allwithextension}",
        new { controller = "CSS", action = "Process" },
        new { allwithextension = @".*\.(css|less)(/.*)?" }
    );

The CSSController in the full example implementation (in the **CSSMinifierDemo** project in the repository; see the [CSSController](https://bitbucket.org/DanRoberts/cssminifier/src/f4b3050b31f0bb9576c5d317b0a9eebfd7667953/CSSMinifierDemo/Controllers/CSSController.cs) file) only supports combination of stylesheets that are in the same folder* but it incorporates caching to disk and in-memory based on the last-modified-date of files such that there is no delay between updating files and the minified content being re-generated and returned in subsequent requests. It also has full support for returning 304 responses when files have not changed as well as support for applying gzip or deflate compression to returned data.

\* _This is a considered compromise; if stylesheets can import other stylesheets from any arbitrary location, then in order to determine whether any of them have changed - and so cached data should be expired so that the changes may be immediately reflected - processing of the entire import chain would be required. If all imports must be within the same folder then a shortcut can be taken by considering the most recent last-modified-date of any file in that folder. There's a chance that cache entries will be invalidated more often than necessary (if a file that isn't imported into a given stylesheet is updated), but it also means that cache entries will definitely be expired if any file in its import chain is updated. If you disagree with this approach then all behaviour can be changed to use different ITextFileLoader and / or ILastModifiedDateRetriever implementations!_

## More advanced features

There is support for many additional features, not all of which are in the demo project yet but which are documented in the code and, to some extent, by unit tests.

The **LessCssLineNumberingTextFileLoader** can be used to inject additional "pseudo html ids" into the generated content to describe where in the non-combined-compiled-and-minified content particular style blocks came from - eg.

    // Source tracing example (test1.css)
    section {
        div.Whatever {
            h2 { font-weight: bold; }
        }
    }

will result in the following selector

    #test1.css_4, section div.Whatever h2 { font-weight: bold; }

In lieu of the kind of a full cross-browser source mapping solution for compiled / minified stylesheets, this can act as a way to track the compiled output back to source (so long as you consider the overhead of the additional markup a reasonable trade-off).

*Note that the **DefaultNonCachedLessCssLoaderFactory** as instantiated above will enable this functionality by default. It has alternate constructor signatures if you want to tweak its behaviour.*

The **MediaQueryGroupingCssLoader** will take all style blocks that are wrapped in media queries and move them below the non-media-query-wrapped content, but any media query blocks with the same criteria will be combined. This can improve performance on some devices, specifically mobiles, at the cost of manipulation of the content - if the ordering of your styles is significant then this could cause problems! (If this is the case then I recommend a look at my blog post [Non-cascading CSS](http://www.productiverage.com/Read/42)! :)

The **LessCssOpeningHtmlTagRenamer** and **DotLessCssCssLoader** classes can be used to remove the overhead of wrapping files in a html tag to restrict the scope of any LESS values or mixins defined in them (done to prevent them over-writing any pre-existing values or mixins with the same name). This is also covered in [Non-cascading CSS](http://www.productiverage.com/Read/42). Essentially if you have source content:

    html
    {
        @color: #4D926F;
        div.Header
        {
            color: @color;
        }
    }

then the value "@color" will will not be overwritten in the parent scope, if it exists, but this is compiled to

    html div.header{color:#4D926F}

But use of **LessCssOpeningHtmlTagRenamer** and **DotLessCssCSSLoader** can allow the html tag to exist in the source and for the scope to be restricted, but for the output to be:

    div.header{color:#4D926F}

*The **DotLessCssCSSLoader**'s primary purpose is to compile LESS content into vanilla CSS but it also has a few additional tricks up its sleeves. One of them is to remove particular tags from selectors, such as in this case. The **LessCssOpeningHtmlTagRenamer** will identify those scope-identifying tags and rename them with "REMOVEME", the **DotLessCssCSSLoader** will then be configured to remove any instances of "REMOVEME" from any generated selectors).*

All of these "advanced options" are enabled if the **EnhancedNonCachedLessCssLoaderFactory** is used instead of the **DefaultNonCachedLessCssLoaderFactory** in the first example. The most straight-forward instantiation from within a Controller is the same:

    public class CSSController : Controller
    {
        public ActionResult Process()
        {
            // This supports import-flattening, LESS compilation, minification,
            // psuedo source mapping markers, scope-restricting-html-tag removal
            // and media-query-grouping
            var cssLoader = (new EnhancedNonCachedLessCssLoaderFactory(Server)).Get();
            var content = cssLoader.Load(Request.FilePath);
            return Content(content.Content, "text/css");
        }
    }
