﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Model
{
    public class SendEmailDto
    {
        public string FileUrl { get; set; } = "";
        public string Subject { get; set; }
        public string ToUser { get; set; }
        public string Content { get; set; } = "";
        public string HtmlContent { get; set; }
        public DateTime AddTime { get; set; }
    }
}