using System;
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

        [Serializable]
        private class Event
        {
            public string SessionId;
            public string Type;
            public object Data;

            public Event(string sessionId, string type, object data)
            {
                SessionId = sessionId;
                Type = type;
                Data = data;
            }
        }

        public void SubmitEvent(string sessionId, string type, object data)
        {
            Assert.IsFalse(string.IsNullOrEmpty(type), "You must supply a type");
            Assert.IsFalse(string.IsNullOrEmpty(sessionId), "You must supply a Session ID");
            Event e = new Event(sessionId, type, data);
            string jsonEvent = JsonUtility.ToJson(e);

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