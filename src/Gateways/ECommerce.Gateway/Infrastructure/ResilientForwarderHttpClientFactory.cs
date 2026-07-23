using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Registry;
using Yarp.ReverseProxy.Forwarder;

namespace ECommerce.Gateway.Infrastructure;

public class ResilientForwarderHttpClientFactory : IForwarderHttpClientFactory
{
    private readonly ResiliencePipelineProvider<string> _pipelineProvider;

    public ResilientForwarderHttpClientFactory(ResiliencePipelineProvider<string> pipelineProvider)
    {
        _pipelineProvider = pipelineProvider;
    }

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
            UseCookies = false
        };

        var pipeline = _pipelineProvider.GetPipeline<HttpResponseMessage>("bulkhead");
        var resilienceHandler = new ResilienceHandler(pipeline)
        {
            InnerHandler = handler
        };

        return new HttpMessageInvoker(resilienceHandler, disposeHandler: true);
    }
}
