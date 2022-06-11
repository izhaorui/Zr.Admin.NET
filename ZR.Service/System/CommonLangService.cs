using Infrastructure.Attribute;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Repository;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 多语言配置Service业务层处理
    ///
    /// @author zr
    /// @date 2022-05-06
    /// </summary>
    [AppService(ServiceType = typeof(ICommonLangService), ServiceLifetime = LifeTime.Transient)]
    public class CommonLangService : BaseService<CommonLang>, ICommonLangService
    {
        private readonly CommonLangRepository _CommonLangrepository;
        public CommonLangService(CommonLangRepository repository)
        {
            _CommonLangrepository = repository;
        }

        #region 业务逻辑代码

        /// <summary>
        /// 查询多语言配置列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<CommonLang> GetList(CommonLangQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<CommonLang>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangCode), it => it.LangCode == parm.LangCode);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangKey), it => it.LangKey.Contains(parm.LangKey));
            predicate = predicate.AndIF(parm.BeginAddtime != null, it => it.Addtime >= parm.BeginAddtime && it.Addtime <= parm.EndAddtime);
            var response = _CommonLangrepository
                .Queryable()
                .Where(predicate.ToExpression())
                .ToPage(parm);
            return response;
        }

        /// <summary>
        /// 行转列
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public dynamic GetListToPivot(CommonLangQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<CommonLang>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangCode), it => it.LangCode == parm.LangCode);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangKey), it => it.LangKey.Contains(parm.LangKey));
            predicate = predicate.AndIF(parm.BeginAddtime != null, it => it.Addtime >= parm.BeginAddtime && it.Addtime <= parm.EndAddtime);
            var response = _CommonLangrepository
                .Queryable()
                .Where(predicate.ToExpression())
                .ToPivotList(it => it.LangCode, it => it.LangKey, it => it.Max(f => f.LangName));
            return response;
        }

        public List<CommonLang> GetLangList(CommonLangQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<CommonLang>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangCode), it => it.LangCode == parm.LangCode);
            //predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.LangKey), it => it.LangKey.Contains(parm.LangKey));
            var response = _CommonLangrepository
                .Queryable()
                .Where(predicate.ToExpression())
                .ToList();
            return response;
        }

        public void StorageCommonLang(CommonLangDto parm)
        {
            List<CommonLang> langs = new();
            foreach (var item in parm.LangList)
            {
                langs.Add(new CommonLang()
                {
                    Addtime = DateTime.Now,
                    LangKey = parm.LangKey,
                    LangCode = item.LangCode,
                    LangName = item.LangName,
                });
            }
            var storage = _CommonLangrepository.Storageable(langs)
                .WhereColumns(it => new { it.LangKey, it.LangCode })
                .ToStorage();

            storage.AsInsertable.ExecuteReturnSnowflakeIdList();//执行插入
            storage.AsUpdateable.UpdateColumns(it => new { it.LangName }).ExecuteCommand();//执行修改
        }

        public Dictionary<string, object> SetLang(List<CommonLang> msgList)
        {
            Dictionary<string, object> dic = new();

            foreach (var item in msgList)
            {
                if (!dic.ContainsKey(item.LangKey))
                {
                    dic.Add(item.LangKey, item.LangName);
                }
            }
            return dic;
        }
        #endregion
    }
}