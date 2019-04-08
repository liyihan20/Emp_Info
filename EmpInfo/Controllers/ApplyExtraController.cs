using EmpInfo.Filter;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Services;

namespace EmpInfo.Controllers
{
    /// <summary>
    /// 除了流程常用的方法外，其它附加的功能点
    /// </summary>
    public class ApplyExtraController : BaseController
    {
        #region 请假

        //行政部发送约谈信息
        public JsonResult LeaveDayExceedPush(string sysNo, string bookTime)
        {
            try {
                new ALSv(sysNo).LeaveDayExceedPush(userInfo, bookTime);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }            

            WriteEventLog("请假条申请", sysNo + ">发送行政面谈通知" + ";bookTime:" + bookTime);

            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult GetLeaveDaysExceedLog(string sysNo)
        {
            var log = new ALSv().GetLeaveDaysExceedLog(sysNo);
            if (log == null) {
                return Json(new SimpleResultModel() { suc = false });
            }
            
            return Json(new SimpleResultModel() { suc = true, msg = string.Format("【发送记录】发送人：{0}；发送时间：{1}；预约时间：{2}", log.send_user, ((DateTime)log.send_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)log.book_date).ToString("yyyy-MM-dd HH:mm")) });
        }

        //查看最近1年内的申请记录
        [SessionTimeOutJsonFilter]
        public JsonResult GetLeaveRecordsInOneYear(string applierNameAndCard)
        { 
            return Json(new { suc = true, list = new ALSv().GetLeaveRecordsInOneYear(applierNameAndCard) });
        }

        #endregion

        #region 仓管权限申请

        [SessionTimeOutJsonFilter]
        public JsonResult GetStockAndAdminByAccount(string accName)
        {            
            return Json(new { suc = true, result = new SASv().GetK3StockAuditor(accName) });
        }

        #endregion

    }
}
