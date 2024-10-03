namespace ReCap.Server.Adapters.Rest.Contracts.Game.UnlockCreatureResponse;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class UnlockCreatureResponseContract : ResponseContract {

    [XmlElement(ElementName = "creature_id")]
    public ulong? CreatureID { get; set; }
}
