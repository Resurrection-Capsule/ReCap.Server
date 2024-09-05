using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class PartListResponseContract : ResponseContract {

    [XmlArray("parts")]
    [XmlArrayItem("part")]
    public List<CreaturePartContract>? Parts { get; set; }
}