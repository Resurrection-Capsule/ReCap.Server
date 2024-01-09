using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using HttpServer;

namespace HttpServer;

[XmlRoot("response")]
public class SurveyResponseContract : ResponseContract {

    [XmlArray("surveys")]
    [XmlArrayItem("survey")]
    public List<SurveyContract>? Surveys { get; set; }
}