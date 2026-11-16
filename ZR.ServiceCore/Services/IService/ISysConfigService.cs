using System;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 参数配置service接口
    ///
    /// @author mr.zhao
    /// @date 2021-09-29
    /// </summary>
    public interface ISysConfigService : IBaseService<SysConfig>
    {
        SysConfig GetSysConfigByKey(string key);

        /// <summary>
        /// 批量更新参数配置（按 ConfigId 更新，仅更新请求中非 null 的字段，ConfigKey 不允许批量修改）
        /// </summary>
        /// <param name="parms">待更新的参数配置集合</param>
        /// <param name="updateBy">更新人</param>
        /// <returns>更新条数</returns>
        int BatchUpdateSysConfig(List<SysConfigDto> parms, string updateBy);
    }
}
