using EmpInfo.FlowSvr;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmpInfo.Services
{
    public class SASv:BillSv
    {
        ei_stockAdminApply bill;
        public SASv() { }
        public SASv(string sysNo)
        {
            bill = db.ei_stockAdminApply.Single(s => s.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "SA"; }
        }

        public override string BillTypeName
        {
            get { return "仓管权限申请单"; }
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            SABeforeApplyModel m = new SABeforeApplyModel();
            m.accounts = (from sc in db.GetK3StockAccoutList()
                          select new K3AccountModel()
                          {
                              number = sc.FAcctNumber,
                              name = sc.FAcctName
                          }).ToList();
            m.sysNum = GetNextSysNum(BillType);
            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_stockAdminApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.k3_stock_name + "(" + bill.k3_stock_num + ")", bill.k3_account_name);
            if (result.suc) {
                db.ei_stockAdminApply.Add(bill);

                db.SaveChanges();

                SendNotification(result);
            }
            else {
                throw new Exception("申请提交失败，原因是：" + result.msg);
            }
        }

        public override object GetBill()
        {
            return bill;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            string formJson = JsonConvert.SerializeObject(bill);
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

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，账套是：{3},仓库是：{4}({5},请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"),bill.k3_account_name,bill.k3_stock_name,bill.k3_stock_num),
                        GetUserEmailByCardNum(bill.applier_num)
                        );

                    SendWxMessageForCompleted(
                        BillTypeName,
                        bill.sys_no,
                        (isSuc ? "批准" : "被拒绝"),
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
                        string.Format("{0}：{1}", bill.k3_account_name,bill.k3_stock_name),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }

        public List<SAK3StockAuditor> GetK3StockAuditor(string accName)
        {
            var result = (from s in db.GetK3StockAuditor(accName)
                          select new SAK3StockAuditor
                          {
                              stockName = s.FName,
                              stockNum = s.FNumber,
                              auditorName = s.FCheckName ?? "",
                              auditorNum = s.FCheckNo ?? ""
                          }).OrderBy(a => a.stockNum).ToList();
            return result;
        }

    }
}