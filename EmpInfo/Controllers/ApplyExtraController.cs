using EmpInfo.Filter;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Services;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    /// <summary>
    /// 除了流程常用的方法外，其它附加的功能点
    /// </summary>
    public class ApplyExtraController : BaseController
    {

        #region 通用模块

        public JsonResult GetAHPushLog(string sysNo)
        {
            var log = new ALSv().GetAHMsgPushLog(sysNo);
            if (log == null) {
                return Json(new SimpleResultModel() { suc = false });
            }

            return Json(new SimpleResultModel() { suc = true, msg = string.Format("【发送记录】发送人：{0}；发送时间：{1}；预约时间：{2}", log.send_user, ((DateTime)log.send_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)log.book_date).ToString("yyyy-MM-dd HH:mm")) });
        }

        #endregion

        #region 请假

        //行政部发送约谈信息
        public JsonResult LeaveDayExceedPush(string sysNo, string bookTime)
        {
            try {
                var sv = new ALSv(sysNo);
                var bill = sv.GetALBill();
                sv.AHPushMsg(sysNo, bill.applier_num, bill.applier_name, bill.dep_long_name, userInfo.name, bookTime, "请假时间超过公司规定天数");
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog("请假条申请", sysNo + ">发送行政面谈通知" + ";bookTime:" + bookTime);

            return Json(new SimpleResultModel() { suc = true });
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

        #region 离职

        public ActionResult ChargerUpdateLeaveDay()
        {
            return View();
        }

        public JsonResult GetJQApply(string searchContent)
        {
            var jq = new JQSv().GetJQApply(searchContent);
            if (jq == null) return Json(new SimpleResultModel() { suc = false, msg = "查询不到离职申请单" });            

            return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(jq) });
        }

        public JsonResult UpdateLeaveDay(string sysNo,string newDay,string notifiers)
        {
            DateTime newDayDt;
            if (!DateTime.TryParse(newDay, out newDayDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "离职日期不正确" });
            }
            if (string.IsNullOrEmpty(notifiers)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请先选择需通知的文员" });
            }
            try {
                new JQSv(sysNo).UpdateLeaveDay(newDayDt, notifiers,userInfo.cardNo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false,msg = ex.Message });
            }
            WriteEventLog("修改离职日期", sysNo + ":" + newDay + ";" + notifiers);
            return Json(new SimpleResultModel() { suc = true });
        }

        public ActionResult CancelJQApply()
        {
            return View();
        }

        public JsonResult BeginCancelJqApply(string sysNo)
        {
            try {
                new JQSv(sysNo).CancelApply(userInfo.cardNo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog("作废离职单", sysNo);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult UpdateJQDepName(string sysNo, string newDepName)
        {
            try {
                new JQSv(sysNo).UpdateDepName(newDepName);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            WriteEventLog("离职流程", "修改部门：" + sysNo + ":" + newDepName);
            return Json(new SimpleResultModel(true));
        }

        //行政部发送约谈信息
        public JsonResult AHPushJQMsg(string sysNo, string bookTime)
        {
            try {
                var sv = new JQSv(sysNo);
                var bill = sv.GetBill() as ei_jqApply;
                sv.AHPushMsg(sysNo, bill.card_number, bill.name, bill.dep_name, userInfo.name, bookTime, "月薪员工离职面谈");
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog("行政离职面谈", sysNo + ">月薪员工离职面谈" + ";bookTime:" + bookTime);

            return Json(new SimpleResultModel() { suc = true });
        }        

        #endregion

        #region 调动

        public JsonResult GetSJEntrys(int sjId)            
        {
            var entrys = new SJSv().GetEntrys(sjId);
            entrys.ForEach(e => { e.ei_sjApply = null; e.sj_id = 0; });
            return Json(entrys, JsonRequestBehavior.AllowGet);
        }

        public JsonResult UpdateSJAgreeField(string entryIds)
        {
            try {
                int[] ids = JsonConvert.DeserializeObject<int[]>(entryIds);
                new SJSv().UpdateIsAgree(ids, userInfo.name);
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }
        #endregion

        #region IE立项结项

        public ActionResult IEBusAndAuditor()
        {
            return View();
        }

        #endregion        

        #region 漏刷卡

        [SessionTimeOutFilter]
        public ActionResult CheckMyKQRecord()
        {
            return View();
        }

        public JsonResult GetKQRecord()
        {
            return Json(new CRSv().GetKQRecored(userInfoDetail.salaryNo),JsonRequestBehavior.AllowGet);
        }

        #endregion

    }
}
