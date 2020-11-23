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
    //宿舍公共区域维修
    public class PPSv:BillSv
    {
        ei_PPApply bill;
        public PPSv(){}
        public PPSv(string sysNo)
        {
            bill = db.ei_PPApply.Single(p => p.sys_no == sysNo);
        }
        public override string BillType
        {
            get { return "PP"; }
        }

        public override string BillTypeName
        {
            get { return "宿舍公共预约维修申请"; }
        }

        public override string AuditViewName()
        {
            return "BeginAuditApplyW";
        }

        public override List<ApplyNavigatorModel> GetApplyNavigatorLinks()
        {
            return new List<ApplyNavigatorModel>(){
                new ApplyNavigatorModel(){
                    text = "宿舍事务集合",
                    url = "Home/DormGroupIndex"
                }
            };
        }

        public override object GetInfoBeforeApply(UserInfo userInfo, UserInfoDetail userInfoDetail)
        {
            return GetNextSysNum(BillType);
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = JsonConvert.DeserializeObject<ei_PPApply>(fc.Get("head"));
            
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;

            string entrys = fc.Get("entrys");
            if (!string.IsNullOrEmpty(entrys)) {
                List<ei_dormRepairIems> list = JsonConvert.DeserializeObject<List<ei_dormRepairIems>>(fc.Get("entrys"));
                foreach (var l in list) {
                    l.sys_no = bill.sys_no;
                    l.is_public_fee = true;
                    l.op_user = userInfo.name;
                    db.ei_dormRepairIems.Add(l);
                }
            }

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.repair_content, bill.addr);
            if (result.suc) {
                try {
                    db.ei_PPApply.Add(bill);
                    db.SaveChanges();
                }
                catch (Exception ex) {
                    //将生成的流程表记录删除
                    client.DeleteApplyForFailure(bill.sys_no);
                    throw new Exception("申请提交失败，原因：" + ex.Message);
                }

                //发送通知邮件到下一环节
                SendNotification(result);
            }
            else {
                throw new Exception("申请提交失败，原因：" + result.msg);
            }

        }

        public override object GetBill()
        {
            var m = new PPCheckApplyModel();
            m.bill = bill;
            m.items = db.ei_dormRepairIems.Where(d => d.sys_no == bill.sys_no).ToList();
            
            return m;
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

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "批准" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}", bill.sys_no, BillTypeName, (isSuc ? "批准" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num)
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
                        bill.repair_content,
                        nextAuditors.ToList()
                        );
                }
            }
        }
    }
}