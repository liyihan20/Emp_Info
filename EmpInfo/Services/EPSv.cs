using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    public class EPSv:BillSv,IBeginAuditOtherInfo
    {
        ei_epApply bill;
        public EPSv() { }
        public EPSv(string sysNo)
        {
            bill = db.ei_epApply.Single(e => e.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "EP"; }
        }

        public override string BillTypeName
        {
            get { return "设备故障报修单"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditEPApply";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo,UserInfoDetail userInfoDetail)
        {
            EPBeforeApplyModel eb = new EPBeforeApplyModel();
            var info = (from p in db.ei_epPrDeps
                        join e in db.ei_epEqDeps on p.eq_dep_id equals e.id
                        join eu in db.ei_epEqUsers on e.id equals eu.eq_dep_id
                        where e.is_forbit == false                        
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
            eb.applierPhone = userInfoDetail.phone + (string.IsNullOrEmpty(userInfoDetail.shortPhone) ? "" : ("(短：" + userInfoDetail.shortPhone + ")"));
            eb.procDepInfo = JsonConvert.SerializeObject(info);
            eb.sysNum = GetNextSysNum(BillType);
            return eb;
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("EQDepManagement", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "设备部门维护",
                    iconFont = "fa-gear",
                    url = "../EP/EquitmentDeps"
                });
            }
            if (HasGotPower("EPReport", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/EPReport"
                });
            }

            return menus;

        }
        
        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_epApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            if (string.IsNullOrEmpty(bill.bus_dep_name)) {
                throw new Exception("事业部必须填写");
            }
            if (string.IsNullOrEmpty(bill.applier_phone)) {
                throw new Exception("联系电话必须填写" );
            }
            if (string.IsNullOrEmpty(bill.produce_dep_name)) {
                throw new Exception("生产车间必须选择");
            }
            if (string.IsNullOrEmpty(bill.produce_dep_addr)) {
                throw new Exception("车间位置必须填写");
            }
            if (string.IsNullOrEmpty(bill.equitment_name)) {
                throw new Exception("设备名称必须填写");
            }
            if (string.IsNullOrEmpty(bill.equitment_modual)) {
                throw new Exception("设备型号必须填写");
            }
            if (string.IsNullOrEmpty(bill.equitment_supplier)) {
                throw new Exception("设备供应商必须填写");
            }
            if (string.IsNullOrEmpty(bill.property_type)) {
                throw new Exception("固定资产类别是必须选择的");
            }

            if (bill.emergency_level == 0 || bill.emergency_level == null) {
                throw new Exception("紧急处理级别必须选择");
            }
            if (string.IsNullOrEmpty(bill.faulty_situation)) {
                throw new Exception("故障现象必须填写");
            }

            if (bill.property_type.Equals("生产设备")) {
                if (string.IsNullOrEmpty(bill.property_number)) {
                    throw new Exception("资产编号必须填写");
                }
                if (bill.property_number.Length > 4) {
                    if (db.vw_epReport.Where(e => e.property_number == bill.property_number && (e.apply_status == "未接单" || e.apply_status == "维修中")).Count() > 0) {
                        throw new Exception("此固定资产编号[" + bill.property_number + "]存在未确认维修的报障申请，不能重复提交");
                    }
                }
            }
            if (string.IsNullOrEmpty(bill.produce_dep_charger_no)) {
                throw new Exception("生产部门主管不存在，请联系管理员");
            }

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.report_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(
                JsonConvert.SerializeObject(bill),
                BillType,
                userInfo.cardNo,
                bill.sys_no,
                bill.equitment_name,
                bill.produce_dep_name
                );
            if (result.suc) {
                try {
                    db.ei_epApply.Add(bill);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message );
                }

                SendNotification(result);
            }
            else {
                throw new Exception("原因是：" + result.msg );
            }
        }

        public override object GetBill()
        {
            return bill;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {            
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            if (stepName.Contains("接单")) {
                bool pass = bool.Parse(fc.Get("pass"));
                string comment = fc.Get("comment");
                string formJson = JsonConvert.SerializeObject(bill);
                var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, pass, comment, formJson);
                if (result.suc) {
                    if (pass) {
                        bill.accept_time = DateTime.Now;
                        bill.accept_user_name = userInfo.name;
                        bill.accept_user_no = userInfo.cardNo;
                        db.SaveChanges();                        
                        //接单后通知申请者
                        EPAcceptedInform();
                    }
                    else {
                        //发送拒接结果给申请者
                        SendNotification(result);
                    }
                }
                return new SimpleResultModel() { suc = result.suc, msg = result.msg };
            }
            else if (stepName.Contains("处理")) {
                bill.transfer_to_repairer = "";//每次都先清空
                MyUtils.SetFieldValueToModel(fc, bill);
                if (!string.IsNullOrEmpty(bill.transfer_to_repairer)) {
                    bill.transfer_to_repairer = GetUserCardByNameAndCardNum(bill.transfer_to_repairer);
                    if (userInfo.cardNo.Equals(bill.transfer_to_repairer)) {
                        return new SimpleResultModel() { suc = false, msg = "不能转给自己处理" };
                    }
                    bill.confirm_later_flag = false;//如果转移了，就不能再延迟处理
                }
                else if (bill.confirm_later_flag == true) {
                    if (string.IsNullOrEmpty(bill.confirm_later_reason)) {
                        return new SimpleResultModel() { suc = false, msg = "延迟处理原因是必填的" };
                    }
                    bill.confirm_time = null;
                    db.SaveChanges();                    
                    return new SimpleResultModel() { suc = true, msg = "延迟处理成功" };
                }
                else {
                    if (string.IsNullOrEmpty(bill.faulty_reason)) {
                        return new SimpleResultModel() { suc = false, msg = "【故障原因】必须填写" };
                    }
                    if (string.IsNullOrEmpty(bill.faulty_type)) {
                        return new SimpleResultModel() { suc = false, msg = "【故障原因类别】必须选择" };
                    }
                    if (string.IsNullOrEmpty(bill.repair_method)) {
                        return new SimpleResultModel() { suc = false, msg = "【修理方法】必须填写" };
                    }
                    if (string.IsNullOrEmpty(bill.repair_result)) {
                        return new SimpleResultModel() { suc = false, msg = "【修理结果】必须填写" };
                    }
                    if (bill.confirm_time == null) {
                        return new SimpleResultModel() { suc = false, msg = "【处理完成时间】必须填写" };
                    }
                    if (bill.confirm_time <= bill.accept_time) {
                        return new SimpleResultModel() { suc = false, msg = "【处理完成时间】必须晚于【接单时间】" };
                    }
                    if (bill.confirm_time > DateTime.Now) {
                        return new SimpleResultModel() { suc = false, msg = "【处理完成时间】不能晚于当前时间" };
                    }
                    bill.confirm_register_time = DateTime.Now;
                    bill.confirm_user_name = userInfo.name;
                    bill.confirm_user_no = userInfo.cardNo;
                }
                string formJson = JsonConvert.SerializeObject(bill);

                var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    SendNotification(result);
                }                
                return new SimpleResultModel() { suc = true, msg = result.msg };
            }
            else if (stepName.Contains("评价")) {
                int score = Int32.Parse(fc.Get("evaluationScore"));
                string evaluationContent = fc.Get("evaluationContent");

                if (score == 0 && string.IsNullOrEmpty(evaluationContent)) {
                    return new SimpleResultModel() { suc = false, msg = "评价为不满意的，请在评价内容里面注明原因" };
                }

                bill.evaluation_score = score;
                bill.evaluation_content = evaluationContent;
                bill.evaluation_time = DateTime.Now;

                string formJson = JsonConvert.SerializeObject(bill);

                var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    SendNotification(result);
                    if (score == 0) {
                        EPUnsatisfyInform();
                    }
                }                
                return new SimpleResultModel() { suc = true, msg = result.msg };

            }
            else if (stepName.Contains("评分")) {
                int score = Int32.Parse(fc.Get("difficultyScore"));

                bill.difficulty_score = score;
                bill.grade_time = DateTime.Now;

                string formJson = JsonConvert.SerializeObject(bill);

                var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, true, "", formJson);
                if (result.suc) {
                    db.SaveChanges();
                    //发送通知到下一级审核人
                    SendNotification(result);
                }
                
                return new SimpleResultModel() { suc = true, msg = result.msg };
            }
            return new SimpleResultModel() { suc = true };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "处理完成" : "被拒接"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "处理完成" : "被拒接")),
                        GetUserEmailByCardNum(bill.applier_num)
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "处理完成" : "被拒接"),
                        db.vw_push_users.Where(v => v.card_number == bill.applier_num).FirstOrDefault()
                        );
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    SendEmailToNextAuditor(
                        bill.sys_no,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no,BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    SendWxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.report_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("生产车间：{0}；设备名称：{1}；影响停产程度：{2}", bill.produce_dep_name, bill.equitment_name, ((EmergencyEnum)bill.emergency_level).ToString()),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        /// <summary>
        /// 评价不满意，需要推送给维修人员、设备经理和设备分部长
        /// </summary>
        /// <param name="ep"></param>
        public void EPUnsatisfyInform()
        {
            List<string> users = new List<string>() { bill.confirm_user_no, bill.equitment_dep_charger_no };
            var eqDep = db.ei_epEqDeps.Where(e => e.dep_name == bill.equitment_dep_name).FirstOrDefault();
            if (eqDep != null) {
                users.Add(eqDep.minister_num);
            }
            foreach (var user in users) {
                var pushUser = db.vw_push_users.Where(u => u.card_number == user && u.wx_push_flow_info == true).FirstOrDefault();
                if (pushUser != null) {
                    wx_pushMsg pm = new wx_pushMsg();
                    pm.FCardNumber = user;
                    pm.FFirst = "有一张维修工单被评价为不满意";
                    pm.FHasSend = false;
                    pm.FInTime = DateTime.Now;
                    pm.FkeyWord1 = "设备故障报修申请流程";
                    pm.FKeyWord2 = bill.sys_no;
                    pm.FKeyWord3 = "不满意，原因：" + bill.evaluation_content;
                    pm.FKeyWord4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    pm.FOpenId = pushUser.wx_openid;
                    pm.FPushType = "办结";
                    pm.FRemark = "点击可查看详情";
                    pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckApply&param={2}", user, MyUtils.getMD5(user), bill.sys_no);
                    db.wx_pushMsg.Add(pm);
                }
            }
            db.SaveChanges();
        }

        /// <summary>
        /// 被接单后推送通知给申请人
        /// </summary>
        /// <param name="ep"></param>
        public void EPAcceptedInform()
        {
            var applier = db.vw_push_users.Where(u => u.card_number == bill.applier_num && u.wx_push_flow_info == true).ToList();
            if (applier.Count() > 0) {
                var pushUser = applier.First();
                wx_pushMsg pm = new wx_pushMsg();
                pm.FCardNumber = bill.applier_num;
                pm.FFirst = "维修人员已接单，请耐心等待处理";
                pm.FHasSend = false;
                pm.FInTime = DateTime.Now;
                pm.FkeyWord1 = bill.sys_no;
                pm.FKeyWord2 = bill.equitment_name;
                pm.FKeyWord3 = bill.accept_user_name;
                pm.FOpenId = pushUser.wx_openid;
                pm.FPushType = "接单通知";
                pm.FRemark = "点击可查看详情";
                pm.FUrl = string.Format("http://emp.truly.com.cn/Emp/WX/WIndex?cardnumber={0}&secret={1}&controllerName=Apply&actionName=CheckApply&param={2}", bill.applier_num, MyUtils.getMD5(bill.applier_num), bill.sys_no);
                db.wx_pushMsg.Add(pm);
                db.SaveChanges();
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return bill;
        }


    }
}