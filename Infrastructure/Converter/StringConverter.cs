using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Converter
{
    /// <summary>
    /// Json任何类型读取到字符串属性
    /// 因为 System.Text.Json 必须严格遵守类型一致，当非字符串读取到字符属性时报错：
    /// The JSON value could not be converted to System.String.
    /// </summary>
    public class StringConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            else
            {
                //非字符类型，返回原生内容
                return GetRawPropertyValue(reader);
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
        /// <summary>
        /// 非字符类型，返回原生内容
        /// </summary>
        /// <param name="jsonReader"></param>
        /// <returns></returns>
        private static string GetRawPropertyValue(Utf8JsonReader jsonReader)
        {
            ReadOnlySpan<byte> utf8Bytes = jsonReader.HasValueSequence ?
            jsonReader.ValueSequence.ToArray() :
            jsonReader.ValueSpan;
            return Encoding.UTF8.GetString(utf8Bytes);
        }
    }
}
