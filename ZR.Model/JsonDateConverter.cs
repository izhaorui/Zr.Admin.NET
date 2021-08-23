using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model
{
    public class JsonDateConverter: IsoDateTimeConverter
    {
        public JsonDateConverter()
        {
            DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        }
    }
}
