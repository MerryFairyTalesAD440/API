using System;
using System.IO;
using System.Net;
using System.Text;

namespace TestCreateContainer
{
    class Program
    {
        public static void Main()
        {

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://createcontainer.azurewebsites.net/api/CreateContainer");
            req.Method = "POST";
            req.ContentType = "application/json";
            Stream stream = req.GetRequestStream();
            string json = "{\"name\": \"this-is-a-test-name\" }";
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            stream.Write(buffer, 0, buffer.Length);
            HttpWebResponse res = (HttpWebResponse)req.GetResponse();

        }

    }
    }