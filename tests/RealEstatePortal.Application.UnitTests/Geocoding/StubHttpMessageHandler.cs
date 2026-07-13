using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RealEstatePortal.Application.UnitTests.Geocoding;

// Returns a queued response per outgoing request, and records the URLs it was called with.
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;
    public List<string> RequestedUris { get; } = new();

    public StubHttpMessageHandler(params string[] jsonResponses)
        => _responses = new Queue<string>(jsonResponses);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!.ToString());
        var body = _responses.Count > 0 ? _responses.Dequeue() : "[]";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        });
    }
}