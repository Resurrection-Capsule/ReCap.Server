using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Collections.Specialized;

using HttpServer;

namespace HttpServer;

class XmlUtils
{
    public static byte[] Serialize<T>(T value)
    {
        var xmlserializer = new XmlSerializer(typeof(T));
        var stringWriter = new StringWriter();
        using (var writer = XmlWriter.Create(stringWriter))
        {
            xmlserializer.Serialize(writer, value);
            string xmlStr = stringWriter.ToString();
            return Encoding.UTF8.GetBytes(xmlStr);
        }
    }
}
