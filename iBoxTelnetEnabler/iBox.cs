using System;
using System.Collections.Generic;
using System.Text;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Threading;


namespace iBoxTelnetEnabler
{
    class iBox
    {
        const string exportPath = "/cgi-bin/export.cgi?sExportMode=text";
        const string importPath = "/cgi-bin/import.cgi?sImportMode=text";
        const string referer = "/en_US/basic/index.htm";

        public IPAddress IP {get {return ip;}}
        public string Host { get { return host; } }

        private IPAddress ip;
        private string host;


        public iBox(IPAddress _ip, string _host)
        {
            this.ip = _ip;
            this.host = _host;
        }

        public static iBox find(bool output = true)
        {
            string altHost = "";
            IPAddress altIP = null;
            foreach (NetworkInterface f in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (f.NetworkInterfaceType == NetworkInterfaceType.Ethernet && f.OperationalStatus == OperationalStatus.Up)
                {
                    try
                    {
                        IPAddress addr = f.GetIPProperties().GatewayAddresses[0].Address;
                        string host = Dns.GetHostEntry(addr).HostName;
                        if (host.EndsWith(".Elisa") || host.EndsWith(".Sauna"))
                        {
                            if (output) Console.WriteLine("Found '" + host + "' (" + addr.ToString() + ") on interface '" + f.Name + "'");
                            return new iBox(addr, host);
                        }
                        else
                        {
                            altHost = host;
                            altIP = addr;
                        }
                    }
                    catch { return null; }
                }
            }
            if (Utils.Confirm("Is '" + altHost + "' (" + altIP + ") Bewan iBox?"))
                return new iBox(altIP, altHost);
            else
                return null;
        }



        private string get(string path)
        {
            string kotiboksiIP = ip.ToString();
            string url = "http://" + kotiboksiIP + path;
            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Referer = "http://" + kotiboksiIP + referer;
            HttpWebResponse res = null;
            try
            {
                res = (HttpWebResponse)req.GetResponse();
            }
            catch (WebException e)
            {
                return null;
            }
            if (res.StatusCode != HttpStatusCode.OK)
                return null;
            string data;
            using (StreamReader sr = new StreamReader(res.GetResponseStream()))
            {
                data = sr.ReadToEnd();
            }
            return data;
        }

        private void up(string path, string data)
        {
            string kotiboksiIP = ip.ToString();
            string url = "http://" + kotiboksiIP + path;
            TcpClient tc = new TcpClient(Dns.GetHostEntry(kotiboksiIP).HostName, 80);
            string b = "--------------------31063722920652";
            string headers = "POST " + path + " HTTP/1.1\r\n" +
                "Host: " + kotiboksiIP + "\r\n" +
                "Referer: http://" + kotiboksiIP + referer + "\r\n" +
                "Content-Type: multipart/form-data; boundary=" + b + "\r\n" +
                "Content-Length: {0}\r\n\r\n";

            string content =
                "--" + b + "\r\n" +
                @"Content-Disposition: form-data; name=""sImportFile""; filename=""config""" +
                "\r\nContent-Type: text/plain\r\n\r\n" +
                data + "\r\n" +
                "--" + b + "--";

            headers = string.Format(headers, content.Length);

            Console.Write("Sending data... ");
            NetworkStream ns = tc.GetStream();
            byte[] db = Encoding.Default.GetBytes(headers + content);
            ns.Write(db, 0, db.Length);
            Console.WriteLine("done.");
            Console.Write("Rebooting... ");
            using (StreamReader sr = new StreamReader(ns))
            {
                data = sr.ReadToEnd();
            }
            Thread.Sleep(3000);
            while (!this.ping()) Thread.Sleep(5000);
            Console.WriteLine("done.");
            Thread.Sleep(2000);
        }

        public bool ping()
        {
            Ping p = new Ping();
            try
            {
                PingReply reply = p.Send(host, 1000);
                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch { }
            return false;
        }

        public void interact()
        {
            Console.Write("Getting current status... ");
            string data = this.get(exportPath);
            if (data != null)
            {
                if (data.IndexOf("Device_Product_Code='A50804'") == -1 && Utils.Confirm("Error occured: product code mismatch. Force proceed?")) return;

                Match m = Regex.Match(data, "Services_Telnet_Enable=(0|1)");
                if (m.Success)
                {
                    Console.WriteLine("done.");
                    bool telnetEnabled = Convert.ToBoolean(Convert.ToInt32(m.Groups[1].Captures[0].Value));
                    Console.WriteLine("Telnet is currently " + (telnetEnabled ? "enabled" : "disabled"));
                    if (Utils.Confirm("Would you like to " + (telnetEnabled ? "disable it" : "enable it and set the root password") + "?"))
                    {
                        string magic = null;
                        if (!telnetEnabled)
                        {
                            Console.Write("New password for root: ");
                            string pw = Console.ReadLine();
                            if (pw != null && pw.Trim().Length > 0)
                            {
                                Console.WriteLine("Empty password is not allowed.");
                                return;
                            }
                            magic = Unix_MD5Crypt.MD5Crypt.crypt(pw, Unix_MD5Crypt.MD5Crypt.randomSalt());
                        }

                        // ugly but working
                        data = data.Replace("Services_Telnet_Enable=" + (telnetEnabled ? "1" : "0"), "Services_Telnet_Enable=" + (telnetEnabled ? "0" : "1"));

                        m = Regex.Match(data, "UserTable_1_Unix_Password='([^']+)'");
                        string currentMagic = m.Groups[1].Captures[0].Value;
                        string defaultMagic = "invalid";
                        if (telnetEnabled)
                        {
                            data = data.Replace("UserTable_1_Unix_Enable=1", "UserTable_1_Unix_Enable=0");
                            data = data.Replace("UserTable_1_Unix_Password='" + currentMagic + "'", "UserTable_1_Unix_Password='" + defaultMagic + "'");
                        }
                        else
                        {
                            data = data.Replace("UserTable_1_Unix_Enable=0", "UserTable_1_Unix_Enable=1");
                            data = data.Replace("UserTable_1_Unix_Password='" + currentMagic + "'", "UserTable_1_Unix_Password='" + magic + "'");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("failed. (error 2)");
                    return;
                }
            }
            else
            {
                Console.WriteLine("failed. (error 1)");
                return;
            }


            if (Utils.Confirm("Save changes? (will reboot the device)"))
                this.up(importPath, data);
        }

    }
}
