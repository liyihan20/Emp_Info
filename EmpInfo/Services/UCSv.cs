using EmpInfo.FlowSvr;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    public class UCSv:BillSv
    {
        ei_ucApply bill;

        public UCSv() { }
        public UCSv(string sysNo)
        {
            bill = db.ei_ucApply.Single(u => u.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "UC"; }
        }

        public override string BillTypeName
        {
            get { return "非正常时间出货申请"; }
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            //申请时间必须在8时至19时之间
            var hour = DateTime.Now.Hour;
            var minute = DateTime.Now.Minute;
            if (hour < 8 || hour >= 17 || (hour == 16 && minute > 45)) {
                throw new Exception("非正常时间出货的申请时间必须在8时到16时45分之间，当前时间不能申请,有问题请联系物流总仓");
            }
            if (DateTime.Now.ToString("yyyy-MM-dd") != "2019-09-29") {
                if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday) {
                    throw new Exception("非正常时间出货的申请不能在周日进行申请");
                }
            }
            UCBeforeApplyModel m = new UCBeforeApplyModel();
            var list = db.flow_auditorRelation.Where(a => a.bill_type == BillType).ToList();
            m.marketList = list.Where(l => l.relate_name == "市场部总经理").Select(l => l.relate_text).Distinct().ToList();
            m.busDepList = list.Where(l => l.relate_name == "事业部长").Select(l => l.relate_text).Distinct().ToList();
            m.accountingList = list.Where(l => l.relate_name == "会计部主管").Select(l => l.relate_text).Distinct().ToList();

            m.sysNum = GetNextSysNum(BillType, 2);
            return m;
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("UCReport", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/UCReport"
                });
            }

            return menus;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_ucApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            List<ei_ucApplyEntry> entrys = JsonConvert.DeserializeObject<List<ei_ucApplyEntry>>(fc.Get("entrys"));

            if (string.IsNullOrEmpty(bill.market_name)) {
                throw new Exception("请选择市场部！");
            }

            if (string.IsNullOrEmpty(bill.customer_name)) {
                throw new Exception("请录入正确的客户编码！" );
            }

            if (string.IsNullOrEmpty(bill.company)) {
                throw new Exception("请选择出货公司！" );
            }

            if (string.IsNullOrEmpty(bill.bus_dep)) {
                throw new Exception("请选择生产事业部！" );
            }

            if (string.IsNullOrEmpty(bill.delivery_company)) {
                throw new Exception("请填写货运公司！" );
            }

            if (entrys.Count() < 1) {
                throw new Exception("出货明细必须至少一条！" );
            }

            if (bill.arrive_time == null) {
                throw new Exception("到达日期不合法" );
            }

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.customer_name, ((DateTime)bill.arrive_time).ToString("yyyy-MM-dd HH:mm"));
            if (result.suc) {
                try {
                    db.ei_ucApply.Add(bill);
                    foreach (var e in entrys) {
                        e.ei_ucApply = bill;
                        db.ei_ucApplyEntry.Add(e);
                    }
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
                throw new Exception("申请提交失败，原因：" + result.msg);
            }           
        }

        public override object GetBill()
        {
            UCCheckApplyModel m = new UCCheckApplyModel();
            m.uc = bill;
            m.entrys = bill.ei_ucApplyEntry.ToList();            
            return m;
            
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            var setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill, setting);
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);
            if (result.suc) {
                //发送通知到下一级审核人
                SendNotification(result);
            }            
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {                
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();
                    if (isSuc) {
                        //通知知会人
                        pushUsers.AddRange(
                            (from u in db.flow_notifyUsers
                             join v in db.vw_push_users on u.card_number equals v.card_number
                             where v.wx_push_flow_info == true
                             && u.bill_type == BillType
                             && (u.cond1 == "所有" || u.cond1 == bill.company)
                             select v).ToList()
                        );
                        ccEmails = string.Join(",", (
                            from u in db.flow_notifyUsers
                            join us in db.ei_users on u.card_number equals us.card_number
                            where u.cond1 == "所有" || u.cond1 == bill.company
                            && u.bill_type == BillType
                            select us.email
                            ).ToArray()
                         );
                    }

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，客户是：{3}；规格型号是：{4}等{5}个，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"), bill.customer_name, bill.ei_ucApplyEntry.First().moduel, bill.ei_ucApplyEntry.Count()),
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
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("{0}：{1}等", bill.customer_name, bill.ei_ucApplyEntry.First().moduel),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public override bool CanAccessApply(UserInfo userInfo)
        {
            return HasGotPower("UnnormalCH", userInfo.id);
        }

    }
}