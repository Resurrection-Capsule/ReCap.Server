using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace ReCap.Server.Adapters.Rest.Contracts.Survey;

[XmlRoot("response")]
public class SurveyResponseContract : ResponseContract {

    [XmlArray("surveys")]
    [XmlArrayItem("survey")]
    public List<SurveyContract>? Surveys { get; set; }
}