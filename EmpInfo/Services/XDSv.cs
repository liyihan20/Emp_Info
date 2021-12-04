using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class XDSv:BillSv
    {
        private ei_xdApply bill;

        public XDSv() { }
        public XDSv(string sysNo)
        {
            bill = db.ei_xdApply.Single(x => x.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "XD"; }
        }

        public override string BillTypeName
        {
            get { return "委外超时申请单"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            var auth = db.ei_flowAuthority.Where(f => (f.bill_type == "XC" || f.bill_type == BillType) && f.relate_value == userInfo.cardNo).ToList();

            if (auth.Where(a => a.relate_type == "加工部门额度").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "申请部门",
                    iconFont = "fa-bullseye",
                    url = "../ApplyExtra/ProcessDep"
                });
            }
            if (auth.Where(a => a.bill_type == BillType && a.relate_type == "查询报表").Count() > 0) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/XDReport",
                });
            }
            

            return menus;
        }

        public override string AuditViewName()
        {
            return "BeginAuditApplyW";
        }

        public override object GetInfoBeforeApply(Models.UserInfo userInfo, Models.UserInfoDetail userInfoDetail)
        {
            XDBeforeApplyModel m = new XDBeforeApplyModel();

            m.sys_no = GetNextSysNum(BillType);
            m.k3Accounts = db.k3_database.Where(k => !new string[] { "光电", "半导体", "香港光电科技" }.Contains(k.account_name))
                .OrderBy(k => k.account_name).Select(k => k.account_name).ToList();
            m.depList = db.ei_xcProcessDep.OrderBy(x => x.dep_name).Select(x=>x.dep_name).ToList();

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_xdApply>(fc.Get("bill"));
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            if (bill.time_from >= bill.time_to) {
                throw new Exception("时间段不正确，保存失败");
            }

            bill.lod_confirm_people_num = GetUserCardByNameAndCardNum(bill.lod_confirm_people);
            bill.stock_confirm_people_num = GetUserCardByNameAndCardNum(bill.stock_confirm_people);
            bill.dep_charger_num = GetUserCardByNameAndCardNum(bill.dep_charger);

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.process_dep+"的委外超时申请", string.Format("{0:yyyy-MM-dd HH:mm} ~ {1:yyyy-MM-dd HH:mm}",bill.time_from,bill.time_to));
            if (result.suc) {
                try {
                    db.ei_xdApply.Add(bill);
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

        public override Models.SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, Models.UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            //string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            FlowResultModel result;

            string formJson = JsonConvert.SerializeObject(bill);
            result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, opinion, formJson);

            if (result.suc) {
                //db.SaveChanges();
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

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    SendQywxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
                        new List<string>() { bill.applier_num }
                        );
                    //2021-11-24 吉全要求流程结束后抄送给张锦生
                    SendQywxMessageToCC(
                        BillTypeName,
                        bill.sys_no,
                        bill.applier_name,
                        bill.apply_time.ToShortDateString(),
                        string.Format("部门：{0}；申请时间段：{1}", bill.process_dep, string.Format("{0:yyyy-MM-dd HH:mm} ~ {1:yyyy-MM-dd HH:mm}", bill.time_from, bill.time_to)),
                        new List<string>() { "080828041" }
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

                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("部门：{0}；申请时间段：{1}", bill.process_dep, string.Format("{0:yyyy-MM-dd HH:mm} ~ {1:yyyy-MM-dd HH:mm}", bill.time_from, bill.time_to)),
                        nextAuditors.ToList()
                        );
                }
            }
        }
    }
}