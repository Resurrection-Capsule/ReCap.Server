using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

using ReCap.Server.Adapters.Rest.Contracts.Response;

namespace HttpServer;

[XmlRoot("response")]
public class SurveyResponseContract : ResponseContract {

    [XmlArray("surveys")]
    [XmlArrayItem("survey")]
    public List<SurveyContract>? Surveys { get; set; }
}