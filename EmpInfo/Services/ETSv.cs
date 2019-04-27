using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.FlowSvr;
using EmpInfo.Interfaces;
using EmpInfo.Models;
using EmpInfo.Util;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    public class ETSv:BillSv
    {
        private ei_etApply bill;

        public ETSv() { }
        public ETSv(string sysNo){
            bill = db.ei_etApply.Single(e => e.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "ET"; }
        }

        public override string BillTypeName
        {
            get { return "紧急出货运输申请"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditETApply";
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("ETReport", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "查询报表",
                    iconFont = "fa-file-text-o",
                    url = "../Report/ETReport"
                });
            }

            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            //申请时间必须在8时至19时之间
            //var hour = DateTime.Now.Hour;
            //if (hour < 8 || hour >= 19) {
            //    throw new Exception("申请时间必须在8时到19时之间，当前时间不能申请");
            //}
            //if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday) {
            //    throw new Exception("申请不能在周日进行申请");
            //}

            ETBeforeApplyModel m = new ETBeforeApplyModel();
            m.sysNum = GetNextSysNum(BillType, 2);
            m.marketList = db.flow_auditorRelation.Where(a => a.bill_type == BillType && a.relate_name == "市场部审批").Select(a => a.relate_text).ToList().Distinct().ToList();
            m.applierPhone = userInfoDetail.phone + (string.IsNullOrEmpty(userInfoDetail.shortPhone) ? "" : ("(" + userInfoDetail.shortPhone + ")"));
            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_etApply();
            MyUtils.SetFieldValueToModel(fc, bill);
            bill.ei_etApplyEntry = JsonConvert.DeserializeObject<List<ei_etApplyEntry>>(fc.Get("entrys"));

            if (string.IsNullOrEmpty(bill.addr)) throw new Exception("送货地址不能为空");
            if (string.IsNullOrEmpty(bill.box_size)) throw new Exception("包装箱尺寸不能为空");
            if (string.IsNullOrEmpty(bill.bus_dep)) throw new Exception("生产事业部不能为空");
            if (string.IsNullOrEmpty(bill.cardboard_size)) throw new Exception("卡板尺寸不能为空");
            if (string.IsNullOrEmpty(bill.company)) throw new Exception("出货公司不能为空");
            if (string.IsNullOrEmpty(bill.customer_name)) throw new Exception("客户名称不能为空");
            if (string.IsNullOrEmpty(bill.demand)) throw new Exception("出货要求不能为空");
            if (string.IsNullOrEmpty(bill.gross_weight)) throw new Exception("总毛重不能为空");
            if (string.IsNullOrEmpty(bill.market_name)) throw new Exception("市场部不能为空");
            if (bill.out_time == null) throw new Exception("出货时间不能为空");
            if (string.IsNullOrEmpty(bill.pack_num)) throw new Exception("件数不能为空");
            if (string.IsNullOrEmpty(bill.reason)) throw new Exception("申请原因不能为空");
            if (string.IsNullOrEmpty(bill.responsibility)) throw new Exception("责任备注不能为空");
            if (string.IsNullOrEmpty(bill.transfer_style)) throw new Exception("运输方式不能为空");

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.customer_name, ((DateTime)bill.out_time).ToString("yyyy-MM-dd HH:mm"));
            if (result.suc) {
                try {
                    db.ei_etApply.Add(bill);                    
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
            ETCheckApplyModel m = new ETCheckApplyModel();
            m.et = bill;
            m.entrys = bill.ei_etApplyEntry.ToList();
            
            return m;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string opinion = fc.Get("opinion");

            if (stepName.Equals("物流填写信息")) {
                string deliveryCompany = fc.Get("deliveryCompany");
                string normalFee = fc.Get("normalFee");
                string applyFee = fc.Get("applyFee");
                string differentFee = fc.Get("differentFee");

                if (string.IsNullOrEmpty(deliveryCompany))  throw new Exception("货运公司不能为空");
                if (string.IsNullOrEmpty(normalFee)) throw new Exception("正常运费不能为空");
                if (string.IsNullOrEmpty(applyFee)) throw new Exception("申请运费不能为空");
                if (string.IsNullOrEmpty(differentFee)) throw new Exception("运费差额不能为空");

                decimal normalFeeDm, applyFeeDm, differentFeeDm;
                if (!decimal.TryParse(normalFee, out normalFeeDm)) throw new Exception("正常运费必须为数字");
                if (!decimal.TryParse(applyFee, out applyFeeDm)) throw new Exception("申请运费必须为数字");
                if (!decimal.TryParse(differentFee, out differentFeeDm)) throw new Exception("运费差额必须为数字");

                bill.dilivery_company = deliveryCompany;
                bill.normal_fee = normalFeeDm;
                bill.apply_fee = applyFeeDm;
                bill.different_fee = differentFeeDm;
            }

            var setting = new JsonSerializerSettings();
            setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill, setting);
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
                        string.Format("你申请的单号为【{0}】的{1}已{2}，客户是：{3}；规格型号是：{4}等{5}个，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝"), bill.customer_name, bill.ei_etApplyEntry.First().item_modual, bill.ei_etApplyEntry.Count()),
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
                        string.Format("{0}：{1}等", bill.customer_name, bill.ei_etApplyEntry.First().item_modual),
                        db.vw_push_users.Where(p => nextAuditors.Contains(p.card_number)).ToList()
                        );
                }
            }
        }
    }
}