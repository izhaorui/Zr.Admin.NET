using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using ZR.Model.Dto;
using ZR.Model.Models;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 通知公告表输入对象
    /// </summary>
    public class SysNoticeDto
    {
        public int NoticeId { get; set; }
        public string NoticeTitle { get; set; }
        public string NoticeType { get; set; }
        public string NoticeContent { get; set; }
        public string Status { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 通知公告表查询对象
    /// </summary>
    public class SysNoticeQueryDto : PagerInfo 
    {
        public string NoticeTitle { get; set; }
        public string NoticeType { get; set; }
        public string CreateBy { get; set; }
        public string Status { get; set; }
    }
}
