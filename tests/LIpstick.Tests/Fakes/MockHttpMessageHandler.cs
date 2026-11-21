using System.Net;
using System.Text;

namespace LIpstick.Tests.Fakes;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> CapturedRequests { get; } = new();
    public List<string> CapturedRequestBodies { get; } = new();

    public HttpRequestMessage? LastRequest => CapturedRequests.LastOrDefault();
    public string? LastRequestBody => CapturedRequestBodies.LastOrDefault();

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseContent { get; set; } = "";
    public Func<HttpRequestMessage, HttpResponseMessage>? ResponseFactory { get; set; }
    public bool SimulateTimeout { get; set; } = false;
    public Exception? ExceptionToThrow { get; set; } = null;
    public int SimulatedDelayMs { get; set; } = 0;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);

        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            CapturedRequestBodies.Add(body);
        }
        else
        {
            CapturedRequestBodies.Add(string.Empty);
        }

        if (SimulatedDelayMs > 0)
        {
            await Task.Delay(SimulatedDelayMs, cancellationToken);
        }

        if (SimulateTimeout)
        {
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout elapsing.", new TimeoutException("The operation has timed out."));
        }

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (ResponseFactory != null)
        {
            return ResponseFactory(request);
        }

        var response = new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(ResponseContent, Encoding.UTF8, "application/json")
        };
        return response;
    }
}
