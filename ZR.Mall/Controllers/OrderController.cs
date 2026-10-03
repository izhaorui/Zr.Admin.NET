using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using ZR.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2025-05-30
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 订单管理
    /// </summary>
    [Route("shopping/Order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class OrderController : BaseController
    {
        private NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 后台订单运营：查询 / 导出 / 统计 / 修改
        /// </summary>
        private readonly IOMSOrderService _OMSOrderService;

        /// <summary>
        /// 订单状态内核：取消 / 批量取消 / 发货
        /// </summary>
        private readonly IOMSOrderStateService _orderStateService;

        public OrderController(IOMSOrderService OMSOrderService, IOMSOrderStateService orderStateService)
        {
            _OMSOrderService = OMSOrderService;
            _orderStateService = orderStateService;
        }

        /// <summary>
        /// 查询订单管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "oms:order:list")]
        public IActionResult QueryOMSOrder([FromQuery] OMSOrderQueryDto parm)
        {
            var response = _OMSOrderService.GetList(parm);
            // 列表 + 概览共 2 条 SQL：概览（各状态计数 + 待发货未发货数）由 GetOrderOverview 一次 GROUP BY 得出，
            // 替代原先额外两次 COUNT。Extra 键名保持不变，前端仍读 data.extra.statusStats。
            var overview = _OMSOrderService.GetOrderOverview();
            response.Extra.Add("NotDelivereOrder", overview.NotDelivered);
            response.Extra.Add("StatusStats", overview.StatusStats);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询订单管理详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "oms:order:query")]
        public IActionResult GetOMSOrder(long Id)
        {
            var response = _OMSOrderService.GetInfo(Id);
            // GetInfo 对不存在的订单返回 null；不判空会导致 Adapt(null) 后返回 200 + data:null（或 NRE）
            if (response == null)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在");
            }

            var info = response.Adapt<OMSOrderDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 查看订单中**敏感字段**的完整值（发货用）。
        ///
        /// 为什么要单独一个接口：订单详情里敏感字段默认脱敏，而虚拟商品发货要的恰恰是
        /// 接收手机号 / 接收账号这类值。三道约束缺一不可 ——
        /// ① 独立权限 <c>oms:order:sensitive</c>（普通订单查询权看不到）；
        /// ② 记操作日志（谁为哪单看过明文，可审计）；
        /// ③ **不保存请求与响应体**：否则明文会原样落进操作日志表，等于绕开①和②从日志里泄露。
        ///
        /// 只返回敏感字段：非敏感字段在详情里本就是明文，再传一遍只会白扩大数据面。
        /// </summary>
        [HttpGet("formValuesRaw/{id}")]
        [ActionPermissionFilter(Permission = "oms:order:sensitive")]
        [Log(Title = "查看买家敏感信息", BusinessType = BusinessType.OTHER, IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult GetOrderFormValuesRaw(long id)
        {
            return SUCCESS(_OMSOrderService.GetSensitiveFormValues(id));
        }

        /// <summary>
        /// 更新订单管理
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "oms:order:edit")]
        [Log(Title = "订单管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateOMSOrder([FromBody] OMSOrderDto parm)
        {
            var modal = parm.Adapt<OMSOrder>().ToUpdate(HttpContext);
            var response = _OMSOrderService.UpdateOMSOrder(parm.OperType, modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 删除订单管理
        /// </summary>
        /// <returns></returns>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "oms:order:delete")]
        [Log(Title = "订单管理", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteOMSOrder([FromRoute] string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);
            return ToResponse(_OMSOrderService.DeleteOrders(idArr));
        }

        /// <summary>
        /// 订单退款
        /// </summary>
        /// <returns></returns>
        [HttpPost("refund")]
        [ActionPermissionFilter(Permission = "oms:order:refund")]
        [Log(Title = "订单退款", BusinessType = BusinessType.UPDATE)]
        public IActionResult RefundOrder([FromBody] OMSOrderDto parm)
        {
            var modal = parm.Adapt<OMSOrder>().ToUpdate(HttpContext);
            var response = _OMSOrderService.UpdateOMSOrder((int)Enum.OrderUpdateOperType.Refund, modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 取消订单（仅待发货可取消，回补库存）
        /// </summary>
        /// <returns></returns>
        [HttpPost("cancel/{id}")]
        [ActionPermissionFilter(Permission = "oms:order:cancel")]
        [Log(Title = "订单取消", BusinessType = BusinessType.UPDATE)]
        public IActionResult CancelOMSOrder(long id)
        {
            var response = _orderStateService.CancelOrder(id);

            return ToResponse(response);
        }

        /// <summary>
        /// 批量取消订单：仅待付款/待发货可取消，其余状态自动跳过；返回成功取消的条数
        /// </summary>
        [HttpPost("cancel/batch")]
        [ActionPermissionFilter(Permission = "oms:order:cancel")]
        [Log(Title = "订单批量取消", BusinessType = BusinessType.UPDATE)]
        public IActionResult CancelOMSOrders([FromBody] List<long> ids)
        {
            var response = _orderStateService.CancelOrders(ids);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出订单管理
        /// </summary>
        /// <returns></returns>
        [Log(Title = "订单管理", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "oms:order:export")]
        public IActionResult Export([FromQuery] OMSOrderQueryDto parm)
        {
            var list = _OMSOrderService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "订单管理", "订单管理");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 订单发货
        /// </summary>
        /// <returns></returns>
        [HttpPut("delivery")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        [Log(Title = "订单发货", BusinessType = BusinessType.UPDATE)]
        public async Task<IActionResult> Delivery([FromBody] OMSOrderDto parm)
        {
            var orderInfo = await _OMSOrderService.GetByIdAsync(parm.Id);
            if (orderInfo == null)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在");
            }
            orderInfo.DeliveryCompany = parm.DeliveryCompany;
            orderInfo.DeliveryNo = parm.DeliveryNo;
            var response = await _orderStateService.OrderDelivery(orderInfo);
            if (response > 0)
            {
                return ToResponse(ResultCode.SUCCESS, "发货成功");
            }
            // 不再把内部结果码（-1/-2/-3）直接抛给用户，映射成可读原因
            return ToResponse(ResultCode.CUSTOM_ERROR, DeliveryFailReason(response));
        }

        /// <summary>
        /// 发货结果码 → 用户可读提示。
        /// 取值来自 <see cref="IOMSOrderStateService.OrderDelivery"/>：&gt;0 成功；
        /// -1 已发货；-2 缺收货地址；-3 非待发货状态（已取消/关闭或并发变更）。
        /// </summary>
        private static string DeliveryFailReason(int result) => result switch
        {
            -1 => "该订单已发货，请勿重复操作",
            -2 => "该订单缺少收货信息，无法发货",
            -3 => "订单当前状态不可发货（可能已取消/关闭或已被处理），请刷新后重试",
            _ => "发货失败，请刷新后重试"
        };

        /// <summary>
        /// 导出待发货订单
        /// </summary>
        /// <returns></returns>
        [Log(Title = "导出待发货订单", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("exportDelivery")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        public async Task<IActionResult> ExportExpress([FromQuery] OMSOrderQueryDto parm)
        {
            if (parm == null || parm.BeginCreateTime == null)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "请选择时间");
            }
            var list = await _OMSOrderService.ExportWaitDeliveryList(parm);
            var result = await ExportExcelMiniAsync(list, "待发货", "待发货订单");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 批量发货
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost("importData")]
        [Log(Title = "批量发货", BusinessType = BusinessType.IMPORT, IsSaveRequestData = false, IsSaveResponseData = true)]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        public async Task<IActionResult> ImportData([FromForm(Name = "file")] IFormFile formFile)
        {
            if (formFile == null || formFile.Length <= 0)
            {
                return ToResponse(ResultCode.FAIL, "请选择要导入的文件");
            }
            var resultList = new List<DeliveryExpressDto>();
            using var stream = formFile.OpenReadStream();
            var rows = await stream.QueryAsync<DeliveryExpressDto>(startCell: "A1");

            var orderNos = rows.Select(x => x.OrderNo).Distinct().ToList();
            var allOrders = await _OMSOrderService.Queryable().In(x => x.OrderNo, orderNos).ToListAsync();

            foreach (var item in rows)
            {
                if (string.IsNullOrWhiteSpace(item.DeliveryCompany) || string.IsNullOrWhiteSpace(item.DeliveryNo))
                {
                    item.Status = "缺少快递信息"; 
                    resultList.Add(item);
                    continue;
                }
                var orderInfo = allOrders.FirstOrDefault(f => f.OrderNo == item.OrderNo);
                if (orderInfo == null)
                {
                    item.Status = "订单号不存在";
                    resultList.Add(item);
                    continue;
                }
                if (orderInfo.DeliveryStatus != Enum.DeliveryStatusEnum.NotDelivered)
                {
                    item.Status = "已发货";
                    resultList.Add(item);
                    continue;
                }
                if (orderInfo.AddressSnapshot == null)
                {
                    item.Status = "缺少收货信息";
                    resultList.Add(item);
                    continue;
                }
                // 用数据库订单实体发货（保留 Id/AddressSnapshot/OrderStatus 等），仅覆盖 Excel 里的快递信息
                orderInfo.DeliveryCompany = item.DeliveryCompany;
                orderInfo.DeliveryNo = item.DeliveryNo;

                var result =  await _orderStateService.OrderDelivery(orderInfo);
                item.Status = result > 0 ? "发货成功" : DeliveryFailReason(result);
                resultList.Add(item);
            }

            return SUCCESS(new
            {
                total = resultList.Count,
                successCount = resultList.Count(x => x.Status == "发货成功"),
                failCount = resultList.Count(x => x.Status != "发货成功"),
                result = resultList
            });
        }

        /// <summary>
        /// 批量发货导入模板下载（仅表头：订单号/物流公司/物流单号）
        /// </summary>
        [HttpGet("importTemplate")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        [Log(Title = "订单批量发货模板", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public IActionResult ImportTemplate()
        {
            var result = DownloadImportTemplate(new List<DeliveryExpressDto>() { }, "order");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 查询销售总
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSales")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSales(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetTotalSales(dto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询销售趋势
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSalesTrade")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSalesTrade(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetSaleTreandByDay(dto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询销售前10的商品
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSaleTopProduct")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSaleTopProduct(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetSaleTopProduct(dto);

            return SUCCESS(response);
        }
    }
}