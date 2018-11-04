using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

namespace EventLogger
{
    public class BackendTransmitter
    {
        [Serializable]
        public class Configuration
        {
            public string BackendUrl;

            public float WaitForAdditionalEventsInterval = 10f;

            public int MaxEventsPerHttpRequest = 1000;
            public int MaxHttpRequestSize = 1 * 1024 * 1024;

            public float InitialCoolDown = 5;
            public float MaxCoolDown = 2 * 60;
            public int MaxRetries = 10;
        }

        private enum State
        {
            WaitingForEvents,
            WaitingForAdditionalEvents,
            PublishingEvents,
            CoolDownAfterFailedPublish,
            Disabled,
        }

        private readonly Configuration config;

        private readonly DiskLogger diskLogger;

        private List<string> jsonEvents = new List<string>();

        private State state = State.WaitingForEvents;

        private float remainingDurationForAdditionalEvents;

        private List<string> eventsInFlight;
        private UnityWebRequest webRequest;
        private UnityWebRequestAsyncOperation webAsyncOperation;

        private float currentCoolDown;
        private int retryCount;
        private float remainingDurationForCoolDown;

        public BackendTransmitter(Configuration config)
        {
            Assert.IsNotNull(config);
            Assert.IsFalse(string.IsNullOrEmpty(config.BackendUrl), "You must supply a target URL for the BackendTransmitter API. BackendTransmitter will be inactive.");

            this.config = config;

            Debug.Log("BackendTransmitter initialized");
        }

        private class Event
        {
            public readonly string SessionId;
            public readonly string Type;
            public readonly object Data;

            public Event(string sessionId, string type, object data)
            {
                SessionId = sessionId;
                Type = type;
                Data = data;
            }
        }

        public void Log(string jsonEvent)
        {
            lock (jsonEvents)
            {
                jsonEvents.Add(jsonEvent);
            }
        }

        public void Flush()
        {
            if (state == State.WaitingForAdditionalEvents)
                remainingDurationForAdditionalEvents = 0;
            if (state == State.CoolDownAfterFailedPublish)
                remainingDurationForCoolDown = 0;
            Update(0);
        }

        public bool Empty { get { return state == State.WaitingForEvents; } }

        public void Update(float deltaTime)
        {
            switch (state)
            {
                case State.WaitingForEvents:
                    lock (jsonEvents)
                    {
                        if (jsonEvents.Count > 0)
                        {
                            state = State.WaitingForAdditionalEvents;
                            remainingDurationForAdditionalEvents = config.WaitForAdditionalEventsInterval;
                        }
                    }
                    break;

                case State.WaitingForAdditionalEvents:
                    if (remainingDurationForAdditionalEvents >= 0f)
                    {
                        remainingDurationForAdditionalEvents -= deltaTime;
                    }
                    else
                    {
                        retryCount = 0;
                        currentCoolDown = config.InitialCoolDown;

                        lock (jsonEvents)
                        {
                            eventsInFlight = ExtractJsonEvents();
                        }
                        PublishEvents(eventsInFlight, out webRequest, out webAsyncOperation);

                        state = State.PublishingEvents;

                    }
                    break;

                case State.PublishingEvents:
                    if (webAsyncOperation.isDone)
                    {
                        if (webRequest.isNetworkError || webRequest.isHttpError)
                        {
                            lock (jsonEvents)
                            {
                                RequeueUnsentJsonEvents(eventsInFlight);
                            }
                            eventsInFlight = null;
                            webRequest = null;
                            webAsyncOperation = null;

                            retryCount++;
                            if (retryCount > config.MaxRetries)
                            {
                                Debug.LogErrorFormat("Failed to publish events to backend after {0} retries; EventTransmitter is disabled for the remainder of the session", config.MaxRetries);
                                state = State.Disabled;
                            }
                            else
                            {
                                remainingDurationForCoolDown = currentCoolDown;
                                currentCoolDown = Mathf.Min(currentCoolDown * 2, config.MaxCoolDown);
                                Debug.LogWarningFormat("Failed to publish events to backend. Will retry in {0} seconds", currentCoolDown);
                                state = State.CoolDownAfterFailedPublish;
                            }
                        }
                        else
                        {
                            if (retryCount > 0)
                                Debug.LogFormat("Successfully published events to backend, after previous failures.");

                            eventsInFlight = null;
                            webRequest = null;
                            webAsyncOperation = null;
                            state = State.WaitingForEvents;
                        }
                    }
                    break;

                case State.CoolDownAfterFailedPublish:
                    if (remainingDurationForCoolDown >= 0f)
                    {
                        remainingDurationForCoolDown -= deltaTime;
                    }
                    else
                    {
                        lock (jsonEvents)
                        {
                            eventsInFlight = ExtractJsonEvents();
                        }
                        PublishEvents(eventsInFlight, out webRequest, out webAsyncOperation);
                        state = State.PublishingEvents;
                    }
                    break;

                case State.Disabled:
                    lock (jsonEvents)
                    {
                        jsonEvents.Clear();
                    }
                    break;
            }
        }

        private List<string> ExtractJsonEvents()
        {
            List<string> extractedEvents = new List<string>();

            int numEventsExtracted = 0;
            for (int requestLength = 0; (numEventsExtracted < jsonEvents.Count) && (numEventsExtracted < config.MaxEventsPerHttpRequest) && ((requestLength + jsonEvents[numEventsExtracted].Length) < config.MaxHttpRequestSize); numEventsExtracted++)
            {
                extractedEvents.Add(jsonEvents[numEventsExtracted]);
                requestLength += jsonEvents[numEventsExtracted].Length + 1;
            }

            jsonEvents.RemoveRange(0, numEventsExtracted);

            return extractedEvents;
        }

        private void RequeueUnsentJsonEvents(List<string> unsentEvents)
        {
            jsonEvents.InsertRange(0, unsentEvents);
        }


        /// <summary>
        /// Given a text body, construct a UnityWebRequest that will post the message to the backend entry point in Google Cloud
        /// </summary>
        private static UnityWebRequest CreateUnityWebRequest(string backendUrl, string message)
        {
            byte[] messageUtf8 = Encoding.UTF8.GetBytes(message);

            UnityWebRequest request = UnityWebRequest.Post(backendUrl, "");
            // Hack: provide the body content via a custom upload handler
            // UnityWebRequest.Post will assume that body is going to be sent as application/x-www-form-urlencoded format, and it will apply that conversion to the body string
            //   ( so {"a":"b"} turns into %7b%22a%22:%22b%22%7d )
            // To get around this, we provide a handler that will send a text string with the appropriate content type
            request.uploadHandler = new UploadHandlerRaw(messageUtf8);
            request.uploadHandler.contentType = "application/json; charset=utf-8";

            return request;
        }

        private void PublishEvents(List<string> eventsToPublish, out UnityWebRequest webRequest, out UnityWebRequestAsyncOperation webAsyncOperation)
        {
            string message = string.Format("[{0}]", string.Join(",", eventsToPublish.ToArray()));

            webRequest = CreateUnityWebRequest(config.BackendUrl, message);
            webAsyncOperation = webRequest.SendWebRequest();
        }
    }
}
