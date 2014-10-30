//
//  Author:
//    Jason Barbier jabarb@serversave.us
//
//  Copyright (c) 2014, Jason Barbier
//
//  All rights reserved.
//
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * The names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
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

namespace MonoBot
{
        public class BotFunctions {

                public void Bender(string channel, string DBConf, StreamWriter SWriter)
                {
                        try
                        {
                                IDbConnection dbcon;
                                dbcon = (IDbConnection)new SQLiteConnection(DBConf);
                                dbcon.Open();
                                IDbCommand dbcmd = dbcon.CreateCommand();
                                dbcmd.CommandText = "SELECT sayings FROM bender ORDER BY RANDOM() LIMIT 1";
                                IDataReader reader = dbcmd.ExecuteReader();
                                IRC IRC = new IRC();
                                /*Spit out the query results */
                                while (reader.Read()) 
                                {
                                        string dbsayings = reader.GetString(0);
                                        IRC.ChanMessage(channel, dbsayings, SWriter);
                                }

                                /* lets clean this up */
                                reader.Close();
                                reader = null;
                                dbcmd.Dispose();
                                dbcmd = null;
                                dbcon.Close();
                                dbcon = null;
                        }
                        catch (Exception eDB)
                        {
                                Console.WriteLine ("Connection to database failed, Please check your connection string.");
                                Console.WriteLine (DBConf);
                                Console.WriteLine (eDB);
                        }
                        return;
                }

                public void ChAnn(string channel, string DBConf, StreamWriter SWriter)
                {
                        try
                        {
                                IDbConnection dbcon;
                                dbcon = (IDbConnection)new System.Data.SQLite.SQLiteConnection(DBConf);
                                dbcon.Open();
                                IDbCommand dbcmd = dbcon.CreateCommand();
                                dbcmd.CommandText = String.Format("SELECT date,announcement FROM announcements WHERE chan == {1} ORDER BY date LIMIT 2", channel);
                                IDataReader reader = dbcmd.ExecuteReader();

                                /*Spit out the query results */
                                while (reader.Read()) 
                                {
                                        string dbsayings = reader.GetString(0) + " - " + reader.GetString(0);
                                        IRC IRC = new IRC();
                                        Console.WriteLine(dbsayings);
                                        if (dbsayings != null) 
                                        {
                                                IRC.ChanMessage (channel, dbsayings, SWriter);
                                        }
                                        else 
                                        {
                                                Console.WriteLine("FUUUUUUUUUUUUU no annoucements!");
                                                IRC.ChanMessage (channel, "No Announcements!", SWriter);
                                        }

                                        /* lets clean this up */
                                        reader.Close();
                                        reader = null;
                                        dbcmd.Dispose();
                                        dbcmd = null;
                                        dbcon.Close();
                                        dbcon = null;
                                }
                        }
                        catch (Exception eDB)
                        {
                                Console.WriteLine ("DBConnect Failed");
                                Console.WriteLine (eDB);
                        }
                        return;
                }

                public void AddAnn(string channel, string announcement, string DBConf, StreamWriter SWriter)
                {
                        IDbConnection dbcon;
                        dbcon = (IDbConnection)new System.Data.SQLite.SQLiteConnection(DBConf);
                        dbcon.Open();
                        IDbCommand dbcmd = dbcon.CreateCommand();
                        IRC IRC = new IRC();
                        dbcmd.CommandText = String.Format(string.Format("Insert into announcements values ({2},{0},{1}", channel, announcement,System.DateTime.Now));
                        dbcmd.ExecuteReader();
                        IRC.ChanMessage (channel, "Announcement Added", SWriter);
                        return;

                }
                        
}
}

