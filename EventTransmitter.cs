using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace EventLogger
{
    public class EventTransmitter
    {
        private readonly BackendTransmitter backendTransmitter;
        private readonly DiskLogger diskLogger;

        public EventTransmitter(BackendTransmitter.Configuration backendTransmitterConfig, DiskLogger.Configuration diskLoggerConfig)
        {
            if (backendTransmitterConfig != null)
                backendTransmitter = new BackendTransmitter(backendTransmitterConfig);

            if (diskLoggerConfig != null)
                diskLogger = new DiskLogger(diskLoggerConfig);
        }

        public void Log(string sessionId, string type, string jsonData)
        {
            Assert.IsFalse(string.IsNullOrEmpty(type), "You must supply a type");
            Assert.IsFalse(string.IsNullOrEmpty(sessionId), "You must supply a Session ID");

            string secondsSinceStartup = Time.realtimeSinceStartup.ToString();

            List<string> keyValuePairs = new List<string>();

            keyValuePairs.Add(string.Format("\"SecondsSinceStartup\":{0}", secondsSinceStartup));
            keyValuePairs.Add(string.Format("\"SessionId\":\"{0}\"", sessionId));
            keyValuePairs.Add(string.Format("\"Type\":\"{0}\"", type));
            if (jsonData != null)
                keyValuePairs.Add(string.Format("\"Type\":{0}", jsonData));

            string jsonEvent = string.Format("{{{0}}}", string.Join(",", keyValuePairs.ToArray()));

            if (backendTransmitter != null)
                backendTransmitter.Log(jsonEvent);

            if (diskLogger != null)
                diskLogger.Log(jsonEvent);
        }

        public void Update(float deltaTime)
        {
            if (backendTransmitter != null)
                backendTransmitter.Update(deltaTime);
        }

        public void Flush()
        {
            if (backendTransmitter != null)
                backendTransmitter.Flush();
        }
    }
}