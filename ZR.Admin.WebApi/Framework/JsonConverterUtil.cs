using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZR.Admin.WebApi.Framework
{
    public class JsonConverterUtil
    {
        public class DateTimeNullConverter : JsonConverter<DateTime?>
        {
            public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => string.IsNullOrEmpty(reader.GetString()) ? default(DateTime?) : ParseDateTime(reader.GetString());

            public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
                => writer.WriteStringValue(value?.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public class DateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var dateTime = ParseDateTime(reader.GetString());
                return dateTime == null ? DateTime.MinValue : dateTime.Value;
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public static DateTime? ParseDateTime(string dateStr)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(dateStr, @"^\d{4}[/-]") && DateTime.TryParse(dateStr, null,System.Globalization.DateTimeStyles.AssumeLocal, out var dateVal))
                return dateVal;
            return null;
        }
    }
}
