/*
 MonoBot
 File: Main.cs
 
 Author:
   Jason Barbier jabarb@serversave.us

 Copyright (c) 2012, Jason Barbier

 All rights reserved.

 Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
      the documentation and/or other materials provided with the distribution.
    * Neither the name of the ORGANIZATION nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

 THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
namespace MonoBot
{
        using System;
        using System.Collections.Generic;
        using System.Configuration;
        using System.Data;
        using System.Data.Linq;
        using System.Data.SQLite;
        using System.IO;
        using System.Net;
        using System.Net.Sockets;
        using System.Text;
        using System.Text.RegularExpressions;
        using System.Threading;
        using System.Xml;

        class IRCBot
        {
                private Config config;
                private TcpClient sock;
                private Stream stm;
                private StreamReader Reader;
                private StreamWriter Writer;
                private bool debug;

                public IRCBot(Config config)
                {
                        this.config = config;
                        try 
                        {
                                /* Basic Bot Setup */
                                this.sock = new TcpClient();
                                this.sock.ReceiveBufferSize = config.buffersize;
                                this.sock.Connect(config.server, config.port);
                                this.stm = this.sock.GetStream();
                                NetworkStream snet = new NetworkStream(this.sock.Client);
                                this.Writer = new StreamWriter(this.stm);
                                this.Reader = new StreamReader(this.stm);
                                this.SendData("USER", config.nick + "  8 * :" + config.name);
                                this.SendData("NICK", config.nick);
                                this.debug = config.debug;
                                /* Kick off the Worker Process */
                                this.IRCWork();
                        } 
                        catch (Exception e) 
                        {
                                Console.WriteLine(e.StackTrace);
                        }
                }

                #region IRC Protocol Functions

                /* Message sending functions*/
                /*Raw Data*/
                public void SendData(string Command, string Message)
                {
                        this.Writer.WriteLine(Command + " " + Message);
                        this.Writer.Flush();
                        Console.WriteLine(string.Format("Sent Data: {0} {1} ", Command, Message));
                }

                /* Channel and Private messages */
                public void ChanMessage(string target, string message)
                {
                        this.SendData("PRIVMSG", target + " :" + message);
                }

                #endregion



                /* Worker function */
                public void IRCWork()
                {
                        try 
                        {
                                if (this.sock.Connected == false) 
                                {
                                        Console.Write("Connection to server lost.");
                                }

                                /* Auth with chanserv first, if you want. */
                                if (this.config.nickserv == true) 
                                {
                                        this.ChanMessage("nickserv", "id " + this.config.nickservUserName + " " + this.config.password);
                                }

                                /* Join all the chans. */
                                if (this.config.channels != null) 
                                {
                                        foreach (string channel in this.config.channels) 
                                        {
                                                this.SendData("JOIN", channel);
                                        }
                                }

                                bool exit = false;
                                /* Worker Loop */
                                while (exit == false) 
                                {
                                        ////string data = Reader.ReadLine().ToString();
                                        byte[] rdata = new byte[this.config.buffersize];
                                        int bytes = this.stm.Read(rdata, 0, rdata.Length);
                                        string data = System.Text.Encoding.UTF8.GetString(rdata, 0, bytes).ToString();
                                        ////Console.Write(data);
                                        if (data == null) 
                                        {
                                                Console.WriteLine("Data is null");
                                        }

                                        string[] splt = Regex.Split(data, " :");
                                        Regex parsingRegex = new Regex(@"^(:(?<prefix>\S+) )?(?<command>\S+)( (?!:)(?<params>.+?))?( :(?<trail>.+))?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
                                        Match messageMatch = parsingRegex.Match(data);
                                        string prefix = messageMatch.Groups["prefix"].Value;
                                        string irccommand = messageMatch.Groups["command"].Value;
                                        string[] ircparams = messageMatch.Groups["params"].Value.Split(' ');
                                        string message = messageMatch.Groups["trail"].Value;

                                        if (this.debug == true) 
                                        {
                                                Console.WriteLine("Regex Parsed string as:\n");
                                                Console.WriteLine(string.Format("Prefix: {0}", prefix));
                                                Console.WriteLine(string.Format("Command: {0}", irccommand));
                                                Console.WriteLine(string.Format("Params: {0}", ircparams));
                                                Console.WriteLine(string.Format("message: {0}", message));
                                        }

                                        if (irccommand.Equals("PING")) 
                                        {
                                                this.SendData("PONG", message);
                                        }

                                        /* Output messages with Usernames and source if possible */
                                        if (ircparams != null) 
                                        {
                                                Console.WriteLine(string.Format("{2} <{0}> {1}",(prefix.Split('!') [0]), message, ircparams [0]));
                                        } 
                                        else 
                                        {
                                                Console.WriteLine(string.Format("<{0}> {1}", (prefix.Split('!')[0]), message));
                                        }

                                        foreach (string admin in this.config.admin) 
                                        {   
                                                if (prefix.StartsWith(admin) & ircparams [0].Equals(this.config.nick)) 
                                                {
                                                        string[] messagesplit;
                                                        string admcommand;
                                                        string admoptions;
                                                        if (message.Contains(" ") == true) 
                                                        {
                                                                messagesplit = Regex.Split(message, " ");
                                                                admcommand = messagesplit[0];
                                                                admoptions = messagesplit[1];
                                                        }
                                                        else 
                                                        { 
                                                                admcommand = message.Trim();
                                                                admoptions = null;
                                                        }

                                                        Console.WriteLine(string.Format("User: {0} requested {1}", prefix, admcommand));
                                                        switch (admcommand) 
                                                        {
                                                                case "help":
                                                                        this.ChanMessage(prefix, "Mono Bot Administrative Help:");
                                                                        this.ChanMessage(prefix, "!join #channel, will order me to join a channel.");
                                                                        this.ChanMessage(prefix, "!part #channel, will order me to leave the requested channel.");
                                                                        this.ChanMessage(prefix, "!quit, will order me to quit all together.");
                                                                        break;
                                                                case "!join":
                                                                        Console.WriteLine(string.Format("Asked to join {0} by {1}", admcommand, prefix));
                                                                        Console.WriteLine (admcommand);
                                                                        this.SendData("JOIN", admoptions);
                                                                        break;
                                                                case "!part":
                                                                        Console.WriteLine(string.Format("Leaving {0} by {1}", admcommand, prefix));
                                                                        this.SendData("PART", admoptions);
                                                                        break;
                                                                case "!debug-on":
                                                                        this.debug = true;
                                                                        break;
                                                                case "!debug-off":
                                                                        this.debug = false;
                                                                        Console.WriteLine(this.debug);
                                                                        break;
                                                                case "!quit":
                                                                        Console.WriteLine(string.Format("I was asked to quit by {0}", prefix));
                                                                        this.ChanMessage(prefix,":;_; goodbye " + ircparams);
                                                                        this.SendData("QUIT", admoptions);
                                                                        exit = true;
                                                                        this.sock.Close();
                                                                        break;
                                                        }
                                                }
                                        }

                                        /* General Commands */
                                        string command = null;
                                        string options = null;
                                        if (message.Contains(" ") == true) 
                                        {
                                                string[] split = Regex.Split(message, "([\x20]+)");
                                                command = split[0];
                                                options = splt[1];
                                        } 
                                        else 
                                        {
                                                command = message.Trim();
                                        }

                                        string user = prefix; 
                                        string channel = ircparams[0];
                                        BotFunctions Functions = new BotFunctions();
                                        Console.WriteLine(string.Format("command: {0}",command));
                                        Console.WriteLine(string.Format("opts: {0}",options));
                                        switch (command) 
                                        {
                                                case ".bender":
                                                        Console.WriteLine(string.Format("{0} is Calling Bender", user),Writer);
                                                        Functions.Bender(channel, this.config.dbConnectionURI, Writer);
                                                        break;

                                                case ".announcements":
                                                        Console.WriteLine(string.Format("{0} is Calling Announcements", user),Writer);
                                                        Functions.ChAnn(channel, this.config.dbConnectionURI, Writer);
                                                        break;
                                                
                                                case ".add-announcement":
                                                        Console.WriteLine(string.Format("{0} is Calling Add Announcements", user));
                                                        Functions.AddAnn(channel, options, this.config.dbConnectionURI, Writer);
                                                        break;

                                                case ".version":
                                                        IRC IRC = new IRC();
                                                        IRC.ChanMessage(channel, string.Format("Running .net version {0} on {1} {2}", System.Environment.Version, System.Environment.OSVersion.Platform, System.Environment.OSVersion),Writer);
                                                        break;
                                        }
                                }

                                this.sock.Close();
                        }
                        catch (ArgumentNullException e) 
                        {
                                Console.WriteLine("Argument Null: {0}", e);
                        } 
                        catch (SocketException e) {
                                Console.WriteLine("Socket Error: {0}", e);
                        } 
                        catch (Exception e) {
                                Console.WriteLine("Error: " + e.StackTrace);
                        }
                }
        }

        public class MainClass
         {
                public static void Main(string[] args)
                {

                        /* Generate the Config */
                        Config conf = new Config();

                        /* nick setup */
                        conf.name = System.Configuration.ConfigurationManager.AppSettings["name"];
                        conf.nick = System.Configuration.ConfigurationManager.AppSettings["nick"];

                        /* Server */
                        conf.server = System.Configuration.ConfigurationManager.AppSettings["server"];
                        string strconfport = System.Configuration.ConfigurationManager.AppSettings["port"];
                        int port;
                        int.TryParse(strconfport, out port);
                        conf.port = port;
                        string buff = System.Configuration.ConfigurationManager.AppSettings["BufferSize"];
                        int.TryParse(buff, out conf.buffersize);

                        /* Admins */
                        string[] admin = { System.Configuration.ConfigurationManager.AppSettings["admin"] };
                        conf.admin = admin;

                        /* Debug */
                        string strDebug = System.Configuration.ConfigurationManager.AppSettings["debug"];
                        bool debug;
                        bool.TryParse(strDebug, out conf.debug);
                        debug = conf.debug;

                        /* Nickserv */
                        string strNickServ = System.Configuration.ConfigurationManager.AppSettings["nickserv"];
                        bool blNickServ;
                        bool.TryParse(strNickServ, out blNickServ);
                        conf.nickserv = blNickServ;
                        conf.nickservUserName = System.Configuration.ConfigurationManager.AppSettings["nickservUserName"];
                        conf.password = System.Configuration.ConfigurationManager.AppSettings["password"];

                        /* Chans */
                        string[] channels = { System.Configuration.ConfigurationManager.AppSettings["channels"] };
                        conf.channels = channels;

                        /* Databases */
                        conf.dbConnectionURI = System.Configuration.ConfigurationManager.AppSettings["DBURI"];

                        /* Start the bot */

                        try 
                        {
                                new IRCBot(conf); /* Generate a bot based on the config and start it */
                        } 
                        catch (Exception e) 
                        {
                                Console.WriteLine("Error..... \n" + e.StackTrace);
                        }  
                }
        }
}
