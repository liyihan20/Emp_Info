using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using EmpInfo.Util;
using EmpInfo.Interfaces;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class SPSv : BillSv, IBeginAuditOtherInfo
    {
        ei_spApply bill;

        public SPSv() { }
        public SPSv(string sysNo)
        {
            bill = db.ei_spApply.Single(s => s.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "SP"; }
        }

        public override string BillTypeName
        {
            get { return "寄/收件申请"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("SPReport", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/SPReport"
                });
            }
            return menus;
        }

        public override string AuditViewName()
        {
            return "BeginAuditSPApply";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return new SPBeforeApplyModel()
            {
                sysNum = GetNextSysNum(BillType),
                applierPhone = string.IsNullOrWhiteSpace(userInfoDetail.shortPhone) ? userInfoDetail.phone : userInfoDetail.shortPhone,
                busDepList = db.flow_auditorRelation.Where(f => f.bill_type == BillType).Select(f => f.relate_text).Distinct().ToList()
            };
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_spApply>(fc.Get("sp"));

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            if (GetExInfo().Count() == 0) {
                throw new Exception("根据当前收寄件地址找不到合适的物流公司，请与物流部周秀花联系");
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.bus_name, bill.content_type + bill.send_or_receive + "申请");
            if (result.suc) {
                try {
                    db.ei_spApply.Add(bill);
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

            if (isPass) {
                string exCompany = fc.Get("ex_company");
                string exType = fc.Get("ex_type");
                string exPriceStr = fc.Get("ex_price");
                string exNo = fc.Get("ex_no");
                if (stepName.Contains("物流")) {
                    if (string.IsNullOrEmpty(exCompany)) {
                        return new SimpleResultModel() { suc = false, msg = "请先选择物流信息" };
                    }
                    if (string.IsNullOrEmpty(exNo)) {
                        return new SimpleResultModel() { suc = false, msg = "请先填写快递编号" };
                    }
                    if (!exCompany.Equals(bill.ex_company)) {
                        bill.ex_log = string.Format("{0}({1})[{2}]:{3}==>{4}({5})[{6}]:{7}", bill.ex_company, bill.ex_type, bill.ex_no, bill.ex_price, exCompany, exType, exNo, exPriceStr);
                    }
                    bill.ex_no = exNo;
                    bill.ex_company = exCompany;
                    bill.ex_type = exType;
                    bill.ex_price = decimal.Parse(exPriceStr);
                }

                if (stepName.Contains("事业部")) {
                    if (string.IsNullOrEmpty(exCompany)) {
                        return new SimpleResultModel() { suc = false, msg = "请先选择物流信息" };
                    }
                    bill.ex_company = exCompany;
                    bill.ex_type = exType;
                    bill.ex_price = decimal.Parse(exPriceStr);
                }
                if (stepName.Contains("申请人")) {
                    if (string.IsNullOrEmpty(exNo)) {
                        return new SimpleResultModel() { suc = false, msg = "请先填写快递编号" };
                    }
                    bill.ex_no = exNo;
                }
            }
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

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;
                    string ccEmails = "";
                    List<vw_push_users> pushUsers = db.vw_push_users.Where(v => v.card_number == bill.applier_num).ToList();

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}{3}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"), bill.ex_log == null ? "" : "(快递信息有变更)"),
                        GetUserEmailByCardNum(bill.applier_num),
                        ccEmails
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no + (bill.ex_log == null ? "" : "(快递信息有变更)"),
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
                        string.Format("{0}的{1}申请", bill.applier_name,bill.send_or_receive),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new SPSv(sysNo).bill;
        }

        public List<SPExInfoModel> GetExInfo()
        {
            return db.Database.SqlQuery<SPExInfoModel>("exec GetExpressInfo @Adr = {0},@DocQty = {1},@Size = {2}, @CardQty = {3},@CSize = {4},@FQty = {5},@FWeight = {6}",
                bill.send_or_receive == "寄件" ? bill.to_addr : bill.from_addr,
                bill.package_num ?? 1,
                bill.box_size ?? "0",
                bill.cardboard_num ?? 0,
                bill.cardboard_size ?? "0",
                bill.item_qty ?? 1,
                bill.total_weight ?? 1
                ).Where(s => s.FReed > 0).ToList();
        }

    }
}