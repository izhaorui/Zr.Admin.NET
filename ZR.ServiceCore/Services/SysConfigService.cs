using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 参数配置Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(ISysConfigService), ServiceLifetime = LifeTime.Transient)]
    public class SysConfigService : BaseService<SysConfig>, ISysConfigService
    {
        /// <summary>
        /// 单次批量更新上限，避免一次请求放大成超长 SQL
        /// </summary>
        private const int BatchUpdateMaxCount = 200;

        public SysConfig GetSysConfigByKey(string key)
        {
            return Queryable().First(f => f.ConfigKey == key);
        }

        /// <summary>
        /// 批量更新参数配置（按 ConfigId 更新，仅更新请求中非 null 的字段，ConfigKey 不允许批量修改）
        /// </summary>
        /// <param name="parms">待更新的参数配置集合</param>
        /// <param name="updateBy">更新人</param>
        /// <returns>更新条数</returns>
        public int BatchUpdateSysConfig(List<SysConfigDto> parms, string updateBy)
        {
            // 校验逻辑全部放在事务之前，异常才能带出具体原因
            if (parms == null || parms.Count == 0)
            {
                throw new CustomException("请求实体不能为空");
            }
            if (parms.Count > BatchUpdateMaxCount)
            {
                throw new CustomException($"单次批量更新不能超过{BatchUpdateMaxCount}条参数配置");
            }

            var ids = parms.Select(x => x.ConfigId).ToList();
            if (ids.Any(x => x <= 0))
            {
                throw new CustomException("参数配置Id不合法");
            }
            if (ids.Distinct().Count() != ids.Count)
            {
                throw new CustomException("存在重复的参数配置Id");
            }
            foreach (var parm in parms)
            {
                if (parm.ConfigName?.Length > 100)
                {
                    throw new CustomException($"参数配置[{parm.ConfigId}]的参数名称不能超过100个字符");
                }
                if (parm.ConfigValue?.Length > 500)
                {
                    throw new CustomException($"参数配置[{parm.ConfigId}]的参数键值不能超过500个字符");
                }
                if (parm.ConfigType?.Length > 1)
                {
                    throw new CustomException($"参数配置[{parm.ConfigId}]的系统内置标识只能为单个字符");
                }
                if (parm.Remark?.Length > 500)
                {
                    throw new CustomException($"参数配置[{parm.ConfigId}]的备注不能超过500个字符");
                }
            }

            var existsList = Queryable().Where(it => ids.Contains(it.ConfigId)).ToList();
            var existsMap = existsList.ToDictionary(it => it.ConfigId);
            var notFoundIds = ids.Where(id => !existsMap.ContainsKey(id)).ToList();
            if (notFoundIds.Count > 0)
            {
                throw new CustomException($"参数配置不存在：{string.Join(",", notFoundIds)}");
            }

            var now = DateTime.Now;
            var updateList = parms.Select(parm =>
            {
                var exists = existsMap[parm.ConfigId];
                return new SysConfig
                {
                    ConfigId = parm.ConfigId,
                    // 未传的字段沿用库中原值，避免前端只提交部分字段时把其他列清空
                    ConfigName = parm.ConfigName ?? exists.ConfigName,
                    ConfigValue = parm.ConfigValue ?? exists.ConfigValue,
                    ConfigType = parm.ConfigType ?? exists.ConfigType,
                    Remark = parm.Remark ?? exists.Remark,
                    Update_by = updateBy,
                    Update_time = now
                };
            }).ToList();

            var tran = UseTran(() =>
            {
                Context.Updateable(updateList)
                    .WhereColumns(it => new { it.ConfigId })
                    .UpdateColumns(it => new
                    {
                        it.ConfigName,
                        it.ConfigValue,
                        it.ConfigType,
                        it.Remark,
                        it.Update_by,
                        it.Update_time
                    })
                    .ExecuteCommand();
            });
            if (!tran.IsSuccess)
            {
                throw tran.ErrorException ?? new CustomException("批量更新参数配置失败：" + tran.ErrorMessage);
            }

            return updateList.Count;
        }
    }
}