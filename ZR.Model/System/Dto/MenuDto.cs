using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Dto
{
    public class MenuDto
    {
        //{"parentId":0,"menuName":"aaa","icon":"documentation","menuType":"M","orderNum":999,"visible":0,"status":0,"path":"aaa"}
        public int parentId { get; set; }
        public string menuName { get; set; }
        public string icon { get; set; } = "";
        public string menuType { get; set; }
        public int orderNum { get; set; }
        public int visible { get; set; }
        public int status { get; set; }
        public string path { get; set; } = "#";
        public int MenuId { get; set; }
        public string component { get; set; }
        public int isCache { get; set; }
        public int isFrame { get; set; }
        public string perms { get; set; } = "";
    }
}
