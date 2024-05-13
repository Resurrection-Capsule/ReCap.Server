using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class BlazeHttpServer
{
    public void Start()
    {
        // Create an SSL certificate for the server
        var certificateBytes = Encoding.ASCII.GetBytes(BlazeCredentials.CERTIFICATE);
        var certificate = new X509Certificate2(certificateBytes);

        string ipAddress = "127.0.0.1";
        int port = 42127;

        var listener = new TcpListener(IPAddress.Parse(ipAddress), port);
        listener.Start();
        Console.WriteLine($"Server started at {ipAddress}:{port}");

        while (true)
        {
            var client = listener.AcceptTcpClient();
            Console.WriteLine("Client connected");

            var sslStream = new SslStream(client.GetStream(), false);
            try {
                sslStream.AuthenticateAsServer(certificate, false, SslProtocols.Ssl3, true);

                Console.WriteLine("Waiting for client message...");
                string messageData = ReadMessage(sslStream);
                Console.WriteLine("Received: {0}", messageData);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
                return;
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }
    }

    private string ReadMessage(SslStream sslStream)
    {
        byte[] buffer = new byte[570];
        StringBuilder messageData = new StringBuilder();
        int bytes = -1;

        bytes = sslStream.Read(buffer, 0, buffer.Length);

        Decoder decoder = Encoding.UTF8.GetDecoder();
        char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
        decoder.GetChars(buffer, 0, bytes, chars, 0);
        messageData.Append(chars);

        return messageData.ToString();
    }

}