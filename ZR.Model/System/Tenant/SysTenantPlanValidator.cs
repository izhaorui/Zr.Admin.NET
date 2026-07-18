namespace ZR.Model.System.Tenant
{
    public class SysTenantPlanValidator : AbstractValidator<SysTenantPlan>
    {
        public SysTenantPlanValidator()
        {
            RuleFor(x => x.PlanCode)
                .NotEmpty().WithMessage("套餐编码不能为空");

            RuleFor(x => x.PlanName)
                .NotEmpty().WithMessage("套餐名称不能为空");
        }
    }
}
