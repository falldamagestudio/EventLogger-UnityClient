using UnityEngine;
using UnityEngine.Assertions;

namespace EventLogger
{
    public class Session
    {
        private readonly string sessionId;
        private readonly EventTransmitter transmitter;

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
                    SubmitEvent("BeginSession", null);
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
                    SubmitEvent("EndSession", null);
                    state = State.Ended;
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Attempted to end session when it already had ended");
                    break;
            }
        }

        public void SubmitEvent(string type, object data)
        {
            switch (state)
            {
                case State.NotStarted:
                    Debug.LogWarningFormat("Cannot submit event {0} / {1}: session has not yet started", type, data);
                    break;
                case State.Started:
                    transmitter.SubmitEvent(sessionId, type, data);
                    break;
                case State.Ended:
                    Debug.LogWarningFormat("Cannot submit event {0} / {1}: session has ended", type, data);
                    break;
            }
        }
    }
}
