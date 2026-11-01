using System.Net;
using System.Xml.Linq;
using FleetManagement.Infrastructure.Configuration;

namespace FleetManagement.Domain.Tests.Notifications;

public sealed class AdpSmsSoapClientTests
{
    [Fact]
    public async Task ارسال_از_قرارداد_RPC_و_نشانی_امن_استفاده_می_کند()
    {
        string? requestXml = null;
        Uri? requestUri = null;
        string? soapAction = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            requestUri = request.RequestUri;
            soapAction = request.Headers.GetValues("SOAPAction").Single();
            requestXml = await request.Content!.ReadAsStringAsync();
            return XmlResponse("""
                <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:enc="http://schemas.xmlsoap.org/soap/encoding/" xmlns:tns="urn:SMSAPIwsdl">
                  <s:Body><tns:SendResponse><return xsi:type="enc:Array" enc:arrayType="tns:SendResult[1]"><item xsi:type="tns:SendResult"><ID>98765</ID><Mobile>989121234567</Mobile></item></return></tns:SendResponse></s:Body>
                </s:Envelope>
                """);
        }));

        var client = new AdpSmsSoapClient(http);
        var result = await client.SendAsync(Configuration(), "989121234567", "متن <آزمون> & پیام");

        Assert.Equal(new Uri("https://www.payamsms.com/services/v2/index.php"), requestUri);
        Assert.Equal("\"urn:SMSAPIwsdl#Send\"", soapAction);
        Assert.Contains("urn:SMSAPIwsdl", requestXml);
        Assert.Contains("arrayType", requestXml);
        Assert.Equal("98765", result.Id);
        Assert.Equal("989121234567", result.Mobile);

        var document = XDocument.Parse(requestXml!);
        Assert.Equal("متن <آزمون> & پیام", document.Descendants().Single(element => element.Name.LocalName == "body").Value);
    }

    [Fact]
    public async Task استعلام_شناسه_اصلی_از_عملیات_و_کد_اکشن_WSDL_پیروی_می_کند()
    {
        string? soapAction = null;
        string? requestXml = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            soapAction = request.Headers.GetValues("SOAPAction").Single();
            requestXml = await request.Content!.ReadAsStringAsync();
            return XmlResponse("""
                <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:enc="http://schemas.xmlsoap.org/soap/encoding/" xmlns:tns="urn:SMSAPIwsdl">
                  <s:Body><tns:getStatuses_OriginalIDResponse><return xsi:type="enc:Array" enc:arrayType="tns:DeliveryResult[1]"><item xsi:type="tns:DeliveryResult"><ID>98765</ID><CheckID>123</CheckID><Status>Delivered</Status></item></return></tns:getStatuses_OriginalIDResponse></s:Body>
                </s:Envelope>
                """);
        }));

        var results = await new AdpSmsSoapClient(http)
            .GetStatusesOriginalIdAsync(Configuration(), [98765]);

        Assert.Equal("\"urn:SMSAPIwsdl#getStatuses\"", soapAction);
        Assert.Contains("getStatuses_OriginalID", requestXml);
        Assert.Contains("Ids", requestXml);
        Assert.Single(results);
        Assert.Equal(new AdpSmsDeliveryResult("98765", "123", "Delivered"), results[0]);
    }

    [Theory]
    [InlineData("E5")]
    [InlineData("E107")]
    [InlineData("E999")]
    public async Task کد_خطای_ADP_به_عنوان_شناسه_موفق_برگردانده_نمی_شود(string code)
    {
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(XmlResponse($"""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:enc="http://schemas.xmlsoap.org/soap/encoding/" xmlns:tns="urn:SMSAPIwsdl">
              <s:Body><tns:SendResponse><return xsi:type="enc:Array" enc:arrayType="tns:SendResult[1]"><item xsi:type="tns:SendResult"><ID>{code}</ID><Mobile>989121234567</Mobile></item></return></tns:SendResponse></s:Body>
            </s:Envelope>
            """))));

        var exception = await Assert.ThrowsAsync<AdpSmsProviderException>(() =>
            new AdpSmsSoapClient(http).SendAsync(Configuration(), "989121234567", "متن"));

        Assert.Equal(code, exception.Code);
    }

    [Fact]
    public async Task SOAP_Fault_به_عنوان_پاسخ_موفق_پذیرفته_نمی_شود()
    {
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(XmlResponse("""
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
              <s:Body><s:Fault><faultcode>Server</faultcode><faultstring>Rejected</faultstring></s:Fault></s:Body>
            </s:Envelope>
            """))));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new AdpSmsSoapClient(http).SendAsync(Configuration(), "989121234567", "متن"));
    }

    private static SmsProviderConfiguration Configuration() => new()
    {
        Organization = "fleet",
        Username = "service-user",
        Password = "test-secret",
        SourceNumber = "982000000"
    };

    private static HttpResponseMessage XmlResponse(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml)
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
