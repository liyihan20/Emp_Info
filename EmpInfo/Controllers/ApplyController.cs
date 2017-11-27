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
            List<FlowMyAppliesModel> list = flow.GetMyApplyList(userInfo.cardNo, lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), "", new ArrayOfString() { "DP" }, "", "", 10, 20).ToList();
            ViewData["list"] = list;

            WriteEventLog("宿舍维修申请", "打开我申请的界面");
            return View();
        }

        //我的代办
        [SessionTimeOutFilter]
        public ActionResult GetMyAuditingDPList()
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", "", "", new ArrayOfInt() { 0 }, new ArrayOfInt() { 10 }, new ArrayOfString() { "DP" }, 100).ToList();
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
            var list = flow.GetAuditList(userInfo.cardNo, "", "", "", "", lastYear.ToShortDateString(), DateTime.Now.ToShortDateString(), new ArrayOfInt() { 1,-1 }, new ArrayOfInt() { 10 }, new ArrayOfString() { "DP" }, 100).ToList();
            list.ForEach(l => l.applier = GetUserNameByCardNum(l.applier));

            ViewData["list"] = list;
            WriteEventLog("宿舍维修申请", "打开我的已办界面");
            return View();
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
            form.fee_share_peple = GetUserNameByCardNum(form.fee_share_peple);

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

    }
}
