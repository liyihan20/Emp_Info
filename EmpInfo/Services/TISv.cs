using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using EmpInfo.FlowSvr;
using EmpInfo.Util;
using Newtonsoft.Json;

namespace EmpInfo.Services
{
    //物流车辆放行流程
    public class TISv:BillSv
    {
        ei_TIApply bill;
        public TISv() { }

        public TISv(string sysNo)
        {
            bill = db.ei_TIApply.Single(t => t.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "TI"; }
        }

        public override string BillTypeName
        {
            get { return "物流放行申请单"; }
        }

        public override List<ApplyMenuItemModel> GetApplyMenuItems(UserInfo userInfo)
        {
            var menus = base.GetApplyMenuItems(userInfo);
            if (HasGotPower("TIViewForGuard", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "门卫车辆放行",
                    iconFont = "fa-cab",
                    url = "../Report/TIViewForGuard"
                });
            }
            if (HasGotPower("TIExcel", userInfo.id)) {
                menus.Add(new ApplyMenuItemModel()
                {
                    text = "报表查询与导出",
                    iconFont = "fa-file-text-o",
                    url = "../Report/TIReport"
                });
            }
            return menus;
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return GetNextSysNum(BillType);
        }
        
        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_TIApply>(fc.Get("head"));
            List<ei_TIApplyEntry> entry = JsonConvert.DeserializeObject<List<ei_TIApplyEntry>>(fc.Get("entry"));

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;
            bill.dep_name = new HRDBSv().GetHREmpDetailInfo(userInfo.cardNo).dep_name;

            if (entry.Count() < 1) {
                throw new Exception("至少需要录入1行司机信息才能保存");
            }

            foreach (var e in entry) {
                if (entry.Where(en => en.car_no == e.car_no && en.driver_no == e.driver_no).Count() > 1) {
                    throw new Exception("同一张申请单内不允许出现重复的车牌号与身份证号，请分开申请。车牌：" + e.car_no + ";身份证：" + e.driver_no);
                }
            }
            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.ex_company+"车辆放行", bill.ex_company+"车辆放行申请");
            if (result.suc) {
                try {
                    db.ei_TIApply.Add(bill);
                    foreach (var e in entry) {
                        e.ei_TIApply = bill;
                        e.t_status = "未进厂";
                        db.ei_TIApplyEntry.Add(e);
                    }
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }
                if (!bill.is_fetch_by_self) {
                    //不是客户自提的，提交后就自动同意，不需行政部审核
                    try {
                        System.Threading.Thread.Sleep(100);
                        client.BeginAudit(bill.sys_no, 1, "080705015", true, "", "{}");
                    }
                    catch (Exception ex) {
                        throw new Exception("提交成功，但自动审批失败，原因：" + ex.Message);
                    }
                }
                else {
                    SendNotification(result);
                }
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

            JsonSerializerSettings js = new JsonSerializerSettings();
            js.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            string formJson = JsonConvert.SerializeObject(bill, js);
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
                        string.Format("{0}的{1}", bill.applier_name, BillTypeName),
                        nextAuditors.ToList()
                        );
                }
            }
        }

        public void ChangeStatus(int entryId, string status)
        {
            var entry = db.ei_TIApplyEntry.Single(s => s.id == entryId);
            var entrys = db.ei_TIApplyEntry.Where(s => s.ti_id == entry.ti_id && s.car_no == entry.car_no).ToList();
            foreach (var e in entrys) {
                e.t_status = status;
                if (status == "已进厂") {
                    e.in_time = DateTime.Now;
                }
                else if (status == "已出厂") {
                    e.out_time = DateTime.Now;
                }
            }
            db.SaveChanges();

            //进厂发送消息给申请人
            bill = entry.ei_TIApply;
            if (status == "已进厂") {
                var msg = new QywxWebSrv.TextMsg();
                msg.touser = bill.applier_num;
                msg.text = new QywxWebSrv.TextContent();
                msg.text.content = string.Format("你申请的物流车辆放行单中，物流公司为【{0}】、车牌号为【{1}】、车型为【{2}】的车辆已于【{3}】进厂，请知悉。", bill.ex_company, entry.car_no, entry.car_type, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                SendQYWXMsg(msg);
            }

        }

        public override bool CanAccessApply(UserInfo userInfo)
        {
            return HasGotPower("BeginApplyTI", userInfo.id);
        }

        public void CancelTIApply(string cardNumber)
        {
            if (!cardNumber.Equals(bill.applier_num)) {
                throw new Exception("你不是此单的申请人，不能作废");
            }

            if (bill.ei_TIApplyEntry.Where(e => e.in_time != null).Count() > 0) {
                throw new Exception("司机已进厂，不能作废");
            }

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var result = flow.CancelFlowAfterFinish(bill.sys_no, cardNumber);
            if (!result.suc) throw new Exception(result.msg);

        }

    }
}