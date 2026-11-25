namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// ONVIF answers as cameras give them, written by hand from the ONVIF Media 1 schema: the
/// namespaces and prefixes differ between brands, so the parser matches local names.
/// </summary>
internal static class OnvifResponses
{
    public const string Capabilities = """
        <?xml version="1.0" encoding="UTF-8"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://www.w3.org/2003/05/soap-envelope" xmlns:tds="http://www.onvif.org/ver10/device/wsdl" xmlns:tt="http://www.onvif.org/ver10/schema">
          <SOAP-ENV:Body>
            <tds:GetCapabilitiesResponse>
              <tds:Capabilities>
                <tt:Media>
                  <tt:XAddr>http://10.0.0.99:80/onvif/Media</tt:XAddr>
                </tt:Media>
              </tds:Capabilities>
            </tds:GetCapabilitiesResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    // A Hikvision-type camera: 16:9 main stream, a 4:3 sub-stream squeezed from it, and a third stream.
    public const string HikvisionProfiles = """
        <?xml version="1.0" encoding="UTF-8"?>
        <env:Envelope xmlns:env="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl" xmlns:tt="http://www.onvif.org/ver10/schema">
          <env:Body>
            <trt:GetProfilesResponse>
              <trt:Profiles token="Profile_1" fixed="true">
                <tt:Name>mainStream</tt:Name>
                <tt:VideoEncoderConfiguration token="VideoEncoderToken_1">
                  <tt:Name>VideoEncoder_1</tt:Name>
                  <tt:Encoding>H264</tt:Encoding>
                  <tt:Resolution><tt:Width>2560</tt:Width><tt:Height>1440</tt:Height></tt:Resolution>
                </tt:VideoEncoderConfiguration>
              </trt:Profiles>
              <trt:Profiles token="Profile_2" fixed="true">
                <tt:Name>subStream</tt:Name>
                <tt:VideoEncoderConfiguration token="VideoEncoderToken_2">
                  <tt:Encoding>H264</tt:Encoding>
                  <tt:Resolution><tt:Width>640</tt:Width><tt:Height>480</tt:Height></tt:Resolution>
                </tt:VideoEncoderConfiguration>
              </trt:Profiles>
              <trt:Profiles token="Profile_3" fixed="true">
                <tt:Name>thirdStream</tt:Name>
                <tt:VideoEncoderConfiguration token="VideoEncoderToken_3">
                  <tt:Encoding>H264</tt:Encoding>
                  <tt:Resolution><tt:Width>1280</tt:Width><tt:Height>720</tt:Height></tt:Resolution>
                </tt:VideoEncoderConfiguration>
              </trt:Profiles>
              <trt:Profiles token="Profile_Audio">
                <tt:Name>audioOnly</tt:Name>
              </trt:Profiles>
            </trt:GetProfilesResponse>
          </env:Body>
        </env:Envelope>
        """;

    public const string MjpegOnlyProfiles = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:m="http://www.onvif.org/ver10/media/wsdl" xmlns:x="http://www.onvif.org/ver10/schema">
          <s:Body>
            <m:GetProfilesResponse>
              <m:Profiles token="0">
                <x:Name>Camera</x:Name>
                <x:VideoEncoderConfiguration>
                  <x:Encoding>JPEG</x:Encoding>
                  <x:Resolution><x:Width>1280</x:Width><x:Height>720</x:Height></x:Resolution>
                </x:VideoEncoderConfiguration>
              </m:Profiles>
            </m:GetProfilesResponse>
          </s:Body>
        </s:Envelope>
        """;

    public const string NotAuthorizedFault = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:ter="http://www.onvif.org/ver10/error">
          <s:Body>
            <s:Fault>
              <s:Code>
                <s:Value>s:Sender</s:Value>
                <s:Subcode><s:Value>ter:NotAuthorized</s:Value></s:Subcode>
              </s:Code>
              <s:Reason><s:Text xml:lang="en">Sender not Authorized</s:Text></s:Reason>
            </s:Fault>
          </s:Body>
        </s:Envelope>
        """;

    public const string SystemDateAndTime = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:tds="http://www.onvif.org/ver10/device/wsdl" xmlns:tt="http://www.onvif.org/ver10/schema">
          <s:Body>
            <tds:GetSystemDateAndTimeResponse>
              <tds:SystemDateAndTime>
                <tt:UTCDateTime>
                  <tt:Time><tt:Hour>10</tt:Hour><tt:Minute>0</tt:Minute><tt:Second>0</tt:Second></tt:Time>
                  <tt:Date><tt:Year>2026</tt:Year><tt:Month>11</tt:Month><tt:Day>25</tt:Day></tt:Date>
                </tt:UTCDateTime>
              </tds:SystemDateAndTime>
            </tds:GetSystemDateAndTimeResponse>
          </s:Body>
        </s:Envelope>
        """;

    // A stream address as a camera gives it: Dahua's are chosen by query; some include the user.
    public static string StreamUri(string uri)
        => $"""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:trt="http://www.onvif.org/ver10/media/wsdl" xmlns:tt="http://www.onvif.org/ver10/schema">
              <s:Body>
                <trt:GetStreamUriResponse>
                  <trt:MediaUri>
                    <tt:Uri>{System.Security.SecurityElement.Escape(uri)}</tt:Uri>
                    <tt:InvalidAfterConnect>false</tt:InvalidAfterConnect>
                  </trt:MediaUri>
                </trt:GetStreamUriResponse>
              </s:Body>
            </s:Envelope>
            """;
}