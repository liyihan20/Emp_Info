using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using EmpInfo.Models;
using Newtonsoft.Json;
using EmpInfo.QywxWebSrv;
using EmpInfo.FlowSvr;

namespace EmpInfo.Services
{
    /// <summary>
    /// 新放行条流程，整合自提与非自提，2021-09
    /// </summary>
    public class FXSv:BillSv
    {
        ei_fxApply bill;

        public FXSv() { }
        public FXSv(string sysNo)
        {
            bill = db.ei_fxApply.Single(a => a.sys_no == sysNo);
        }

        public override string BillType
        {
            get { return "FX"; }
        }

        public override string BillTypeName
        {
            get { return "放行条申请单"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditFXApply";
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            var m = new FXSelectTypeNameModel();
            m.fxTypes = db.ei_fxType.Where(f => !f.is_deleted).OrderBy(f => f.type_no).ToList();

            var lastApply = db.ei_fxApply.Where(f => f.applier_num == userInfo.cardNo).OrderByDescending(f => f.id).FirstOrDefault();
            if (lastApply != null) {
                m.lastTypeName = lastApply.fx_type_name;
                m.lastTypeNo = lastApply.fx_type_no;
            }

            return m;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_fxApply>(fc.Get("head"));
            List<ei_fxApplyEntry> entrys = JsonConvert.DeserializeObject<List<ei_fxApplyEntry>>(fc.Get("entrys"));

            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;
            db.ei_fxApply.Add(bill);

            foreach (var en in entrys) {
                en.sys_no = bill.sys_no;
                db.ei_fxApplyEntry.Add(en);
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.bus_name,bill.fx_type_name);
            if (result.suc) {
                try {                    
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
            return new FXCheckApplyModel()
            {
                bill = bill,
                entrys = db.ei_fxApplyEntry.Where(f => f.sys_no == bill.sys_no).ToList()
            };
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
                //发送通知到下一级审核人
                SendNotification(result);
            }
            return new SimpleResultModel() { suc = result.suc, msg = result.msg };

        }

        public override void SendNotification(FlowSvr.FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {

                    if (bill.fx_type_no.StartsWith("2")) {
                        //自提的，流程完结后可查看放行二维码
                        bill.can_print = true;
                        db.SaveChanges();
                    }

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
                        new List<string>() { bill.applier_num },
                        model.opinion
                        );

                }
                else {
                    FlowSvrSoapClient flow = new FlowSvrSoapClient();
                    var result = flow.GetCurrentStep(bill.sys_no);

                    if (bill.fx_type_no.StartsWith("1")) {
                        //非自提的，下一步是物流中心时，就可以打印了。
                        if (result.stepName.Contains("物流中心")) {
                            bill.can_print = true;
                            db.SaveChanges();

                            SendQYWXMsg(new TextMsg()
                            {
                                touser = bill.applier_num,
                                text = new TextContent()
                                {
                                    content = "你的非自提放行条审批流程【" + bill.sys_no + "】已到达物流中心节点，请到申请单查看界面打印放行条后，尽快与物品一起拿到物流中心处理并寄件，否则超时将被作废。"
                                }
                            });

                        }
                    }

                    return;

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
                        string.Format("{0}的放行条申请，业务类型：{1}", bill.applier_name, bill.fx_type_name),
                        nextAuditors.ToList()
                        );

                    

                }
            }
        }
    }
}