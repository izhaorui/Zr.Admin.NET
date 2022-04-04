using System;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using System.Collections.Generic;

namespace ZR.Service.Business.IBusinessService
{
    /// <summary>
    /// 演示service接口
    ///
    /// @author zz
    /// @date 2022-03-31
    /// </summary>
    public interface IGenDemoService : IBaseService<GenDemo>
    {
        PagedInfo<GenDemo> GetList(GenDemoQueryDto parm);

    }
}
