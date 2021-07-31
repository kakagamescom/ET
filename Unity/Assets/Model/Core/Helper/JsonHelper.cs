using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace ET
{
    public static class JsonHelper
    {
#if NOT_UNITY
        private static readonly JsonWriterSettings logDefineSettings =
                new JsonWriterSettings() { OutputMode = JsonOutputMode.RelaxedExtendedJson };
#endif

        public static string ToJson(object message)
        {
#if NOT_UNITY
            return BsonExtensionMethods.ToJson(message, logDefineSettings);
#else
            return LitJson.JsonMapper.ToJson(message);
#endif
        }

        public static object FromJson(Type type, string json)
        {
#if NOT_UNITY
            return BsonSerializer.Deserialize(json, type);
#else
            return LitJson.JsonMapper.ToObject(json, type);
#endif
        }

        public static T FromJson<T>(string json)
        {
#if NOT_UNITY
            return BsonSerializer.Deserialize<T>(json);
#else
            return LitJson.JsonMapper.ToObject<T>(json);
#endif
        }
    }
}