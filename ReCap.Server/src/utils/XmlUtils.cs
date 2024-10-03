namespace ReCap.Server.Utils.Xml;

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Collections.Specialized;

public class XmlUtils
{
    public static byte[] Serialize<T>(T value)
    {
        var encoding = Encoding.Latin1;
        var xmlserializer = new XmlSerializer(typeof(T));
        var stream = new MemoryStream();

        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Encoding = encoding;
        settings.NewLineChars = "\r\n";
        settings.NewLineOnAttributes = true;

        using (var writer = XmlWriter.Create(stream, settings))
        {
            xmlserializer.Serialize(writer, value);
            return stream.ToArray();
        }
    }
}
