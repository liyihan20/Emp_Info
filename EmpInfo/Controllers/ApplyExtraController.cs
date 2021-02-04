using EmpInfo.Filter;
using EmpInfo.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.Services;
using Newtonsoft.Json;
using EmpInfo.Util;

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

        public JsonResult GetITPushLog(string sysNo)
        {
            var log = new ALSv().GetAHMsgPushLog(sysNo);
            if (log == null) {
                return Json(new SimpleResultModel() { suc = false });
            }

            return Json(new SimpleResultModel() { suc = true, msg = string.Format("【发送记录】发送人：{0}；发送时间：{1}；", log.send_user, ((DateTime)log.send_date).ToString("yyyy-MM-dd HH:mm")) });
        }

        //申请人催办
        public JsonResult UrgeAuditor(string sysNo)
        {
            //只能在白天催办
            var currentHour = DateTime.Now.Hour;
            if (currentHour < 8 || currentHour > 20) {
                return Json(new SimpleResultModel(false, "只能在白天8时到20时催办"));
            }

            var today = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd"));
            var a = (from ap in db.flow_apply
                     from ad in ap.flow_applyEntry
                     where ap.sys_no == sysNo
                     select new
                     {
                         ap,
                         ad
                     }).ToList();
            if (a.Count() < 1) {
                return Json(new SimpleResultModel(false, "流水号不存在"));
            }

            var apply = a.First().ap;
            if (apply.success != null) {
                return Json(new SimpleResultModel(false, "此流程已完结"));
            }
            if (apply.start_date > today) {
                return Json(new SimpleResultModel(false, "当前处理人超过1天未处理的才可以催办"));
            }
            var lastAuditedEntry = a.Where(x => x.ad.pass == true).OrderByDescending(x => x.ad.step).FirstOrDefault();
            if (lastAuditedEntry != null && lastAuditedEntry.ad.audit_time > today) {
                return Json(new SimpleResultModel(false, "当前处理人超过1天未处理的才可以催办"));
            }
            List<string> currentAuditors = new List<string>();
            int step=0;
            string stepName = "";
            foreach (var entry in a.Where(x => x.ad.pass == null).Select(x => x.ad).ToList()) {
                currentAuditors.AddRange(entry.auditors.Split(new char[] { ';' }).ToList());
                step = (int)entry.step;
                stepName = entry.step_name;
            }
            if (step == 0) {
                return Json(new SimpleResultModel(false, "当前处理人不存在"));
            }
            string currentAuditorsStr = string.Join(";", currentAuditors);
            var urgeRecord = db.ei_urgeRecords.Where(u => u.sys_no == sysNo && u.auditor_number == currentAuditorsStr).OrderByDescending(u => u.id).FirstOrDefault();
            if (urgeRecord != null && urgeRecord.urge_time > today) {
                return Json(new SimpleResultModel(false, "当前处理人今天已经催办过，至少需要明天才能继续催办"));
            }
            try {
                //验证结束，开始发送催办企业微信推送
                string flowName = apply.flow_template.name;
                new ShareSv().SendQywxMsgToAuditors(
                            flowName + "（催办）",
                            sysNo,
                            step,
                            stepName,
                            userInfo.name,
                            (DateTime)apply.start_date,
                            string.Format("{0}；{1}", apply.form_title, apply.form_sub_title),
                            currentAuditors
                            );

                db.ei_urgeRecords.Add(new ei_urgeRecords()
                {
                    applier_name = userInfo.name,
                    auditor_number = userInfo.cardNo,
                    sys_no = sysNo,
                    urge_time = DateTime.Now
                });
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "操作失败:" + ex.Message));
            }
            return Json(new SimpleResultModel(true));
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

        //public ActionResult ChargerUpdateJQLeaveDay()
        //{
        //    return View();
        //}

        //public JsonResult GetJQApply(string searchContent)
        //{
        //    var jq = new JQSv().GetJQApply(searchContent);
        //    if (jq == null) return Json(new SimpleResultModel() { suc = false, msg = "查询不到离职申请单" });

        //    return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(jq) });
        //}

        //public JsonResult UpdateJQLeaveDay(string sysNo, string newDay, string notifiers)
        //{
        //    DateTime newDayDt;
        //    if (!DateTime.TryParse(newDay, out newDayDt)) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "离职日期不正确" });
        //    }
        //    if (string.IsNullOrEmpty(notifiers)) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "请先选择需通知的文员" });
        //    }
        //    try {
        //        new JQSv(sysNo).UpdateLeaveDay(newDayDt, notifiers, userInfo.cardNo);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
        //    }
        //    WriteEventLog("修改离职日期", sysNo + ":" + newDay + ";" + notifiers);
        //    return Json(new SimpleResultModel() { suc = true });
        //}

        //public ActionResult CancelJQApply()
        //{
        //    return View();
        //}

        //public JsonResult BeginCancelJqApply(string sysNo)
        //{
        //    try {
        //        new JQSv(sysNo).CancelApply(userInfo.cardNo);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
        //    }

        //    WriteEventLog("作废离职单", sysNo);
        //    return Json(new SimpleResultModel() { suc = true });
        //}

        //public JsonResult UpdateJQDepName(string sysNo, string newDepName)
        //{
        //    try {
        //        new JQSv(sysNo).UpdateDepName(newDepName);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel(ex));
        //    }
        //    WriteEventLog("离职流程", "修改部门：" + sysNo + ":" + newDepName);
        //    return Json(new SimpleResultModel(true));
        //}

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

        #region 新计件辞职

        //public ActionResult ChargerUpdateMQLeaveDate()
        //{
        //    return View();
        //}

        //public JsonResult GetMQApply(string searchContent)
        //{
        //    var mq = new MQSv().GetMQApply(searchContent);
        //    if (mq == null) return Json(new SimpleResultModel() { suc = false, msg = "查询不到计件辞职申请单" });

        //    return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(mq) });
        //}

        //public JsonResult UpdateMQLeaveDay(string sysNo, string newDay, string notifiers)
        //{
        //    DateTime newDayDt;
        //    if (!DateTime.TryParse(newDay, out newDayDt)) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "离职日期不正确" });
        //    }
        //    if (string.IsNullOrEmpty(notifiers)) {
        //        return Json(new SimpleResultModel() { suc = false, msg = "请先选择需通知的文员" });
        //    }
        //    try {
        //        new MQSv(sysNo).UpdateLeaveDay(newDayDt, notifiers, userInfo.cardNo,userInfo.name);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
        //    }
        //    WriteEventLog("修改离职日期", sysNo + ":" + newDay + ";" + notifiers);
        //    return Json(new SimpleResultModel() { suc = true });
        //}

        //public ActionResult CancelMQApply()
        //{
        //    return View();
        //}

        //public JsonResult BeginCancelMqApply(string sysNo)
        //{
        //    try {
        //        new MQSv(sysNo).CancelApply(userInfo.cardNo);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
        //    }

        //    WriteEventLog("作废离职单", sysNo);
        //    return Json(new SimpleResultModel() { suc = true });
        //}

        //public JsonResult UpdateMQDepName(string sysNo, string newDepName)
        //{
        //    try {
        //        new MQSv(sysNo).UpdateDepName(newDepName);
        //    }
        //    catch (Exception ex) {
        //        return Json(new SimpleResultModel(ex));
        //    }
        //    WriteEventLog("离职流程", "修改部门：" + sysNo + ":" + newDepName);
        //    return Json(new SimpleResultModel(true));
        //}

        public JsonResult NeedHRTalk(string sysNo)
        {
            try {
                new MQSv(sysNo).NeedHRTalk();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        #endregion

        #region 整合离职流程，新旧整合在一起

        public ActionResult ChargerUpdateJMLeaveDay()
        {
            return View();
        }

        public JsonResult GetJMApply(string searchContent)
        {
            var jq = new JQSv().GetJQApply(searchContent);
            var mq = new MQSv().GetMQApply(searchContent);
            if (jq == null && mq == null) return Json(new SimpleResultModel() { suc = false, msg = "查询不到离职申请单" });

            if (mq == null) {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(jq) });
            }
            if (jq == null) {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(mq) });
            }
            if (mq.apply_time > jq.apply_time) {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(mq) });
            }
            else {
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(jq) });
            }
        }

        public JsonResult UpdateJMLeaveDay(string sysNo, string newDay, string notifiers)
        {
            DateTime newDayDt;
            if (!DateTime.TryParse(newDay, out newDayDt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "离职日期不正确" });
            }
            if (string.IsNullOrEmpty(notifiers)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请先选择需通知的文员" });
            }
            try {
                if (sysNo.StartsWith("JQ")) {
                    new JQSv(sysNo).UpdateLeaveDay(newDayDt, notifiers, userInfo.cardNo);
                }
                if (sysNo.StartsWith("MQ")) {
                    new MQSv(sysNo).UpdateLeaveDay(newDayDt, notifiers, userInfo.cardNo, userInfo.name);
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            WriteEventLog("修改离职日期", sysNo + ":" + newDay + ";" + notifiers);
            return Json(new SimpleResultModel() { suc = true });
        }

        public ActionResult CancelJMApply()
        {
            return View();
        }

        public JsonResult BeginCancelJMApply(string sysNo)
        {
            try {
                if (sysNo.StartsWith("JQ")) {
                    new JQSv(sysNo).CancelApply(userInfo.cardNo);
                }
                if (sysNo.StartsWith("MQ")) {
                    new MQSv(sysNo).CancelApply(userInfo.cardNo);
                }
                
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }

            WriteEventLog("作废离职单", sysNo);
            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult UpdateJMDepName(string sysNo, string newDepName)
        {
            try {
                if (sysNo.StartsWith("JQ")) {
                    new JQSv(sysNo).UpdateDepName(newDepName);
                }
                if (sysNo.StartsWith("MQ")) {
                    new MQSv(sysNo).UpdateDepName(newDepName);
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            WriteEventLog("离职流程", "修改部门：" + sysNo + ":" + newDepName);
            return Json(new SimpleResultModel(true));
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
            try {
                return Json(new HRDBSv().GetKQRecored(userInfoDetail.salaryNo), JsonRequestBehavior.AllowGet);
            }
            catch {
                return Json(new GetKQRecord_Result() { KDATE = DateTime.Now, DW = "考勤系统故障，打卡记录获取失败" });
            }
        }

        #endregion

        #region 电脑报修

        [SessionTimeOutFilter]
        public ActionResult ManageITItems()
        {
            return View();
        }

        public JsonResult GetITITems()
        {
            try {
                var list = new ITSv().GetITItems();
                return Json(list, JsonRequestBehavior.AllowGet);
            }
            catch {
                return Json(new List<ei_itItems>(), JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult SaveItItem(string jsonStr)
        {
            ei_itItems item = JsonConvert.DeserializeObject<ei_itItems>(jsonStr);
            try {
                new ITSv().SaveItItem(item);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveItItem(int itemId)
        {
            try {
                new ITSv().RemoveItItem(itemId);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }
                
        //打印维修二维码、取回电脑
        public ActionResult PrintITCode()
        {
            return View();
        }

        public JsonResult GetITBillForPrint(string sysNo, string applierNumber)
        {
            try {
                var sv = new ITSv(sysNo);
                var bill = sv.GetBill() as ei_itApply;
                if (!applierNumber.Equals(bill.applier_num)) {
                    return Json(new SimpleResultModel(false, "申请流水号与厂牌号不匹配，请确认"));
                }
                if (bill.accept_man_name == null) {
                    return Json(new SimpleResultModel(false, "此维修单还未接单，不能打印"));
                }
                if (!"现场维修".Equals(bill.repair_way)) {
                    return Json(new SimpleResultModel(false, "不是现场维修的不能打印"));
                }
                if (bill.repair_time != null) {
                    return Json(new SimpleResultModel(false, "已维修完成的不能打印"));
                }

                sv.UpdatePrintTime();
                return Json(new SimpleResultModel(true, "获取信息成功", JsonConvert.SerializeObject(
                    new
                    {
                        bill.sys_no,
                        bill.applier_num,
                        bill.applier_name,
                        apply_time = ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        bill.dep_name,
                        bill.accept_man_name,
                        print_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    }
            )));
            }
            catch {
                return Json(new SimpleResultModel(false, "申请流水号不存在，请确认"));
            }
        }

        public JsonResult GetITCodeInfo(string codeContent)
        {
            if (!codeContent.StartsWith("IT:") && !codeContent.StartsWith("IT：")) {
                return Json(new SimpleResultModel(false, "二维码格式不正确"));
            }
            var sysNo = codeContent.Substring(3);
            ITSv sv;
            try {
                sv = new ITSv(sysNo);
            }
            catch {
                return Json(new SimpleResultModel(false, "此流水号不存在"));
            }

            var bill = sv.GetBill() as ei_itApply;
            return Json(new SimpleResultModel(true, "信息获取成功", JsonConvert.SerializeObject(new
            {
                applier = bill.applier_name + "(" + bill.applier_num + ")",
                applierDep = bill.dep_name
            })));
        }

        public JsonResult ITFetchComputer(string codeContent, string cardNumber, string name, string phone)
        {
            if (!codeContent.StartsWith("IT:") && !codeContent.StartsWith("IT：")) {
                return Json(new SimpleResultModel(false, "二维码格式不正确"));
            }
            var sysNo = codeContent.Substring(3);
            ITSv sv;
            try {
                sv = new ITSv(sysNo);
            }
            catch {
                return Json(new SimpleResultModel(false, "此流水号不存在"));
            }

            var bill = sv.GetBill() as ei_itApply;
            if (bill.repair_time == null) {
                return Json(new SimpleResultModel(false, "此维修单还未处理完成，不能取回，请联系IT部处理人"));
            }
            if (bill.fetch_time != null) {
                return Json(new SimpleResultModel(false, "此电脑已被取回，请不要重复操作"));
            }
            HRDBSv dbSv = new HRDBSv();
            var emp = dbSv.GetHREmpInfo(cardNumber);
            if (emp == null) {
                return Json(new SimpleResultModel(false, "取回人厂牌在人事系统中不存在，请确认"));
            }
            if (!emp.emp_name.Equals(name)) {
                return Json(new SimpleResultModel(false, "取回人厂牌和姓名不匹配，请确认"));
            }
            try {
                sv.FetchComputer(cardNumber, name, phone);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "申请单更新失败，请联系管理员。错误信息：" + ex.Message));
            }

            return Json(new SimpleResultModel(true));
        }

        //健林调高优先级到6
        public ActionResult TurnUPITPriority()
        {
            return View();
        }

        public JsonResult UpdateITPriority(string sysNo)
        {
            try {
                new ITSv().UpdatePriority(sysNo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true,"优先级已提升"));
        }

        public JsonResult SearchITApply(string searchContent)
        {
            var bill = new ITSv().SearchItApply(searchContent);
            if (bill == null) {
                return Json(new SimpleResultModel(false,"查询不到符合条件的申请记录"));
            }
            return Json(new SimpleResultModel(true,"", JsonConvert.SerializeObject(new { bill.sys_no, bill.applier_name, bill.apply_time, bill.dep_name, bill.faulty_items })));
        }

        #endregion

        #region 后勤工程费用支出

        public ActionResult DENames()
        {
            ViewData["subjectsJson"] = JsonConvert.SerializeObject(db.ei_DESubjects.ToList());
            return View();
        }

        public JsonResult GetDENames()
        {
            var result = from s in db.ei_DESubjects
                         join n in db.ei_DENames on s.name equals n.subject_name
                         select new
                         {
                             catalog_name = s.catalog_name,
                             subject_name = n.subject_name,
                             name = n.name,
                             id = n.id
                         };
            return Json(result,JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveDENames(string jsonStr)
        {
            try {
                ei_DENames dname = JsonConvert.DeserializeObject<ei_DENames>(jsonStr);
                if (dname.id == 0) {
                    db.ei_DENames.Add(dname);
                }
                else {
                    var existed = db.ei_DENames.Single(d => d.id == dname.id);
                    MyUtils.CopyPropertyValue(dname, existed);
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveDEName(int id)
        {
            try {
                db.ei_DENames.Remove(db.ei_DENames.Single(d => d.id == id));
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }


        #endregion

        #region 换货申请

        public JsonResult GetOrderModual(string company, string orderNo)
        {
            try {
                var result = new HHSv().GetOrderModuel(company, orderNo);
                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult UpdateHHEntry(FormCollection fc)
        {
            string whoIsSaving = fc.Get("whoIsSaving");
            ei_hhApplyEntry entry;
            try {
                entry = JsonConvert.DeserializeObject<ei_hhApplyEntry>(fc.Get("obj"));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false,"json转换失败："+ ex.Message));
            }
            var svr = new HHSv();
            try {
                switch (whoIsSaving) {
                    case "品质经理":
                        svr.SaveEntryByQuality(entry.id, entry.real_return_qty, entry.is_on_line);
                        break;
                    case "生产主管":
                        svr.SaveEntryByCharger(entry.id, entry.real_fill_qty);
                        break;
                    case "物流":
                        svr.SaveEntryByLogistics(entry.id, entry.send_qty, entry.sender_name);
                        break;
                    default:
                        return Json(new SimpleResultModel(false, "审核步骤不存在"));
                }
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(false, "保存失败：" + ex.Message));
            }

            return Json(new SimpleResultModel(true));
        }

        public JsonResult SaveHHReturnDetail(string obj)
        {
            try {
                ei_hhReturnDetail detail = JsonConvert.DeserializeObject<ei_hhReturnDetail>(obj);
                var result = new HHSv().SaveReturnDetail(detail);
                return Json(new SimpleResultModel(true, "保存成功", JsonConvert.SerializeObject(result, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, NullValueHandling = NullValueHandling.Ignore })));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult RemoveHHReturnDetail(int id)
        {
            try {
                new HHSv().RemoveReturnDetail(id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        #endregion

        #region 物流车辆预约

        public JsonResult CancelTIApply(string sysNo)
        {
            try {
                new TISv(sysNo).CancelTIApply(userInfo.cardNo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        #endregion

        #region 宿舍维修

        public JsonResult SearchDPRepairItem(string itemName)
        {
            var result = new DPSv().SearchRepairItems(itemName, userInfo.name);
            if (result.Count() < 1) {
                return Json(new SimpleResultModel(false, "查询不到维修配件的信息"));
            }
            return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result.OrderByDescending(r=>r.inventory).ToList())));
        }

        public JsonResult SaveDPRepairItem(string obj)
        {
            try {
                var im = JsonConvert.DeserializeObject<DormRepairItemModel>(obj);
                var result = new DPSv().SaveRepairItem(im, userInfo.name);

                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(result)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult UpdateDPRepairItemQty(int id, int qty)
        {
            try {
                new DPSv().UpdateRepairItemQty(id, qty);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true, "保存成功"));
        }

        public JsonResult RemoveDPRepairItem(int id)
        {
            try {
                new DPSv().RemoveRepairItem(id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true, "删除成功"));
        }

        public JsonResult UpdateDPRepairItemPublicFee(int id)
        {
            try {
                var result = new DPSv().UpdateRepairItemPublicFee(id);
                return Json(new SimpleResultModel(true, "", result ? "1" : "0"));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        #endregion

        #region 项目单流程

        public JsonResult AddXASpplier(string sysNo,string supplierName)
        {
            try {
                if (db.ei_xaApplySupplier.Where(s => s.sys_no == sysNo && s.supplier_name == supplierName).Count() > 0) {
                    throw new Exception("此供应商已存在，不能再新增");
                }
                var supplier = db.ei_xaApplySupplier.Add(new ei_xaApplySupplier()
                {
                    sys_no = sysNo,
                    supplier_name = supplierName
                });
                db.SaveChanges();
                return Json(new SimpleResultModel(true, "", JsonConvert.SerializeObject(supplier)));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult UpdateXASupplierName(int id, string supplierName)
        {
            try {
                var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
                if (supplier != null) {
                    supplier.supplier_name = supplierName;
                    db.SaveChanges();
                }
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult UpdateXASupplierPrice(int id, decimal price)
        {
            try {
                var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
                if (supplier != null) {
                    if (price == 0) {
                        supplier.price = null;
                    }
                    else {
                        supplier.price = price;
                    }
                    db.SaveChanges();
                }
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public JsonResult RemoveXASupplier(int id)
        {
            try {
                var supplier = db.ei_xaApplySupplier.SingleOrDefault(s => s.id == id);
                if (supplier != null) {
                    db.ei_xaApplySupplier.Remove(supplier);
                    db.SaveChanges();
                }
                return Json(new SimpleResultModel(true));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
        }

        public ActionResult XAAuditorSetting()
        {
            //采购员
            var buyerList = (from a in db.flow_auditorRelation
                               join u in db.ei_users on a.relate_value equals u.card_number
                               where a.bill_type == "XA" && a.relate_name == "采购部审批"
                               orderby a.relate_text
                               select new XAAuditorsModel()
                               {
                                   company = a.relate_text,
                                   userName = u.name
                               }).ToList();
            ViewData["buyers"] = buyerList;
            return View();
        }

        public JsonResult GetXAAuditors()
        {
            //总经理审批
            var auditorList = (from a in db.flow_auditorRelation
                               join u in db.ei_users on a.relate_value equals u.card_number
                               where a.bill_type == "XA" && a.relate_name == "部门总经理"
                               orderby a.relate_text
                               select new XAAuditorsModel()
                               {
                                   id = a.id,
                                   position = "总经理",
                                   company = a.relate_text,
                                   userName = u.name,
                                   userNumber = a.relate_value
                               }).ToList();
            auditorList.ForEach(a =>
            {
                a.deptName = a.company.Split(new char[] { '_' })[1];
                a.company = a.company.Split(new char[] { '_' })[0];
            });
            
            //CEO抄送
            var ceoList = db.flow_notifyUsers
                .Where(f => f.bill_type == "XA" && f.cond2 != "")
                .OrderBy(f => f.cond1)
                .ThenBy(f => f.cond2)
                .Select(f => new XAAuditorsModel()
                {
                    id = f.id,
                    position = "CEO",
                    company = f.cond1,
                    deptName = f.cond2,
                    userName = f.name,
                    userNumber = f.card_number
                }).ToList();

            return Json(auditorList.Union(ceoList),JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveXAAuditor(string obj)
        {
            try {
                XAAuditorsModel m = JsonConvert.DeserializeObject<XAAuditorsModel>(obj);

                if (m.position.Equals("总经理")) {
                    string relateText = m.company + "_" + m.deptName;
                    string relateValue = GetUserCardByNameAndCardNum(m.userName);
                    if (db.flow_auditorRelation.Where(f => f.bill_type == "XA" && f.relate_name == "部门总经理" && f.relate_text == relateText && f.relate_value == relateValue && f.id != m.id).Count() > 0) {
                        return Json(new SimpleResultModel(false, "信息已存在，不能重复增加"));
                    }
                    if (m.id == 0) {
                        var rel = new flow_auditorRelation();
                        rel.bill_type = "XA";
                        rel.relate_name = "部门总经理";
                        rel.relate_text = relateText;
                        rel.relate_value = relateValue;

                        db.flow_auditorRelation.Add(rel);
                    }
                    else {
                        var rel = db.flow_auditorRelation.Single(f => f.id == m.id);

                        rel.relate_text = relateText;
                        rel.relate_value = relateValue;
                    }
                }
                else if (m.position.Equals("CEO")) {
                    string userNumber = GetUserCardByNameAndCardNum(m.userName);
                    string userName = GetUserNameByNameAndCardNum(m.userName);
                    if (db.flow_notifyUsers.Where(f => f.bill_type == "XA" && f.cond1 == m.company && f.cond2 == m.deptName && f.card_number == m.userNumber && f.id != m.id).Count() > 0) {
                        return Json(new SimpleResultModel(false, "信息已存在，不能重复增加"));
                    }
                    if (m.id == 0) {
                        var nty = new flow_notifyUsers();
                        nty.bill_type = "XA";
                        nty.card_number = userNumber;
                        nty.name = userName;
                        nty.cond1 = m.company;
                        nty.cond2 = m.deptName;

                        db.flow_notifyUsers.Add(nty);
                    }
                    else {
                        var nty = db.flow_notifyUsers.Single(f => f.id == m.id);
                        nty.card_number = userNumber;
                        nty.name = userName;
                        nty.cond1 = m.company;
                        nty.cond2 = m.deptName;
                    }
                }

                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }

            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveXAAuditor(string position, int id)
        {
            try {
                if (position.Equals("总经理")) {
                    db.flow_auditorRelation.Remove(db.flow_auditorRelation.Single(f => f.bill_type == "XA" && f.id == id));
                }
                else if (position.Equals("CEO")) {
                    db.flow_notifyUsers.Remove(db.flow_notifyUsers.Single(f => f.bill_type == "XA" && f.id == id));
                }
                db.SaveChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }

            return Json(new SimpleResultModel(true));
        }

        #endregion

        #region 设备保养流程

        //保养文件
        public ActionResult CheckMTFiles()
        {
            return View();
        }

        public JsonResult GetMTFiles()
        {
            return Json(new MTSv().CheckFiles(),JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMTFile(int id)
        {
            ei_mtFile file;
            if (id == 0) {
                file = new ei_mtFile();
            }
            else {
                try {
                    file = new MTSv().GetFile(id);
                }
                catch (Exception ex) {
                    return Json(new SimpleResultModel(ex));
                }
            }
            return Json(new SimpleResultModel(true,"",JsonConvert.SerializeObject(file)));
        }

        public JsonResult SaveMTFile(FormCollection fc)
        {
            ei_mtFile file = JsonConvert.DeserializeObject<ei_mtFile>(fc.Get("obj"));
            try {
                new MTSv().SaveFile(file.id, file.file_no, file.maintenance_content, file.maintenance_steps, userInfo.name);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveMTFile(int id)
        {
            try {
                new MTSv().RemoveFile(id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }


        //设备科室
        public ActionResult CheckMTClasses()
        {
            return View();
        }

        public JsonResult GetMTClasses()
        {
            return Json(new MTSv().GetClasses(),JsonRequestBehavior.AllowGet);
        }

        public JsonResult SaveMTClass(string obj)
        {
            ei_mtClass cla = JsonConvert.DeserializeObject<ei_mtClass>(obj);
            try {
                new MTSv().SaveClass(cla);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveMTClass(int id)
        {
            try {
                new MTSv().RemoveClass(id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }


        //设备资料
        public ActionResult CheckMTEqInfo()
        {
            return View();
        }

        public JsonResult CheckMyMTEqInfo()
        {
            return Json(new MTSv().GetEqInfoList(userInfo.cardNo),JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetMTFileDetail(string fileNo)
        {
            return Json(new MTSv().GetFileDetail(fileNo));
        }

        public ActionResult UpdateMTEqInfo(int id)
        {
            var mt = new MTSv();
            MTUpdateEqInfoModel m = new MTUpdateEqInfoModel();
            if (id > 0) {
                m.info = mt.GetEqInfoDetail(id);
            }
            m.classesList = mt.GetClassesForSelect();
            m.depList = mt.GetEpDepList();
            m.fileNoList = mt.GetFileNoList();
            m.myClassId = mt.GetMyClassId(userInfo.cardNo);

            ViewData["m"] = m;

            return View();
        }

        public JsonResult SaveMTEqInfo(string obj)
        {
            ei_mtEqInfo info = JsonConvert.DeserializeObject<ei_mtEqInfo>(obj);
            try {
                new MTSv().SaveEqInfo(info,userInfo);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult RemoveMTEqInfo(int id)
        {
            try {
                new MTSv().RemoveEqInfo(id);
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel(true));
        }

        public JsonResult GetMTApplyRecord(int eqInfoId)
        {
            return Json(new MTSv().GetApplyRecord(eqInfoId));
        }

        #endregion

    }
}