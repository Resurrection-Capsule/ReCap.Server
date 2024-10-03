namespace ReCap.Server.Adapters.Rest.Contracts.Survey.Survey;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

[XmlRoot("survey")]
public class SurveyContract {

    [XmlElement(ElementName = "id")]
    public string? Id { get; set; }

    [XmlElement(ElementName = "trigger1")]
    public string? Trigger1 { get; set; }

    [XmlElement(ElementName = "trigger2")]
    public string? Trigger2 { get; set; }
}