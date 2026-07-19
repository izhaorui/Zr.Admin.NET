using Infrastructure.Attribute;
using Mapster;
using SqlSugar;
using ZR.Model;
using System;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 个人待办Service业务层处理（数据按当前登录用户隔离）
    /// </summary>
    [AppService(ServiceType = typeof(ISysTodoService), ServiceLifetime = LifeTime.Transient)]
    public class SysTodoService : BaseService<SysTodo>, ISysTodoService
    {

        /// <summary>
        /// 分页查询（统一追加 UserId 过滤）
        /// </summary>
        public PagedInfo<SysTodo> GetPages(SysTodoQueryDto parm, long userId)
        {
            var predicate = Expressionable.Create<SysTodo>();
            predicate = predicate.And(m => m.UserId == userId);
            predicate = predicate.AndIF(!parm.KeyWord.IsEmpty(), m => m.Title.Contains(parm.KeyWord) || m.Content.Contains(parm.KeyWord));
            predicate = predicate.AndIF(!parm.Status.IsEmpty(), m => m.Status == parm.Status);
            predicate = predicate.AndIF(parm.Priority.HasValue, m => m.Priority == parm.Priority.Value);
            predicate = predicate.AndIF(parm.BeginTime.HasValue, m => m.DueTime >= parm.BeginTime);
            predicate = predicate.AndIF(parm.EndTime.HasValue, m => m.DueTime <= parm.EndTime);

            return GetPages(predicate.ToExpression(), parm, m => m.Id, OrderByType.Desc);
        }

        /// <summary>
        /// 查询详情（校验归属用户，越权返回 null）
        /// </summary>
        public SysTodo GetById(long id, long userId)
        {
            return Queryable().First(m => m.Id == id && m.UserId == userId);
        }

        /// <summary>
        /// 新增
        /// </summary>
        public int AddSysTodo(SysTodo model)
        {
            return Insert(model, it => new
            {
                it.UserId,
                it.Title,
                it.Content,
                it.Status,
                it.Priority,
                it.DueTime,
                it.ReminderTime,
                it.Create_by,
                it.Create_time,
                it.Remark
            });
        }

        /// <summary>
        /// 更新（按当前用户隔离）
        /// </summary>
        public int UpdateSysTodo(SysTodo model)
        {
            return Update(w => w.Id == model.Id && w.UserId == model.UserId, it => new SysTodo()
            {
                Title = model.Title,
                Content = model.Content,
                Priority = model.Priority,
                DueTime = model.DueTime,
                ReminderTime = model.ReminderTime,
                Update_by = model.Update_by,
                Update_time = model.Update_time
            });
        }

        /// <summary>
        /// 删除（按当前用户隔离）
        /// </summary>
        public int DeleteSysTodo(long id, long userId)
        {
            return Deleteable().Where(w => w.Id == id && w.UserId == userId).ExecuteCommand();
        }

        /// <summary>
        /// 切换完成状态（完成时记录完成时间，取消完成时清空）
        /// </summary>
        public int ChangeStatus(long id, string status, long userId)
        {
            DateTime? finishTime = status == "1" ? DateTime.Now : (DateTime?)null;
            return Update(w => w.Id == id && w.UserId == userId, it => new SysTodo()
            {
                Status = status,
                FinishTime = finishTime
            });
        }

        /// <summary>
        /// 统计（按当前用户隔离）
        /// </summary>
        public SysTodoStatsDto GetStats(long userId)
        {
            var q = Queryable().Where(m => m.UserId == userId);
            var today = DateTime.Today;

            return new SysTodoStatsDto
            {
                Total = q.Count(),
                Undone = q.Count(m => m.Status == "0"),
                DueToday = q.Count(m => m.Status == "0" && m.DueTime.HasValue && m.DueTime.Value.Date == today),
                Overdue = q.Count(m => m.Status == "0" && m.DueTime.HasValue && m.DueTime.Value.Date < today)
            };
        }

        /// <summary>
        /// 未完成待办数（Status=0），用于消息中心待办 tab 红点。
        /// </summary>
        public int GetTodoReminderCount(long userId)
        {
            return Queryable().Count(t => t.UserId == userId && t.Status == "0");
        }

        /// <summary>
        /// 未完成待办列表（Status=0），按 DueTime 升序（无截止时间排后）、Priority 降序。
        /// 供消息中心待办 tab 打开时查询，不写消息、不标记已提醒。
        /// </summary>
        public List<SysTodo> GetReminderTodos(long userId)
        {
            return Queryable()
                .Where(t => t.UserId == userId && t.Status == "0")
                .OrderBy(t => t.DueTime, OrderByType.Asc)
                .OrderBy(t => t.Priority, OrderByType.Desc)
                .ToList();
        }
    }
}
