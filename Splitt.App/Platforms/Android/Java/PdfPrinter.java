package android.print;

import android.os.CancellationSignal;
import android.os.ParcelFileDescriptor;
import android.webkit.ValueCallback;

/**
 * Drives a PrintDocumentAdapter straight to a file, with no print dialog.
 *
 * LayoutResultCallback and WriteResultCallback have hidden constructors, so they can only be
 * subclassed from code compiled into this package - C# cannot do it. This class is the whole
 * reason the Java source exists; everything else stays in HtmlToPdf.cs.
 *
 * The callback receives null on success, or the failure message.
 */
public final class PdfPrinter {
    private PdfPrinter() { }

    public static void write(final PrintDocumentAdapter adapter, PrintAttributes attributes,
                             final ParcelFileDescriptor output, final ValueCallback<String> done) {
        adapter.onStart();
        adapter.onLayout(null, attributes, new CancellationSignal(),
                new PrintDocumentAdapter.LayoutResultCallback() {
                    @Override
                    public void onLayoutFinished(PrintDocumentInfo info, boolean changed) {
                        adapter.onWrite(new PageRange[] { PageRange.ALL_PAGES }, output,
                                new CancellationSignal(),
                                new PrintDocumentAdapter.WriteResultCallback() {
                                    @Override
                                    public void onWriteFinished(PageRange[] pages) {
                                        adapter.onFinish();
                                        done.onReceiveValue(null);
                                    }

                                    @Override
                                    public void onWriteFailed(CharSequence error) {
                                        adapter.onFinish();
                                        done.onReceiveValue("write failed: " + error);
                                    }

                                    @Override
                                    public void onWriteCancelled() {
                                        adapter.onFinish();
                                        done.onReceiveValue("write cancelled");
                                    }
                                });
                    }

                    @Override
                    public void onLayoutFailed(CharSequence error) {
                        adapter.onFinish();
                        done.onReceiveValue("layout failed: " + error);
                    }

                    @Override
                    public void onLayoutCancelled() {
                        adapter.onFinish();
                        done.onReceiveValue("layout cancelled");
                    }
                }, null);
    }
}
