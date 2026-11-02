<?xml version="1.0" encoding="utf-8"?>
<!-- Leaves the server's exe out of the harvested files: Components.wxs installs it with the
     Windows service, and a file may belong to one component only. -->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">
  <xsl:output method="xml" indent="yes" />

  <xsl:key name="server-exe"
           match="wix:Component[wix:File[substring(@Source, string-length(@Source) - string-length('\AtcSoft.Video.Surveillance.Api.exe') + 1) = '\AtcSoft.Video.Surveillance.Api.exe']]"
           use="@Id" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:Component[key('server-exe', @Id)]" />
  <xsl:template match="wix:ComponentRef[key('server-exe', @Id)]" />
</xsl:stylesheet>