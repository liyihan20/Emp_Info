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
    public class DPSv : BillSv, IBeginAuditOtherInfo
    {
        ei_dormRepair bill;
        public DPSv() { }
        public DPSv(string sysNo)
        {
            bill = db.ei_dormRepair.Single(s => s.sys_no == sysNo);
        }


        public override string BillType
        {
            get { return "DP"; }
        }

        public override string BillTypeName
        {
            get { return "宿舍维修申请"; }
        }


        public override string AuditViewName()
        {
            return "BeginAuditDPApply";
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
            var info = db.GetEmpDormInfo(userInfo.cardNo).ToList();
            if (info.Count() == 0) {
                throw new Exception("你当前没有入住，不能申请");
            }
            DormRepairBeginApplyModel model = new DormRepairBeginApplyModel();

            model.sysNo = GetNextSysNum(BillType);
            model.areaName = info.First().area;
            model.dormNumber = info.First().dorm_number;

            var roomMate = db.GetDormRoomMate(model.dormNumber).ToList().Where(r => r.card_number != userInfo.cardNo).ToList();
            string roomMateStr = "";
            foreach (var rm in roomMate) {
                roomMateStr += rm.card_number + ":" + rm.emp_name + ";";
            }
            model.roomMaleList = roomMateStr;
            model.phoneNumber = userInfoDetail.phone;
            model.shortPhoneNumber = userInfoDetail.shortPhone;
            model.contactName = userInfo.name;

            return model;
        }

        public override void SaveApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            bill = new ei_dormRepair();
            MyUtils.SetFieldValueToModel(fc, bill);
            bill.applier_name = userInfo.name;
            bill.applier_num = userInfo.cardNo;
            bill.apply_time = DateTime.Now;
            bill.is_outside = false;

            if (!"舍友分摊".Equals(bill.fee_share_type)) {
                bill.fee_share_peple = null;

                //独自承担时，要验证是否7天内新入住的，如果是且有舍友的情况下，必须选择舍友分摊
                if (!new DormSv().validateWhileSelfPay((DateTime)bill.apply_time, bill.dorm_num, bill.applier_num)) {
                    throw new Exception("系统检测到你是7天内新入住多人房的职员，费用承担方式必须选择舍友分摊才能提交");
                }
            }

            bill.repair_time = null;
            //if (!"预约维修".Equals(bill.repair_type)) {
            //    bill.repair_time = null;
            //}

            FlowSvrSoapClient client = new FlowSvrSoapClient();
            var result = client.StartWorkFlow(JsonConvert.SerializeObject(bill), BillType, userInfo.cardNo, bill.sys_no, bill.repair_content, bill.area_name + "_" + bill.dorm_num);
            if (result.suc) {
                try {
                    db.ei_dormRepair.Add(bill);
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
            var m = new DPCheckApplyModel();

            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var records = flow.GetFlowRecord(bill.sys_no).ToList();
            records.ForEach(r => r.auditors = GetUserNameByCardNum(r.auditors));
            m.records = records;

            bill.fee_share_peple = bill.fee_share_peple == null ? "" : GetUserNameByCardNum(bill.fee_share_peple);
            m.bill = bill;

            m.items = db.ei_dormRepairIems.Where(d => d.sys_no == bill.sys_no).ToList();

            return m;
        }

        public override SimpleResultModel HandleApply(System.Web.Mvc.FormCollection fc, UserInfo userInfo)
        {
            int step = Int32.Parse(fc.Get("step"));
            string stepName = fc.Get("stepName");
            bool isPass = bool.Parse(fc.Get("isPass"));
            string auditOption = "";

            FlowResultModel result = null;
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            

            //处理表单信息
            if (stepName.Contains("舍友")) {
                //不需要处理表单信息
            }
            else if (stepName.Contains("接单")) {
                string accepterComment = fc.Get("accepterComment");
                string accepter = fc.Get("accepter");
                string confirmTime = fc.Get("confirmTime");

                bill.accept_comment = accepterComment;
                bill.is_accept = isPass;
                if (isPass) {
                    bill.accepter_name = accepter;
                    bill.accepter_num = userInfo.cardNo;
                    if (!string.IsNullOrEmpty(confirmTime)) {
                        bill.confirm_repair_time = DateTime.Parse(confirmTime);
                        if (bill.confirm_repair_time < DateTime.Now) {
                            throw new Exception("确认维修日期不能早于当前日期");
                        }
                    }
                }
                auditOption = accepterComment;
            }
            else if (stepName.Contains("完成")) {
                string repairSubject = fc.Get("repairSubject");
                string repairFinishTime = fc.Get("repairFinishTime");
                string repairFee = fc.Get("repairFee");

                if (!string.IsNullOrEmpty(repairFinishTime)) {
                    bill.finish_repair_time = DateTime.Parse(repairFinishTime);
                    if (bill.finish_repair_time > DateTime.Now) {
                        throw new Exception("完成维修日期不能晚于当前日期");
                    }
                }

                bill.repaire_subject = repairSubject;
                bill.charge_fee = decimal.Parse(repairFee);
                if (!bill.is_outside) {
                    //厂外的不需要重新计算，否则会被覆盖掉，变为空，导致扣不到费
                    bill.emp_id_should_pay = new DormSv().GetEmpIdShouldPay((DateTime)bill.apply_time, bill.applier_num, bill.fee_share_peple);
                }
            }
            else if (stepName.Contains("评价")) {
                string rateScore = fc.Get("rateScore");
                string rateOpinion = fc.Get("rateOpinion");

                int rateScoreInt;
                if (!Int32.TryParse(rateScore, out rateScoreInt)) {
                    throw new Exception("评分不合法");
                }
                if (rateScoreInt <= 2 && string.IsNullOrEmpty(rateOpinion)) {
                    throw new Exception("评分2星以下必须填写评价意见");
                }

                bill.applier_evaluation = rateOpinion;
                bill.applier_evaluation_score = rateScoreInt;
            }

            //走流程
            string formJson = JsonConvert.SerializeObject(bill);
            result = flow.BeginAudit(bill.sys_no, step, userInfo.cardNo, isPass, auditOption, formJson);
            if (result != null && result.suc) {
                db.SaveChanges();
                //发送通知到下一级审核人
                SendNotification(result);
            }

            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override SimpleResultModel AbortApply(UserInfo userInfo, string sysNo, string reason = "")
        {
            FlowSvrSoapClient flow = new FlowSvrSoapClient();
            var currentStep = flow.GetCurrentStep(sysNo);
            if (!currentStep.stepName.Contains("舍友")) {
                return new SimpleResultModel() { suc = false, msg = "不能撤销，因为后勤部已接单" };
            }

            var result = flow.AbortFlow(userInfo.cardNo, sysNo);

            return new SimpleResultModel() { suc = result.suc, msg = result.msg };
        }

        public override void SendNotification(FlowResultModel model)
        {
            if (model.suc) {
                if (model.msg.Contains("完成") || model.msg.Contains("NG")) {
                    bool isSuc = model.msg.Contains("NG") ? false : true;

                    SendEmailForCompleted(
                        bill.sys_no,
                        BillTypeName + "已" + (isSuc ? "处理完成" : "被拒绝"),
                        bill.applier_name,
                        string.Format("你申请的单号为【{0}】的{1}已{2}，请知悉。", bill.sys_no, BillTypeName, (isSuc ? "处理完成" : "被拒绝")),
                        GetUserEmailByCardNum(bill.applier_num)
                        );
                    SendQywxMessageForCompleted(
                        BillTypeName, 
                        bill.sys_no,
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
                        string.Format("你有一张待审批的{0}", BillTypeName),
                        GetUserNameByCardNum(model.nextAuditors),
                        string.Format("你有一张待处理的单号为【{0}】的{1}，请尽快登陆系统处理。", bill.sys_no, BillTypeName),
                        GetUserEmailByCardNum(model.nextAuditors)
                        );
                    SendQywxMessageToNextAuditor(
                        BillTypeName,
                        bill.sys_no,
                        result.step,
                        result.stepName,
                        bill.applier_name,
                        ((DateTime)bill.apply_time).ToString("yyyy-MM-dd HH:mm"),
                        string.Format("宿舍号：{0}；维修内容：{1}", bill.dorm_num, bill.repair_content),
                        model.nextAuditors.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList()
                        );
                }
            }
        }

        public object GetBeginAuditOtherInfo(string sysNo, int step)
        {
            return new DPSv(sysNo).GetBill();
        }

        public List<DormRepairItemModel> SearchRepairItems(string itemName, string opUser)
        {
            var list = db.Database.SqlQuery<DormRepairItemModel>("exec DP_SearchInventory @itemName = {0},@opUser = {1}", itemName, opUser).ToList();

            //库存数需要减去【宿舍公共维修】流程中此人已提交但还未审批完成的数量
            var itemIds = list.Select(l => l.item_id).ToList();

            var auditingPPApply = (from p in db.ei_PPApply
                                   join i in db.ei_dormRepairIems on p.sys_no equals i.sys_no
                                   join a in db.flow_apply on p.sys_no equals a.sys_no
                                   where a.success == null && p.applier_name == opUser && itemIds.Contains(i.item_id)
                                   select new
                                   {
                                       i.item_id,
                                       i.qty
                                   }).ToList();
            foreach (var a in auditingPPApply) {
                list.Where(l => l.item_id == a.item_id).FirstOrDefault().inventory -= a.qty;
            }

            return list;
        }

        public ei_dormRepairIems SaveRepairItem(DormRepairItemModel im,string opUser){

            if (db.ei_dormRepairIems.Where(d => d.sys_no == im.sys_no && d.item_id == im.item_id).Count() > 0) {
                throw new Exception("不能选择重复的配件，如果多个请直接修改数量");
            }

            ei_dormRepairIems item = new ei_dormRepairIems();
            MyUtils.CopyPropertyValue(im, item);
            item.op_user = opUser;
            item.qty = 1;

            db.ei_dormRepairIems.Add(item);
            db.SaveChanges();

            return item;
        }

        public void UpdateRepairItemQty(int id, int qty)
        {
            var item = db.ei_dormRepairIems.Single(r => r.id == id);
            if (item.inventory < qty) {
                throw new Exception("使用数量不能大于库存数量，当前库存数是：" + item.inventory);
            }
            item.qty = qty;
            db.SaveChanges();
        }

        public void RemoveRepairItem(int id)
        {
            var item = db.ei_dormRepairIems.Single(r => r.id == id);
            db.ei_dormRepairIems.Remove(item);
            db.SaveChanges();
        }

        public bool UpdateRepairItemPublicFee(int id)
        {
            var item = db.ei_dormRepairIems.Single(r => r.id == id);
            item.is_public_fee = !item.is_public_fee;
            db.SaveChanges();
            return item.is_public_fee;
        }
    }
}