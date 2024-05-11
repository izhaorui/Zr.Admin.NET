using Microsoft.AspNetCore.Mvc;
using ZR.Model.Public.Dto;
using ZR.Model.Public;
using ZR.Service.Public.IPublicService;
using ZR.Admin.WebApi.Filters;

//创建时间：2024-05-11
namespace ZR.Admin.WebApi.Controllers.Public
{
    /// <summary>
    /// 广告管理
    /// </summary>
    [Verify]
    [Route("public/BannerConfig")]
    public class BannerConfigController : BaseController
    {
        /// <summary>
        /// 广告管理接口
        /// </summary>
        private readonly IBannerConfigService _BannerConfigService;

        public BannerConfigController(IBannerConfigService BannerConfigService)
        {
            _BannerConfigService = BannerConfigService;
        }

        /// <summary>
        /// 查询广告管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "bannerconfig:list")]
        public IActionResult QueryBannerConfig([FromQuery] BannerConfigQueryDto parm)
        {
            var response = _BannerConfigService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询广告管理详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "bannerconfig:query")]
        public IActionResult GetBannerConfig(int Id)
        {
            var response = _BannerConfigService.GetInfo(Id);

            var info = response.Adapt<BannerConfigDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 添加广告管理
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "bannerconfig:add")]
        [Log(Title = "广告管理", BusinessType = BusinessType.INSERT)]
        public IActionResult AddBannerConfig([FromBody] BannerConfigDto parm)
        {
            var modal = parm.Adapt<BannerConfig>().ToCreate(HttpContext);

            var response = _BannerConfigService.AddBannerConfig(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 更新广告管理
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "bannerconfig:edit")]
        [Log(Title = "广告管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateBannerConfig([FromBody] BannerConfigDto parm)
        {
            var modal = parm.Adapt<BannerConfig>();
            var response = _BannerConfigService.UpdateBannerConfig(modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 删除广告管理
        /// </summary>
        /// <returns></returns>
        [HttpDelete("delete/{ids}")]
        [ActionPermissionFilter(Permission = "bannerconfig:delete")]
        [Log(Title = "广告管理", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteBannerConfig([FromRoute] string ids)
        {
            var idArr = Tools.SplitAndConvert<int>(ids);

            return ToResponse(_BannerConfigService.Delete(idArr, "删除广告管理"));
        }

        /// <summary>
        /// 导出广告管理
        /// </summary>
        /// <returns></returns>
        [Log(Title = "广告管理", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "bannerconfig:export")]
        public IActionResult Export([FromQuery] BannerConfigQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _BannerConfigService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "广告管理", "广告管理");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 保存排序
        /// </summary>
        /// <param name="id">主键</param>
        /// <param name="value">排序值</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "bannerconfig:edit")]
        [HttpGet("ChangeSort")]
        [Log(Title = "保存排序", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeSort(int id = 0, int value = 0)
        {
            if (id <= 0) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            var response = _BannerConfigService.Update(w => w.Id == id, it => new BannerConfig()
            {
                SortId = value,
            });

            return ToResponse(response);
        }

        /// <summary>
        /// 查询广告管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("bannerList")]
        [AllowAnonymous]
        public IActionResult QueryBannerList([FromQuery] BannerConfigQueryDto parm)
        {
            var response = _BannerConfigService.GetBannerList(parm);
            return SUCCESS(new { list = response });
        }
    }
}