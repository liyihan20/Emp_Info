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
    /// <summary>
    /// 旧架构，没有service层，没有分层，所有流程代码都在这里实现，冗余，不易维护，2019-04-08开始停用
    /// </summary>
    public class ApplyOldController : BaseController
    {
        const string DORM_REPAIR_BILL_TYPE = "DP";
        const string ASK_LEAVE_BILL_TYPE = "AL";
        const string STOCK_ADMIN_BILL_TYPE = "SA";
        const string UNNORMAL_CH_BILL_TYPE = "UC";
        const string CARD_REGISTER_BILL_TYPE = "CR";
        const string SWITCH_VACATION_BILL_TYPE = "SV";
        const string EQUITMENT_REPAIRE_BILL_TYPE = "EP";

        #region 宿舍维修申请 DP

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
            var result = flow.StartWorkFlow(JsonConvert.SerializeObject(dr), DORM_REPAIR_BILL_TYPE, userInfo.cardNo, sysNo, repairContent, area + "_" + dorm);
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

        #region 请假申请 AL  

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

            //最近30天请假天数
            int days = 0;
            decimal hours = 0;
            DateTime aMonthAgo = DateTime.Now.AddDays(-30);
            var vwLeaveDays = db.vw_leaving_days.Where(a => a.apply_time >= aMonthAgo && a.applier_num == userInfo.cardNo).ToList();
            if (vwLeaveDays.Count() > 0) {
                days = vwLeaveDays.Sum(a => a.work_days) ?? 0;
                hours = vwLeaveDays.Sum(a => a.work_hours) ?? 0;
                if (hours >= 8) {
                    days += (int)Math.Floor(hours) / 8;
                    hours = hours % 8;
                }
            }
            
            //剩余年假天数            

            ViewData["times"] = vwLeaveDays.Count();
            ViewData["days"] = days;
            ViewData["hours"] = hours;
            ViewData["sysNum"] = GetNextSysNum(ASK_LEAVE_BILL_TYPE, 4);
            ViewData["pLevels"] = db.ei_empLevel.OrderBy(e => e.level_no).ToList();
            try {
                ViewData["vacationDaysLeft"] = db.GetVacationDaysLeftProc(userInfo.cardNo).First();
            }
            catch (Exception ex) {
                ViewBag.tip = "计算本年度剩余年休假时出现错误，请联系部门负责做考勤的文员确认，错误信息：" + ex.Message;
                return View("Error");
            }
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
            
            //一线员工必须选择到最底层的部门
            if (al.emp_level == 0) {
                if (db.ei_department.Where(d => d.FParent == al.dep_no && (d.FIsDeleted == null || d.FIsDeleted == false) && (d.FIsForbit == null || d.FIsForbit == false)).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "存在子级部门，请展开部门文件夹继续选择" });
                }
            }
            
            //处理一下审核队列,将姓名（厂牌）格式更换为厂牌
            if (!string.IsNullOrEmpty(al.auditor_queues)) {
                var queueList = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(al.auditor_queues);
                queueList.ForEach(q => q.auditors = GetUserCardByNameAndCardNum(q.auditors));
                if (queueList.Where(q => q.auditors.Contains("离职")).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程审核人中存在已离职的员工，请联系部门管理员处理" });
                }
                al.auditor_queues = JsonConvert.SerializeObject(queueList);
            }
            

            if (al.to_date == null || al.from_date == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "请假日期不合法" });
            }
            else if (al.to_date <= al.from_date) {
                return Json(new SimpleResultModel() { suc = false, msg = "请检查请假期间" });
            }

            if (al.leave_type.Equals("年假") && ((DateTime)al.to_date).Year != ((DateTime)al.from_date).Year) {
                return Json(new SimpleResultModel() { suc = false, msg = "不能跨年度休年假，请分开请假" });
            }
            //return Json(new SimpleResultModel() { suc = true, msg = "测试ok" });
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(al), ASK_LEAVE_BILL_TYPE, userInfo.cardNo, al.sys_no, al.leave_type, ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + "~" + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm"));
            if (result.suc) {
                try {
                    db.ei_askLeave.Add(al);

                    //将部门保存到用户表
                    var user = db.ei_users.Single(e => e.card_number == al.applier_num);
                    user.dep_long_name = al.dep_long_name;
                    user.dep_no = al.dep_no;

                    db.SaveChanges();
                }
                catch(Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(al.sys_no);
                    return Json(new SimpleResultModel() { suc = false, msg = "申请提交失败，原因：" + ex.Message });
                }

                ALEmail(al, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + al.sys_no });
            }
            else {
                
                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }         
            //return Json(new SimpleResultModel() { suc = false, msg = "test"});
        }
        
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

            var daySpan = ((DateTime)al.to_date - (DateTime)al.from_date).Days;
            if (daySpan - al.work_days > 4 || al.work_days - daySpan > 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "请检查请假期间和请假天数是否对应" });
            }

            if (al.leave_type.Equals("年假") && ((DateTime)al.to_date).Year != ((DateTime)al.from_date).Year) {
                return Json(new SimpleResultModel() { suc = false, msg = "不能跨年度休年假，请分开请假" });
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
            var c = al.is_continue;
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(al),ASK_LEAVE_BILL_TYPE);
            List<flow_applyEntryQueue> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(result.msg);
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

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == al.applier_num && u.wx_push_flow_info == true).ToList();
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
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
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

            string addr, phone;
            if (al.dep_no.StartsWith("106")) {
                addr = "研发楼一楼行政及人力资源部";
                phone = "0752-6568888/7888";
            }
            else {
                addr = "写字楼（行政大楼）一楼行政部";
                phone = "3003";
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
                pm.FFirst = string.Format("{0}，请您于下述时间，前往{2}与{1}面谈。", al.applier_name, userInfo.name,addr);
                pm.FHasSend = false;
                pm.FInTime = DateTime.Now;
                pm.FkeyWord1 = "请假时间超过公司规定天数";
                pm.FKeyWord2 = dt.ToString("yyyy-MM-dd HH:mm");
                pm.FOpenId = pushUser.wx_openid;
                pm.FPushType = "行政面谈";
                pm.FRemark = "请您准时到达，如有疑问。请致电行政部" + userInfo.name + "，电话：" + phone;
                db.wx_pushMsg.Add(pm);
            }
            db.SaveChanges();

            //发送邮件
            string subject = "行政部面谈通知";
            string emailAddrs = GetUserEmailByCardNum(al.applier_num);
            string ccEmails = GetUserEmailByCardNum(step1Auditors);
            string content = "<div>" + string.Format("{0}，你好！", al.applier_name) + "</div>";
            content += "<div style='margin-left:30px;'>请您于下述时间，前往" + addr + "与" + userInfo.name + "面谈。<br /> 面谈内容：请假时间超过公司规定天数 <br/> 预约时间：" + dt.ToString("yyyy-MM-dd HH:mm") + "</div>";
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

        //查看最近1年内的申请记录
        [SessionTimeOutJsonFilter]
        public JsonResult GetLeaveRecordsInOneYear(string applierNameAndCard)
        {            
            DateTime aYearAgo = DateTime.Now.AddYears(-1);
            string applierNumber = GetUserCardByNameAndCardNum(applierNameAndCard);

            var result = (from v in db.vw_askLeaveReport
                          where v.applier_num == applierNumber
                          && v.status == "已通过"
                          && v.apply_time >= aYearAgo
                          && !(v.work_days == 0 && v.work_hours == 0)
                          orderby v.apply_time descending
                          select v
                          ).ToList();

            var list = (from r in result
                        select new ALRecordModel()
                        {
                            sysNo = r.sys_no,
                            applyTime = ((DateTime)r.apply_time).ToString("yyyy-MM-dd"),
                            leaveType = r.leave_type,
                            leaveDays = r.work_days + "天" + r.work_hours + "小时",
                            leaveDateSpan = ((DateTime)r.from_date).ToString("MM-dd HH:mm") + " ~ " + ((DateTime)r.to_date).ToString("MM-dd HH:mm")
                        }).ToList();

            return Json(new { suc = true, list = list });
        }

        public string testalemail()
        {
            ALEmail(db.ei_askLeave.Single(a => a.sys_no == "AL1804020024"), new FlowResultModel() { suc = true, msg = "完成" });
            return "ok";
        }

        #endregion

        #region 出差申请 BT

        public ActionResult BeginApplyBT()
        {
            return View();
        }

        #endregion

        #region 仓管权限申请 SA

        [SessionTimeOutFilter]
        public ActionResult BeginApplySA()
        {
            var accounts = (from sc in db.GetK3StockAccoutList()
                            select new K3AccountModel()
                            {
                                number = sc.FAcctNumber,
                                name = sc.FAcctName
                            }).ToList();
            ViewData["stockAccounts"] = accounts;
            ViewData["sysNum"] = GetNextSysNum(STOCK_ADMIN_BILL_TYPE, 3);

            return View();                                     
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetStockAndAdminByAccount(string accName)
        {
            var result = (from s in db.GetK3StockAuditor(accName)
                          select new
                          {
                              stockName = s.FName,
                              stockNum = s.FNumber,
                              auditorName = s.FCheckName ?? "",
                              auditorNum = s.FCheckNo ?? ""
                          }).ToList().OrderBy(a => a.stockNum);
            return Json(new { suc = true, result = result });
        }

        [SessionTimeOutJsonFilter]
        public JsonResult SaveApplySA(FormCollection fc)
        {
            ei_stockAdminApply es = new ei_stockAdminApply();
            es.sys_no = fc.Get("sysNum");
            es.applier_name = userInfo.name;
            es.applier_num = userInfo.cardNo;
            es.apply_time = DateTime.Now;
            es.k3_stock_name = fc.Get("stockName");
            es.k3_stock_num = fc.Get("stockNum");
            es.stock_auditor_name = fc.Get("auditorName");
            es.stock_auditor_num = fc.Get("auditorNum");
            es.k3_account_name = fc.Get("accountName");
            es.comment = fc.Get("comment");
            

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(es), STOCK_ADMIN_BILL_TYPE, userInfo.cardNo, es.sys_no, es.k3_stock_name + "(" + es.k3_stock_num + ")", es.k3_account_name);
            if (result.suc) {
                db.ei_stockAdminApply.Add(es);        

                db.SaveChanges();

                SAEmail(es, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + es.sys_no });
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }
            //return Json(new SimpleResultModel() { suc = false, msg = "test"});
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckSAApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            var sas = db.ei_stockAdminApply.Where(s => s.sys_no == sysNo).ToList();
            if (sas.Count() < 1) {
                MyUtils.ClearCookie(this.Response, this.Session);
                return View("Close");
                //ViewBag.tip = "此申请流水号不存在";
                //return View("Error");
            }

            var auditStatus = new FlowSvrSoapClient().GetApplyResult(sysNo);

            WriteEventLog("仓管权限申请", "查看申请单：" + sysNo);

            ViewData["auditStatus"] = auditStatus;
            ViewData["sa"] = sas.First();
            return View();
        }

        //开始审核申请
        [SessionTimeOutFilter]
        public ActionResult BeginAuditSAApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("仓管权限申请", "进入审批" + sysNo + ";step:" + step);

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
        public JsonResult HandleSAApply(string sysNo, int step, bool isPass, string opinion)
        {
            var sa = db.ei_stockAdminApply.Single(a => a.sys_no == sysNo);
            string formJson = JsonConvert.SerializeObject(sa);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                SAEmail(sa, result);
            }
            WriteEventLog("仓管权限申请", "处理申请单:" + sysNo + ";step:" + step + ";isPass:" + isPass);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        public void SAEmail(ei_stockAdminApply sa, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(sa.sys_no);

            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }

                    //流程正常结束
                    subject = "仓库管理权限申请单已" + (isSuc ? "批准" : "拒绝");
                    emailAddrs = GetUserEmailByCardNum(sa.applier_num);                    
                    names = sa.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + sa.sys_no + "】的仓库管理权限申请单已被" + (isSuc ? "批准" : "拒绝") + ",账套是：" + sa.k3_account_name + "仓库是：" + sa.k3_stock_name + "(" + sa.k3_stock_num + ")" + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckSAApply?sysNo={0}'>内网用户点击此链接</a></div>", sa.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckSAApply?sysNo={0}'>外网用户点击此链接</a></div></div>", sa.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == sa.applier_num && u.wx_push_flow_info == true).ToList();
                    if (pushUsers.Count() > 0) {
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = sa.applier_num;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "仓库管理权限申请流程";
                        pm.FKeyWord2 = sa.sys_no;
                        pm.FKeyWord3 = isSuc ? "审批通过" : "审批不通过";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckSAApply&param={2}", sa.applier_num, MyUtils.getMD5(sa.applier_num), sa.sys_no);
                        db.wx_pushMsg.Add(pm);
                        db.SaveChanges();
                    }
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的仓库管理权限申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + sa.sys_no + "】的仓库管理权限申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditSAApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", sa.sys_no, result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditSAApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", sa.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
                            continue;
                        }
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项：" + sa.sys_no;
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "仓库管理权限申请流程";
                        pm.FKeyWord2 = sa.applier_name;
                        pm.FKeyWord3 = ((DateTime)sa.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = sa.k3_account_name + ":" + sa.k3_stock_name;
                        pm.FKeyWord5 = result.stepName;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审2";
                        pm.FRemark = "点击可进入审批此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditSAApply&param={2}", ad, MyUtils.getMD5(ad), sa.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                }

            }
            WriteEventLog("仓库管理权限申请", sa.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        #endregion

        #region 非正常时间出货 UC

        [SessionTimeOutFilter]
        public ActionResult BeginApplyUC()
        {
            //申请时间必须在8时至19时之间
            var hour = DateTime.Now.Hour;
            if (hour < 8 || hour >= 19) {
                ViewBag.tip = "非正常时间出货的申请时间必须在8时到19时之间，当前时间不能申请";
                return View("Error");
            }
            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday) {
                ViewBag.tip = "非正常时间出货的申请不能在周日进行申请";
                return View("Error");
            }

            var list = db.flow_auditorRelation.Where(a => a.bill_type == UNNORMAL_CH_BILL_TYPE).ToList();
            ViewData["marketList"] = list.Where(l => l.relate_name == "市场部总经理").Select(l => l.relate_text).Distinct().ToList();
            ViewData["busDepList"] = list.Where(l => l.relate_name == "事业部长").Select(l => l.relate_text).Distinct().ToList();
            ViewData["accountingList"] = list.Where(l => l.relate_name == "会计部主管").Select(l => l.relate_text).Distinct().ToList();

            ViewData["sysNum"] = GetNextSysNum("UC",2);
            return View();
        }

        [SessionTimeOutJsonFilter]
        public ActionResult SaveApplyUC(FormCollection fc)
        {
            var uc = new ei_ucApply();
            MyUtils.SetFieldValueToModel(fc, uc);
            List<ei_ucApplyEntry> entrys = JsonConvert.DeserializeObject<List<ei_ucApplyEntry>>(fc.Get("entrys"));
            
            if (string.IsNullOrEmpty(uc.market_name)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请选择市场部！" });
            }

            if (string.IsNullOrEmpty(uc.customer_name)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请录入正确的客户编码！" });
            }

            if (string.IsNullOrEmpty(uc.company)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请选择出货公司！" });
            }

            if (string.IsNullOrEmpty(uc.bus_dep)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请选择生产事业部！" });
            }

            if (string.IsNullOrEmpty(uc.delivery_company)) {
                return Json(new SimpleResultModel() { suc = false, msg = "请填写货运公司！" });
            }

            if (entrys.Count() < 1) {
                return Json(new SimpleResultModel() { suc = false, msg = "出货明细必须至少一条！" });
            }

            if (uc.arrive_time == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "到达日期不合法" });
            }

            DateTime today = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd 19:00:00"));
            DateTime tomorrow = today.AddDays(1);

            if ((uc.arrive_time >= today && uc.arrive_time <= today.AddHours(3)) || (uc.arrive_time >= tomorrow && uc.arrive_time <= tomorrow.AddHours(3))) {
                uc.applier_name = userInfo.name;
                uc.applier_num = userInfo.cardNo;
                uc.apply_time = DateTime.Now;

                FlowSvrSoapClient client = new FlowSvrSoapClient();
                var result = client.StartWorkFlow(JsonConvert.SerializeObject(uc), UNNORMAL_CH_BILL_TYPE, userInfo.cardNo, uc.sys_no, uc.customer_name, ((DateTime)uc.arrive_time).ToString("yyyy-MM-dd HH:mm"));
                if (result.suc) {
                    try {
                        db.ei_ucApply.Add(uc);
                        foreach (var e in entrys) {
                            e.ei_ucApply = uc;
                            db.ei_ucApplyEntry.Add(e);
                        }
                        db.SaveChanges();
                    }
                    catch(Exception ex) {
                        //将生成的流程表记录删除
                        client.DeleteApplyForFailure(uc.sys_no);
                        return Json(new SimpleResultModel() { suc = false, msg = "申请提交失败，原因："+ ex.Message});
                    }

                    UCEmail(uc, result);

                    WriteEventLog("非正常出货申请", "提交申请：" + uc.sys_no);
                    return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + uc.sys_no });
                }
                else {                    
                    return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
                }         
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "到达时间不在今明19时至22时范围内" });
            }            
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckUCApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }
            var uc = db.ei_ucApply.Single(u => u.sys_no == sysNo);

            ViewData["uc"] = uc;
            ViewData["entrys"] = uc.ei_ucApplyEntry.ToList();
            ViewData["auditStatus"] = new FlowSvrSoapClient().GetApplyResult(sysNo);
            if (uc.has_attachment == true) {
                ViewData["attachments"] = MyUtils.GetAttachmentInfo(sysNo);
            }

            WriteEventLog("非正常出货", "查看申请单：" + sysNo);
            return View();
        }

        //开始审核申请
        [SessionTimeOutFilter]
        public ActionResult BeginAuditUCApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("非正常出货", "进入审批" + sysNo + ";step:" + step);

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
        public JsonResult HandleUCApply(string sysNo, int step, bool isPass, string opinion)
        {
            var uc = db.ei_ucApply.Single(a => a.sys_no == sysNo);
            
            //最后一步的审批时间限定为早上8时到中午17时
            //if (step == 6 && DateTime.Now > DateTime.Parse("2019-03-18")) {
            //    var hour = DateTime.Now.Hour;
            //    if (hour < 8 || hour >= 17) {
            //        return Json(new SimpleResultModel() { suc = false, msg = "处理失败，审批时间必须介于8时至17时。如有问题，请联系市场部" });
            //    }
            //}

            var setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(uc, setting);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                UCEmail(uc, result);
            }
            WriteEventLog("非正常时间出货申请", "处理申请单:" + sysNo + ";step:" + step + ";isPass:" + isPass);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        private void UCEmail(ei_ucApply uc, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(uc.sys_no);                       

            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";            
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }
                    
                    //流程正常结束
                    subject = "非正常时间出货申请单已" + (isSuc ? "批准" : "拒绝");
                    emailAddrs = GetUserEmailByCardNum(uc.applier_num);
                    names = uc.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + uc.sys_no + "】的非正常时间出货申请单已被" + (isSuc ? "批准" : "拒绝") + ",客户是：" + uc.customer_name + "规格型号是：" + uc.ei_ucApplyEntry.First().moduel + "等" + uc.ei_ucApplyEntry.Count() + "个" + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckUCApply?sysNo={0}'>内网用户点击此链接</a></div>", uc.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckUCApply?sysNo={0}'>外网用户点击此链接</a></div></div>", uc.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == uc.applier_num && u.wx_push_flow_info == true).ToList();
                    if (isSuc) {
                        //通知知会人
                        pushUsers.AddRange(
                            (from u in db.ei_ucNotifyUsers
                             join v in db.vw_push_users on u.card_number equals v.card_number
                             where v.wx_push_flow_info == true && (u.company == "所有" || u.company == uc.company)
                             select v).ToList()
                        );
                        ccEmails = string.Join(",", (
                            from u in db.ei_ucNotifyUsers
                            join us in db.ei_users on u.card_number equals us.card_number
                            where u.company == "所有" || u.company == uc.company
                            select us.email
                            ).ToArray()
                         );
                    }

                    foreach (var pushUser in pushUsers) {
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = pushUser.card_number;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "非正常时间出货申请流程";
                        pm.FKeyWord2 = uc.sys_no;
                        pm.FKeyWord3 = isSuc ? "审批通过" : "审批不通过";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckUCApply&param={2}", pushUser.card_number, MyUtils.getMD5(pushUser.card_number), uc.sys_no);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的非正常时间出货申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + uc.sys_no + "】的非正常时间出货申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditUCApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", uc.sys_no, result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditUCApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", uc.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
                            continue;
                        }
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项：" + uc.sys_no;
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "非正常时间出货申请流程";
                        pm.FKeyWord2 = uc.applier_name;
                        pm.FKeyWord3 = ((DateTime)uc.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = uc.customer_name + ":" + uc.ei_ucApplyEntry.First().moduel + "等";
                        pm.FKeyWord5 = result.stepName;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审2";
                        pm.FRemark = "点击可进入审批此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditUCApply&param={2}", ad, MyUtils.getMD5(ad), uc.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                }

            }
            WriteEventLog("非正常时间出货申请", uc.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        #endregion

        #region 电子考勤补记申请流程（漏刷卡） CR

        [SessionTimeOutFilter]
        public ActionResult BeginApplyCR()
        {
            var appliedBills = db.ei_askLeave.Where(a => a.applier_num == userInfo.cardNo).ToList();
            if (appliedBills.Count() > 0) {
                var ab = appliedBills.OrderByDescending(a => a.id).First();
                if (ab.dep_long_name.Equals(GetDepLongNameByNum(ab.dep_no))) {
                    ViewData["depName"] = ab.dep_long_name;
                    ViewData["depNum"] = ab.dep_no;
                    ViewData["depId"] = ab.dep_id;
                }
            }
            
            ViewData["sysNum"] = GetNextSysNum(CARD_REGISTER_BILL_TYPE, 4);
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetCRFlowQueue(FormCollection fc)
        {
            ei_CRApply cr = new ei_CRApply();
            MyUtils.SetFieldValueToModel(fc, cr);
            cr.applier_name = userInfo.name;
            cr.applier_num = userInfo.cardNo;
            cr.apply_time = DateTime.Now;

            if (!cr.dep_no.StartsWith("104")) {
                return Json(new { suc = false, msg = "此流程的部门只支持选择信利电子子部门" });
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(cr), CARD_REGISTER_BILL_TYPE);
            List<flow_applyEntryQueue> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(result.msg);
                queue.ForEach(q => q.auditors = GetUserNameAndCardByCardNum(q.auditors));
                return Json(new { suc = true, list = queue });
            }
            else {
                return Json(new { suc = false, msg = result.msg });
            }

        }

        public JsonResult SaveApplyCR(FormCollection fc)
        {
            ei_CRApply cr = new ei_CRApply();
            MyUtils.SetFieldValueToModel(fc, cr);
            cr.applier_name = userInfo.name;
            cr.applier_num = userInfo.cardNo;
            cr.apply_time = DateTime.Now;

            if (cr.has_attachment == false && cr.reason.Equals("补办厂牌")) {
                return Json(new SimpleResultModel() { suc = false, msg = "原因是补办厂牌的，必须上传附件！" });
            }

            //处理一下审核队列,将姓名（厂牌）格式更换为厂牌
            if (!string.IsNullOrEmpty(cr.auditor_queues)) {
                var queueList = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(cr.auditor_queues);
                queueList.ForEach(q => q.auditors = GetUserCardByNameAndCardNum(q.auditors));
                if (queueList.Where(q => q.auditors.Contains("离职")).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程审核人中存在已离职的员工，请联系部门管理员处理" });
                }
                cr.auditor_queues = JsonConvert.SerializeObject(queueList);
            }

                        
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(cr), CARD_REGISTER_BILL_TYPE, userInfo.cardNo, cr.sys_no, ((DateTime)cr.forgot_date).ToString("yyyy_MM-dd"), cr.time1 ?? "" + " " + cr.time2 ?? "" + " " + cr.time3 ?? "" + " " + cr.time4 ?? "");
            if (result.suc) {
                try {
                    db.ei_CRApply.Add(cr);

                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(cr.sys_no);
                    return Json(new SimpleResultModel() { suc = false, msg = "申请提交失败，原因：" + ex.Message });
                }

                CREmail(cr, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + cr.sys_no });
            }
            else {

                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }            
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckCRApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            var cr = db.ei_CRApply.Single(a => a.sys_no == sysNo);
            
            ViewData["auditStatus"] = new FlowSvrSoapClient().GetApplyResult(sysNo);
            if (cr.has_attachment == true) {
                ViewData["attachments"] = MyUtils.GetAttachmentInfo(sysNo);
            }

            WriteEventLog("考勤补记申请单", "查看申请单：" + sysNo);

            ViewData["cr"] = cr;
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditCRApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("考勤补记申请单", "进入审批" + sysNo + ";step:" + step);

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
        public JsonResult HandleCRApply(string sysNo, int step, bool isPass, string opinion)
        {
            var cr = db.ei_CRApply.Single(a => a.sys_no == sysNo);
            string formJson = JsonConvert.SerializeObject(cr);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                CREmail(cr, result);
            }
            WriteEventLog("调休申请", "处理申请单:" + sysNo + ";step:" + step + ";isPass:" + isPass);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        public void CREmail(ei_CRApply cr, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(cr.sys_no);

            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }

                    //流程正常结束
                    subject = "考勤补记申请单已" + (isSuc ? "批准" : "拒绝");
                    emailAddrs = GetUserEmailByCardNum(cr.applier_num);
                    names = cr.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + cr.sys_no + "】的考勤补记申请单已被" + (isSuc ? "批准" : "拒绝") + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckCRApply?sysNo={0}'>内网用户点击此链接</a></div>", cr.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckCRApply?sysNo={0}'>外网用户点击此链接</a></div></div>", cr.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == cr.applier_num && u.wx_push_flow_info == true).ToList();
                    if (pushUsers.Count() > 0) {
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = cr.applier_num;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "考勤补记申请流程";
                        pm.FKeyWord2 = cr.sys_no;
                        pm.FKeyWord3 = isSuc ? "审批通过" : "审批不通过";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckCRApply&param={2}", cr.applier_num, MyUtils.getMD5(cr.applier_num), cr.sys_no);
                        db.wx_pushMsg.Add(pm);
                        db.SaveChanges();
                    }
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的考勤补记申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + cr.sys_no + "】的考勤补记申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditCRApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", cr.sys_no, result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditCRApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", cr.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
                            continue;
                        }
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项：" + cr.sys_no;
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "考勤补记申请流程";
                        pm.FKeyWord2 = cr.applier_name;
                        pm.FKeyWord3 = ((DateTime)cr.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = ((DateTime)cr.forgot_date).ToString("yyyy-MM-dd");
                        pm.FKeyWord5 = result.stepName;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审2";
                        pm.FRemark = "点击可进入审批此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditCRApply&param={2}", ad, MyUtils.getMD5(ad), cr.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                }

            }
            WriteEventLog("考勤补记申请", cr.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        #endregion

        #region 电子调休申请流程 SV

        [SessionTimeOutFilter]
        public ActionResult BeginApplySV()
        {
            var appliedBills = db.ei_askLeave.Where(a => a.applier_num == userInfo.cardNo).ToList();
            if (appliedBills.Count() > 0) {
                var ab = appliedBills.OrderByDescending(a => a.id).First();
                if (ab.dep_long_name.Equals(GetDepLongNameByNum(ab.dep_no))) {
                    ViewData["depName"] = ab.dep_long_name;
                    ViewData["depNum"] = ab.dep_no;
                    ViewData["depId"] = ab.dep_id;
                }
            }

            ViewData["sysNum"] = GetNextSysNum(SWITCH_VACATION_BILL_TYPE, 4);
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetSVFlowQueue(FormCollection fc)
        {
            ei_SVApply sv = new ei_SVApply();
            MyUtils.SetFieldValueToModel(fc, sv);
            sv.applier_name = userInfo.name;
            sv.applier_num = userInfo.cardNo;
            sv.apply_time = DateTime.Now;

            if (!sv.dep_no.StartsWith("104")) {
                return Json(new { suc = false, msg = "此流程的部门只支持选择信利电子子部门" });
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(sv), SWITCH_VACATION_BILL_TYPE);
            List<flow_applyEntryQueue> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(result.msg);
                queue.ForEach(q => q.auditors = GetUserNameAndCardByCardNum(q.auditors));
                return Json(new { suc = true, list = queue });
            }
            else {
                return Json(new { suc = false, msg = result.msg });
            }

        }

        public JsonResult SaveApplySV(FormCollection fc)
        {
            ei_SVApply sv = new ei_SVApply();
            MyUtils.SetFieldValueToModel(fc, sv);
            sv.applier_name = userInfo.name;
            sv.applier_num = userInfo.cardNo;
            sv.apply_time = DateTime.Now;

            //处理一下审核队列,将姓名（厂牌）格式更换为厂牌
            if (!string.IsNullOrEmpty(sv.auditor_queues)) {
                var queueList = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(sv.auditor_queues);
                queueList.ForEach(q => q.auditors = GetUserCardByNameAndCardNum(q.auditors));
                if (queueList.Where(q => q.auditors.Contains("离职")).Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "流程审核人中存在已离职的员工，请联系部门管理员处理" });
                }
                sv.auditor_queues = JsonConvert.SerializeObject(queueList);
            }


            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(sv), SWITCH_VACATION_BILL_TYPE, userInfo.cardNo, sv.sys_no, string.Format("值班时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}", sv.duty_date_from, sv.duty_date_to), string.Format("调休时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}", sv.vacation_date_from, sv.vacation_date_to));
            if (result.suc) {
                try {
                    db.ei_SVApply.Add(sv);

                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(sv.sys_no);
                    return Json(new SimpleResultModel() { suc = false, msg = "申请提交失败，原因：" + ex.Message });
                }

                SVEmail(sv, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + sv.sys_no });
            }
            else {

                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckSVApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            var sv = db.ei_SVApply.Single(a => a.sys_no == sysNo);

            ViewData["auditStatus"] = new FlowSvrSoapClient().GetApplyResult(sysNo);

            WriteEventLog("调休申请单", "查看申请单：" + sysNo);

            ViewData["sv"] = sv;
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditSVApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("调休申请单", "进入审批" + sysNo + ";step:" + step);

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
        public JsonResult HandleSVApply(string sysNo, int step, bool isPass, string opinion)
        {
            var sv = db.ei_SVApply.Single(a => a.sys_no == sysNo);
            string formJson = JsonConvert.SerializeObject(sv);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                SVEmail(sv, result);
            }
            WriteEventLog("调休申请", "处理申请单:" + sysNo + ";step:" + step + ";isPass:" + isPass);
            return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
        }

        public void SVEmail(ei_SVApply sv, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(sv.sys_no);

            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }

                    //流程正常结束
                    subject = "调休申请单已" + (isSuc ? "批准" : "拒绝");
                    emailAddrs = GetUserEmailByCardNum(sv.applier_num);
                    names = sv.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + sv.sys_no + "】的调休申请单已被" + (isSuc ? "批准" : "拒绝") + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckSVApply?sysNo={0}'>内网用户点击此链接</a></div>", sv.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckSVApply?sysNo={0}'>外网用户点击此链接</a></div></div>", sv.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == sv.applier_num && u.wx_push_flow_info == true).ToList();
                    if (pushUsers.Count() > 0) {
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = sv.applier_num;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "调休申请流程";
                        pm.FKeyWord2 = sv.sys_no;
                        pm.FKeyWord3 = isSuc ? "审批通过" : "审批不通过";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckSVApply&param={2}", sv.applier_num, MyUtils.getMD5(sv.applier_num), sv.sys_no);
                        db.wx_pushMsg.Add(pm);
                        db.SaveChanges();
                    }
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待审批的调休申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + sv.sys_no + "】的调休申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditSVApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", sv.sys_no, result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditSVApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", sv.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
                            continue;
                        }
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项：" + sv.sys_no;
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "调休申请流程";
                        pm.FKeyWord2 = sv.applier_name;
                        pm.FKeyWord3 = ((DateTime)sv.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = string.Format("调休时间：{0:yyyy-MM-dd HH:mm}~{1:yyyy-MM-dd HH:mm}", sv.vacation_date_from, sv.vacation_date_to);
                        pm.FKeyWord5 = result.stepName;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审2";
                        pm.FRemark = "点击可进入审批此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditSVApply&param={2}", ad, MyUtils.getMD5(ad), sv.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }
                    db.SaveChanges();
                }

            }
            WriteEventLog("调休申请", sv.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        #endregion

        #region 设备故障维修流程 EP

        [SessionTimeOutFilter]
        public ActionResult BeginApplyEP()
        {
            var info = (from p in db.ei_epPrDeps
                        join e in db.ei_epEqDeps on p.eq_dep_id equals e.id
                        join eu in db.ei_epEqUsers on e.id equals eu.eq_dep_id
                        where e.is_forbit == false
                        //&& new string[] { "工程师", "经理" }.Contains(eu.job_position)
                        select new
                        {
                            prDepNo = p.dep_num,
                            prDepName = p.dep_name,
                            prDepChargerName = p.charger_name,
                            prDepChargerNum = p.charger_num,
                            eqDepChargerName = e.charger_name,
                            eqDepChargerNum = e.charger_num,
                            busDepName = p.bus_dep_name,
                            equitmentName = e.dep_name
                        }).Distinct().OrderBy(d => d.prDepName).ToList();

            ViewData["procDepInfo"] = JsonConvert.SerializeObject(info);
            ViewData["applierPhone"] = userInfoDetail.phone + (string.IsNullOrEmpty(userInfoDetail.shortPhone) ? "" : ("(短：" + userInfoDetail.shortPhone + ")"));
            ViewData["sysNum"] = GetNextSysNum(EQUITMENT_REPAIRE_BILL_TYPE);
            return View();
        }                

        [SessionTimeOutJsonFilter]
        public JsonResult SaveApplyEP(ei_epApply apply)
        {
            if (string.IsNullOrEmpty(apply.bus_dep_name)) {
                return Json(new SimpleResultModel() { suc = false, msg = "事业部必须填写" });
            }
            if (string.IsNullOrEmpty(apply.applier_phone)) {
                return Json(new SimpleResultModel() { suc = false, msg = "联系电话必须填写" });
            }
            if (string.IsNullOrEmpty(apply.produce_dep_name)) {
                return Json(new SimpleResultModel() { suc = false, msg = "生产车间必须选择" });
            }
            if (string.IsNullOrEmpty(apply.produce_dep_addr)) {
                return Json(new SimpleResultModel() { suc = false, msg = "车间位置必须填写" });
            }
            if (string.IsNullOrEmpty(apply.equitment_name)) {
                return Json(new SimpleResultModel() { suc = false, msg = "设备名称必须填写" });
            }
            if (string.IsNullOrEmpty(apply.equitment_modual)) {
                return Json(new SimpleResultModel() { suc = false, msg = "设备型号必须填写" });
            }
            if (string.IsNullOrEmpty(apply.equitment_supplier)) {
                return Json(new SimpleResultModel() { suc = false, msg = "设备供应商必须填写" });
            }
            if (string.IsNullOrEmpty(apply.property_type)) {
                return Json(new SimpleResultModel() { suc = false, msg = "固定资产类别是必须选择的" });
            }
            
            if (apply.emergency_level == 0 || apply.emergency_level == null) {
                return Json(new SimpleResultModel() { suc = false, msg = "紧急处理级别必须选择" });
            }
            if (string.IsNullOrEmpty(apply.faulty_situation)) {
                return Json(new SimpleResultModel() { suc = false, msg = "故障现象必须填写" });
            }

            if (apply.property_type.Equals("生产设备")) {
                if (string.IsNullOrEmpty(apply.property_number)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "资产编号必须填写" });
                }
                if (apply.property_number.Length > 4) {
                    if (db.vw_epReport.Where(e => e.property_number == apply.property_number && (e.apply_status == "未接单" || e.apply_status == "维修中")).Count() > 0) {
                        return Json(new SimpleResultModel() { suc = false, msg = "此固定资产编号[" + apply.property_number + "]存在未确认维修的报障申请，不能重复提交" });
                    }
                }
            }
            if (string.IsNullOrEmpty(apply.produce_dep_charger_no)) {
                return Json(new SimpleResultModel() { suc = false, msg = "生产部门主管不存在，请联系管理员" });                
            }

            apply.applier_name = userInfo.name;
            apply.applier_num = userInfo.cardNo;
            apply.report_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(
                JsonConvert.SerializeObject(apply),
                EQUITMENT_REPAIRE_BILL_TYPE,
                userInfo.cardNo,
                apply.sys_no,
                apply.equitment_name,
                apply.produce_dep_name
                );
            if (result.suc) {
                try {
                    db.ei_epApply.Add(apply);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(apply.sys_no);
                    return Json(new SimpleResultModel() { suc = false, msg = "申请提交失败，原因：" + ex.Message });
                }

                EPEmail(apply, result);
                return Json(new SimpleResultModel() { suc = true, msg = "申请编号是：" + apply.sys_no });
            }
            else {
                return Json(new SimpleResultModel() { suc = false, msg = "原因是：" + result.msg });
            }                        
        }

        //查看申请
        [SessionTimeOutFilter]
        public ActionResult CheckEPApply(string sysNo, string param)
        {
            if (string.IsNullOrEmpty(sysNo) & !string.IsNullOrEmpty(param)) {
                sysNo = param;
            }

            var ep = db.ei_epApply.Single(a => a.sys_no == sysNo);

            ViewData["auditStatus"] = new FlowSvrSoapClient().GetApplyResult(sysNo);

            WriteEventLog("设备故障报修", "查看申请单：" + sysNo);

            ViewData["ep"] = ep;
            return View();
        }

        [SessionTimeOutFilter]
        public ActionResult BeginAuditEPApply(string sysNo, int? step, string param)
        {
            if (string.IsNullOrEmpty(sysNo) && !string.IsNullOrEmpty(param)) {
                sysNo = param.Split(';')[0];
                step = int.Parse(param.Split(';')[1]);
            }
            WriteEventLog("设备故障报修", "进入审批" + sysNo + ";step:" + step);

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
            if (bam.stepName.Contains("处理")) {
                ViewData["ep"] = db.ei_epApply.Single(e => e.sys_no == sysNo);
            }
            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult HandleEPApply(FormCollection fc)
        {
            string sysNum = fc.Get("sysNum");
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");

            var ep = db.ei_epApply.Single(a => a.sys_no == sysNum);            
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            if (stepName.Contains("接单")) {
                bool pass = bool.Parse(fc.Get("pass"));
                string comment = fc.Get("comment");
                string formJson = JsonConvert.SerializeObject(ep);
                var result = flow.BeginAudit(sysNum, step, userInfo.cardNo, pass, comment, formJson);
                if (result.suc) {
                    if (pass) {
                        ep.accept_time = DateTime.Now;
                        ep.accept_user_name = userInfo.name;
                        ep.accept_user_no = userInfo.cardNo;
                        db.SaveChanges();
                        WriteEventLog("设备故障报修", "维修接单:" + sysNum + ";step:" + step);
                        //接单后通知申请者
                        EPAcceptedInform(ep);
                    }
                    else {
                        //发送拒接结果给申请者
                        EPEmail(ep, result);
                    }
                }
                return Json(new SimpleResultModel() { suc = result.suc, msg = result.msg });
            }
            else if (stepName.Contains("处理")) {
                ep.transfer_to_repairer = "";//每次都先清空
                MyUtils.SetFieldValueToModel(fc, ep);
                if (!string.IsNullOrEmpty(ep.transfer_to_repairer)) {
                    ep.transfer_to_repairer = GetUserCardByNameAndCardNum(ep.transfer_to_repairer);
                    if (userInfo.cardNo.Equals(ep.transfer_to_repairer)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "不能转给自己处理" });
                    }
                    ep.confirm_later_flag = false;//如果转移了，就不能再延迟处理
                    WriteEventLog("设备故障报修", "转移处理：" + ep.sys_no + ":" + ep.transfer_to_repairer);
                }
                else if (ep.confirm_later_flag == true) {
                    if (string.IsNullOrEmpty(ep.confirm_later_reason)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "延迟处理原因是必填的" });
                    }
                    ep.confirm_time = null;
                    db.SaveChanges();
                    WriteEventLog("设备故障报修", "延迟处理：" + ep.sys_no);
                    return Json(new SimpleResultModel() { suc = true, msg = "延迟处理成功" });
                }
                else {
                    if (string.IsNullOrEmpty(ep.faulty_reason)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【故障原因】必须填写" });
                    }
                    if (string.IsNullOrEmpty(ep.faulty_type)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【故障原因类别】必须选择" });
                    }
                    if (string.IsNullOrEmpty(ep.repair_method)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【修理方法】必须填写" });
                    }
                    if (string.IsNullOrEmpty(ep.repair_result)) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【修理结果】必须填写" });
                    }
                    if (ep.confirm_time == null) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【处理完成时间】必须填写" });
                    }
                    if (ep.confirm_time <= ep.accept_time) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【处理完成时间】必须晚于【接单时间】" });
                    }
                    if (ep.confirm_time > DateTime.Now) {
                        return Json(new SimpleResultModel() { suc = false, msg = "【处理完成时间】不能晚于当前时间" });
                    }
                    ep.confirm_register_time = DateTime.Now;
                    ep.confirm_user_name = userInfo.name;
                    ep.confirm_user_no = userInfo.cardNo;
                }
                string formJson = JsonConvert.SerializeObject(ep);

                var result = flow.BeginAudit(sysNum, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    EPEmail(ep, result);
                }
                WriteEventLog("设备故障报修", "维修处理:" + sysNum + ";step:" + step);
                return Json(new SimpleResultModel() { suc = true, msg = result.msg });
            }
            else if (stepName.Contains("评价")) {
                int score = Int32.Parse(fc.Get("evaluationScore"));
                string evaluationContent = fc.Get("evaluationContent");

                if (score == 0 && string.IsNullOrEmpty(evaluationContent)) {
                    return Json(new SimpleResultModel() { suc = false, msg = "评价为不满意的，请在评价内容里面注明原因" });
                }

                ep.evaluation_score = score;
                ep.evaluation_content = evaluationContent;
                ep.evaluation_time = DateTime.Now;

                string formJson = JsonConvert.SerializeObject(ep);

                var result = flow.BeginAudit(sysNum, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    EPEmail(ep, result);
                    if (score == 0) {
                        EPUnsatisfyInform(ep);
                    }
                }
                WriteEventLog("设备故障报修", "服务评价:" + sysNum + ";step:" + step);
                return Json(new SimpleResultModel() { suc = true, msg = result.msg });

            }
            else if (stepName.Contains("评分")) {
                int score = Int32.Parse(fc.Get("difficultyScore"));

                ep.difficulty_score = score;
                ep.grade_time = DateTime.Now;

                string formJson = JsonConvert.SerializeObject(ep);

                var result = flow.BeginAudit(sysNum, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    EPEmail(ep, result);                    
                }
                WriteEventLog("设备故障报修", "难度评分:" + sysNum + ";step:" + step);
                return Json(new SimpleResultModel() { suc = true, msg = result.msg });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        /// <summary>
        /// 评价不满意，需要推送给维修人员、设备经理和设备分部长
        /// </summary>
        /// <param name="ep"></param>
        public void EPUnsatisfyInform(ei_epApply ep)
        {
            List<string> users = new List<string>() { ep.confirm_user_no, ep.equitment_dep_charger_no };
            var eqDep = db.ei_epEqDeps.Where(e => e.dep_name == ep.equitment_dep_name).FirstOrDefault();
            if (eqDep != null) {
                users.Add(eqDep.minister_num);
            }
            foreach (var user in users) {
                var pushUser = db.vw_push_users.Where(u => u.card_number == user && u.wx_push_flow_info == true).FirstOrDefault();
                if (pushUser!=null) {
                    wx_pushMsg pm = new wx_pushMsg();
                    pm.FCardNumber = user;
                    pm.FFirst = "有一张维修工单被评价为不满意";
                    pm.FHasSend = false;
                    pm.FInTime = DateTime.Now;
                    pm.FkeyWord1 = "设备故障报修申请流程";
                    pm.FKeyWord2 = ep.sys_no;
                    pm.FKeyWord3 = "不满意，原因：" + ep.evaluation_content;
                    pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    pm.FOpenId = pushUser.wx_openid;
                    pm.FPushType = "办结";
                    pm.FRemark = "点击可查看详情";
                    pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckEPApply&param={2}", user, MyUtils.getMD5(user), ep.sys_no);
                    db.wx_pushMsg.Add(pm);                    
                }
            }
            db.SaveChanges();
        }

        /// <summary>
        /// 被接单后推送通知给申请人
        /// </summary>
        /// <param name="ep"></param>
        public void EPAcceptedInform(ei_epApply ep)
        {
            var applier = db.vw_push_users.Where(u => u.card_number == ep.applier_num && u.wx_push_flow_info == true).ToList();
            if (applier.Count() > 0) {
                var pushUser = applier.First();
                wx_pushMsg pm = new wx_pushMsg();
                pm.FCardNumber = ep.applier_num;
                pm.FFirst = "维修人员已接单，请耐心等待处理";
                pm.FHasSend = false;
                pm.FInTime = DateTime.Now;
                pm.FkeyWord1 = ep.sys_no;
                pm.FKeyWord2 = ep.equitment_name;
                pm.FKeyWord3 = ep.accept_user_name;
                pm.FOpenId = pushUser.wx_openid;
                pm.FPushType = "接单通知";
                pm.FRemark = "点击可查看详情";
                pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckEPApply&param={2}", ep.applier_num, MyUtils.getMD5(ep.applier_num), ep.sys_no);
                db.wx_pushMsg.Add(pm);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 发送微信推送和邮件
        /// </summary>
        /// <param name="ep"></param>
        /// <param name="model"></param>
        public void EPEmail(ei_epApply ep, FlowResultModel model)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.GetCurrentStep(ep.sys_no);
            
            string subject = "", emailAddrs = "", content = "", names = "", ccEmails = "";
            //处理成功才发送邮件
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = true;
                    if (model.msg.Contains("NG")) {
                        isSuc = false;
                    }

                    //流程正常结束
                    subject = "设备故障报修申请已处理完成";
                    emailAddrs = GetUserEmailByCardNum(ep.applier_num);
                    names = ep.applier_name;
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你申请的单号为【" + ep.sys_no + "】的设备故障报修申请单" + (isSuc ? "已处理完毕" : "已被拒接") + "，请知悉。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可查看此申请单详情。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/CheckEPApply?sysNo={0}'>内网用户点击此链接</a></div>", ep.sys_no);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/CheckEPApply?sysNo={0}'>外网用户点击此链接</a></div></div>", ep.sys_no);

                    var pushUsers = db.vw_push_users.Where(u => u.card_number == ep.applier_num && u.wx_push_flow_info == true).ToList();
                    if (pushUsers.Count() > 0) {
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ep.applier_num;
                        pm.FFirst = "你有一张申请单已审批完成";
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "设备故障报修申请流程";
                        pm.FKeyWord2 = ep.sys_no;
                        pm.FKeyWord3 = isSuc ? "处理完成" : "处理失败";
                        pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "办结";
                        pm.FRemark = "点击可查看详情";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckEPApply&param={2}", ep.applier_num, MyUtils.getMD5(ep.applier_num), ep.sys_no);
                        db.wx_pushMsg.Add(pm);
                        db.SaveChanges();
                    }
                }
                else if (!string.IsNullOrEmpty(model.nextAuditors)) {
                    //流转到下一环节
                    subject = "你有一张待处理的设备故障报修申请单" + (model.msg.Contains("撤销") ? "(撤销)" : "");
                    emailAddrs = GetUserEmailByCardNum(model.nextAuditors);
                    names = GetUserNameByCardNum(model.nextAuditors);
                    content = "<div>" + names + ",你好：</div>";
                    content += "<div style='margin-left:30px;'>你有一张待处理的单号为【" + ep.sys_no + "】的设备故障报修申请单，请尽快登陆系统处理。</div>";
                    content += "<div style='clear:both'><br/>单击以下链接可进入系统审核这张单据。</div>";
                    content += string.Format("<div><a href='http://192.168.90.100/Emp/Apply/BeginAuditEPApply?sysNo={0}&step={1}'>内网用户点击此链接</a></div>", ep.sys_no, result.step);
                    content += string.Format("<div><a href='http://emp.truly.com.cn/Emp/Apply/BeginAuditEPApply?sysNo={0}&step={1}'>外网用户点击此链接</a></div></div>", ep.sys_no, result.step);

                    //微信推送
                    foreach (var ad in model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                        var pushUsers = db.vw_push_users.Where(u => u.card_number == ad && u.wx_push_flow_info == true).ToList();
                        if (pushUsers.Count() == 0) {
                            continue;
                        }
                        var pushUser = pushUsers.First();
                        wx_pushMsg pm = new wx_pushMsg();
                        pm.FCardNumber = ad;
                        pm.FFirst = "你有一张待审批事项：" + ep.sys_no;
                        pm.FHasSend = false;
                        pm.FInTime = DateTime.Now;
                        pm.FkeyWord1 = "设备故障报修申请流程";
                        pm.FKeyWord2 = ep.applier_name;
                        pm.FKeyWord3 = ((DateTime)ep.report_time).ToString("yyyy-MM-dd HH:mm:ss");
                        pm.FKeyWord4 = string.Format("生产车间：{0}；设备名称：{1}；影响停产程度：{2}", ep.produce_dep_name, ep.equitment_name, ((EmergencyEnum)ep.emergency_level).ToString());
                        pm.FKeyWord5 = result.stepName;
                        pm.FOpenId = pushUser.wx_openid;
                        pm.FPushType = "待审2";
                        pm.FRemark = "点击可进入处理此申请";
                        pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=BeginAuditEPApply&param={2}", ad, MyUtils.getMD5(ad), ep.sys_no + ";" + result.step);
                        db.wx_pushMsg.Add(pm);
                    }

                    db.SaveChanges();
                }

            }
            WriteEventLog("设备故障报修", ep.sys_no + ">发送通知邮件，地址：" + emailAddrs + ";content:" + content);

            MyEmail.SendEmail(subject, emailAddrs, content, ccEmails);
        }

        #endregion


        #region 所有流程共享方法 除了宿舍维修流程

        //单据类别名称
        private string GetBillName(string billType)
        {
            switch (billType) {
                case "AL":
                    return "请假申请单";
                case "BT":
                    return "出差申请单";
                case "SA":
                    return "仓管权限申请";
                case "UC":
                    return "非正常时间出货申请";
                case "SV":
                    return "调休申请";
                case "CR":
                    return "考勤补登记";
                case "EP":
                    return "设备故障报修单";
                default:
                    return "";
            }
        }

        // 主界面
        [SessionTimeOutFilter]
        public ActionResult ApplyIndex(string billType)
        {
            //FlowSvrSoapClient client = new FlowSvrSoapClient();
            //int myDealingApplyCount = client.GetMyDealingApplyCount(userInfo.cardNo, new ArrayOfString() { billType });

            //ViewData["dealingCount"] = myDealingApplyCount;
            var auts = (from a in db.ei_authority
                        from g in db.ei_groups
                        from gu in g.ei_groupUser
                        from ga in g.ei_groupAuthority
                        where ga.authority_id == a.id
                        && gu.user_id == userInfo.id
                        select a.en_name).Distinct().ToArray();
            string autStr = string.Join(",", auts);

            ViewData["autStr"] = autStr;
            ViewData["billType"] = billType;
            ViewData["billName"] = GetBillName(billType);
            WriteEventLog(billType, "打开主界面");

            return View();
        }

        [SessionTimeOutJsonFilter]
        public JsonResult GetMyDealingApplyCount(string billType)
        {
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            int myDealingApplyCount = client.GetMyDealingApplyCount(userInfo.cardNo, new ArrayOfString() { billType });

            return Json(new { suc = true, count = myDealingApplyCount });
        }

        //开始申请
        [SessionTimeOutFilter]
        public ActionResult BeginApply(string billType)
        {
            return RedirectToAction("BeginApply" + billType);
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

            WriteEventLog(billType, "打开我申请的界面");
            return View();
        }

        //查看申请详情
        [SessionTimeOutFilter]
        public ActionResult CheckApply(string sysNo)
        {
            string billType = sysNo.Substring(0, 2);
            string actionName = "Check" + billType + "Apply";
            return RedirectToAction(actionName, new { sysNo = sysNo });
        }

        //我的待办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditingList(string billType)
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 0 }, new ArrayOfString() { billType }, 400).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));
            ViewData["list"] = list;
            ViewData["billType"] = billType;
            WriteEventLog(billType, "打开我的待办界面");
            
            return View();
        }        

        //开始审批
        [SessionTimeOutFilter]
        public ActionResult BeginAuditApply(string sysNo, int? step)
        {
            string billType = sysNo.Substring(0, 2);
            string actionName = "BeginAudit" + billType + "Apply";            
            return RedirectToAction(actionName, new { sysNo = sysNo, step = step });
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
            
            ViewData["list"] = list;
            ViewData["billType"] = billType;
            ViewData["fromDate"] = fromDate;
            ViewData["toDate"] = toDate;
            ViewData["sysNo"] = sysNo;
            ViewData["cardNo"] = cardNo;

            WriteEventLog(billType, "打开我的已办界面");
            return View("GetMyAuditedList");
        }

        //撤销申请
        [SessionTimeOutJsonFilter]
        public JsonResult AbortApply(string sysNo, string reason = "")
        {
            string billType = sysNo.Substring(0, 2);

            if ("AL".Equals(billType)) {
                //return RedirectToAction("AbortALApply", new { sysNo = sysNo, reason = reason });
                return AbortALApply(sysNo, reason);
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;

            if (flow.ApplyHasBeenAudited(sysNo)) {
                var auditStatus = flow.GetApplyResult(sysNo);                
                return Json(new SimpleResultModel() { suc = false, msg = "不能撤销，当前状态是：" + auditStatus });                
            }
            else {
                //流程还未审批可以直接撤销
                result = flow.AbortFlow(userInfo.cardNo, sysNo);
            }

            WriteEventLog(billType, "撤销、中止流程：" + sysNo);
            
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

        #endregion

    }
}
