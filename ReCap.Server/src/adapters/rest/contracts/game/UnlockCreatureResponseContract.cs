using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class UnlockCreatureResponseContract : ResponseContract {

    [XmlElement(ElementName = "creature_id")]
    public ulong? CreatureID { get; set; }
}
