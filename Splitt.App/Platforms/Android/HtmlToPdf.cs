using Android.Graphics.Pdf;
using Android.Views;
using Android.Webkit;

namespace Splitt.App;

/// <summary>
/// Renders HTML to a PDF by laying it out in an offscreen WebView and drawing that view
/// onto a PdfDocument canvas. Going through the web engine is what makes Persian shape
/// and run right-to-left correctly without shipping a PDF library, and drawing to the
/// canvas keeps the text as text rather than a bitmap.
///
/// Android's own print adapter would paginate for us, but its result callbacks have no
/// public Java constructor, so they cannot be subclassed from C#. Pagination therefore
/// happens upstream: the HTML is built as fixed-height A4 blocks, and each is drawn onto
/// its own page by translating the canvas. Page breaks land where the caller put them.
/// </summary>
public static class HtmlToPdf
{
    /// <summary>A4 at 72dpi, in points — one CSS pixel maps to one point.</summary>
    public const int PageWidth = 595;
    public const int PageHeight = 842;

    public static async Task RenderAsync(string html, int pageCount, string filePath)
    {
        var totalHeight = PageHeight * pageCount;
        var loaded = new TaskCompletionSource<Android.Webkit.WebView>();
        Android.Views.ViewGroup? host = null;
        Android.Webkit.WebView? view = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var webView = new Android.Webkit.WebView(Platform.AppContext);
            webView.Settings.JavaScriptEnabled = false;
            // Without these the WebView ignores the page's viewport and lays out in
            // density-independent pixels, so the content draws at the screen's density
            // (~2.6x) and only a corner of it lands on the page. Together they pin one
            // CSS pixel to one device pixel, which is what makes 595px mean A4 width.
            webView.Settings.UseWideViewPort = true;
            webView.Settings.LoadWithOverviewMode = false;
            webView.SetInitialScale(100);
            // A PdfDocument canvas cannot record hardware layers; software keeps the
            // drawing as vector operations instead of silently producing a blank page.
            webView.SetLayerType(LayerType.Software, null);
            webView.SetWebViewClient(new LoadedClient(loaded));

            // A WebView only initialises its renderer once attached to a window - detached,
            // it lays out and draws nothing at all. Park it off to the side of the screen
            // at full size so it renders for real without ever being visible.
            host = Platform.CurrentActivity?.FindViewById<Android.Views.ViewGroup>(
                Android.Resource.Id.Content);
            webView.TranslationX = 10_000;
            host?.AddView(webView, new Android.Views.ViewGroup.LayoutParams(PageWidth, totalHeight));

            // The base URL is what lets @font-face reach the bundled Vazirmatn files.
            webView.LoadDataWithBaseURL("file:///android_asset/", html, "text/html", "UTF-8", null);
        });

        view = await loaded.Task;

        // OnPageFinished fires before the webfonts have been applied; give layout a beat.
        await Task.Delay(500);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            view.Measure(
                Android.Views.View.MeasureSpec.MakeMeasureSpec(PageWidth, MeasureSpecMode.Exactly),
                Android.Views.View.MeasureSpec.MakeMeasureSpec(totalHeight, MeasureSpecMode.Exactly));
            view.Layout(0, 0, PageWidth, totalHeight);

            var document = new PdfDocument();
            for (var i = 0; i < pageCount; i++)
            {
                var info = new PdfDocument.PageInfo.Builder(PageWidth, PageHeight, i + 1).Create();
                var page = document.StartPage(info);
                var canvas = page!.Canvas!;
                canvas.Translate(0, -i * PageHeight);
                view.Draw(canvas);
                document.FinishPage(page);
            }

            using (var stream = File.Create(filePath))
                document.WriteTo(stream);
            document.Close();

            host?.RemoveView(view);
            view.Destroy();
        });
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
}
