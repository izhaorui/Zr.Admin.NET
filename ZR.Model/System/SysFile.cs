using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    [SqlSugar.SugarTable("sys_file")]
    public class SysFile
    {
        public int Id { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string StorePath { get; set; }
        public string AccessPat { get; set; }
        public int FileSize { get; set; }
        public string FileExt { get; set; }
        public DateTime? AddTime { get; set; }

    }
}
