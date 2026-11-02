using System.Xml.Linq;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>کلاینت SOAP 1.1 سرویس پیامک ADP.</summary>
public sealed class AdpSmsSoapClient(HttpClient httpClient)
{
    private static readonly Uri Endpoint = new("https://www.payamsms.com/services/v2/index.php");
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace SoapEncoding = "http://schemas.xmlsoap.org/soap/encoding/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Service = "urn:SMSAPIwsdl";

    /// <summary>ارسال پیام به یک گیرنده.</summary>
    public async Task<AdpSmsSendResult> SendAsync(
        SmsProviderConfiguration configuration,
        string recipient,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var response = await InvokeAsync(
            "Send",
            "urn:SMSAPIwsdl#Send",
            [
                StringParameter("organization", configuration.Organization),
                StringParameter("username", configuration.Username),
                StringParameter("password", configuration.Password),
                StringParameter("srcNumber", configuration.SourceNumber),
                StringParameter("body", body),
                StringArrayParameter("destNo", [recipient])
            ],
            cancellationToken);

        var results = ReadRows(response, "ID", "Mobile");
        if (results.Count != 1)
            throw new InvalidDataException("پاسخ سرویس پیامک باید دقیقاً یک نتیجه ارسال داشته باشد.");

        var result = results[0];
        if (TryGetProviderErrorCode(result.First, out var errorCode))
            throw new AdpSmsProviderException(errorCode);

        return new AdpSmsSendResult(result.First, result.Second);
    }

    /// <summary>دریافت وضعیت چند پیام بر پایه شناسه اصلی سرور.</summary>
    public async Task<IReadOnlyList<AdpSmsDeliveryResult>> GetStatusesOriginalIdAsync(
        SmsProviderConfiguration configuration,
        IReadOnlyCollection<long> messageIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count is 0 or > 250) throw new ArgumentOutOfRangeException(nameof(messageIds));

        var response = await InvokeAsync(
            "getStatuses_OriginalID",
            "urn:SMSAPIwsdl#getStatuses",
            [
                StringParameter("organization", configuration.Organization),
                StringParameter("username", configuration.Username),
                StringParameter("password", configuration.Password),
                LongArrayParameter("Ids", messageIds)
            ],
            cancellationToken);

        return ReadRows(response, "ID", "CheckID", "Status")
            .Select(row => new AdpSmsDeliveryResult(row.First, row.Second, row.Third))
            .ToArray();
    }

    private async Task<XElement> InvokeAsync(
        string operation,
        string soapAction,
        IReadOnlyCollection<XElement> parameters,
        CancellationToken cancellationToken)
    {
        var envelope = new XDocument(
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", Xsd),
                new XAttribute(XNamespace.Xmlns + "enc", SoapEncoding),
                new XAttribute(XNamespace.Xmlns + "tns", Service),
                new XElement(Soap + "Body",
                    new XElement(Service + operation, parameters))));

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
        request.Content = new StringContent(
            envelope.ToString(SaveOptions.DisableFormatting),
            System.Text.Encoding.UTF8,
            "text/xml");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"سرویس پیامک با وضعیت HTTP {(int)response.StatusCode} پاسخ داد.",
                null,
                response.StatusCode);
        }

        var document = XDocument.Parse(responseXml);
        var body = document.Root?.Element(Soap + "Body")
            ?? throw new InvalidDataException("ساختار پاسخ SOAP سرویس پیامک معتبر نیست.");

        if (body.Elements().Any(element => element.Name.LocalName == "Fault"))
            throw new InvalidDataException("سرویس پیامک SOAP Fault برگرداند.");

        return body.Elements().FirstOrDefault(element => element.Name.LocalName == operation + "Response")
            ?? throw new InvalidDataException("پاسخ SOAP عملیات پیامک پیدا نشد.");
    }

    private static bool TryGetProviderErrorCode(string id, out string errorCode)
    {
        var value = id.Trim();
        if (value.Length is >= 2 and <= 4 && value[0] == 'E'
            && value.Skip(1).All(char.IsAsciiDigit))
        {
            errorCode = value;
            return true;
        }

        errorCode = string.Empty;
        return false;
    }

    private static IReadOnlyList<(string First, string Second, string Third)> ReadRows(
        XElement response,
        string firstName,
        string secondName,
        string? thirdName = null)
    {
        var result = response.Descendants()
            .Where(element => element.Elements().Any(child => child.Name.LocalName == firstName))
            .Select(element => (
                First: element.Elements().FirstOrDefault(child => child.Name.LocalName == firstName)?.Value ?? string.Empty,
                Second: element.Elements().FirstOrDefault(child => child.Name.LocalName == secondName)?.Value ?? string.Empty,
                Third: thirdName is null
                    ? string.Empty
                    : element.Elements().FirstOrDefault(child => child.Name.LocalName == thirdName)?.Value ?? string.Empty))
            .Where(row => !string.IsNullOrWhiteSpace(row.First))
            .ToArray();

        return result;
    }

    private static XElement StringParameter(string name, string value) =>
        new(name, new XAttribute(Xsi + "type", "xsd:string"), value);

    private static XElement StringArrayParameter(string name, IReadOnlyCollection<string> values) =>
        ArrayParameter(name, "tns:Destinations", "xsd:string", values.Select(value =>
            new XElement("item", new XAttribute(Xsi + "type", "xsd:string"), value)));

    private static XElement LongArrayParameter(string name, IReadOnlyCollection<long> values) =>
        ArrayParameter(name, "tns:checkingMessageIds", "xsd:long", values.Select(value =>
            new XElement("item", new XAttribute(Xsi + "type", "xsd:long"), value)));

    private static XElement ArrayParameter(
        string name,
        string arrayType,
        string elementType,
        IEnumerable<XElement> values)
    {
        var items = values.ToArray();
        return new XElement(name,
            new XAttribute(Xsi + "type", arrayType),
            new XAttribute(SoapEncoding + "arrayType", $"{elementType}[{items.Length}]"),
            items);
    }
}

public sealed record AdpSmsSendResult(string Id, string Mobile);

public sealed record AdpSmsDeliveryResult(string Id, string CheckId, string Status);

public sealed class AdpSmsProviderException(string code)
    : InvalidOperationException($"ارائه‌دهنده پیامک کد خطای {code} برگرداند.")
{
    public string Code { get; } = code;
}
