﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Microsoft.Spark.CSharp.Interop.Ipc;
using Microsoft.Spark.CSharp.Network;
using Microsoft.Spark.CSharp.Services;
using Microsoft.Spark.CSharp.Sql;

namespace Microsoft.Spark.CSharp.Core
{
    /// <summary>
    /// Used for collect operation on RDD
    /// </summary>
    class RDDCollector : IRDDCollector
    {
		private static ILoggerService logger;
		private static ILoggerService Logger
		{
			get
			{
				if (logger != null) return logger;
				logger = LoggerServiceFactory.GetLogger(typeof(RDDCollector));
				return logger;
			}
		}

		public IEnumerable<dynamic> Collect(SocketInfo info, SerializedMode serializedMode, Type type)
        {
            IFormatter formatter = new BinaryFormatter();
            var sock = SocketFactory.CreateSocket();
            sock.Connect(IPAddress.Loopback, info.Port, null);

            using (var s = sock.GetStream())
            {
                if (info.Secret != null)
                {
                    SerDe.Write(s, info.Secret);
                    var reply = SerDe.ReadString(s);
                    Logger.LogDebug("Connect back to JVM: " + reply);
                }
                byte[] buffer;
                while ((buffer = SerDe.ReadBytes(s)) != null && buffer.Length > 0)
                {
                    if (serializedMode == SerializedMode.Byte)
                    {
                        MemoryStream ms = new MemoryStream(buffer);
                        yield return formatter.Deserialize(ms);
                    }
                    else if (serializedMode == SerializedMode.String)
                    {
                        yield return Encoding.UTF8.GetString(buffer);
                    }
                    else if (serializedMode == SerializedMode.Pair)
                    {
                        MemoryStream ms = new MemoryStream(buffer);
                        MemoryStream ms2 = new MemoryStream(SerDe.ReadBytes(s));

                        ConstructorInfo ci = type.GetConstructors()[0];
                        yield return ci.Invoke(new object[] { formatter.Deserialize(ms), formatter.Deserialize(ms2) });
                    }
                    else if (serializedMode == SerializedMode.Row)
                    {
                        var unpickledObjects = PythonSerDe.GetUnpickledObjects(buffer);
                        foreach (var item in unpickledObjects)
                        {
                            yield return (item as RowConstructor).GetRow();
                        }
                    }
                }
            }
        }
    }
}
