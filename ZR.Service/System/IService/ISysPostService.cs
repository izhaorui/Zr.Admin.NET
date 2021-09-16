using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysPostService: IBaseService<SysPost>
    {
        string CheckPostNameUnique(SysPost sysPost);
        string CheckPostCodeUnique(SysPost sysPost);
    }
}
