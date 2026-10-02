using ZR.Mall.Enum;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 运费模板Service业务层处理
    /// </summary>
    /// <remarks>
    /// 承担三件"不能只靠数据库约束"的事：
    /// 参数有效性校验（非法模板会让引擎按兜底规则算钱，算错了还查不出来）、
    /// 默认模板唯一性（多行 IsDefault=1 时"取第一条"是随机的，运费会随之下波动）、
    /// 删除前的商品引用校验（留下指向已删模板的商品，运费会静默变成 0）。
    /// </remarks>
    [AppService(ServiceType = typeof(IFreightTemplateService), ServiceLifetime = LifeTime.Transient)]
    public class FreightTemplateService : BaseService<FreightTemplate>, IFreightTemplateService
    {
        /// <summary>
        /// 查询运费模板列表（含已绑定商品数）
        /// </summary>
        public PagedInfo<FreightTemplateDto> GetList(FreightTemplateQueryDto parm)
        {
            parm ??= new FreightTemplateQueryDto();
            var predicate = Expressionable.Create<FreightTemplate>();
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.TemplateName),
                it => it.TemplateName.Contains(parm.TemplateName));
            predicate = predicate.AndIF(parm.ChargeType != null,
                it => it.ChargeType == parm.ChargeType.Value);

            var page = Queryable()
                .Where(predicate.ToExpression())
                .ToPage<FreightTemplate, FreightTemplateDto>(parm);

            // 绑定商品数：一次 GroupBy 取回本页各模板的计数，避免逐行查库
            var rows = page.Result ?? new List<FreightTemplateDto>();
            var ids = rows.Select(x => x.Id).ToList();
            if (ids.Count > 0)
            {
                var counts = Context.Queryable<Product>()
                    .Where(x => x.FreightTemplateId != null && ids.Contains(x.FreightTemplateId.Value))
                    .GroupBy(x => x.FreightTemplateId.Value)
                    .Select(x => new { TemplateId = x.FreightTemplateId.Value, Count = SqlFunc.AggregateCount(x.ProductId) })
                    .ToList()
                    .ToDictionary(x => x.TemplateId, x => x.Count);

                foreach (var item in rows)
                {
                    item.ProductCount = counts.TryGetValue(item.Id, out var c) ? c : 0;
                }
            }

            return page;
        }

        /// <summary>
        /// 获取详情（含不配送地区）
        /// </summary>
        public FreightTemplateDto GetInfo(long id)
        {
            var entity = Queryable().First(x => x.Id == id);
            if (entity == null)
            {
                return null;
            }

            var dto = entity.Adapt<FreightTemplateDto>();
            dto.ProductCount = Context.Queryable<Product>().Count(x => x.FreightTemplateId == id);
            dto.ExcludeRegions = Context.Queryable<FreightTemplateExcludeRegion>()
                .Where(x => x.TemplateId == id)
                .OrderBy(x => x.Id)
                .ToList()
                .Select(x => new FreightTemplateRegionDto
                {
                    AreaCode = x.AreaCode,
                    AreaName = x.AreaName,
                    Level = x.Level
                })
                .ToList();
            return dto;
        }

        /// <summary>
        /// 全部模板（下拉选项用），只取必要字段
        /// </summary>
        public List<FreightTemplateDto> ListAll()
        {
            return Queryable()
                .OrderByDescending(x => x.IsDefault)
                .OrderBy(x => x.Id)
                .Select(x => new FreightTemplateDto
                {
                    Id = x.Id,
                    TemplateName = x.TemplateName,
                    ChargeType = x.ChargeType,
                    Freight = x.Freight,
                    IsDefault = x.IsDefault
                })
                .ToList();
        }

        /// <summary>
        /// 新增运费模板
        /// </summary>
        public long AddTemplate(FreightTemplateInputDto parm)
        {
            Validate(parm);
            var entity = parm.Adapt<FreightTemplate>();
            entity.Id = 0;

            // UseTran 收 Action（无返回值），新 Id 用外层变量捕获
            long newId = 0;
            UseTran(() =>
            {
                ClearOtherDefault(0);
                newId = InsertReturnEntity(entity)?.Id ?? 0;
                SaveRegions(newId, parm.ExcludeRegions);
            });
            return newId;
        }

        /// <summary>
        /// 修改运费模板
        /// </summary>
        public int UpdateTemplate(FreightTemplateInputDto parm)
        {
            if (parm.Id <= 0)
            {
                throw new CustomException("模板 Id 不能为空");
            }
            if (Queryable().First(x => x.Id == parm.Id) == null)
            {
                throw new CustomException("运费模板不存在或已删除");
            }
            Validate(parm);

            var entity = parm.Adapt<FreightTemplate>();
            // 差异日志：模板改动直接影响已挂商品的运费，必须留痕
            var rows = Context.Updateable(entity)
                .UpdateColumns(t => new
                {
                    t.TemplateName,
                    t.ChargeType,
                    t.FirstPiece,
                    t.AddPiece,
                    t.FirstWeight,
                    t.AddWeight,
                    t.Freight,
                    t.FreeShippingThreshold,
                    t.IsDefault
                })
                .IgnoreColumns(true)
                .EnableDiffLogEvent("修改运费模板")
                .Where(t => t.Id == parm.Id)
                .ExecuteCommand();

            // 排除地区整体重写：多选集合做差集更新容易漏删，整体替换语义更简单也更可靠
            SaveRegions(parm.Id, parm.ExcludeRegions);
            return rows;
        }

        /// <summary>
        /// 删除运费模板
        /// </summary>
        /// <remarks>
        /// 被商品引用时拒绝删除：留下指向已删模板的商品，其运费会**静默变成 0**
        /// （回落不到默认模板），用户看到"免运费"而下单，商家直接亏本发货。
        /// </remarks>
        public int DeleteTemplates(long[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;

            var usedIds = Context.Queryable<Product>()
                .Where(p => p.FreightTemplateId != null && ids.Contains(p.FreightTemplateId.Value))
                .Select(p => p.FreightTemplateId.Value)
                .Distinct()
                .ToList();

            if (usedIds.Count > 0)
            {
                var names = Queryable()
                    .Where(b => usedIds.Contains(b.Id))
                    .Select(b => b.TemplateName)
                    .ToList();
                throw new CustomException($"运费模板【{string.Join("、", names)}】已被商品使用，请先在商品上解除关联后再删除");
            }

            // 先删排除地区再删模板：模板表无外键约束，但留着孤儿行会让
            // 后续同 Id 复用（表重置/导入）时莫名带上旧规则
            int deleted = 0;
            UseTran(() =>
            {
                Context.Deleteable<FreightTemplateExcludeRegion>()
                    .Where(x => ids.Contains(x.TemplateId))
                    .EnableDiffLogEventIF(true, "删除运费模板不配送地区")
                    .ExecuteCommand();

                deleted = Context.Deleteable<FreightTemplate>(ids)
                    .EnableDiffLogEventIF(true, "删除运费模板")
                    .ExecuteCommand();
            });
            return deleted;
        }

        #region 私有方法

        /// <summary>
        /// 参数校验：只校验**会被当前计费方式用到**的那组参数。
        /// </summary>
        /// <remarks>
        /// 不交叉校验另一组（如按件时不看 AddWeight），是为了让运营能先填好两套参数、
        /// 随时切换计费方式而不必重填。
        /// 真正会导致算错的两条硬约束必须拦：续件/续重为 0 会让引擎无法切分。
        /// </remarks>
        private static void Validate(FreightTemplateInputDto parm)
        {
            if (parm == null) throw new CustomException("参数不能为空");
            if (string.IsNullOrWhiteSpace(parm.TemplateName))
            {
                throw new CustomException("模板名称不能为空");
            }
            if (parm.Freight < 0m)
            {
                throw new CustomException("运费不能为负数");
            }
            if (parm.FreeShippingThreshold < 0m)
            {
                throw new CustomException("包邮门槛不能为负数");
            }

            if (parm.ChargeType == (int)FreightChargeTypeEnum.ByPiece)
            {
                if (parm.FirstPiece < 0m)
                {
                    throw new CustomException("首件不能为负数");
                }
                if (parm.AddPiece < 1m)
                {
                    throw new CustomException("续件必须大于等于 1，否则无法按件计费");
                }
            }
            else if (parm.ChargeType == (int)FreightChargeTypeEnum.ByWeight)
            {
                if (parm.FirstWeight < 0m)
                {
                    throw new CustomException("首重不能为负数");
                }
                if (parm.AddWeight <= 0m)
                {
                    throw new CustomException("续重必须大于 0，否则无法按重量计费");
                }
            }
            else
            {
                throw new CustomException("计费方式无效，请选择按件或按重");
            }
        }

        /// <summary>
        /// 把本条设为默认时，先清掉其他默认标记。
        /// </summary>
        /// <remarks>
        /// 必须显式清而不是靠"取第一条"：多条 IsDefault=1 时取哪条取决于数据库返回顺序，
        /// 运费会随之下波动，且难以复现。
        /// </remarks>
        private void ClearOtherDefault(long keepId)
        {
            Context.Updateable<FreightTemplate>()
                .SetColumns(x => x.IsDefault == 0)
                .Where(x => x.IsDefault == 1 && x.Id != keepId)
                .ExecuteCommand();
        }

        /// <summary>
        /// 覆盖式保存不配送地区
        /// </summary>
        private void SaveRegions(long templateId, List<FreightTemplateRegionDto> regions)
        {
            Context.Deleteable<FreightTemplateExcludeRegion>()
                .Where(x => x.TemplateId == templateId)
                .ExecuteCommand();

            if (regions == null || regions.Count == 0)
            {
                return;
            }

            var rows = regions
                .Where(x => !string.IsNullOrWhiteSpace(x.AreaCode))
                .Select(x => new FreightTemplateExcludeRegion
                {
                    TemplateId = templateId,
                    AreaCode = x.AreaCode.Trim(),
                    AreaName = x.AreaName,
                    Level = x.Level
                })
                .ToList();
            if (rows.Count > 0)
            {
                Context.Insertable(rows).ExecuteCommand();
            }
        }

        #endregion
    }
}
