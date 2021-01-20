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
    public class ALSv:BillSv,IApplyEntryQueue
    {
        ei_askLeave bill;

        public ALSv() { }

        public ALSv(string sysNo)
        {
            bill = db.ei_askLeave.Single(a => a.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "AL"; }
        }

        public override string BillTypeName
        {
            get { return "请假申请单"; }
        }

        /// <summary>
        /// 我申请的界面，请假流程因为完成后也可以撤销，所以独立出来
        /// </summary>
        /// <returns></returns>
        //public override string GetMyAppliesViewName()
        //{
        //    return "GetMyALApplyList";
        //}

        public override string AuditViewName()
        {
            return "BeginAuditALApply";
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var list = base.GetApplyMenuItems(userInfo);
            if (db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo)).Count() > 0) {
                list.Add(new ApplyMenuItemModel()
                {
                    text="请假报表",
                    iconFont="fa-file-text-o",
                    url = "../Report/ALReport",
                    linkClass="screen_900"
                });
            }
            return list;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            ALBeforeApplyModel ba = new ALBeforeApplyModel();
            try {
                ba.vacationDaysLeft = db.GetVacationDaysLeftProc(userInfo.cardNo).First();                
            }
            catch(Exception ex) {
                throw new Exception("计算本年度剩余年休假时出现错误，请联系部门负责做考勤的文员确认，错误信息：" + ex.Message);
            }
            var appliedBills = db.ei_askLeave.Where(a => a.applier_num == userInfo.cardNo).ToList();
            if (appliedBills.Count() > 0) {
                var ab = appliedBills.OrderByDescending(a => a.id).First();
                if (ab.dep_long_name.Equals(GetDepLongNameByNum(ab.dep_no))) {
                    ba.depName = ab.dep_long_name;
                    ba.depNum = ab.dep_no;
                    ba.depId = ab.dep_id;
                    ba.empLevel = ab.emp_level;
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
            ba.days = days;
            ba.hours = hours;
            ba.times = vwLeaveDays.Count();

            ba.sysNum = GetNextSysNum(BillType, 4);
            ba.pLevels = JsonConvert.SerializeObject(db.ei_empLevel.OrderBy(e => e.level_no).ToList());

            return ba;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
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
                    throw new Exception("存在子级部门，请展开部门文件夹继续选择");
                }
            }

            //处理一下审核队列,将姓名（厂牌）格式更换为厂牌
            if (!string.IsNullOrEmpty(al.auditor_queues)) {
                var queueList = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(al.auditor_queues);
                queueList.ForEach(q => q.auditors = GetUserCardByNameAndCardNum(q.auditors));
                if (queueList.Where(q => q.auditors.Contains("离职")).Count() > 0) {
                    throw new Exception("流程审核人中存在已离职的员工，请联系部门管理员处理");
                }
                al.auditor_queues = JsonConvert.SerializeObject(queueList);
            }

            if (al.to_date == null || al.from_date == null) {
                throw new Exception("请假日期不合法");
            }
            else if (al.to_date <= al.from_date) {
                throw new Exception("请检查请假期间");
            }
            else if ((((DateTime)al.to_date) - ((DateTime)al.from_date)).TotalDays > 300) {
                throw new Exception("请假开始时间和结束时间的日期跨度不能超过300天");
            }

            if (al.leave_type.Equals("年假") && ((DateTime)al.to_date).Year != ((DateTime)al.from_date).Year) {
                throw new Exception("不能跨年度休年假，请分开请假");
            }
            
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(al), BillType, userInfo.cardNo, al.sys_no, al.leave_type, ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + "~" + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm"));
            if (result.suc) {
                try {
                    db.ei_askLeave.Add(al);

                    //将部门保存到用户表
                    var user = db.ei_users.Single(e => e.card_number == al.applier_num);
                    user.dep_long_name = al.dep_long_name;
                    user.dep_no = al.dep_no;

                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(al.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }

                bill = al;
                SendNotification(result);
            }
            else {
                throw new Exception("原因是：" + result.msg);
            }         
        }

        public List<flow_applyEntryQueue> GetApplyEntryQueue(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            ei_askLeave al = new ei_askLeave();
            MyUtils.SetFieldValueToModel(fc, al);
            al.inform_man = GetUserCardByNameAndCardNum(al.inform_man);
            al.agent_man = GetUserCardByNameAndCardNum(al.agent_man);
            al.applier_name = userInfo.name;
            al.applier_num = userInfo.cardNo;
            al.apply_time = DateTime.Now;

            if (al.to_date == null || al.from_date == null) {
                throw new Exception("请假日期不合法");
            }
            else if (al.to_date <= al.from_date) {
                throw new Exception("请检查请假期间");
            }

            var daySpan = ((DateTime)al.to_date - (DateTime)al.from_date).Days;
            if (daySpan - al.work_days > 4 || al.work_days - daySpan > 1) {
                throw new Exception("请检查请假期间和请假天数是否对应");
            }

            if (al.leave_type.Equals("年假") && ((DateTime)al.to_date).Year != ((DateTime)al.from_date).Year) {
                throw new Exception("不能跨年度休年假，请分开请假" );
            }

            if (!string.IsNullOrEmpty(al.agent_man)) {
                if (string.IsNullOrEmpty(GetUserEmailByCardNum(al.agent_man))) {
                    throw new Exception("代理人邮箱没有设置");
                }
            }
            foreach (var im in al.inform_man.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (string.IsNullOrEmpty(GetUserEmailByCardNum(im))) {
                    throw new Exception("知会人（" + GetUserNameAndCardByCardNum(im) + "）邮箱没有设置");
                }
            }
            var c = al.is_continue;
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.GetFlowQueue(JsonConvert.SerializeObject(al), BillType);
            List<flow_applyEntryQueue> queue = null;

            if (result.suc) {
                queue = JsonConvert.DeserializeObject<List<flow_applyEntryQueue>>(result.msg);
                queue.ForEach(q => q.auditors = GetUserNameAndCardByCardNum(q.auditors));
                return queue;
            }
            else {
                throw new Exception(result.msg);
            }
        }
                
        public override SimpleResultModel AbortApply(UserInfo userInfo, string sysNo, string reason = "")
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;

            if (flow.ApplyHasBeenAudited(sysNo)) {
                var auditStatus = flow.GetApplyResult(sysNo);
                if (auditStatus.Contains("审批中")) {
                    return new SimpleResultModel() { suc = false, msg = "流程正在审批中，不能撤销，请联系当前处理人NG" };
                }
                else if (auditStatus.Contains("撤销")) {
                    return new SimpleResultModel() { suc = false, msg = "流程已经撤销，不能再次操作" };
                }
                else if (auditStatus.Contains("拒绝")) {
                    return new SimpleResultModel() { suc = false, msg = "流程已被拒绝，不需要再撤销" };
                }
                else if (auditStatus.Contains("通过")) {
                    if (db.ei_askLeave.Where(a => a.sys_no == sysNo && a.from_date < DateTime.Now).Count() > 0) {
                        return new SimpleResultModel() { suc = false, msg = "当前请假时间已生效，不能撤销" };
                    }
                    else {
                        result = flow.AbortAfterFinish(sysNo, reason);
                    }
                }
                else {
                    return new SimpleResultModel() { suc = false, msg = "不能撤销，当前状态是：" + auditStatus };
                }
            }
            else {
                //流程还未审批可以直接撤销
                result = flow.AbortFlow(userInfo.cardNo, sysNo);
            }

            //ALEmail(al, result);
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override object GetBill()
        {
            ALApplyModel am = new ALApplyModel(bill);

            am.applierNameAndCard = bill.applier_name + "(" + bill.applier_num + ")";
            am.agentMan = GetUserNameAndCardByCardNum(bill.agent_man);
            am.informMan = GetUserNameAndCardByCardNum(bill.inform_man);
            am.empLevel = db.ei_empLevel.Single(e => e.level_no == bill.emp_level).level_name;
            am.account = db.ei_users.Where(u => u.card_number == bill.applier_num).FirstOrDefault().salary_no;

            return am;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            string sysNo, opinion;
            int step;
            bool isPass;

            try {
                sysNo = fc.Get("sysNo");
                step = Int32.Parse(fc.Get("step"));
                isPass = bool.Parse(fc.Get("isPass"));
                opinion = fc.Get("opinion");
            }
            catch (Exception ex) {
                return new SimpleResultModel() { suc = false, msg = "参数错误：" + ex.Message };
            }

            string formJson = JsonConvert.SerializeObject(bill);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(sysNo, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                SendNotification(result);
            }
            
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }
                        
        public List<ALRecordModel> GetLeaveRecordsInOneYear(string applierNameAndCard)
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
            return list;
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已被{2}，请假时间是：{3}~{4}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "拒绝"), ((DateTime)bill.from_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)bill.to_date).ToString("yyyy-MM-dd HH:mm")),
                        GetUserEmailByCardNum(bill.applier_num),
                        isSuc ? GetUserEmailByCardNum(bill.inform_man + (string.IsNullOrEmpty(bill.agent_man) ? "" : (";" + bill.agent_man))) : ""
                        );

                    //SendWxMessageForCompleted(
                    //    BillTypeName, bill.sys_no,
                    //    (isSuc ? "审批通过" : "审批不通过"),
                    //    db.vw_push_users.Where(v => v.card_number == bill.applier_num).FirstOrDefault()
                    //    );
                    SendQywxMessageForCompleted(
                        BillTypeName, bill.sys_no,
                        (isSuc ? "审批通过" : "审批不通过"),
                        new List<string>() { bill.applier_num }
                        );
                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    SendEmailToNextAuditor(
                        bill.sys_no,
                        result.step,
                        string.Format("你有一张待审批的{0}", BillTypeName + (model.msg.Contains("撤销") ? "(撤销)" : "")),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no,BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    //SendWxMessageToNextAuditor(
                    //    BillTypeName + (model.msg.Contains("撤销") ? "(撤销)" : ""),
                    //    bill.sys_no,
                    //    result.step,
                    //    result.stepName,
                    //    bill.applier_name,
                    //    ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                    //    string.Format("部门【{0}】;请假时间【{1} ~ {2}】", bill.dep_long_name, ((DateTime)bill.from_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)bill.to_date).ToString("yyyy-MM-dd HH:mm")),
                    //    db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                    //    );

                    SendQywxMessageToNextAuditor(
                        BillTypeName + (model.msg.Contains("撤销") ? "(撤销)" : ""),
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("部门【{0}】;请假时间【{1} ~ {2}】", bill.dep_long_name, ((DateTime)bill.from_date).ToString("yyyy-MM-dd HH:mm"), ((DateTime)bill.to_date).ToString("yyyy-MM-dd HH:mm")),
                        nextAuditors.ToList()
                        );

                }
            }
        }

        public ei_askLeave GetALBill()
        {
            return bill;
        }

        //public override IComparer<FlowAuditListModel> GetAuditListComparer()
        //{
        //    return new BillComparer();
        //}

        //private class BillComparer : IComparer<FlowAuditListModel>
        //{
        //    public int Compare(FlowAuditListModel x, FlowAuditListModel y)
        //    {
        //        if (x == null && y == null) return 0;
        //        if (x == null) return -1;
        //        if (y == null) return 1;
                
        //        if (x.applyTime == null) return -1;
        //        if (y.applyTime == null) return 1;
        //        return x.applyTime > y.applyTime ? -1 : 1;
                
        //    }
        //}

    }
}