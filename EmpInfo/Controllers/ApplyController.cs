using EmpInfo.Filter;
using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace EmpInfo.Controllers
{
    /// <summary>
    /// 新架构，分层实现，增加代码可维护性和扩展性。2019-04-08启用
    /// </summary>
    public class ApplyController : BaseController
    {        

        // 主界面
        [SessionTimeOutFilter]
        public ActionResult ApplyIndex(string billType)
        {
            try {
                SetBillByType(billType);
            }
            catch {
                ViewBag.tip = "流程不存在";
                return View("Error");
            }
            
            ViewData["billName"] = bill.BillTypeName;
            ViewData["menuItems"] = bill.GetApplyMenuItems(userInfo);
            ViewData["navigatorLinks"] = bill.GetApplyNavigatorLinks();
            //WriteEventLog(billType, "打开主界面");

            return View();
        }

        /// <summary>
        /// 开始申请
        /// </summary>
        /// <param name="billType"></param>
        /// <returns></returns>
        [SessionTimeOutFilter]
        public ActionResult BeginApply(string billType)
        {
            if (db.vw_push_users.Where(v => v.card_number == userInfo.cardNo).Count() < 1) {
                ViewBag.tip = "必须绑定【信利e家】微信公众号之后才能进行申请";
                return View("Error");
            }

            try {
                SetBillByType(billType);
                ViewData["infoBeforeApply"] = bill.GetInfoBeforeApply(userInfo, userInfoDetail);
            }
            catch (Exception ex) {
                ViewBag.tip = ex.Message;
                return View("Error");
            }
            return View(bill.CreateViewName());
        }

        /// <summary>
        /// 保存申请
        /// </summary>
        /// <param name="fc"></param>
        /// <returns></returns>
        [SessionTimeOutJsonFilter]
        public JsonResult SaveApply(FormCollection fc)
        {
            string sysNo = fc.Get("sys_no");
            if (string.IsNullOrEmpty(sysNo)) {
                return Json(new SimpleResultModel() { suc = false, msg = "找不到合适的处理流程，请联系管理员" });
            }
            SetBillByType(sysNo);

            try {
                bill.SaveApply(fc, userInfo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog(bill.BillTypeName, "提交申请单：" + sysNo);
            return Json(new SimpleResultModel() { suc = true, msg = "流程提交成功,申请单号：" + sysNo });
        }

        /// <summary>
        /// 获取审批队列，不是每个流程都有
        /// </summary>
        /// <param name="fc"></param>
        /// <returns></returns>
        [SessionTimeOutJsonFilter]
        public JsonResult GetFlowQueue(FormCollection fc)
        {
            string sysNo = fc.Get("sys_no");
            if (string.IsNullOrEmpty(sysNo)) {
                return Json(new SimpleResultModel() { suc = false, msg = "找不到合适的处理流程，请联系管理员" });
            }
            SetBillByType(sysNo);
            var iae = bill as IApplyEntryQueue;
            if (iae == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "此流程不支持审核人预览" });
            }
            try {
                var queue = iae.GetApplyEntryQueue(fc, userInfo);
                return Json(new { suc = true, list = queue });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }

        //我申请的
        [SessionTimeOutFilter]
        public ActionResult GetMyApplyList(string billType)
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            List<FlowMyAppliesModel> list = flow.GetMyApplyList(userInfo.cardNo, lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), "", new ArrayOfString() { billType }, "", "", 10, 200).ToList();
            ViewData["list"] = list;
            ViewData["billType"] = billType;

            SetBillByType(billType);
            ViewData["billTypeName"] = bill.BillTypeName;
            ViewData["navigatorLinks"] = bill.GetApplyNavigatorLinks();

            //WriteEventLog(bill.BillTypeName, "打开我申请的界面");
            return View(bill.GetMyAppliesViewName());
        }

         //撤销申请
        [SessionTimeOutJsonFilter]
        public JsonResult AbortApply(string sysNo, string reason = "")
        {
            SetBillByType(sysNo);
            var result = bill.AbortApply(userInfo, sysNo, reason);

            WriteEventLog(bill.BillTypeName, "撤销、中止流程：" + sysNo + ";result:" + result.msg);

            return Json(result);
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            SetBillBySysNo(sysNo);
            var m = bill.GetBill();
            ViewData["am"] = m;
            ViewData["auditStatus"] = bill.GetAuditStatus(sysNo);

            WriteEventLog(bill.BillTypeName, "查看申请单：" + sysNo); 

            return View(bill.CheckViewName());

        }

        //查看流转记录
        [SessionTimeOutFilter]
        public ActionResult CheckFlowRecord(string sysNo)
        {
            SetBillByType(sysNo);
            ViewData["record"] = bill.CheckFlowRecord(sysNo);

            return View();
        }

        //我的待办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditingList(string billType)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            List<FlowAuditListModel> list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 0 }, new ArrayOfString() { billType }, 400).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));
            SetBillByType(billType);
            ViewData["list"] = list;
            ViewData["billType"] = billType;
            ViewData["billTypeName"] = bill.BillTypeName;
            ViewData["navigatorLinks"] = bill.GetApplyNavigatorLinks();
            //WriteEventLog(billType, "打开我的待办界面");

            return View();
        }

        //我的已办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditedList(string billType)
        {
            return RedirectToAction("CheckMyAuditedList", new { billType = billType, fromDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd"), toDate = DateTime.Now.ToString("yyyy-MM-dd") });
        }

        //查询我的已办
        [SessionTimeOutJsonFilter]
        public ActionResult CheckMyAuditedList(string billType, string fromDate, string toDate, string sysNo = "", string cardNo = "")
        {
            DateTime fromDateDt, toDateDt;
            if (!DateTime.TryParse(fromDate, out fromDateDt)) {
                fromDateDt = DateTime.Now.AddMonths(-1);
            }
            if (!DateTime.TryParse(toDate, out toDateDt)) {
                toDateDt = DateTime.Now;
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, sysNo, cardNo, "", "", fromDateDt.ToShortDateString(), toDateDt.ToShortDateString(), new ArrayOfInt() { 1, -1 }, new ArrayOfInt() { 10 }, new ArrayOfString() { billType }, 300).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));

            SetBillByType(billType);

            ViewData["list"] = list;
            ViewData["billType"] = billType;
            ViewData["fromDate"] = fromDate;
            ViewData["toDate"] = toDate;
            ViewData["sysNo"] = sysNo;
            ViewData["cardNo"] = cardNo;
            ViewData["billTypeName"] = bill.BillTypeName;
            ViewData["navigatorLinks"] = bill.GetApplyNavigatorLinks();

            //WriteEventLog(billType, "打开我的已办界面");
            return View("GetMyAuditedList");
        }

        //开始审核申请
        [SessionTimeOutFilter]
        public ActionResult BeginAuditApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.ApplyHasAudit(sysNo, (int)step, userInfo.cardNo);
            if (!result.suc) {
                ViewBag.tip = result.msg;
                return View("Error");
            }

            SetBillByType(sysNo);

            BeginAuditModel bam = new BeginAuditModel()
            {
                sysNum = sysNo,
                step = (int)step,
                stepName = result.stepName,
                auditorName = userInfo.name,
                auditorNumber = userInfo.cardNo,
                isPass = result.isPass,
                opinion = result.opinion,
                billType = bill.BillType,
                billTypeName = bill.BillTypeName
            };

            var ib = bill as IBeginAuditOtherInfo;
            if (ib != null) {
                bam.otherInfo = ib.GetBeginAuditOtherInfo(sysNo, (int)step);
            }

            ViewData["bam"] = bam;

            WriteEventLog(bill.BillTypeName, "进入审批" + sysNo + ";step:" + step);
            return View(bill.AuditViewName());
        }

        //处理审核申请
        [SessionTimeOutJsonFilter]
        public JsonResult HandleApply(FormCollection fc)
        {
            string sysNo = fc.Get("sysNo");
            SetBillBySysNo(sysNo);
            try {
                var result = bill.HandleApply(fc, userInfo);

                WriteEventLog(bill.BillTypeName, "处理申请单：" + sysNo + ";msg:" + result.msg);
                return Json(result);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = "处理失败：" + ex.Message });
            }

        }

        #region 兼容旧Apply控制器的方法,预计可以在2019-8-1开始取消兼容（3个月兼容期）

        [SessionTimeOutFilter]
        public ActionResult CheckALApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditALApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckSAApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditSAApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckUCApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditUCApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckCRApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditCRApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckSVApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditSVApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckEPApply(string sysNo, string param)
        {
            return RedirectToAction("CheckApply", new { sysNo = sysNo, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditEPApply(string sysNo, int? step, string param)
        {
            return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = step, param = param });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckAndAuditDPApply(string sysNo)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();

            var currentStep = flow.GetCurrentStep(sysNo);            
            if (currentStep.step > 0) {
                if (currentStep.auditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Contains(userInfo.cardNo)) {
                    return RedirectToAction("BeginAuditApply", new { sysNo = sysNo, step = currentStep.step });
                }
            }
            return RedirectToAction("CheckApply", new { sysNo = sysNo });
        }

        public ActionResult CheckDPApplyInDormSys(string sysNo)
        {
            SetBillBySysNo(sysNo);

            var m = bill.GetBill();
            ViewData["am"] = m;
            ViewData["fromDormSys"] = true;

            return View(bill.CheckViewName());
        }

        #endregion

    }
}