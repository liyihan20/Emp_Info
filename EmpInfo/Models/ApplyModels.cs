using System;
using System.Collections.Generic;

namespace EmpInfo.Models
{
    public class AuditModel
    {
        public bool isAuditing { get;set; }
        public int step { get; set; }
        public string stepName { get; set; }
        public string cardNumber { get; set; }
        public string userName { get; set; }
    }

    public class ALApplyModel
    {
        public ALApplyModel() { }
        public ALApplyModel(ei_askLeave al)
        {
            this.sysNo = al.sys_no;
            this.depLongName = al.dep_long_name;
            this.applyTime = al.apply_time == null ? "" : ((DateTime)al.apply_time).ToString("yyyy-MM-dd HH:mm:ss");
            this.isDirectCharge = al.is_direct_charge == true ? "是" : "否";
            this.leaveType = al.leave_type;
            this.leaveReason = al.leave_reason;
            this.leaveTimeZone = ((DateTime)al.from_date).ToString("yyyy-MM-dd HH:mm") + " ~ " + ((DateTime)al.to_date).ToString("yyyy-MM-dd HH:mm");
            this.totalLeaveDays = "共 " + al.work_days + " 天 " + al.work_hours + " 小时";
            this.isContinue = al.is_continue ? "是" : "否";
            this.hasAttachment = al.has_attachment ?? false;
        }
        public string auditStatus { get; set; }
        public string sysNo { get; set; }
        public string applierNameAndCard { get; set; }
        public string applyTime { get; set; }
        public string depLongName { get; set; }
        public string empLevel { get; set; }
        public string isDirectCharge { get; set; }
        public string leaveType { get; set; }
        public string leaveReason { get; set; }
        public string leaveTimeZone { get; set; }
        public string totalLeaveDays { get; set; }
        public string isContinue { get; set; }
        public string agentMan { get; set; }
        public string informMan { get; set; }
        public bool hasAttachment { get; set; }
        public string account { get; set; }
    }

    public class AttachmentModel
    {
        public string fileName { get; set; }
        public long fileSize { get; set; }
        public string ext { get; set; }
    }

    public class FlowQueueModel
    {
        public string sys_no { get; set; }
        public int step { get; set; }
        public string step_name { get; set; }
        public string auditors { get; set; }
    }

    public class BeginAuditModel
    {
        public string sysNum { get; set; }
        public int step { get; set; }
        public string stepName { get; set; }
        public string auditorName { get; set; }
        public string auditorNumber { get; set; }
        public bool? isPass { get; set; }
        public string opinion { get; set; }
        public string billType { get; set; }
        public string billTypeName { get; set; }
        public object otherInfo { get; set; }
    }

    public class ALRecordModel
    {
        public string sysNo { get; set; }
        public string applyTime { get; set; }
        public string leaveType { get; set; }
        public string leaveDays { get; set; }
        public string leaveDateSpan { get; set; }
    }

    public class ApplyMenuItemModel
    {
        public string url { get; set; }
        public string text { get; set; }
        public string iconFont { get; set; }
        public string colorClass { get; set; }
        public string linkClass { get; set; }
    }

    public class ApplyNavigatorModel
    {
        public string url { get; set; }
        public string text { get; set; }
    }

    public class ALBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string depNum { get; set; }
        public int? depId { get; set; }
        public int? empLevel { get; set; }
        public int times { get; set; }
        public int days { get; set; }
        public decimal hours { get; set; }
        public string pLevels { get; set; }
        public decimal? vacationDaysLeft { get; set; }
    }

    public class EPBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string procDepInfo { get; set; }
        public string applierPhone { get; set; }
    }

    public class UCBeforeApplyModel
    {
        public string sysNum { get; set; }
        public List<string> marketList { get; set; }
        public List<string> busDepList { get; set; }
        public List<string> accountingList { get; set; }
    }

    public class UCCheckApplyModel
    {
        public ei_ucApply uc { get; set; }
        public List<ei_ucApplyEntry> entrys { get; set; }
    }

    public class SABeforeApplyModel
    {
        public string sysNum { get; set; }
        public List<K3AccountModel> accounts { get; set; }
    }

    public class SAK3StockAuditor
    {
        public string stockName { get; set; }
        public string stockNum { get; set; }
        public string auditorName { get; set; }
        public string auditorNum { get; set; }
    }

    public class CRSVBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string depNum { get; set; }
        public int? depId { get; set; }
    }

    public class DPCheckApplyModel
    {
        public ei_dormRepair bill { get; set; }
        public List<FlowSvr.FlowRecordModels> records { get; set; }
    }

    public class ETBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string applierPhone { get; set; }
        public List<string> marketList { get; set; }
        public List<string> busDepList { get; set; }
    }

    public class ETCheckApplyModel
    {
        public ei_etApply et { get; set; }
        public List<ei_etApplyEntry> entrys { get; set; }
    }

    public class APBeforeApplyModel
    {
        public ei_apApply ap { get; set; }
        public List<string> k3AccountList { get; set; }

    }

    public class APCheckApplyModel
    {
        public ei_apApply ap { get; set; }
        public List<ei_apApplyEntry> entrys { get; set; }
    }

    public class JQBeforeApplyModel
    {
        public string sysNum { get; set; }
        public string applierNum { get; set; }
    }

    public class JQAuditOtherInfoModel
    {
        public string work_evaluation { get; set; }
        public string work_comment { get; set; }
        public string wanna_employ { get; set; }
        public string employ_comment { get; set; }
        public string quit_type { get; set; }
        public string leave_date { get; set; }
    }

    public class SPBeforeApplyModel
    {
        public string sys_no { get; set; }
        public string applier_phone { get; set; }
        public string send_or_receive { get; set; }
        public string company { get; set; }
        public string aging { get; set; }
        public string bus_name { get; set; }
        public string content_type { get; set; }
        public string from_addr { get; set; }
        public string to_addr { get; set; }
        public string receiver_name { get; set; }
        public string receiver_phone { get; set; }
        public string scope { get; set; }
        public List<string> busDepList { get; set; }
    }

    public class SPExInfoModel
    {
        public string FName { get; set; }
        public string FDelivery { get; set; }
        public decimal FReed { get; set; }
    }

    public class ITAuditOtherInfoModel
    {
        public string faultyItems { get; set; }
        public string loginName { get; set; }
        public string loginPassword { get; set; }
    }

    //public class TIBeforeApplyModel
    //{
    //    public string sys_no { get; set; }

    //}
}