using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class AuthResponseContract : ResponseContract {

    [XmlElement(ElementName = "account")]
    public AccountContract? Account { get; set; }
}