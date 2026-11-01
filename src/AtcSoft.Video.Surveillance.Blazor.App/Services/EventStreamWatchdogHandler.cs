namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// Hands the event stream's response (GET /events) to the
/// <see cref="EventStreamWatchdog"/>; every other response passes as it is.
/// </summary>
public sealed class EventStreamWatchdogHandler(EventStreamWatchdog watchdog) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode &&
            string.Equals(ApiRequest.OperationOf(request), ApiOperations.ListEvents, StringComparison.Ordinal))
        {
            response.Content = new WatchedContent(response.Content, watchdog);
        }

        return response;
    }

    private sealed class WatchedContent : HttpContent
    {
        private readonly HttpContent inner;
        private readonly EventStreamWatchdog watchdog;

        public WatchedContent(
            HttpContent inner,
            EventStreamWatchdog watchdog)
        {
            this.inner = inner;
            this.watchdog = watchdog;
            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task<Stream> CreateContentReadStreamAsync(
            CancellationToken cancellationToken)
            => watchdog.Watch(await inner.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));

        protected override Task<Stream> CreateContentReadStreamAsync()
            => CreateContentReadStreamAsync(CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            System.Net.TransportContext? context,
            CancellationToken cancellationToken)
        {
            var source = await CreateContentReadStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                await source.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            }
        }

        protected override Task SerializeToStreamAsync(
            Stream stream,
            System.Net.TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}