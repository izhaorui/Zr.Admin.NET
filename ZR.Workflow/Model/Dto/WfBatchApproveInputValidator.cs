using FluentValidation;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 批量审批输入校验
    /// </summary>
    public class WfBatchApproveInputValidator : AbstractValidator<WfBatchApproveInput>
    {
        public WfBatchApproveInputValidator()
        {
            RuleFor(x => x.TaskIds)
                .NotEmpty().WithMessage("任务Id不能为空")
                .Must(ContainValidId).WithMessage("任务Id格式不正确，至少包含一个有效的任务Id");
        }

        /// <summary>
        /// 逗号分隔的任务Id中至少包含一个可解析的 long
        /// </summary>
        private static bool ContainValidId(string taskIds)
        {
            if (string.IsNullOrWhiteSpace(taskIds)) return false;
            return taskIds.SplitByComma().Any(s => long.TryParse(s, out _));
        }
    }
}
