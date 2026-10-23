using Android.Graphics.Pdf;
using Android.OS;
using Android.Print;
using Android.Runtime;
using Android.Webkit;

namespace Splitt.App;

/// <summary>
/// Renders HTML to a PDF through the WebView's own print pipeline, so the file is real
/// vector output: text stays text with Vazirmatn embedded, sharp at any zoom and
/// selectable. Going through the web engine is also what makes Persian shape and run
/// right-to-left correctly without shipping a PDF library.
///
/// An earlier version drew the WebView onto a PdfDocument canvas. That needed a software
/// layer, which paints into a bitmap first, so every page was a 72dpi image.
///
/// The print adapter's result callbacks cannot be subclassed from C#, so the small Java
/// class in Java/PdfPrinter.java drives it. Pagination still happens upstream: the HTML is
/// built as fixed A4 blocks and the print stylesheet below breaks after each one.
/// </summary>
public static class HtmlToPdf
{
    /// <summary>
    /// The report is laid out on a 595x842px sheet (A4 in points), but print lays out A4 at
    /// 96dpi, 794x1123px. Zoom bridges the two; it is taken from the height and rounded down
    /// so a sheet can never spill a hairline onto an extra blank page.
    /// </summary>
    private const string PrintCss = """
        @page { size: A4; margin: 0; }
        html { zoom: 1.3325; }
        .page { break-after: page; }
        .page:last-child { break-after: auto; }
        * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        """;

    public static async Task RenderAsync(string html, int pageCount, string filePath)
    {
        html = html.Replace("</style>", PrintCss + "</style>");

        var loaded = new TaskCompletionSource<Android.Webkit.WebView>();
        var written = new TaskCompletionSource<string?>();
        Android.Views.ViewGroup? host = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var webView = new Android.Webkit.WebView(Platform.AppContext);
            webView.Settings.JavaScriptEnabled = false;
            webView.SetWebViewClient(new LoadedClient(loaded));

            // A WebView only initialises its renderer once attached to a window - detached,
            // it never finishes loading. Park it off to the side of the screen so it renders
            // for real without ever being visible.
            host = Platform.CurrentActivity?.FindViewById<Android.Views.ViewGroup>(
                Android.Resource.Id.Content);
            webView.TranslationX = 10_000;
            host?.AddView(webView, new Android.Views.ViewGroup.LayoutParams(595, 842));

            // The base URL is what lets @font-face reach the bundled Vazirmatn files.
            webView.LoadDataWithBaseURL("file:///android_asset/", html, "text/html", "UTF-8", null);
        });

        var view = await loaded.Task;

        // OnPageFinished fires before the webfonts have been applied; give layout a beat.
        await Task.Delay(500);

        var output = ParcelFileDescriptor.Open(
            new Java.IO.File(filePath),
            ParcelFileMode.Create | ParcelFileMode.Truncate | ParcelFileMode.WriteOnly)!;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var attributes = new PrintAttributes.Builder()
                    .SetMediaSize(PrintAttributes.MediaSize.IsoA4!)!
                    .SetResolution(new PrintAttributes.Resolution("pdf", "pdf", 600, 600))!
                    .SetMinMargins(PrintAttributes.Margins.NoMargins!)!
                    .Build();

                var adapter = view.CreatePrintDocumentAdapter("report");
                Write(adapter, attributes, output, new DoneCallback(written));
            });

            var error = await written.Task;
            if (error is not null)
                throw new InvalidOperationException($"PDF export {error}");
        }
        finally
        {
            output.Close();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                host?.RemoveView(view);
                view.Destroy();
            });
        }

        // The formatter decides the page breaks; if print ever paginates differently, the
        // zoom or the page CSS above has drifted and pages are blank or split.
        using var pdf = new PdfRenderer(ParcelFileDescriptor.Open(
            new Java.IO.File(filePath), ParcelFileMode.ReadOnly)!);
        if (pdf.PageCount != pageCount)
            Android.Util.Log.Warn("HtmlToPdf", $"expected {pageCount} pages, printed {pdf.PageCount}");
    }

    private static void Write(PrintDocumentAdapter adapter, PrintAttributes attributes,
        ParcelFileDescriptor output, DoneCallback done)
    {
        var type = JNIEnv.FindClass("android/print/PdfPrinter");
        try
        {
            var method = JNIEnv.GetStaticMethodID(type, "write",
                "(Landroid/print/PrintDocumentAdapter;Landroid/print/PrintAttributes;" +
                "Landroid/os/ParcelFileDescriptor;Landroid/webkit/ValueCallback;)V");
            JNIEnv.CallStaticVoidMethod(type, method,
                new JValue(adapter), new JValue(attributes), new JValue(output), new JValue(done));
        }
        finally
        {
            JNIEnv.DeleteGlobalRef(type);
        }
    }

    private sealed class LoadedClient(TaskCompletionSource<Android.Webkit.WebView> source)
        : WebViewClient
    {
        public override void OnPageFinished(Android.Webkit.WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            if (view is not null)
                source.TrySetResult(view);
        }
    }

    /// <summary>Receives null on success or the failure message from PdfPrinter.</summary>
    private sealed class DoneCallback(TaskCompletionSource<string?> source)
        : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => source.TrySetResult(value?.ToString());
    }
}
