namespace ReCap.Server.Adapters.Rest.Contracts.Game.PartListResponse;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Game.Models.CreaturePart;
using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class PartListResponseContract : ResponseContract {

    [XmlArray("parts")]
    [XmlArrayItem("part")]
    public List<CreaturePartContract>? Parts { get; set; }
}