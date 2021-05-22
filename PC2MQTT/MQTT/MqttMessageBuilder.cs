using System.Diagnostics;
using static PC2MQTT.MQTT.MqttMessage;

namespace PC2MQTT.MQTT
{
    public class MqttMessageBuilder
    {

        /// <inheritdoc cref="MqttMessage.AddDeviceId"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder AddDeviceId
        {
            get
            {
                _mqttMessage.AddDeviceIdToTopic();
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.AddDeviceIdToTopic"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder AddDeviceIdToTopic
        {
            get
            {
                _mqttMessage.AddDeviceIdToTopic();
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.AddMultiLevelWildcard"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder AddMultiLevelWildcard
        {
            get
            {
                _mqttMessage.AddTopic("#");
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.AddSingleLevelWildcard"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder AddSingleLevelWildcard
        {
            get
            {
                _mqttMessage.AddTopic("+");
                return this;
            }
        }

        /// <summary>
        /// Specifies not to retain the message. Sets the retain flag to false. This is the default.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder DoNotRetain
        {
            get
            {
                _mqttMessage.retain = false;
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.PublishMessage"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder PublishMessage
        {
            get
            {
                _mqttMessage.messageType = MqttMessageType.MQTT_PUBLISH;
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.QueueMessage"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder QueueMessage
        {
            get
            {
                _mqttMessage.sendImmediately = false;
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.ReceivedMessage"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder ReceivedMessage
        {
            get
            {
                _mqttMessage.messageType = MqttMessageType.MQTT_RECEIVED;
                return this;
            }
        }

        /// <summary>
        /// Specifies to the MQTT server to retain this message. Sets the retain flag to true.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder Retain
        {
            get
            {
                _mqttMessage.retain = true;
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.SendImmediately"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder SendImmediately
        {
            get
            {
                _mqttMessage.sendImmediately = true;
                return this;
            }
        }

        /// <inheritdoc cref="MqttMessage.SubscribeMessage"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder SubscribeMessage
        {
            get
            {
                _mqttMessage.messageType = MqttMessageType.MQTT_SUBSCRIBE;
                return this;
            }
        }


        /// <inheritdoc cref="MqttMessage.UnsubscribeMessage"/>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public MqttMessageBuilder UnsubscribeMessage
        {
            get
            {
                _mqttMessage.messageType = MqttMessageType.MQTT_UNSUBSCRIBE;
                return this;
            }
        }

        private MqttMessage _mqttMessage;

        /// <summary>
        /// Initialize a new <see cref="MqttMessageBuilder"/>.
        /// </summary>
        public MqttMessageBuilder()
        {
            _mqttMessage = new MqttMessage();
        }

        /// <summary>
        /// Creates a new <see cref="MqttMessage"/> to build using <see cref="MqttMessageBuilder"/>.
        /// </summary>
        /// <example>
        /// <code> 
        /// var msg = MqttMessageBuilder.
        ///         NewMessage().
        ///         SubscribeMessage.
        ///         AddDeviceId.
        ///         AddTopic("example").
        ///         AddMultiLevelWildcard.
        ///         DoNotRetain.
        ///         QueueMessage.
        ///         Build();
        /// </code>
        /// </example>
        public static MqttMessageBuilder NewMessage()
        {
            return new MqttMessageBuilder();
        }

        /// <inheritdoc cref="MqttMessage.AddTopic"/>
        public MqttMessageBuilder AddTopic(string topic)
        {
            _mqttMessage.AddTopic(topic);
            return this;
        }

        /// <summary>
        /// Builds and returns a <see cref="MqttMessage"/>.
        /// </summary>
        /// <returns></returns>
        public MqttMessage Build()
        {
            return _mqttMessage;
        }

        /// <inheritdoc cref="MqttMessage.SetMessage"/>
        public MqttMessageBuilder SetMessage(string message)
        {
            _mqttMessage.SetMessage(message);
            return this;
        }

        /// <inheritdoc cref="MqttMessage.SetMessage"/>
        public MqttMessageBuilder SetRawMessage(byte[] message)
        {
            _mqttMessage.SetRawMessage(message);
            return this;
        }

        /// <inheritdoc cref="MqttMessage.SetMessageType"/>
        public MqttMessageBuilder SetMessageType(MqttMessageType messageType)
        {
            _mqttMessage.messageType = messageType;
            return this;
        }
    }
}