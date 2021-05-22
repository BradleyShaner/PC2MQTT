using ExtensionMethods;
using System.Collections.Generic;
using System.Diagnostics;

namespace PC2MQTT.MQTT
{
    public class MqttMessage
    {
        /// <summary>
        /// Add the <see cref="deviceId"/> to the current topic list.
        /// </summary>
        /// <remarks>
        /// This is a shorter version of <see cref="AddDeviceIdToTopic"/>
        /// </remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage AddDeviceId
        {
            get
            {
                this.AddDeviceIdToTopic();

                return this;
            }
        }

        /// <summary>
        /// Add a multi-level wildcard to the current topic list.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage AddMultiLevelWildcard
        {
            get
            {
                this.AddTopic("#");

                return this;
            }
        }

        /// <summary>
        /// Add a single-level wildcard to the current topic list.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage AddSingleLevelWildcard
        {
            get
            {
                this.AddTopic("+");

                return this;
            }
        }

        /// <summary>
        /// This specifies what type of message this <see cref="MqttMessage"/> contains.
        /// </summary>
        public enum MqttMessageType
        {
            MQTT_NONE = 0,
            MQTT_PUBLISH,
            MQTT_SUBSCRIBE,
            MQTT_UNSUBSCRIBE,
            MQTT_RECEIVED
        }


        /// <summary>
        /// Sets the <see cref="MqttMessageType"/> to <see cref="MqttMessageType.MQTT_PUBLISH"/>.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage PublishMessage
        {
            get
            {
                this.messageType = MqttMessageType.MQTT_PUBLISH;
                return this;
            }
        }

        /// <summary>
        /// Allow this message to be added to the queue. This is the default behavior. To skip the queue, use <see cref="SendImmediately"/>.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage QueueMessage
        {
            get
            {
                this.sendImmediately = false;
                return this;
            }
        }


        /// <summary>
        /// Sets the <see cref="MqttMessageType"/> to <see cref="MqttMessageType.MQTT_RECEIVED"/>.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage ReceivedMessage
        {
            get
            {
                this.messageType = MqttMessageType.MQTT_RECEIVED;
                return this;
            }
        }

        /// <summary>
        /// Forces the message to skip the queue and be sent immediately.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage SendImmediately
        {
            get
            {
                this.sendImmediately = true;
                return this;
            }
        }

        /// <summary>
        /// Sets the <see cref="MqttMessageType"/> to <see cref="MqttMessageType.MQTT_SUBSCRIBE"/>.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage SubscribeMessage
        {
            get
            {
                this.messageType = MqttMessageType.MQTT_SUBSCRIBE;
                return this;
            }
        }

        /// <summary>
        /// Sets the <see cref="MqttMessageType"/> to <see cref="MqttMessageType.MQTT_UNSUBSCRIBE"/>.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessage UnsubscribeMessage
        {
            get
            {
                this.messageType = MqttMessageType.MQTT_UNSUBSCRIBE;
                return this;
            }
        }

        public string deviceId = ExtensionMethods.Extensions.deviceId;
        public string message = "";
        public byte[] rawMessage;
        public ushort messageId = 0;
        public MqttMessageType messageType = MqttMessageType.MQTT_NONE;
        public bool retain = false;
        public bool sendImmediately = false;
        private string _rawTopicCache = "";
        private List<string> topicList;

        /// <summary>
        /// Creates a new <see cref="MqttMessage"/>.
        /// </summary>
        public MqttMessage()
        {
            topicList = new List<string>();
        }

        /// <summary>
        /// Add the <see cref="deviceId"/> as a new topic level.
        /// </summary>
        /// <returns></returns>
        public MqttMessage AddDeviceIdToTopic()
        {
            this.AddTopic(deviceId);

            return this;
        }

        /// <summary>
        /// Add an additional topic level. Can add multiple topic levels if separated by a slash '/'.
        /// </summary>
        /// <param name="topic"></param>
        /// <returns></returns>
        public MqttMessage AddTopic(string topic)
        {
            if (topic.Length == 0)
                return this;

            if (topic.Contains("/"))
            {
                var topics = topic.Split("/");
                foreach (var item in topics)
                {
                    if (item.Length > 0)
                        topicList.Add(item);
                }
            }
            else
            {
                topicList.Add(topic);
            }

            UpdateRawTopicCache();

            return this;
        }

        /// <summary>
        /// Retrieves the deviceId.
        /// </summary>
        /// <returns></returns>
        public string GetDeviceId()
        {
            return deviceId;
        }

        /// <summary>
        /// Retrieves the <see cref="message"/> for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <returns></returns>
        public string GetMessage()
        {
            return message;
        }

        /// <summary>
        /// Retrieves the <see cref="rawMessage"/> for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <returns></returns>
        public byte[] GetRawMessage()
        {
            return rawMessage;
        }

        public ushort GetMessageId()
        {
            return messageId;
        }

        /// <summary>
        /// Returns the raw topic for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <returns>Full, unmodified topic string</returns>
        public string GetRawTopic()
        {
            return _rawTopicCache;
        }

        /// <summary>
        /// Retrieve the <see cref="MqttMessage"/> topic without <see cref="deviceId"/>.
        /// </summary>
        /// <returns>Topic string without prepended <see cref="deviceId"/></returns>
        public string GetTopicWithoutDeviceId()
        {
            return GetRawTopic().RemoveDeviceId();
        }

        /// <summary>
        /// Sets the <see cref="message"/> data for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public MqttMessage SetMessage(string message)
        {
            this.message = message;

            return this;
        }

        /// <summary>
        /// Sets the <see cref="rawMessage"/> data for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <param name="rawMessage"></param>
        /// <returns></returns>
        public MqttMessage SetRawMessage(byte[] message)
        {
            this.rawMessage = message;

            return this;
        }

        public MqttMessage SetMessageId(ushort messageId)
        {
            this.messageId = messageId;

            return this;
        }

        /// <summary>
        /// Sets the <see cref="MqttMessageType"/> for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public MqttMessage SetMessageType(MqttMessageType messageType)
        {
            this.messageType = messageType;

            return this;
        }
        /// <summary>
        /// Sets the retain flag for this <see cref="MqttMessage"/>.
        /// </summary>
        /// <param name="retain"></param>
        /// <returns></returns>
        public MqttMessage SetRetainFlag(bool retain)
        {
            this.retain = retain;

            return this;
        }

        /// <summary>
        /// Clears the current topic and replaces it for this <see cref="MqttMessage"/>. Leaving <paramref name="topic"/> empty will clear the current topic.
        /// </summary>
        /// <param name="topic"></param>
        /// <remarks>Be sure to use the proper/topic/format here!</remarks>
        public MqttMessage SetTopic(string topic = "")
        {
            topicList.Clear();

            if (topic.Length != 0)
                this.AddTopic(topic);

            return this;
        }

        /// <summary>
        /// Update the <see cref="_rawTopicCache"/> based on the current <see cref="topicList"/> contents.
        /// </summary>
        private void UpdateRawTopicCache()
        {
            _rawTopicCache = string.Join("/", topicList);
        }
    }
}