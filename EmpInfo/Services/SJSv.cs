using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.Interfaces;
using EmpInfo.EmpWebSvr;
using EmpInfo.Util;
using EmpInfo.FlowSvr;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class SJSv:BillSv
    {
        ei_sjApply bill;

        public SJSv() { }
        public SJSv(string sysNo)
        {
            bill = db.ei_sjApply.Single(j => j.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "SJ"; }
        }

        public override string BillTypeName
        {
            get { return "员工调动流程"; }
        }

        public override List<ApplyNavigatorModel> GetApplyNavigatorLinks()
        {
            var list = base.GetApplyNavigatorLinks();
            list.Add(
                new ApplyNavigatorModel()
                {
                    text = "无纸化流程",
                    url = "Home/NoPaperProcess"
                }
            );
            return list;
        }
        
        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            //请假流程的统计员权限就能看离职报表
            if (db.ei_department.Where(d => d.FReporter.Contains(userInfo.cardNo)).Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/SJReport"
                });                
            }

            if(MyUtils.hasGotPower(userInfo.id,"Admin","UpdateHREmpDept")){
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "人工调动",
                    iconFont = "fa-refresh",
                    url = "../Admin/UpdateHREmpDept"
                });
            }
            return menus;
        }

        public override string AuditViewName()
        {
            return "BeginAuditApplyW";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return GetNextSysNum(BillType);
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_sjApply();
            MyUtils.SetFieldValueToModel(fc, bill);

            if (bill.out_dep_position != null && bill.out_dep_position.Length > 50) throw new Exception("调出部门岗位内容太多，请删减");
            if (bill.in_dep_position != null && bill.in_dep_position.Length > 50) throw new Exception("调入部门岗位内容太多，请删减");

            if (bill.out_time == null) throw new Exception("请输入正确的调出时间");
            if (bill.in_time == null) throw new Exception("请输入正确的到岗时间");

            
            if (string.IsNullOrWhiteSpace(bill.in_clerk_name)) throw new Exception("必须选择调入部门文员");
            if (string.IsNullOrWhiteSpace(bill.out_clerk_name)) throw new Exception("必须选择调出部门文员");
            if (string.IsNullOrWhiteSpace(bill.in_manager_name)) throw new Exception("必须选择调入部门主管/经理");
            if (string.IsNullOrWhiteSpace(bill.out_manager_name)) throw new Exception("必须选择调出部门主管/经理");

            bill.in_clerk_num = GetUserCardByNameAndCardNum(bill.in_clerk_name);
            bill.out_clerk_num = GetUserCardByNameAndCardNum(bill.out_clerk_name);
            bill.in_manager_num = GetUserCardByNameAndCardNum(bill.in_manager_name);
            bill.out_manager_num = GetUserCardByNameAndCardNum(bill.out_manager_name);

            if (!"计件".Equals(bill.salary_type)) {
                if (string.IsNullOrWhiteSpace(bill.in_minister_name)) throw new Exception("必须选择调入部门部长/助理");
                if (string.IsNullOrWhiteSpace(bill.out_minister_name)) throw new Exception("必须选择调出部门部长/助理");

                bill.in_minister_num = GetUserCardByNameAndCardNum(bill.in_minister_name);
                bill.out_minister_num = GetUserCardByNameAndCardNum(bill.out_minister_name);
            }

            //公司内、跨公司调动判断部门
            string[] copKeys = new string[] { "信利半导体", "信利光电股份", "信利电子", "信利仪器", "信利工业", "信元光电", "第三方", "光电仁寿" };
            foreach (var k in copKeys) {
                if (bill.switch_type == "公司内") {
                    if (bill.out_dep_name.Contains(k) && !bill.in_dep_name.Contains(k)) {
                        throw new Exception("调入部门和调出部门不属于同一公司，调动类型必须选择跨公司");
                    }
                }
                if (bill.switch_type == "跨公司") {
                    if (bill.out_dep_name.Contains(k) && bill.in_dep_name.Contains(k)) {
                        throw new Exception("调入部门和调出部门属于同一公司，调动类型必须选择公司内");
                    }
                }
            }

            //审核人一开始没有在权限组里，在这里加入
            var autGroup = db.ei_groups.Where(g => g.name == "员工调动申请组").FirstOrDefault();
            if (autGroup != null) {
                if (db.ei_groupUser.Where(gu => gu.group_id == autGroup.id && gu.user_id == userInfo.id).Count() < 1) {
                    db.ei_groupUser.Add(new ei_groupUser()
                    {
                        group_id = autGroup.id,
                        user_id = userInfo.id
                    });
                }
            }

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.out_dep_name + "-->"+bill.in_dep_name, bill.name + "调动申请");
            if (result.suc) {
                try {
                    db.ei_sjApply.Add(bill);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }

                SendNotification(result);
            }
            else {
                throw new Exception("申请提交失败，原因：" + result.msg);
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
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            string formJson = JsonConvert.SerializeObject(bill);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();

                    if (isSuc) {
                        List<string> additionCardNumber = new List<string>(); //需要额外通知的厂牌
                        additionCardNumber.Add(bill.in_clerk_num); //调入部门文员
                        additionCardNumber.Add(bill.out_clerk_num); //调出部门文员
                        if ("计件".Equals(bill.salary_type)) {
                            additionCardNumber.AddRange(db.flow_notifyUsers.Where(n => n.bill_type == BillType && n.cond1 == "计件").Select(n => n.card_number));
                        }
                        else {
                            additionCardNumber.AddRange(db.flow_notifyUsers.Where(n => n.bill_type == BillType && n.cond1 == "月薪").Select(n => n.card_number));
                        }

                        pushUsers.AddRange(
                            from v in db.vw_push_users
                            where v.wx_push_flow_info == true
                            && additionCardNumber.Contains(v.card_number)
                            select v
                            );
                    }

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
                        pushUsers
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
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no, BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );

                    string[] nextAuditors = model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    SendWxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("{0}的调动申请：{1}--->{2}", bill.name,bill.out_dep_name,bill.in_dep_name),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public void UpdateHREmpDept(string cardNumber, int depId, DateTime inDate, string position)
        {
            db.UpdateHrEmpDept(cardNumber, depId, inDate, position);
        }

    }
}