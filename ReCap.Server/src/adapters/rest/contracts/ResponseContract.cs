using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class ResponseContract {

    [XmlElement(ElementName = "stat")]
    public string? Stat { get; set; }
    public bool ShouldSerializeStat() => Stat != null;

    [XmlElement(ElementName = "code")]
    public int? Code { get; set; }
    public bool ShouldSerializeCode() => Code.HasValue;

    [XmlElement(ElementName = "result")]
    public int? Result { get; set; }
    public bool ShouldSerializeResult() => Result.HasValue;

    [XmlElement(ElementName = "version")]
    public string? Version { get; set; }
    public bool ShouldSerializeVersion() => Version != null;

    [XmlElement(ElementName = "timestamp")]
    public int? Timestamp { get; set; }
    public bool ShouldSerializeTimestamp() => Timestamp.HasValue;

    [XmlElement(ElementName = "exectime")]
    public int? ExecTime { get; set; }
    public bool ShouldSerializeExecTime() => ExecTime.HasValue;
}