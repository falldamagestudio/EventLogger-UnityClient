using UnityEngine;
using UnityEngine.Assertions;

namespace EventLogger
{
    public class Session
    {
        private readonly string sessionId;
        private readonly EventTransmitter transmitter;
        private int sequenceId;

        private enum State
        {
            NotStarted,
            Started,
            Ended
        }

        private State state = State.NotStarted;

        public static string GetNewSessionId()
        {
            return string.Format("Session-{0}", System.Guid.NewGuid().ToString());
        }

        public Session(string sessionId, EventTransmitter transmitter)
        {
            Assert.IsFalse(string.IsNullOrEmpty(sessionId), "You must supply a session ID for the EventLogger.Session backend API. EventLogger.Session will be inactive.");

            this.transmitter = transmitter;
            this.sessionId = sessionId;
        }

        public void Begin()
        {
            switch (state)
            {
                case State.NotStarted:
                    state = State.Started;
                    Log("BeginSession");
                    break;
                case State.Started:
                    Debug.LogWarningFormat("Attempted to begin session when it already had started");
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Attempted to begin session after it had ended");
                    break;
            }
        }

        public void End()
        {
            switch (state)
            {
                case State.NotStarted:
                    Debug.LogWarningFormat("Attempted to end session before it had started");
                    break;
                case State.Started:
                    Log("EndSession");
                    state = State.Ended;
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Attempted to end session when it already had ended");
                    break;
            }
        }

        public void Log(string type)
        {
            Log(type, null);
        }

        private int GetNextSequenceId()
        {
            return sequenceId++;
        }

        public void Log(string type, string jsonData)
        {
            switch (state)
            {
                case State.NotStarted:
                    Debug.LogWarningFormat("Cannot submit event {0}: session has not yet started", type);
                    break;
                case State.Started:
                    // TODO: validate that jsonData is valid JSON
                    transmitter.Log(sessionId, GetNextSequenceId(), type, jsonData);
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Cannot submit event {0}: session has ended", type);
                    break;
            }
        }

        public void Log<LogEventType>(LogEventType logEvent) where LogEventType : LogEvent
        {
            switch (state)
            {
                case State.NotStarted:
                    Debug.LogWarningFormat("Cannot submit event {0}: session has not yet started", typeof(LogEventType).Name);
                    break;
                case State.Started:
                    string jsonData = JsonUtility.ToJson(logEvent);
                    transmitter.Log(sessionId, GetNextSequenceId(), typeof(LogEventType).Name, jsonData);
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Cannot submit event {0}: session has ended", typeof(LogEventType).Name);
                    break;
            }
        }
    }
}
