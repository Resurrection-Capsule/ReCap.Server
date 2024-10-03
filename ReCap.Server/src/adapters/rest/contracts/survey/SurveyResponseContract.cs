namespace ReCap.Server.Adapters.Rest.Contracts.Survey.SurveyResponse;

using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using ReCap.Server.Adapters.Rest.Contracts.Survey.Survey;
using ReCap.Server.Adapters.Rest.Contracts.Response;

[XmlRoot("response")]
public class SurveyResponseContract : ResponseContract {

    [XmlArray("surveys")]
    [XmlArrayItem("survey")]
    public List<SurveyContract>? Surveys { get; set; }
}