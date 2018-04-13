using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using EmpInfo.FlowSvr;
using EmpInfo.Filter;
using EmpInfo.Models;
using EmpInfo.Util;
using TrulyEmail;
using Newtonsoft.Json;

namespace EmpInfo.Controllers
{
    public class ApplyController : BaseController
    {
        const string DORM_REPAIR_BILL_TYPE="DP";
        const string ASK_LEAVE_BILL_TYPE = "AL";

        #region 宿舍维修申请

        [SessionTimeOutFilter]
        public ActionResult DormRepairIndex()
        {
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            //int myApplyCount = client.GetMyApplyCount(userInfo.cardNo, new ArrayOfString() { DORM_REPAIR_BILL_TYPE });
            int myDealingApplyCount = client.GetMyDealingApplyCount(userInfo.cardNo, new ArrayOfString() { DORM_REPAIR_BILL_TYPE });

            ViewData["dealingCount"] = myDealingApplyCount;

            WriteEventLog("宿舍维修申请", "打开主界面");

            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetDormInfoAndRoomMale()
        {
            var info = db.GetEmpDormInfo(userInfo.cardNo).ToList();            
            if (info.Count() == 0) {
                return Json(new SimpleResultModel() { suc = false, msg = "你当前没有入住，不能申请" });
            }
            DormRepairBeginApplyModel model = new DormRepairBeginApplyModel();

            model.areaName = info.First().area;
            model.dormNumber = info.First().dorm_number;

            var roomMate = db.GetDormRoomMate(model.dormNumber).ToList().Where(r => r.card_number != userInfo.cardNo).ToList();
            string roomMateStr = "";
            foreach (var rm in roomMate) {
                roomMateStr += rm.card_number + ":" + rm.emp_name + ";";
            }
            model.roomMaleList = roomMateStr;
            model.phoneNumber = userInfoDetail.phone;
            model.shortPhoneNumber = userInfoDetail.shortPhone;
            model.contactName = userInfo.name;

            WriteEventLog("宿舍维修申请", "新增一张维修单");

            return Json(new { suc = true, model = model });

        }

        [SessionTimeOutJsonFilter]
        public JsonResult SubmitDPApply(FormCollection fc)
        {
            string area = fc.Get("area");
            string dorm = fc.Get("dorm");
            string repairType = fc.Get("repairType");
            string repairTime = fc.Get("repairTime");
            string feeShareType = fc.Get("feeShareType");
            string feeShareRoomMates = fc.Get("feeShareRoomMates");
            string contactName = fc.Get("contactName");
            string phoneNumber = fc.Get("phoneNumber");
            string shortPhoneNumber = fc.Get("shortPhoneNumber");
            string repairContent = fc.Get("repairContent");

            string sysNo = GetNextSysNum(DORM_REPAIR_BILL_TYPE);

            ei_dormRepair dr = new ei_dormRepair();
            dr.sys_no = sysNo;
            dr.applier_name = userInfo.name;
            dr.applier_num = userInfo.cardNo;
            dr.apply_time = DateTime.Now;
            dr.area_name = area;
            dr.contact_name = contactName;
            dr.contact_phone = phoneNumber;
            dr.contact_short_phone = shortPhoneNumber;
            dr.dorm_num = dorm;            
            dr.fee_share_type = feeShareType;
            if ("舍友分摊".Equals(feeShareType)) {
                dr.fee_share_peple = feeShareRoomMates;
            }
            dr.repair_content = repairContent;            
            dr.repair_type = repairType;
            if ("预约维修".Equals(repairType)) {
                dr.repair_time = string.IsNullOrEmpty(repairTime) ? (DateTime?)null : (DateTime.Parse(repairTime));
            }
            db.ei_dormRepair.Add(dr);
            
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.StartWorkFlow(JsonConvert.SerializeObject(dr), DORM_REPAIR_BILL_TYPE, userInfo.cardNo, sysNo, "宿舍维修申请单", area + "_" + dorm);
            if (result.suc) {

                //发送通知邮件到下一环节
                DPEmail(sysNo, result, userInfo.cardNo);

                db.SaveChanges();
                WriteEventLog("宿舍维修申请", "提交成功,申请编号是：" + sysNo);
                return Json(new SimpleResultModel() { suc = true, msg = "提交成功,申请编号是：" + sysNo });
            }
            else {
                WriteEventLog("宿舍维修申请", "提交失败：" + result.msg);
                return Json(new SimpleResultModel() { suc = false, msg = result.msg });
            }
        }

        //我申请的
        [SessionTimeOutFilter]
        public ActionResult GetMyApplyDPList()
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            List<FlowMyAppliesModel> list = flow.GetMyApplyList(userInfo.cardNo, lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), "", new ArrayOfString() { DORM_REPAIR_BILL_TYPE }, "", "", 10, 20).ToList();
            ViewData["list"] = list;

            WriteEventLog("宿舍维修申请", "打开我申请的界面");
            return View();
        }

        //我的待办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditingDPList()
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 10 }, new ArrayOfString() { DORM_REPAIR_BILL_TYPE }, 100).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));
            ViewData["list"] = list;
            WriteEventLog("宿舍维修申请", "打开我的代办界面");
            return View();
        }

        //我的已办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditedDPList()
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), new ArrayOfInt() { 1, -1 }, new ArrayOfInt() { 10 }, new ArrayOfString() { DORM_REPAIR_BILL_TYPE }, 100).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));

            ViewData["list"] = list;
            WriteEventLog("宿舍维修申请", "打开我的已办界面");
            return View();
        }

        //撤销申请
        [SessionTimeOutJsonFilter]
        public JsonResult AbortDPApply(string sysNo) {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();

            var currentStep = flow.GetCurrentStep(sysNo);
            if (!currentStep.stepName.Contains("舍友")) {
                return Json(new SimpleResultModel() { suc = false, msg = "不能撤销，因为后勤部已接单" });
            }

            var result = flow.AbortFlow(userInfo.cardNo, sysNo);
            WriteEventLog("宿舍维修申请", "撤销、中止流程：" + sysNo);

            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        //查看和审核宿舍维修申请
        public ActionResult CheckAndAuditDPApply(string sysNo,bool fromDormSys = false)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var records = flow.GetFlowRecord(sysNo).ToList();
            var form = db.ei_dormRepair.Where(s => s.sys_no == sysNo).First();
            var am = new AuditModel();
            if (fromDormSys) {
                am.isAuditing = false;
            }
            else {
                am.userName = userInfo.name;
                am.cardNumber = userInfo.cardNo;

                var isAuditingRecord = records.Where(r => r.auditors.Contains(userInfo.cardNo) && r.auditResult == "审核中");
                if (isAuditingRecord.Count() > 0) {
                    am.isAuditing = true;
                    am.step = (int)isAuditingRecord.First().step;
                    am.stepName = isAuditingRecord.First().stepName;
                }
                else {
                    am.isAuditing = false;
                }
                WriteEventLog("宿舍维修申请", "查看或审批：" + sysNo);
                ViewData["userInfo"] = userInfoDetail;
            }
            records.ForEach(r => r.auditors = GetUserNameByCardNum(r.auditors));
            form.fee_share_peple = form.fee_share_peple == null ? "" : GetUserNameByCardNum(form.fee_share_peple);

            ViewData["records"] = records;
            ViewData["formData"] = form;
            ViewData["auditModel"] = am;
            

            return View();
        }

        //舍友确认审批
        [SessionTimeOutJsonFilter]
        public JsonResult RoomateConfirmDP(string sysNo, int step, bool isPass)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var dr = db.ei_dormRepair.Single(d => d.sys_no == sysNo);
            string formJson = JsonConvert.SerializeObject(dr);
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, "", formJson);
            if (result.suc) {

                //发邮件
                DPEmail(sysNo, result, dr.applier_num);
                
            }
            WriteEventLog("宿舍维修申请", "舍友审批：" + sysNo + ";isPass:" + isPass + ";msg:" + result.msg);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
            
        }

        //受理人审批维修申请
        [SessionTimeOutJsonFilter]
        public JsonResult AccepterConfirmDP(FormCollection fc)
        {
            string sysNo = fc.Get("sysNo");
            string step = fc.Get("step");
            string isPass = fc.Get("isPass");
            string accepterComment = fc.Get("accepterComment");
            string accepter = fc.Get("accepter");
            string accepterPhone = fc.Get("accepterPhone");
            string accepterShortPhone = fc.Get("accepterShortPhone");
            string confirmTime = fc.Get("confirmTime");

            if (bool.Parse(isPass)) {
                if (!string.IsNullOrEmpty(confirmTime)) {
                    if (DateTime.Now > DateTime.Parse(confirmTime)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "确认维修日期不能早于当前日期" });
                    }
                }
            }

            var dr = db.ei_dormRepair.Single(d => d.sys_no == sysNo);
            dr.accept_comment = accepterComment;            
            dr.is_accept = bool.Parse(isPass) ? true : false;
            if (bool.Parse(isPass)) {
                dr.accepter_name = accepter;
                dr.accepter_phone = accepterPhone;
                dr.accepter_short_phone = accepterShortPhone;
                dr.confirm_repair_time = string.IsNullOrEmpty(confirmTime) ? (DateTime?)null : DateTime.Parse(confirmTime);
            }            

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, int.Parse(step), userInfo.cardNo, bool.Parse(isPass), accepterComment, JsonConvert.SerializeObject(dr));

            if (result.suc) {
                db.SaveChanges();

                //发邮件
                DPEmail(sysNo, result, dr.applier_num);
            }
            WriteEventLog("宿舍维修申请", "维修接单：" + sysNo + ";isPass:" + isPass + ";msg:" + result.msg);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });

        }

        //维修完成
        [SessionTimeOutJsonFilter]
        public JsonResult RepairFinishDP(FormCollection fc)
        {
            string sysNo = fc.Get("sysNo");
            string step = fc.Get("step");
            string isPass = fc.Get("isPass");
            string repairSubject = fc.Get("repairSubject");
            string repairFinishTime = fc.Get("repairFinishTime");
            string repairFee = fc.Get("repairFee");

            var dr = db.ei_dormRepair.Single(d => d.sys_no == sysNo);

            if (!string.IsNullOrEmpty(repairFinishTime)) {
                if (DateTime.Now.AddHours(-1) > DateTime.Parse(repairFinishTime)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "完成维修日期不能迟于当前日期" });
                }
            }
            dr.repaire_subject = repairSubject;
            dr.finish_repair_time = string.IsNullOrEmpty(repairFinishTime) ? (DateTime?)null : DateTime.Parse(repairFinishTime);
            dr.charge_fee = decimal.Parse(repairFee);

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, int.Parse(step), userInfo.cardNo, bool.Parse(isPass), "", JsonConvert.SerializeObject(dr));

            if (result.suc) {
                db.SaveChanges();

                //发邮件
                DPEmail(sysNo, result, dr.applier_num);
            }
            WriteEventLog("宿舍维修申请", "维修完成：" + sysNo + ";isPass:" + isPass + ";msg:" + result.msg);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });

        }

        //宿舍维修服务评价
        [SessionTimeOutJsonFilter]
        public JsonResult EvalateDP(FormCollection fc)
        {
            string sysNo = fc.Get("sysNo");
            string step = fc.Get("step");
            string isPass = fc.Get("isPass");
            string rateScore = fc.Get("rateScore");
            string rateOpinion = fc.Get("rateOpinion");

            int rateScoreInt;
            if (!Int32.TryParse(rateScore, out rateScoreInt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "评分不合法" });
            }
            if (rateScoreInt <= 2 && string.IsNullOrEmpty(rateOpinion)) {
                return Json(new SimpleResultModel() { suc = false, msg = "评分2星以下必须填写评价意见" });
            }
            var dr = db.ei_dormRepair.Single(d => d.sys_no == sysNo);
            dr.applier_evaluation = rateOpinion;
            dr.applier_evaluation_score = rateScoreInt;
            dr.applier_confirm_time = DateTime.Now;

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, int.Parse(step), userInfo.cardNo, bool.Parse(isPass), "", JsonConvert.SerializeObject(dr));

            if (result.suc) {
                db.SaveChanges();
                
                //发邮件
                DPEmail(sysNo, result, dr.applier_num);
            }

            WriteEventLog("宿舍维修申请", "服务评价：" + sysNo + ";isPass:" + isPass + ";msg:" + result.msg);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        //宿舍维修申请发送邮件
        public void DPEmail(string sysNo, FlowResultModel model, string applier)
        {
            string subject = "", emailAddrs = "", content = "", names = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成")) {
                    //流程正常结束
                    subject = "维修申请单已完结";
                    emailAddrs = GetUserEmailByCardNum(applier);
                    names = GetUserNameByCardNum(applier);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + sysNo + "】的维修申请单已处理完成，请知悉。</div>";
                }
                else if (model.msg.Contains("NG")) {
                    //流程被NG
                    subject = "维修申请单被拒绝";
                    emailAddrs = GetUserEmailByCardNum(applier);
                    names = GetUserNameByCardNum(applier);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + sysNo + "】的维修申请单被拒绝，可登陆系统了解详情，请知悉。</div>";
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的维修申请单";
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + sysNo + "】的维修申请单，请尽快登陆系统处理。</div>";
                }

            }
            WriteEventLog("宿舍维修申请", sysNo+">发送通知邮件，地址：" + emailAddrs + ";content:" + content);            
            
            MyEmail.SendEmail(subject, emailAddrs, content);
        }
        
        #endregion

        #region 请假申请

        [SessionTimeOutFilter]
        public ActionResult AskLeaveIndex()
        {
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            int myDealingApplyCount = client.GetMyDealingApplyCount(userInfo.cardNo, new ArrayOfString() { ASK_LEAVE_BILL_TYPE });

            ViewData["dealingCount"] = myDealingApplyCount;

            WriteEventLog("宿舍维修申请", "打开主界面");
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult BeginApplyAL()
        {            
            var appliedBills = db.ei_askLeave.Where(a => a.applier_num == userInfo.cardNo).ToList();
            if (appliedBills.Count() > 0) {
                var ab = appliedBills.OrderByDescending(a => a.id).First();
                if (ab.dep_long_name.Equals(GetDepLongNameByNum(ab.dep_no))) {
                    ViewData["depName"] = ab.dep_long_name;
                    ViewData["depNum"] = ab.dep_no;
                    ViewData["depId"] = ab.dep_id;
                    ViewData["empLevel"] = ab.emp_level;                    
                }
            }

            int days = 0;
            decimal hours = 0;
            DateTime aMonthAgo = DateTime.Now.AddDays(-30);
            var vwLeaveDays = db.vw_leaving_days.Where(a => a.apply_time >= aMonthAgo && a.applier_num==userInfo.cardNo).ToList();
            if (vwLeaveDays.Count() > 0) {
                days = vwLeaveDays.Sum(a => a.work_days) ?? 0;
                hours = vwLeaveDays.Sum(a => a.work_hours) ?? 0;
                if (hours >= 8) {
                    days += (int)Math.Floor(hours) / 8;
                    hours = hours % 8;
                }
            }
            ViewData["times"] = vwLeaveDays.Count();
            ViewData["days"] = days;
            ViewData["hours"] = hours;
            ViewData["sysNum"] = GetNextSysNum(ASK_LEAVE_BILL_TYPE, 4);
            ViewData["pLevels"] = db.ei_empLevel.OrderBy(e => e.level_no).ToList();
            return View();
        }

        public JsonResult SaveApplyAL(FormCollection fc)
        {
            ei_askLeave al = new ei_askLeave();
            MyUtils.SetFieldValueToModel(fc, al);
            al.inform_man = GetUserCardByNameAndCardNum(al.inform_man);
            al.agent_man = GetUserCardByNameAndCardNum(al.agent_man);
            al.applier_name = userInfo.name;
            al.applier_num = userInfo.cardNo;
            al.apply_time = DateTime.Now;
            
            if (al.to_date == null || al.from_date == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "请假日期不合法" });
            }
            else if (al.to_date <= al.from_date) {
                return Json(new SimpleResultModel() { suc = false, msg = "请检查请假期间" });
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(al), ASK_LEAVE_BILL_TYPE, userInfo.cardNo, al.sys_no, "请假申请单", ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + "~" + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm"));
            if (result.suc) {
                db.ei_askLeave.Add(al);

                //将部门保存到用户表
                var user = db.ei_users.Single(e => e.card_number == al.applier_num);
                user.dep_long_name = al.dep_long_name;
                user.dep_no = al.dep_no;

                db.SaveChanges();

                ALEmail(al, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + al.sys_no });
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }
        }

        //我申请的
        [SessionTimeOutFilter]
        public ActionResult GetMyApplyALList()
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            List<FlowMyAppliesModel> list = flow.GetMyApplyList(userInfo.cardNo, lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), "", new ArrayOfString() { ASK_LEAVE_BILL_TYPE }, "", "", 10, 100).ToList();
            ViewData["list"] = list;

            WriteEventLog("请假申请单", "打开我申请的界面");
            return View();
        }

        //我的待办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditingALList()
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 10 }, new ArrayOfString() { ASK_LEAVE_BILL_TYPE }, 100).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));
            ViewData["list"] = list;
            WriteEventLog("请假申请单", "打开我的代办界面");
            return View();
        }

        //我的已办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditedALList()
        {
            DateTime lastYear = DateTime.Now.AddYears(-1);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), new ArrayOfInt() { 1, -1 }, new ArrayOfInt() { 10 }, new ArrayOfString() { ASK_LEAVE_BILL_TYPE }, 100).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));

            ViewData["list"] = list;
            WriteEventLog("请假申请单", "打开我的已办界面");
            return View();
        }

        //撤销申请
        [SessionTimeOutJsonFilter]
        public JsonResult AbortALApply(string sysNo, string reason = "")
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;
            
            if (flow.ApplyHasBeenAudited(sysNo)) {
                var auditStatus = flow.GetApplyResult(sysNo);
                if (auditStatus.Contains("审批中")) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程正在审批中，不能撤销，请联系当前处理人NG" });
                }
                else if (auditStatus.Contains("撤销")) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程已经撤销，不能再次操作" });
                }
                else if (auditStatus.Contains("拒绝")) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程已被拒绝，不需要再撤销" });
                }
                else if (auditStatus.Contains("通过")) {
                    if (db.ei_askLeave.Where(a => a.sys_no == sysNo && a.from_date < DateTime.Now).Count() > 0) {
                        return Json(new SimpleResultModel() { suc = false, msg = "当前请假时间已生效，不能撤销" });
                    }
                    else {
                        result = flow.AbortAfterFinish(sysNo, reason);
                    }
                }
                else {
                    return Json(new SimpleResultModel() { suc = false, msg = "不能撤销，当前状态是：" + auditStatus });
                }
            }
            else {
                //流程还未审批可以直接撤销
                result = flow.AbortFlow(userInfo.cardNo, sysNo);
            }

            WriteEventLog("请假申请单", "撤销、中止流程：" + sysNo);

            ALEmail(db.ei_askLeave.Single(a => a.sys_no == sysNo), result);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckALApply(string sysNo,string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            var al = db.ei_askLeave.Single(a => a.sys_no == sysNo);
            ALApplyModel am = new ALApplyModel(al);

            am.applierNameAndCard = al.applier_name + "(" + al.applier_num + ")";
            am.agentMan = GetUserNameAndCardByCardNum(al.agent_man);
            am.informMan = GetUserNameAndCardByCardNum(al.inform_man);
            am.empLevel = db.ei_empLevel.Single(e => e.level_no == al.emp_level).level_name;
            am.auditStatus = new FlowSvrSoapClient().GetApplyResult(sysNo);

            if (am.hasAttachment) {
                //有附件，获取附件信息
                am.attachments = MyUtils.GetAttachmentInfo(sysNo);
            }

            WriteEventLog("请假申请单", "查看申请单：" + sysNo); 

            ViewData["am"] = am;
            return View();
        }

        //开始审核申请
        [SessionTimeOutFilter]
        public ActionResult BeginAuditALApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("请假申请单", "进入审批" + sysNo + ";step:" + step);

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.ApplyHasAudit(sysNo, (int)step, userInfo.cardNo);
            if (!result.suc) {
                ViewBag.tip = result.msg;
                return View("Error");
            }

            BeginAuditModel bam = new BeginAuditModel()
            {
                sysNum = sysNo,
                step = (int)step,
                stepName = result.stepName,
                isPass = result.isPass,
                opinion = result.opinion
            };
            ViewData["bam"] = bam;
            return View();
        }

        //处理审核申请
        [SessionTimeOutJsonFilter]
        public JsonResult HandleAlApply(string sysNo,int step,bool isPass,string opinion)
        {
            var al=db.ei_askLeave.Single(a => a.sys_no == sysNo);
            string formJson = JsonConvert.SerializeObject(al);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                ALEmail(al, result);
            }
            WriteEventLog("请假申请单", "处理申请单:" + sysNo + ";step:" + step + ";isPass:" + isPass);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        //查看流转记录
        [SessionTimeOutFilter]
        public ActionResult CheckFlowRecord(string sysNo)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetFlowRecord(sysNo).ToList();
            result.ForEach(r => r.auditors = GetUserNameByCardNum(r.auditors));

            if (flow.GetApplyResult(sysNo) == "已通过") {
                result.Add(new FlowRecordModels()
                {
                    stepName = "完成申请",
                    auditors = "系统",
                    auditResult = "成功",
                    auditTimes = result.OrderByDescending(r => r.auditTimes).First().auditTimes,
                    opinions = ""
                });
            }

            ViewData["record"] = result;
            
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetALFlowQueue(FormCollection fc)
        {
            ei_askLeave al = new ei_askLeave();
            MyUtils.SetFieldValueToModel(fc, al);
            al.inform_man = GetUserCardByNameAndCardNum(al.inform_man);
            al.agent_man = GetUserCardByNameAndCardNum(al.agent_man);
            al.applier_name = userInfo.name;
            al.applier_num = userInfo.cardNo;
            al.apply_time = DateTime.Now;

            if (al.to_date == null || al.from_date == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "请假日期不合法" });
            }
            else if (al.to_date <= al.from_date) {
                return Json(new SimpleResultModel() { suc = false, msg = "请检查请假期间" });
            }

            if (!string.IsNullOrEmpty(al.agent_man)) {
                if (string.IsNullOrEmpty(GetUserEmailByCardNum(al.agent_man))) {
                    return Json(new SimpleResultModel() { suc = false, msg = "代理人邮箱没有设置" });
                }
            }
            foreach (var im in al.inform_man.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (string.IsNullOrEmpty(GetUserEmailByCardNum(im))) {
                    return Json(new SimpleResultModel() { suc = false, msg = "知会人（" + GetUserNameAndCardByCardNum(im) + "）邮箱没有设置" });
                }
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(al));
            List<FlowQueueModel> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<FlowQueueModel>>(result.msg);
                queue.ForEach(q => q.auditors = GetUserNameAndCardByCardNum(q.auditors));
                return Json(new { suc = true, list = queue });
            }
            else {
                return Json(new { suc = false, msg = result.msg });
            }
            
        }

        //发送请假申请单通知邮件
        public void ALEmail(ei_askLeave al, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(al.sys_no);

            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }

                    //流程正常结束
                    subject = "请假申请单已" + (isSuc ? "批准" : "拒绝");
                    emailAddrs = GetUserEmailByCardNum(al.applier_num);
                    if(isSuc){
                        ccEmails = GetUserEmailByCardNum(al.inform_man + (string.IsNullOrEmpty(al.agent_man) ? "" : (";" + al.agent_man)));
                    }
                    names = al.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + al.sys_no + "】的请假申请单已被" + (isSuc ? "批准" : "拒绝") + ",请假时间是：" + ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + "~" + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm") + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckALApply?sysNo={0}'>内网用户点击此链接</a></div>", al.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckALApply?sysNo={0}'>外网用户点击此链接</a></div></div>",al.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == al.applier_num).ToList();
                    if (pushUsers.Count() > 0) {
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = al.applier_num;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "请假申请流程";
                        pm.FKeyWord2 = al.sys_no;
                        pm.FKeyWord3 = isSuc ? "审批通过" : "审批不通过";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckALApply&param={2}", al.applier_num, MyUtils.getMD5(al.applier_num), al.sys_no);
                        db.wx_pushMsg.Add(pm);
                        db.SaveChanges();
                    }                    
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的请假申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + al.sys_no + "】的请假申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditALApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", al.sys_no,result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditALApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", al.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers=db.vw_push_users.Where(u=>u.card_number==ad).ToList();
                        if(pushUsers.Count()==0){
                            continue;
                        }
                        var pushUser=pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "请假申请流程" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                        pm.FKeyWord2 = al.sys_no;
                        pm.FKeyWord3 = ((DateTime)al.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = al.applier_name;
                        pm.FKeyWord5 = al.dep_long_name;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审";
                        pm.FRemark = "点击可进入审批此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditALApply&param={2}", ad, MyUtils.getMD5(ad), al.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                    
                }

            }
            WriteEventLog("请假条申请", al.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        //行政部发送约谈信息
        public JsonResult LeaveDayExceedPush(string sysNo, string bookTime) {
            DateTime dt;
            if (!DateTime.TryParse(bookTime, out dt)) {
                return Json(new SimpleResultModel() { suc = false, msg = "预约时间不是日期格式" });
            }

            ei_leaveDayExceedPushLog log = new ei_leaveDayExceedPushLog();
            log.book_date = dt;
            log.send_date = DateTime.Now;
            log.send_user = userInfo.name;
            log.sys_no = sysNo;
            db.ei_leaveDayExceedPushLog.Add(log);            

            ei_askLeave al = db.ei_askLeave.Single(a => a.sys_no == sysNo);            
            var receviers = al.applier_num;
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var step1Auditors = flow.GetCertainStepAuditors(sysNo, 1);
            if (!string.IsNullOrEmpty(step1Auditors)) {
                receviers += ";" + step1Auditors;
            }

            //发微信
            foreach (var ad in receviers.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                var pushUsers = db.vw_push_users.Where(u => u.card_number == ad).ToList();
                if (pushUsers.Count() == 0) {
                    continue;
                }
                var pushUser = pushUsers.First();
                wx_pushMsg pm = new wx_pushMsg();
                pm.FCardNumber = ad;
                pm.FFirst = string.Format("{0}，请您于下述时间，前往写字楼（行政大楼）一楼行政部与{1}面谈。", al.applier_name, userInfo.name);
                pm.FHasSend = false;
                pm.FInTime = DateTime.Now;
                pm.FkeyWord1 = "请假时间超过公司规定天数";
                pm.FKeyWord2 = dt.ToString("yyyy-MM-dd HH:mm");
                pm.FOpenId = pushUser.wx_openid;
                pm.FPushType = "行政面谈";
                pm.FRemark = "请您准时到达，如有疑问。请致电行政部" + userInfo.name + "，内线电话：3003";
                db.wx_pushMsg.Add(pm);
            }
            db.SaveChanges();

            //发送邮件
            string subject = "行政部面谈通知";
            string emailAddrs = GetUserEmailByCardNum(al.applier_num);
            string ccEmails = GetUserEmailByCardNum(step1Auditors);
            string content = "<div>" + string.Format("{0}，你好！", al.applier_name) + "</div>";
            content += "<div style='margin-left:30px;'>请您于下述时间，前往写字楼（行政大楼）一楼行政部与" + userInfo.name + "面谈。<br /> 面谈内容：请假时间超过公司规定天数 <br/> 预约时间：" + dt.ToString("yyyy-MM-dd HH:mm") + "</div>";

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);

            WriteEventLog("请假条申请", al.sys_no + ">发送行政面谈邮件，地址：" + emailAddrs + ";content:" + content);

            return Json(new SimpleResultModel() { suc = true });
        }

        public JsonResult GetLeaveDaysExceedLog(string sysNo)
        {
            var logs = db.ei_leaveDayExceedPushLog.Where(l => l.sys_no == sysNo).ToList();
            if (logs.Count() == 0) {
                return Json(new SimpleResultModel() { suc = false });
            }
            var log = logs.Last();
            return Json(new SimpleResultModel() { suc = true, msg = string.Format("【发送记录】发送人：{0}；发送时间：{1}；预约时间：{2}",log.send_user,((DateTime)log.send_date).ToString("yyyy-MM-dd HH:mm"),((DateTime)log.book_date).ToString("yyyy-MM-dd HH:mm")) });
        }

        public string testalemail()
        {
            ALEmail(db.ei_askLeave.Single(a => a.sys_no == "AL1804020024"), new FlowResultModel() { suc = true, msg = "完成" });
            return "ok";
        }

        #endregion

        #region 出差申请

        public ActionResult BeginApplyBT()
        {
            return View();
        }

        #endregion
    }
}
